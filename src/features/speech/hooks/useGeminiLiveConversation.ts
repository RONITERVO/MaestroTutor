import { useCallback, useEffect, useRef, useState } from 'react';
import { GoogleGenAI, LiveServerMessage, Modality, Blob as GenAIBlob } from '@google/genai';
import { mergeInt16Arrays, trimSilence } from '../utils/audioProcessing';
import { debugLogService } from '../../diagnostics';
import { FLOAT_TO_INT16_PROCESSOR_URL, FLOAT_TO_INT16_PROCESSOR_NAME } from '../worklets';

export type LiveSessionState = 'idle' | 'connecting' | 'active' | 'error';

export interface UseGeminiLiveConversationCallbacks {
  onStateChange?: (state: LiveSessionState) => void;
  onError?: (message: string) => void;
  /**
   * Called when a turn completes with consolidated transcripts and audio.
   * @param userText - The user's transcribed speech
   * @param modelText - The model's transcribed response
   * @param userAudioPcm - Optional user audio as Int16Array (16kHz)
   * @param modelAudioLines - Optional array of model audio segments (24kHz), split by transcript newlines.
   *                          Each element corresponds to a line in modelText (target line, then native translation line).
   *                          Splitting accounts for delay between audio arrival and transcript appearance.
   */
  onTurnComplete?: (userText: string, modelText: string, userAudioPcm?: Int16Array, modelAudioLines?: Int16Array[]) => void;
}

const INPUT_SAMPLE_RATE = 16000;
const OUTPUT_SAMPLE_RATE = 24000;

// Session counter to prevent stale callback execution after cleanup
let liveConversationSessionCounter = 0;

