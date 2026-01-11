
import { useCallback, useEffect, useRef, useState } from 'react';
import { GoogleGenAI, LiveServerMessage, Modality, Blob as GenAIBlob } from '@google/genai';

export type LiveSessionState = 'idle' | 'connecting' | 'active' | 'error';

export interface UseGeminiLiveConversationCallbacks {
  onStateChange?: (state: LiveSessionState) => void;
  onError?: (message: string) => void;
}

// Helpers for manual PCM encoding/decoding as per guidelines
function encode(bytes: Uint8Array) {
  let binary = '';
  const len = bytes.byteLength;
  for (let i = 0; i < len; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}

function decode(base64: string) {
  const binaryString = atob(base64);
  const len = binaryString.length;
  const bytes = new Uint8Array(len);
  for (let i = 0; i < len; i++) {
    bytes[i] = binaryString.charCodeAt(i);
  }
  return bytes;
}

function createBlob(data: Float32Array): GenAIBlob {
  const l = data.length;
  const int16 = new Int16Array(l);
  for (let i = 0; i < l; i++) {
    int16[i] = data[i] * 32768;
  }
  return {
    data: encode(new Uint8Array(int16.buffer)),
    mimeType: 'audio/pcm;rate=16000',
  };
}

async function decodeAudioData(
  data: Uint8Array,
  ctx: AudioContext,
  sampleRate: number,
  numChannels: number,
): Promise<AudioBuffer> {
  const int16 = new Int16Array(data.buffer);
  const frameCount = int16.length / numChannels;
  const buffer = ctx.createBuffer(numChannels, frameCount, sampleRate);

  for (let channel = 0; channel < numChannels; channel++) {
    const channelData = buffer.getChannelData(channel);
    for (let i = 0; i < frameCount; i++) {
      channelData[i] = int16[i * numChannels + channel] / 32768.0;
    }
  }
  return buffer;
}

export function useGeminiLiveConversation(
  callbacks: UseGeminiLiveConversationCallbacks = {}
) {
  const [state, setState] = useState<LiveSessionState>('idle');
  
  // Stores the active session PROMISE to allow immediate chaining before resolution
  const sessionPromiseRef = useRef<Promise<any> | null>(null);
  const activeSessionRef = useRef<any>(null);

  const inputContextRef = useRef<AudioContext | null>(null);
  const outputContextRef = useRef<AudioContext | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const outputNodeRef = useRef<GainNode | null>(null);
  const processorRef = useRef<ScriptProcessorNode | null>(null); 
  
  const nextStartTimeRef = useRef<number>(0);
  const frameIntervalRef = useRef<number | null>(null);

  const callbacksRef = useRef(callbacks);
  useEffect(() => { callbacksRef.current = callbacks; }, [callbacks]);

  const updateState = useCallback((s: LiveSessionState) => {
    setState(s);
    callbacksRef.current.onStateChange?.(s);
  }, []);

  const cleanup = useCallback(() => {
    if (frameIntervalRef.current) {
      clearInterval(frameIntervalRef.current);
      frameIntervalRef.current = null;
    }
    if (processorRef.current) {
        processorRef.current.disconnect();
        processorRef.current = null;
    }
    if (streamRef.current) {
      streamRef.current.getTracks().forEach(t => t.stop());
      streamRef.current = null;
    }
    if (inputContextRef.current) {
      inputContextRef.current.close().catch(() => {});
      inputContextRef.current = null;
    }
    if (outputContextRef.current) {
      outputContextRef.current.close().catch(() => {});
      outputContextRef.current = null;
    }
    outputNodeRef.current = null;
    nextStartTimeRef.current = 0;
    
    // Close the session if active
    if (activeSessionRef.current) {
        try {
            // Live session close not always exposed but good practice if available
            if (typeof activeSessionRef.current.close === 'function') {
                activeSessionRef.current.close();
            }
        } catch {}
        activeSessionRef.current = null;
    }
    sessionPromiseRef.current = null;
  }, []);

  const start = useCallback(async (opts: { systemInstruction?: string, stream?: MediaStream, videoElement?: HTMLVideoElement | null }) => {
    updateState('connecting');
    cleanup();

    try {
      const ai = new GoogleGenAI({ apiKey: process.env.API_KEY });
      const sessionPromise = ai.live.connect({
        model: 'gemini-2.5-flash-native-audio-preview-12-2025',
        config: {
          responseModalities: [Modality.AUDIO],
          systemInstruction: opts.systemInstruction,
        },
        callbacks: {
          onopen: () => {
            updateState('active');
            try {
                startAudioCapture();
            } catch (e) {
                console.error("Audio capture start failed", e);
                updateState('error');
                callbacksRef.current.onError?.("Failed to start microphone");
                cleanup();
            }
          },
          onmessage: async (msg: LiveServerMessage) => {
             const audioData = msg.serverContent?.modelTurn?.parts?.[0]?.inlineData?.data;
             if (audioData && outputContextRef.current && outputNodeRef.current) {
                 await playAudioChunk(audioData);
             }
             if (msg.serverContent?.interrupted) {
                 nextStartTimeRef.current = 0; 
             }
          },
          onclose: () => {
            updateState('idle');
            cleanup();
          },
          onerror: (err) => {
            updateState('error');
            callbacksRef.current.onError?.(String(err));
            cleanup();
          }
        }
      });
      
      sessionPromiseRef.current = sessionPromise;
      // Store resolved session when available for later direct access if needed, but rely on promise chaining for data flow
      sessionPromise.then(s => { activeSessionRef.current = s; });

      // Initialize Output Audio Context
      const AudioCtx = window.AudioContext || (window as any).webkitAudioContext;
      const outputCtx = new AudioCtx({ sampleRate: 24000 }); 
      outputContextRef.current = outputCtx;
      const gain = outputCtx.createGain();
      gain.connect(outputCtx.destination);
      outputNodeRef.current = gain;

      // Handle video frame streaming
      if (opts.videoElement) {
          const videoEl = opts.videoElement;
          const canvas = document.createElement('canvas');
          const ctx = canvas.getContext('2d');
          
          frameIntervalRef.current = window.setInterval(async () => {
              if (videoEl.paused || videoEl.ended) return;
              if (videoEl.videoWidth === 0) return;
              
              canvas.width = videoEl.videoWidth;
              canvas.height = videoEl.videoHeight;
              ctx?.drawImage(videoEl, 0, 0);
              
              const base64 = canvas.toDataURL('image/jpeg', 0.5).split(',')[1];
              try {
                  // Chain off the promise to ensure we only send when connected
                  if (sessionPromiseRef.current) {
                      sessionPromiseRef.current.then(session => {
                          session.sendRealtimeInput({
                              media: { mimeType: 'image/jpeg', data: base64 }
                          });
                      });
                  }
              } catch { }
          }, 1000); 
      }

      function startAudioCapture() {
          const InputAudioCtx = window.AudioContext || (window as any).webkitAudioContext;
          const inputCtx = new InputAudioCtx({ sampleRate: 16000 });
          inputContextRef.current = inputCtx;

          navigator.mediaDevices.getUserMedia({ 
              audio: { 
                  echoCancellation: true,
                  noiseSuppression: true,
                  sampleRate: 16000
              } 
          }).then(stream => {
              streamRef.current = stream;
              const source = inputCtx.createMediaStreamSource(stream);
              const processor = inputCtx.createScriptProcessor(4096, 1, 1);
              processorRef.current = processor;
              
              processor.onaudioprocess = (e) => {
                  const inputData = e.inputBuffer.getChannelData(0);
                  const pcmBlob = createBlob(inputData);
                  
                  // CRITICAL: Always use the promise to ensure session is ready
                  if (sessionPromiseRef.current) {
                      sessionPromiseRef.current.then(session => {
                          session.sendRealtimeInput({ media: pcmBlob });
                      });
                  }
              };
              
              source.connect(processor);
              processor.connect(inputCtx.destination);
          });
      }

      async function playAudioChunk(base64: string) {
          if (!outputContextRef.current || !outputNodeRef.current) return;
          const ctx = outputContextRef.current;
          const audioBuffer = await decodeAudioData(decode(base64), ctx, 24000, 1);
          
          const source = ctx.createBufferSource();
          source.buffer = audioBuffer;
          source.connect(outputNodeRef.current);
          
          const now = ctx.currentTime;
          const start = Math.max(now, nextStartTimeRef.current);
          source.start(start);
          nextStartTimeRef.current = start + audioBuffer.duration;
      }

    } catch (e) {
      updateState('error');
      callbacksRef.current.onError?.(e instanceof Error ? e.message : String(e));
      cleanup();
    }
  }, [updateState, cleanup]);

  const stop = useCallback(async () => {
    updateState('idle');
    cleanup();
  }, [cleanup, updateState]);

  useEffect(() => {
      return () => cleanup();
  }, [cleanup]);

  return { start, stop };
}
