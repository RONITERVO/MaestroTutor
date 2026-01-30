// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * Gemini Live TTS Service
 * 
 * Uses the Gemini Live API as a Text-to-Speech engine by:
 * 1. Connecting with AUDIO response modality
 * 2. Including the text to speak in the system instruction
 * 3. Triggering the model with a pre-recorded audio prompt
 * 4. Streaming audio response immediately for playback
 * 
 * This approach bypasses the limitations of the preview TTS model:
 * - Faster response time (streaming)
 * - Better voice quality (native audio model)
 * - Full conversation context support
 * 
 * ARCHITECTURE:
 * - Queue all lines to speak in a single session for context
 * - Split audio by transcript newlines (same as live conversation)
 * - Stream audio playback as chunks arrive
 * - Cache audio segments per line for replay
 */

import { GoogleGenAI, Modality, LiveServerMessage } from '@google/genai';
import { debugLogService } from '../../diagnostics';
import { TRIGGER_AUDIO_PCM_24K, TRIGGER_SAMPLE_RATE } from './triggerAudioAsset';
import { getApiKeyOrThrow } from '../../../core/security/apiKeyStorage';

// ============================================================================
// TYPES
// ============================================================================

export interface GeminiLiveTtsLine {
  text: string;
  langCode: string;
  voiceName?: string;
  cacheKey?: string;
  onAudioCached?: (audioDataUrl: string) => void;
}

export interface GeminiLiveTtsParams {
  lines: GeminiLiveTtsLine[];
  audioContext: AudioContext;
  abortSignal?: AbortSignal;
  voiceName?: string;
  onLineStart?: (lineIndex: number, text: string) => void;
  onLineComplete?: (lineIndex: number, audioPcm: Int16Array) => void;
  onStatusUpdate?: (status: string) => void;
  onError?: (error: string) => void;
}

export interface GeminiLiveTtsResult {
  isComplete: boolean;
  error?: string;
  audioSegments: Int16Array[];
}

// ============================================================================
// UTILITIES
// ============================================================================