function toBase64(bytes: Uint8Array) {
  let binary = '';
  const len = bytes.byteLength;
  for (let i = 0; i < len; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}

function encodeInt16ToBlob(pcm: Int16Array): GenAIBlob {
  const bytes = new Uint8Array(pcm.buffer, pcm.byteOffset, pcm.byteLength);
  return {
    data: toBase64(bytes),
    mimeType: 'audio/pcm;rate=16000',
  };
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

async function decodePcm16ToAudioBuffer(
  data: Uint8Array,
  ctx: AudioContext,
  sampleRate: number,
  numChannels: number,
): Promise<AudioBuffer> {
  const int16 = new Int16Array(data.buffer, data.byteOffset, Math.floor(data.byteLength / 2));
  const frameCount = Math.floor(int16.length / numChannels);
  const buffer = ctx.createBuffer(numChannels, frameCount, sampleRate);
  for (let channel = 0; channel < numChannels; channel++) {
    const channelData = buffer.getChannelData(channel);
    for (let i = 0; i < frameCount; i++) {
      const sample = int16[i * numChannels + channel];
      channelData[i] = sample / 32768;
    }
  }
  return buffer;
}

const blobToBase64 = (blob: Blob): Promise<string> => new Promise((resolve, reject) => {
  const reader = new FileReader();
  reader.onloadend = () => {
    const result = reader.result as string;
    const [, base64] = result.split(',', 2);
    resolve(base64 || '');
  };
  reader.onerror = reject;
  reader.readAsDataURL(blob);
});

/**
 * Manage a live Gemini conversation with real-time microphone capture, periodic video frames, model audio playback, transcription accumulation, and turn-level callbacks.
 *
 * @param callbacks - Optional handlers:
 *   - onStateChange(state): invoked when the session state changes ('idle' | 'connecting' | 'active' | 'error')
 *   - onError(message): invoked with an error message when the session encounters an error
 *   - onTurnComplete(userText, modelText, userAudioPcm?, modelAudioLines?): invoked when an exchange completes with consolidated transcripts and optional Int16Array PCM audio for user and model
 * @returns An object with:
 *   - start(opts): begins a live session using the provided media stream and optional `systemInstruction` and `videoElement`
 *   - stop(): stops the session and releases all audio/video resources and internal state
 */
export function useGeminiLiveConversation(
  callbacks: UseGeminiLiveConversationCallbacks = {}
) {
  const [, setState] = useState<LiveSessionState>('idle');
  
  const sessionRef = useRef<any>(null);
  const frameIntervalRef = useRef<number | null>(null);
  const inputAudioContextRef = useRef<AudioContext | null>(null);
  const outputAudioContextRef = useRef<AudioContext | null>(null);
  const microphoneStreamRef = useRef<MediaStream | null>(null);
  const captureVideoRef = useRef<HTMLVideoElement | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const workletNodeRef = useRef<AudioWorkletNode | null>(null);
  const nextAudioStartTimeRef = useRef<number>(0);
  const audioSourcesRef = useRef<Set<AudioBufferSourceNode>>(new Set());
  const logRef = useRef<ReturnType<typeof debugLogService.logRequest> | null>(null);
  const logFinalizedRef = useRef<boolean>(false);
  const modelRef = useRef<string>('');
  const pendingUserTurnRef = useRef<{ text: string; transcript: string; audio: Int16Array } | null>(null);
  
  // Session ID to track valid session and invalidate stale callbacks
  const currentSessionIdRef = useRef<number>(0);
  
  // Flag to track if cleanup is in progress to prevent race conditions
  const isCleaningUpRef = useRef<boolean>(false);

  // Transcription & Audio Accumulators
  const currentInputTranscriptionRef = useRef<string>('');
  const currentOutputTranscriptionRef = useRef<string>('');
  const currentUserAudioChunksRef = useRef<Int16Array[]>([]);
  const currentModelAudioChunksRef = useRef<Int16Array[]>([]);
  
  // Audio-Transcript Synchronization Tracking
  // These track the correlation between streaming audio and delayed transcripts
  // Split points are recorded when newlines appear in transcript, using current audio length
  const currentModelAudioTotalLengthRef = useRef<number>(0);
  const modelAudioSplitPointsRef = useRef<number[]>([]);
  const lastNewlineCountRef = useRef<number>(0);

  const callbacksRef = useRef(callbacks);
  useEffect(() => { callbacksRef.current = callbacks; }, [callbacks]);

  const updateState = useCallback((s: LiveSessionState) => {
    setState(s);
    callbacksRef.current.onStateChange?.(s);
  }, []);

  const stopAllAudio = useCallback(() => {
    audioSourcesRef.current.forEach((source) => {
      try { source.stop(); } catch { /* ignore */ }
    });
    audioSourcesRef.current.clear();
    nextAudioStartTimeRef.current = 0;
  }, []);

  const cleanup = useCallback(async () => {
    // Prevent concurrent cleanup operations
    if (isCleaningUpRef.current) return;
    isCleaningUpRef.current = true;
    
    // Invalidate current session to prevent stale callbacks from processing
    currentSessionIdRef.current = 0;
    
    if (frameIntervalRef.current) {
      window.clearInterval(frameIntervalRef.current);
      frameIntervalRef.current = null;
    }
    
    // Clear worklet message handler FIRST to stop new audio from accumulating
    if (workletNodeRef.current) {
        try { workletNodeRef.current.port.onmessage = null; workletNodeRef.current.disconnect(); } catch { }
        workletNodeRef.current = null;
    }
    if (microphoneStreamRef.current) {
      microphoneStreamRef.current.getTracks().forEach(t => {
        try { t.stop(); } catch { }
      });
      microphoneStreamRef.current = null;
    }
    
    // Safely close input context
    if (inputAudioContextRef.current) {
      const ctx = inputAudioContextRef.current;
      inputAudioContextRef.current = null;
      if (ctx.state !== 'closed') {
          try { await ctx.close(); } catch { }
      }
    }
    
    // Safely close output context
    if (outputAudioContextRef.current) {
      stopAllAudio();
      const ctx = outputAudioContextRef.current;
      outputAudioContextRef.current = null;
      if (ctx.state !== 'closed') {
          try { await ctx.close(); } catch { }
      }
    }
    
    if (captureVideoRef.current) {
        try {
            // Only fully detach if we created this hidden element
            if (captureVideoRef.current.parentElement === document.body && captureVideoRef.current.style.position === 'fixed') {
                captureVideoRef.current.pause();
                captureVideoRef.current.srcObject = null;
                document.body.removeChild(captureVideoRef.current);
            } 
        } catch { }
        captureVideoRef.current = null;
    }
    canvasRef.current = null;

    if (sessionRef.current) {
        const session = sessionRef.current;
        sessionRef.current = null;
        try { if (typeof session.close === 'function') session.close(); } catch {}
    }
    
    // Clear all accumulators to free memory
    currentInputTranscriptionRef.current = '';
    currentOutputTranscriptionRef.current = '';
    currentUserAudioChunksRef.current = [];
    currentModelAudioChunksRef.current = [];
    currentModelAudioTotalLengthRef.current = 0;
    modelAudioSplitPointsRef.current = [];
    lastNewlineCountRef.current = 0;
    pendingUserTurnRef.current = null;
    
    isCleaningUpRef.current = false;
  }, [stopAllAudio]);

  // Load the AudioWorklet module (only needs to happen once per AudioContext)
  const ensureCaptureWorklet = useCallback(async (ctx: AudioContext) => {
    if (!ctx.audioWorklet || typeof ctx.audioWorklet.addModule !== 'function') {
      throw new Error('AudioWorklet is not supported');
    }
    await ctx.audioWorklet.addModule(FLOAT_TO_INT16_PROCESSOR_URL);
  }, []);

  const ensureVideoElementReady = useCallback(async (stream: MediaStream, providedElement?: HTMLVideoElement | null) => {
    if (providedElement && providedElement.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA && providedElement.videoWidth > 0 && providedElement.videoHeight > 0) {
      captureVideoRef.current = providedElement;
      return providedElement;
    }
    const video = providedElement ?? document.createElement('video');
    video.muted = true;
    video.playsInline = true;
    video.srcObject = stream;
    if (!providedElement) {
      video.style.position = 'fixed';
      video.style.width = '0px';
      video.style.height = '0px';
      video.style.opacity = '0';
      document.body.appendChild(video);
    }
    await video.play().catch(() => undefined);
    if (video.videoWidth === 0 || video.videoHeight === 0) {
      await new Promise<void>((resolve) => {
        const handler = () => { video.removeEventListener('loadedmetadata', handler); resolve(); };
        video.addEventListener('loadedmetadata', handler);
      });
    }
    captureVideoRef.current = video;
    return video;
  }, []);

  const start = useCallback(async (opts: { systemInstruction?: string, stream?: MediaStream, videoElement?: HTMLVideoElement | null }) => {
    const { stream, videoElement, systemInstruction } = opts;
    
    // Wait for any in-progress cleanup to finish
    while (isCleaningUpRef.current) {
      await new Promise(resolve => setTimeout(resolve, 10));
    }
    
    updateState('connecting');
    
    // Ensure previous session is fully cleaned
    await cleanup();
    
    // Generate a new session ID for this start call
    const sessionId = ++liveConversationSessionCounter;
    currentSessionIdRef.current = sessionId;

    const abortIfInvalidated = async () => {
      if (currentSessionIdRef.current !== sessionId) {
        while (isCleaningUpRef.current) {
          await new Promise(resolve => setTimeout(resolve, 10));
        }
        await cleanup();
        return true;
      }
      return false;
    };

    try {
      if (!stream || !stream.active) throw new Error('No active stream provided');

      // Video Setup
      await ensureVideoElementReady(stream, videoElement);
      if (await abortIfInvalidated()) return;
      const canvas = document.createElement('canvas');
      canvasRef.current = canvas;

      // Audio Setup
      const AudioContextCtor: typeof AudioContext = (window.AudioContext || (window as any).webkitAudioContext);
      
      const inputCtx = new AudioContextCtor({ sampleRate: INPUT_SAMPLE_RATE });
      inputAudioContextRef.current = inputCtx;
      
      // Use a new dedicated stream for audio to avoid conflicts
      const micStream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true } });
      microphoneStreamRef.current = micStream;
      if (await abortIfInvalidated()) return;
      
      const source = inputCtx.createMediaStreamSource(micStream);
      await ensureCaptureWorklet(inputCtx);
      if (await abortIfInvalidated()) return;
      const workletNode = new AudioWorkletNode(inputCtx, FLOAT_TO_INT16_PROCESSOR_NAME, { numberOfInputs: 1, numberOfOutputs: 0 });
      workletNodeRef.current = workletNode;

      const outputCtx = new AudioContextCtor({ sampleRate: OUTPUT_SAMPLE_RATE });
      outputAudioContextRef.current = outputCtx;

      const model = 'gemini-2.5-flash-native-audio-preview-12-2025';
      modelRef.current = model;
      logFinalizedRef.current = false;
      logRef.current = debugLogService.logRequest('useGeminiLiveConversation', model, {
        responseModalities: [Modality.AUDIO],
        systemInstruction: systemInstruction || '',
        inputAudioTranscription: {},
        outputAudioTranscription: {},
      });

      const ai = new GoogleGenAI({ apiKey: process.env.API_KEY });
      const session = await ai.live.connect({
        model,
        config: {
          responseModalities: [Modality.AUDIO],
          systemInstruction: systemInstruction,
          // Empty config objects to enable transcription without specifying parameters causing invalid argument errors
          inputAudioTranscription: {},
          outputAudioTranscription: {},
        },
        callbacks: {
          onopen: () => {
            // Check session is still valid before updating state
            if (currentSessionIdRef.current !== sessionId) return;
            updateState('active');
          },
          onmessage: async (msg: LiveServerMessage) => {
             // Check session is still valid before processing message
             if (currentSessionIdRef.current !== sessionId) return;
             
             // 1. Handle Audio Output
             const inlineAudio = msg.serverContent?.modelTurn?.parts?.[0]?.inlineData?.data;
             if (inlineAudio && outputAudioContextRef.current) {
                 try {
                    // Accumulate raw PCM16 for turn handling
                    const pcm16 = base64ToInt16(inlineAudio);
                    currentModelAudioChunksRef.current.push(pcm16);
                    // Track total audio sample count for transcript-synchronized splitting
                    currentModelAudioTotalLengthRef.current += pcm16.length;

                    const ctx = outputAudioContextRef.current;
                    const buffer = await decodePcm16ToAudioBuffer(base64ToUint8(inlineAudio), ctx, OUTPUT_SAMPLE_RATE, 1);
                    const source = ctx.createBufferSource();
                    source.buffer = buffer;
                    source.connect(ctx.destination);
                    const next = Math.max(nextAudioStartTimeRef.current, ctx.currentTime);
                    source.start(next);
                    nextAudioStartTimeRef.current = next + buffer.duration;
                    audioSourcesRef.current.add(source);
                    source.addEventListener('ended', () => audioSourcesRef.current.delete(source));
                 } catch (e) {
                     console.warn('Audio decode failed', e);
                 }
             }

             // 2. Handle Transcript Accumulation & Split Point Detection
             if (msg.serverContent?.inputTranscription?.text) {
               currentInputTranscriptionRef.current += msg.serverContent.inputTranscription.text;
             }
             if (msg.serverContent?.outputTranscription?.text) {
               const textPart = msg.serverContent.outputTranscription.text;
               currentOutputTranscriptionRef.current += textPart;
               
               // Detect new newlines to mark audio split points
               // Transcripts arrive with delay after audio, so we record the current
               // accumulated audio length as the split boundary when a newline appears.
               // This naturally accounts for the audio-ahead-of-transcript timing.
               const currentText = currentOutputTranscriptionRef.current;
               const newlineCount = (currentText.match(/\n/g) || []).length;
               
               if (newlineCount > lastNewlineCountRef.current) {
                 const diff = newlineCount - lastNewlineCountRef.current;
                 for (let i = 0; i < diff; i++) {
                   // Mark split point at current audio accumulation position
                   modelAudioSplitPointsRef.current.push(currentModelAudioTotalLengthRef.current);
                 }
                 lastNewlineCountRef.current = newlineCount;
               }
             }

             // 3. Handle Turn Completion (Exchange Finished)
             if (msg.serverContent?.turnComplete) {
                const userText = currentInputTranscriptionRef.current.trim();
                const modelText = currentOutputTranscriptionRef.current.trim();
                
                // Consolidate User Audio
                let userAudioFull = mergeInt16Arrays(currentUserAudioChunksRef.current);
                
                // Trim silence from user audio to avoid saving dead air
                if (userAudioFull.length > 0) {
                    userAudioFull = trimSilence(userAudioFull, INPUT_SAMPLE_RATE);
                }

                // Consolidate and Split Model Audio by transcript newlines
                const modelAudioFull = mergeInt16Arrays(currentModelAudioChunksRef.current);
                const modelAudioLines: Int16Array[] = [];
                
                if (modelAudioSplitPointsRef.current.length > 0 && modelAudioFull.length > 0) {
                    let startSample = 0;
                    // Ensure unique sorted split points within bounds
                    const points = [...new Set(modelAudioSplitPointsRef.current)]
                        .sort((a, b) => a - b)
                        .filter(p => p > 0 && p < modelAudioFull.length);
                    
                    for (const point of points) {
                        if (point > startSample) {
                            modelAudioLines.push(modelAudioFull.slice(startSample, point));
                            startSample = point;
                        }
                    }
                    // Add remainder as final segment
                    if (startSample < modelAudioFull.length) {
                        modelAudioLines.push(modelAudioFull.slice(startSample));
                    }
                } else if (modelAudioFull.length > 0) {
                    // No newlines detected, one single audio block
                    modelAudioLines.push(modelAudioFull);
                }

                const inputTranscript = currentInputTranscriptionRef.current;
                const outputTranscript = currentOutputTranscriptionRef.current;

                if (!modelText) {
                    if (userText || userAudioFull.length > 0 || inputTranscript) {
                        if (pendingUserTurnRef.current) {
                            const prev = pendingUserTurnRef.current;
                            const mergedText = [prev.text, userText].filter(Boolean).join('\n').trim();
                            const mergedTranscript = [prev.transcript, inputTranscript].filter(Boolean).join(' ').trim();
                            const mergedAudio = mergeInt16Arrays([prev.audio, userAudioFull]);
                            pendingUserTurnRef.current = { text: mergedText, transcript: mergedTranscript, audio: mergedAudio };
                        } else {
                            pendingUserTurnRef.current = {
                                text: userText,
                                transcript: inputTranscript,
                                audio: userAudioFull,
                            };
                        }
                    }
                    if (logRef.current && !logFinalizedRef.current) {
                      logRef.current.complete({
                        status: 'no-model-response',
                        userText,
                        inputTranscript,
                        modelAudioLinesCount: modelAudioLines.length,
                        userAudioSamples: userAudioFull.length,
                      });
                    }
                } else {
                    let finalUserText = userText;
                    let finalUserAudio = userAudioFull;
                    let finalInputTranscript = inputTranscript;
                    const pending = pendingUserTurnRef.current;
                    if (pending) {
                        finalUserText = pending.text || userText;
                        finalInputTranscript = pending.transcript || inputTranscript;
                        if (pending.audio.length > 0 && userAudioFull.length > 0) {
                            finalUserAudio = mergeInt16Arrays([pending.audio, userAudioFull]);
                        } else if (pending.audio.length > 0) {
                            finalUserAudio = pending.audio;
                        }
                        pendingUserTurnRef.current = null;
                    }

                    callbacksRef.current.onTurnComplete?.(finalUserText, modelText, finalUserAudio, modelAudioLines);
                    if (logRef.current && !logFinalizedRef.current) {
                      logRef.current.complete({
                        status: 'turn-complete',
                        userText: finalUserText,
                        modelText,
                        inputTranscript: finalInputTranscript,
                        outputTranscript,
                        modelAudioLinesCount: modelAudioLines.length,
                        userAudioSamples: finalUserAudio.length,
                      });
                    }
                    const turnLog = debugLogService.logRequest('useGeminiLiveConversation.turn', modelRef.current || 'gemini-live', {
                      inputTranscript: finalInputTranscript,
                      outputTranscript,
                    });
                    turnLog.complete({
                      status: 'turn-complete',
                      userText: finalUserText,
                      modelText,
                      inputTranscript: finalInputTranscript,
                      outputTranscript,
                      modelAudioLinesCount: modelAudioLines.length,
                      userAudioSamples: finalUserAudio.length,
                    });
                }
                // Reset all accumulators for next turn
                currentInputTranscriptionRef.current = '';
                currentOutputTranscriptionRef.current = '';
                currentUserAudioChunksRef.current = [];
                currentModelAudioChunksRef.current = [];
                currentModelAudioTotalLengthRef.current = 0;
                modelAudioSplitPointsRef.current = [];
                lastNewlineCountRef.current = 0;
             }

             // 4. Handle Interruption
             if (msg.serverContent?.interrupted) {
                 stopAllAudio();
                 // Reset model output accumulators and sync tracking
                 currentOutputTranscriptionRef.current = '';
                 currentModelAudioChunksRef.current = [];
                 currentModelAudioTotalLengthRef.current = 0;
                 modelAudioSplitPointsRef.current = [];
                 lastNewlineCountRef.current = 0;
             }
          },
          onclose: () => {
            // Check session is still valid before updating state
            if (currentSessionIdRef.current !== sessionId) return;
            sessionRef.current = null;
            if (logRef.current && !logFinalizedRef.current) {
              logFinalizedRef.current = true;
              logRef.current.complete({
                status: 'closed',
                inputTranscript: currentInputTranscriptionRef.current,
                outputTranscript: currentOutputTranscriptionRef.current,
              });
            }
            updateState('idle');
            cleanup();
          },
          onerror: (err: any) => {
            // Check session is still valid before updating state
            if (currentSessionIdRef.current !== sessionId) return;
            updateState('error');
            let message = "Connection error";
            try {
                if (err instanceof Error) message = err.message;
                else if (typeof err === 'string') message = err;
                else if (err && typeof err === 'object') {
                    if (err.type === 'error' && !err.message) message = "Connection Failed: Network or API Error";
                    else if (err.message) message = String(err.message);
                    else message = JSON.stringify(err);
                }
            } catch {
                message = "Unknown Connection Error";
            }
            if (logRef.current && !logFinalizedRef.current) {
              logFinalizedRef.current = true;
              logRef.current.error({
                message,
                inputTranscript: currentInputTranscriptionRef.current,
                outputTranscript: currentOutputTranscriptionRef.current,
              });
            }
            callbacksRef.current.onError?.(message);
            cleanup();
          }
        }
      });
      
      sessionRef.current = session;
      
      // Check if session was invalidated during async connect
      if (currentSessionIdRef.current !== sessionId) {
        try { session.close(); } catch {}
        return;
      }

      // Audio Streaming Loop (Worklet) with session validation
      workletNode.port.onmessage = (event: MessageEvent<Int16Array>) => {
          // CRITICAL: Check session is still valid before processing audio
          if (currentSessionIdRef.current !== sessionId) return;
          
          const pcm = event.data;
          if (!(pcm instanceof Int16Array) || !pcm.length) return;
          
          // Accumulate User Audio for history saving
          currentUserAudioChunksRef.current.push(pcm.slice()); // Slice to copy for accumulation
          
          try {
              sessionRef.current?.sendRealtimeInput({ media: encodeInt16ToBlob(pcm) });
          } catch {
              // Session may have been closed, ignore send errors
          }
      };
      source.connect(workletNode);

      // Video Streaming Loop (Canvas Resize) with session validation
      frameIntervalRef.current = window.setInterval(() => {
          // Check session is still valid
          if (currentSessionIdRef.current !== sessionId) return;
          
          const activeSession = sessionRef.current;
          const activeVideo = captureVideoRef.current;
          const activeCanvas = canvasRef.current;
          if (!activeSession || !activeVideo || !activeCanvas) return;
          if (activeVideo.videoWidth === 0) return;
          
          const ctx = activeCanvas.getContext('2d');
          if (!ctx) return;
          activeCanvas.width = activeVideo.videoWidth;
          activeCanvas.height = activeVideo.videoHeight;
          ctx.drawImage(activeVideo, 0, 0);
          
          activeCanvas.toBlob(async (blob) => {
              if (blob && sessionRef.current) {
                  const b64 = await blobToBase64(blob);
                  sessionRef.current.sendRealtimeInput({ media: { data: b64, mimeType: 'image/jpeg' } });
              }
          }, 'image/jpeg', 0.5);
      }, 1000);

    } catch (e) {
      updateState('error');
      if (logRef.current && !logFinalizedRef.current) {
        logFinalizedRef.current = true;
        logRef.current.error({
          message: e instanceof Error ? e.message : String(e),
          inputTranscript: currentInputTranscriptionRef.current,
          outputTranscript: currentOutputTranscriptionRef.current,
        });
      }
      callbacksRef.current.onError?.(e instanceof Error ? e.message : String(e));
      await cleanup();
    }
  }, [updateState, cleanup, stopAllAudio, ensureCaptureWorklet, ensureVideoElementReady]);

  const stop = useCallback(async () => {
    updateState('idle');
    if (logRef.current && !logFinalizedRef.current) {
      logFinalizedRef.current = true;
      logRef.current.complete({
        status: 'stopped',
        inputTranscript: currentInputTranscriptionRef.current,
        outputTranscript: currentOutputTranscriptionRef.current,
      });
    }
    await cleanup();
  }, [cleanup, updateState]);

  // Store cleanup in a ref so the unmount effect doesn't depend on cleanup identity
  const cleanupRef = useRef(cleanup);
  cleanupRef.current = cleanup;

  useEffect(() => {
      return () => { cleanupRef.current(); };
  }, []); // Empty deps - only runs on unmount

  return { start, stop };
}