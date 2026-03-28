// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { GoogleGenAI, Modality, type LiveServerMessage } from '@google/genai';
import { debugLogService } from '../../diagnostics';
import { getGeminiModels } from '../../../core/config/models';
import { resolveLiveConnectAccess } from '../../../api/gemini/client';
import { mergeInt16Arrays, pcmToWav } from '../utils/audioProcessing';
import { TRIGGER_AUDIO_PCM_24K, TRIGGER_SAMPLE_RATE } from './triggerAudioAsset';

const OUTPUT_SAMPLE_RATE = 24000;
const SESSION_TIMEOUT_MS = 180000;
const MAX_TRIGGER_DURATION_MS = 10000;

const toBase64 = (bytes: Uint8Array): string => {
  let binary = '';
  for (let i = 0; i < bytes.byteLength; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
};

const base64ToUint8 = (base64: string): Uint8Array => {
  const binaryString = atob(base64);
  const bytes = new Uint8Array(binaryString.length);
  for (let i = 0; i < binaryString.length; i++) {
    bytes[i] = binaryString.charCodeAt(i);
  }
  return bytes;
};

const base64ToInt16 = (base64: string): Int16Array => {
  const bytes = base64ToUint8(base64);
  return new Int16Array(bytes.buffer);
};

export interface GeminiAudioNoteResult {
  dataUrl: string;
  mimeType: 'audio/wav';
  durationSeconds: number;
}

export const synthesizeGeminiAudioNote = async (params: {
  text: string;
  langCode?: string;
  voiceName?: string;
  abortSignal?: AbortSignal;
}): Promise<GeminiAudioNoteResult> => {
  const text = (params.text || '').trim();
  if (!text) {
    throw new Error('Audio note text is empty.');
  }

  const liveAccess = await resolveLiveConnectAccess({ purpose: 'live' });
  const ai = new GoogleGenAI({ apiKey: liveAccess.apiKey });
  const model = getGeminiModels().audio.live;
  const voiceName = (params.voiceName || 'Kore').trim() || 'Kore';

  const systemInstructionText = [
    'You are a professional text-to-speech engine.',
    'Read the provided text aloud exactly as written.',
    'Do not add any intro, explanation, or extra words.',
    'Keep the delivery warm and clear.',
    params.langCode ? `Language hint: ${params.langCode}` : '',
    'TEXT TO READ:',
    text,
  ].filter(Boolean).join('\n');

  const log = debugLogService.logRequest('synthesizeGeminiAudioNote', model, {
    textLength: text.length,
    voiceName,
    langCode: params.langCode,
  });

  return new Promise<GeminiAudioNoteResult>((resolve, reject) => {
    let session: any = null;
    let releaseLiveAccess: (() => Promise<void>) | null = liveAccess.release;
    let isSettled = false;
    let isStreaming = false;
    let cleanupStream: (() => void) | null = null;
    const audioChunks: Int16Array[] = [];

    const resolveOnce = (result: GeminiAudioNoteResult) => {
      if (isSettled) return;
      isSettled = true;
      resolve(result);
    };

    const rejectOnce = (error: any) => {
      if (isSettled) return;
      isSettled = true;
      reject(error);
    };

    const cleanup = () => {
      isStreaming = false;
      if (cleanupStream) cleanupStream();
      if (releaseLiveAccess) {
        void releaseLiveAccess().catch(() => undefined);
        releaseLiveAccess = null;
      }
    };

    const finalize = () => {
      const mergedAudio = mergeInt16Arrays(audioChunks);
      if (!mergedAudio.length) {
        const error = new Error('No audio note was generated.');
        log.error(error);
        rejectOnce(error);
        return;
      }

      const dataUrl = pcmToWav(mergedAudio, OUTPUT_SAMPLE_RATE, 1);
      const durationSeconds = mergedAudio.length / OUTPUT_SAMPLE_RATE;
      log.complete({ durationSeconds, sampleCount: mergedAudio.length });
      resolveOnce({
        dataUrl,
        mimeType: 'audio/wav',
        durationSeconds,
      });
    };

    const abort = (reason: string) => {
      cleanup();
      try { session?.close(); } catch {}
      rejectOnce(new Error(reason));
    };

    void (async () => {
      try {
        session = await ai.live.connect({
          model,
          config: {
            responseModalities: [Modality.AUDIO],
            speechConfig: { voiceConfig: { prebuiltVoiceConfig: { voiceName } } },
            systemInstruction: { parts: [{ text: systemInstructionText }] },
            outputAudioTranscription: {},
            thinkingConfig: { thinkingBudget: 0 },
          } as any,
          callbacks: {
            onmessage: (msg: LiveServerMessage) => {
              const inlineAudio = msg.serverContent?.modelTurn?.parts?.[0]?.inlineData?.data;
              if (inlineAudio) {
                try {
                  audioChunks.push(base64ToInt16(inlineAudio));
                } catch (error) {
                  console.warn('[GeminiAudioNote] Failed to decode inline audio chunk.', error);
                }
              }

              if (msg.serverContent?.turnComplete) {
                cleanup();
                try { session?.close(); } catch {}
                finalize();
              }
            },
            onclose: () => {
              cleanup();
              if (!isSettled && audioChunks.length > 0) {
                finalize();
              }
            },
            onerror: (error: any) => {
              cleanup();
              const message = error?.message || 'Audio note generation failed.';
              log.error({ message });
              rejectOnce(new Error(message));
            },
          },
        });

        isStreaming = true;
        const triggerBytes = base64ToUint8(TRIGGER_AUDIO_PCM_24K);
        const chunkDurationMs = 100;
        const chunkSize = Math.floor((TRIGGER_SAMPLE_RATE * chunkDurationMs) / 1000) * 2;
        let sendOffset = 0;
        const triggerStartTime = Date.now();
        const sessionTimeoutId = setTimeout(() => {
          if (!isStreaming || isSettled) return;
          abort('Audio note generation timed out.');
        }, SESSION_TIMEOUT_MS);

        const intervalId = setInterval(() => {
          if (!isStreaming || !session) {
            clearInterval(intervalId);
            clearTimeout(sessionTimeoutId);
            return;
          }

          const triggerElapsed = Date.now() - triggerStartTime;
          if (triggerElapsed > MAX_TRIGGER_DURATION_MS && sendOffset >= triggerBytes.length) {
            return;
          }

          let base64Chunk = '';
          if (sendOffset < triggerBytes.length) {
            const end = Math.min(sendOffset + chunkSize, triggerBytes.length);
            base64Chunk = toBase64(triggerBytes.slice(sendOffset, end));
            sendOffset = end;
          } else if (triggerElapsed <= MAX_TRIGGER_DURATION_MS) {
            base64Chunk = toBase64(new Uint8Array(chunkSize));
          } else {
            return;
          }

          try {
            session.sendRealtimeInput({
              media: {
                mimeType: `audio/pcm;rate=${TRIGGER_SAMPLE_RATE}`,
                data: base64Chunk,
              },
            });
          } catch (error) {
            abort((error as Error)?.message || 'Audio note trigger failed.');
          }
        }, chunkDurationMs);

        cleanupStream = () => {
          clearInterval(intervalId);
          clearTimeout(sessionTimeoutId);
        };

        if (params.abortSignal) {
          if (params.abortSignal.aborted) {
            abort('Audio note generation aborted.');
            return;
          }
          params.abortSignal.addEventListener('abort', () => abort('Audio note generation aborted.'), { once: true });
        }
      } catch (error: any) {
        cleanup();
        const message = error?.message || 'Audio note session failed.';
        log.error({ message });
        rejectOnce(new Error(message));
      }
    })();
  });
};