function toBase64(bytes: Uint8Array): string {
  let binary = '';
  const len = bytes.byteLength;
  for (let i = 0; i < len; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}

function base64ToUint8(base64: string): Uint8Array {
  const binaryString = atob(base64);
  const len = binaryString.length;
  const bytes = new Uint8Array(len);
  for (let i = 0; i < len; i++) {
    bytes[i] = binaryString.charCodeAt(i);
  }
  return bytes;
}

function base64ToInt16(base64: string): Int16Array {
  const bytes = base64ToUint8(base64);
  return new Int16Array(bytes.buffer);
}

function mergeInt16Arrays(arrays: Int16Array[]): Int16Array {
  const totalLength = arrays.reduce((acc, curr) => acc + curr.length, 0);
  const result = new Int16Array(totalLength);
  let offset = 0;
  for (const arr of arrays) {
    result.set(arr, offset);
    offset += arr.length;
  }
  return result;
}

function pcmToAudioBuffer(
  pcmData: Uint8Array,
  audioContext: AudioContext,
  sampleRate: number = 24000,
  numChannels: number = 1
): AudioBuffer {
  const dataInt16 = new Int16Array(pcmData.buffer, pcmData.byteOffset, pcmData.byteLength / 2);
  const frameCount = dataInt16.length / numChannels;
  const buffer = audioContext.createBuffer(numChannels, frameCount, sampleRate);

  for (let channel = 0; channel < numChannels; channel++) {
    const channelData = buffer.getChannelData(channel);
    for (let i = 0; i < frameCount; i++) {
      channelData[i] = dataInt16[i * numChannels + channel] / 32768.0;
    }
  }
  
  return buffer;
}

// ============================================================================
// MAIN SERVICE
// ============================================================================

const OUTPUT_SAMPLE_RATE = 24000;
const SESSION_TIMEOUT_MS = 300000; // 5 minute timeout for entire session
const MAX_TRIGGER_DURATION_MS = 10000; // Stop sending trigger audio after 10 seconds

/**
 * Stream speech using Gemini Live API as TTS.
 * 
 * Queues all lines in a single session, uses transcript-aware audio splitting,
 * and streams audio playback as chunks arrive.
 */
export async function streamGeminiLiveTts(params: GeminiLiveTtsParams): Promise<GeminiLiveTtsResult> {
  const { lines, audioContext, abortSignal, voiceName = 'Kore', onLineStart, onLineComplete, onStatusUpdate, onError } = params;
  
  if (!lines.length) {
    return { isComplete: true, audioSegments: [] };
  }

  // Validate API key is available
  let apiKey: string;
  try {
    apiKey = await getApiKeyOrThrow();
  } catch (e: any) {
    const errorMsg = e?.message || 'Missing API key';
    onError?.(errorMsg);
    return { isComplete: false, error: errorMsg, audioSegments: [] };
  }

  // Build the text block for the system instruction
  // Each line on a new line for proper transcript splitting
  const textBlock = lines.map(l => `[${(l.langCode || '')}] ${l.text}`).join('\n\n');
  
  const systemInstructionText = `You are a professional Text-to-Speech engine. Your ONLY task is to read the following text aloud, exactly as written, when the user says "Play". 
IMPORTANT RULES:
- Read EXACTLY what is written, character by character
- Speak each line clearly with a brief pause between lines
- Do NOT add any intro, outro, commentary, or acknowledgment
- Do NOT modify, translate, or interpret the text
- Just speak the text immediately
TEXT TO READ:
${textBlock}`;

  const model = 'gemini-2.5-flash-native-audio-preview-12-2025';
  const config = {
    responseModalities: [Modality.AUDIO],
    speechConfig: { voiceConfig: { prebuiltVoiceConfig: { voiceName } } },
    systemInstruction: { parts: [{ text: systemInstructionText }] },
    outputAudioTranscription: {},
  };

  const log = debugLogService.logRequest('streamGeminiLiveTts', model, {
    lineCount: lines.length,
    systemInstructionText,
  });

  return new Promise(async (resolve) => {
    let nextStartTime = audioContext.currentTime;
    let isStreaming = false;
    let streamCleanup: (() => void) | null = null;
    let session: any = null;
    let isResolved = false;
    const activeSources: AudioBufferSourceNode[] = [];
    let finalized = false;

    let sessionStartTime: number | null = null;
    const scheduledLineStarts: Array<{ lineIndex: number; text: string; startTime: number }> = [];
    const pendingLineStarts: Array<{ lineIndex: number; text: string; startSample: number }> = [];
    let rafId: number | null = null;

    const ensureHighlightLoop = () => {
      if (rafId !== null) return;
      const tick = () => {
        if (scheduledLineStarts.length === 0) {
          rafId = null;
          return;
        }
        const now = audioContext.currentTime;
        while (scheduledLineStarts.length > 0 && scheduledLineStarts[0].startTime <= now) {
          const next = scheduledLineStarts.shift();
          if (next) onLineStart?.(next.lineIndex, next.text);
        }
        rafId = requestAnimationFrame(tick);
      };
      rafId = requestAnimationFrame(tick);
    };

    const scheduleLineStart = (lineIndex: number, text: string, startSample: number) => {
      if (sessionStartTime === null) {
        pendingLineStarts.push({ lineIndex, text, startSample });
        return;
      }
      const startTime = sessionStartTime + startSample / OUTPUT_SAMPLE_RATE;
      scheduledLineStarts.push({ lineIndex, text, startTime });
      scheduledLineStarts.sort((a, b) => a.startTime - b.startTime);
      ensureHighlightLoop();
    };

    // Audio accumulation for transcript-aware splitting
    const audioChunks: Int16Array[] = [];
    let audioTotalLength = 0;
    const splitPoints: number[] = [];
    let lastNewlineCount = 0;
    let transcriptAccumulator = '';
    let startedLineIndex: number | null = null;

    const cleanup = () => {
      isStreaming = false;
      if (streamCleanup) streamCleanup();
      if (rafId !== null) {
        cancelAnimationFrame(rafId);
        rafId = null;
      }
      while (activeSources.length > 0) {
        const source = activeSources.pop();
        try { source?.stop(); } catch {}
        try { source?.disconnect(); } catch {}
      }
    };

    const resolveOnce = (result: GeminiLiveTtsResult) => {
      if (isResolved) return;
      isResolved = true;
      resolve(result);
    };

    const finalizeAudioSegments = (reason: string) => {
      if (finalized) return;
      finalized = true;

      const fullAudio = mergeInt16Arrays(audioChunks);
      const audioSegments: Int16Array[] = [];
      const includeTrailingRemainder = reason === 'turn-complete';

      // Split audio at recorded points
      if (splitPoints.length > 0 && fullAudio.length > 0) {
        let startSample = 0;
        const uniquePoints = [...new Set(splitPoints)]
          .sort((a, b) => a - b)
          .filter(p => p > 0 && p < fullAudio.length);

        for (const point of uniquePoints) {
          if (point > startSample) {
            audioSegments.push(fullAudio.slice(startSample, point));
            startSample = point;
          }
        }
        if (includeTrailingRemainder && startSample < fullAudio.length) {
          audioSegments.push(fullAudio.slice(startSample));
        }
      } else if (fullAudio.length > 0) {
        if (includeTrailingRemainder) {
          audioSegments.push(fullAudio);
        }
      }

      // No offset compensation for TTS
      for (let i = 0; i < audioSegments.length && i < lines.length; i++) {
        onLineComplete?.(i, audioSegments[i]);
      }

      return { audioSegments };
    };

    try {
      const ai = new GoogleGenAI({ apiKey });
      
      session = await ai.live.connect({
        model,
        config: config as any,
        callbacks: {
          onopen: () => {
            onStatusUpdate?.('CONNECTED / STREAMING');
          },
          onmessage: (msg: LiveServerMessage) => {
            // 1. Handle Audio Response - stream immediately for playback
            const inlineAudio = msg.serverContent?.modelTurn?.parts?.[0]?.inlineData?.data;
            if (inlineAudio) {
              try {
                // Accumulate raw PCM for splitting
                const pcm16 = base64ToInt16(inlineAudio);
                audioChunks.push(pcm16);
                audioTotalLength += pcm16.length;

                // Trigger line start when first audio arrives (schedule to playback time)
                if (startedLineIndex === null) {
                  const initialIndex = Math.min(lastNewlineCount, lines.length - 1);
                  startedLineIndex = initialIndex;
                }

                // Decode and schedule playback immediately
                const chunk = base64ToUint8(inlineAudio);
                const audioBuffer = pcmToAudioBuffer(chunk, audioContext, OUTPUT_SAMPLE_RATE);
                const source = audioContext.createBufferSource();
                source.buffer = audioBuffer;
                source.connect(audioContext.destination);
                activeSources.push(source);
                
                // Clean up the source when it finishes playing to free memory
                source.onended = () => {
                  const idx = activeSources.indexOf(source);
                  if (idx !== -1) {
                    activeSources.splice(idx, 1);
                  }
                  try { source.disconnect(); } catch {}
                };
                
                nextStartTime = Math.max(audioContext.currentTime, nextStartTime);
                if (sessionStartTime === null) {
                  sessionStartTime = nextStartTime;
                  const initialIndex = Math.min(lastNewlineCount, lines.length - 1);
                  if (initialIndex >= 0 && lines[initialIndex]) {
                    scheduleLineStart(initialIndex, lines[initialIndex].text, 0);
                  }
                  if (pendingLineStarts.length > 0) {
                    for (const pending of pendingLineStarts) {
                      scheduleLineStart(pending.lineIndex, pending.text, pending.startSample);
                    }
                    pendingLineStarts.length = 0;
                  }
                }
                source.start(nextStartTime);
                nextStartTime += audioBuffer.duration;
              } catch (e) {
                console.warn('[GeminiLiveTts] Audio decode failed', e);
              }
            }

            // 2. Handle Transcript - detect newlines for split points
            if (msg.serverContent?.outputTranscription?.text) {
              transcriptAccumulator += msg.serverContent.outputTranscription.text;
              const normalizedTranscript = transcriptAccumulator.replace(/\r\n/g, '\n').replace(/\n+/g, '\n');
              const newlineCount = (normalizedTranscript.match(/\n/g) || []).length;
              if (newlineCount > lastNewlineCount) {
                const diff = newlineCount - lastNewlineCount;
                for (let i = 0; i < diff; i++) {
                  splitPoints.push(audioTotalLength);
                }
                if (startedLineIndex !== null) {
                  for (let i = 0; i < diff; i++) {
                    const lineIndex = lastNewlineCount + i + 1;
                    if (lineIndex < lines.length) {
                      scheduleLineStart(lineIndex, lines[lineIndex].text, audioTotalLength);
                    }
                  }
                }
                lastNewlineCount = newlineCount;
              }
            }

            // 3. Handle Turn Completion
            if (msg.serverContent?.turnComplete) {
              // Clean up first to stop timers and prevent timeout firing
              cleanup();
              const finalizedResult = finalizeAudioSegments('turn-complete');
              session.close();
              log.complete({
                status: 'complete',
                segmentCount: finalizedResult?.audioSegments.length || 0,
                transcript: transcriptAccumulator,
              });
              resolveOnce({ isComplete: true, audioSegments: finalizedResult?.audioSegments || [] });
            }
          },
          onclose: () => {
            cleanup();
          },
          onerror: (err: any) => {
            cleanup();
            const errorMsg = err?.message || 'Connection error';
            log.error({ message: errorMsg, transcript: transcriptAccumulator });
            onError?.(errorMsg);
            resolveOnce({ isComplete: false, error: errorMsg, audioSegments: [] });
          }
        }
      });

      // --- Trigger Logic: Send pre-recorded audio to wake up model ---
      isStreaming = true;
      onStatusUpdate?.('TRIGGERING...');
      
      const triggerUint8 = base64ToUint8(TRIGGER_AUDIO_PCM_24K);
      let sendOffset = 0;
      const chunkDurationMs = 100;
      const chunkSize = Math.floor((TRIGGER_SAMPLE_RATE * chunkDurationMs) / 1000) * 2; // 16-bit = 2 bytes
      const triggerStartTime = Date.now();
      let sessionTimeoutId: ReturnType<typeof setTimeout> | null = null;

      // Session timeout - if no response within limit, fail gracefully
      sessionTimeoutId = setTimeout(() => {
        if (isStreaming) {
          cleanup();
          session?.close();
          const errorMsg = 'Session timeout - no response from model';
          log.error({ message: errorMsg, transcript: transcriptAccumulator });
          onError?.(errorMsg);
          resolveOnce({ isComplete: false, error: errorMsg, audioSegments: [] });
        }
      }, SESSION_TIMEOUT_MS);

      const intervalId = setInterval(() => {
        if (!isStreaming || !session) {
          clearInterval(intervalId);
          if (sessionTimeoutId) clearTimeout(sessionTimeoutId);
          return;
        }

        // Stop sending trigger audio after max duration
        const triggerElapsed = Date.now() - triggerStartTime;
        if (triggerElapsed > MAX_TRIGGER_DURATION_MS && sendOffset >= triggerUint8.length) {
          // Already sent trigger audio, stop sending silence - just wait for response
          return;
        }

        let b64Data = "";
        if (sendOffset < triggerUint8.length) {
          const end = Math.min(sendOffset + chunkSize, triggerUint8.length);
          const chunk = triggerUint8.slice(sendOffset, end);
          b64Data = toBase64(chunk);
          sendOffset = end;
        } else if (triggerElapsed <= MAX_TRIGGER_DURATION_MS) {
          // Only send silence for a limited time
          const silence = new Uint8Array(chunkSize);
          b64Data = toBase64(silence);
        } else {
          return; // Don't send anything more
        }

        try {
          session.sendRealtimeInput({
            media: { mimeType: `audio/pcm;rate=${TRIGGER_SAMPLE_RATE}`, data: b64Data }
          });
        } catch (e) {
          cleanup();
          clearInterval(intervalId);
          if (sessionTimeoutId) clearTimeout(sessionTimeoutId);
        }
      }, chunkDurationMs);

      streamCleanup = () => {
        clearInterval(intervalId);
        if (sessionTimeoutId) clearTimeout(sessionTimeoutId);
      };

      if (abortSignal) {
        if (abortSignal.aborted) {
          cleanup();
          try { session?.close(); } catch {}
          const finalizedResult = finalizeAudioSegments('aborted');
          log.complete({
            status: 'aborted',
            segmentCount: finalizedResult?.audioSegments.length || 0,
            transcript: transcriptAccumulator,
          });
          resolveOnce({ isComplete: false, error: 'ABORTED', audioSegments: finalizedResult?.audioSegments || [] });
          return;
        }
        abortSignal.addEventListener('abort', () => {
          cleanup();
          try { session?.close(); } catch {}
          const finalizedResult = finalizeAudioSegments('aborted');
          log.complete({
            status: 'aborted',
            segmentCount: finalizedResult?.audioSegments.length || 0,
            transcript: transcriptAccumulator,
          });
          resolveOnce({ isComplete: false, error: 'ABORTED', audioSegments: finalizedResult?.audioSegments || [] });
        }, { once: true });
      }

    } catch (e: any) {
      cleanup();
      const errorMsg = e?.message || 'Connection failed';
      log.error({ message: errorMsg, transcript: transcriptAccumulator });
      onError?.(errorMsg);
      resolveOnce({ isComplete: false, error: errorMsg, audioSegments: [] });
    }
  });
}
