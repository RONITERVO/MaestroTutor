
import { useCallback, useRef, useState, useEffect } from 'react';
import { GoogleGenAI, LiveServerMessage, Modality, Blob } from '@google/genai';

export interface UseGeminiLiveSttReturn {
  start: (language?: string) => Promise<void>;
  stop: () => void;
  transcript: string;
  isListening: boolean;
  error: string | null;
}

function encode(bytes: Uint8Array) {
  let binary = '';
  const len = bytes.byteLength;
  for (let i = 0; i < len; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}

function createBlob(data: Float32Array): Blob {
  const l = data.length;
  const int16 = new Int16Array(l);
  for (let i = 0; i < l; i++) {
    let s = Math.max(-1, Math.min(1, data[i]));
    int16[i] = s < 0 ? s * 0x8000 : s * 0x7FFF;
  }
  return {
    data: encode(new Uint8Array(int16.buffer)),
    mimeType: 'audio/pcm;rate=16000',
  };
}

export function useGeminiLiveStt(): UseGeminiLiveSttReturn {
  const [transcript, setTranscript] = useState('');
  const [isListening, setIsListening] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const sessionRef = useRef<any>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const processorRef = useRef<ScriptProcessorNode | null>(null);
  const accumulatedTranscriptRef = useRef('');

  const cleanup = useCallback(() => {
    if (processorRef.current) {
      processorRef.current.disconnect();
      processorRef.current = null;
    }
    if (streamRef.current) {
      streamRef.current.getTracks().forEach(t => t.stop());
      streamRef.current = null;
    }
    if (audioContextRef.current) {
      audioContextRef.current.close().catch(() => {});
      audioContextRef.current = null;
    }
    if (sessionRef.current) {
      sessionRef.current = null;
    }
  }, []);

  const stop = useCallback(() => {
    cleanup();
    setIsListening(false);
  }, [cleanup]);

  const start = useCallback(async (language?: string) => {
    cleanup();
    setError(null);
    setTranscript('');
    accumulatedTranscriptRef.current = '';

    try {
      const ai = new GoogleGenAI({ apiKey: process.env.API_KEY });
      const sessionPromise = ai.live.connect({
        model: 'gemini-2.5-flash-native-audio-preview-12-2025',
        config: {
          responseModalities: [Modality.AUDIO], // Required, even if we ignore output
          inputAudioTranscription: {}, // Enable input transcription
        },
        callbacks: {
          onopen: async () => {
            setIsListening(true);
            try {
              const stream = await navigator.mediaDevices.getUserMedia({ 
                audio: {
                  sampleRate: 16000,
                  channelCount: 1,
                  echoCancellation: true,
                  noiseSuppression: true,
                }
              });
              streamRef.current = stream;

              const AudioCtx = window.AudioContext || (window as any).webkitAudioContext;
              const ctx = new AudioCtx({ sampleRate: 16000 });
              audioContextRef.current = ctx;

              const source = ctx.createMediaStreamSource(stream);
              const processor = ctx.createScriptProcessor(4096, 1, 1);
              processorRef.current = processor;

              processor.onaudioprocess = (e) => {
                const inputData = e.inputBuffer.getChannelData(0);
                const blob = createBlob(inputData);
                sessionPromise.then(session => {
                   session.sendRealtimeInput({ media: blob });
                });
              };

              source.connect(processor);
              processor.connect(ctx.destination);
            } catch (err) {
              console.error("Mic error:", err);
              setError("Microphone access failed");
              stop();
            }
          },
          onmessage: (msg: LiveServerMessage) => {
            if (msg.serverContent?.inputTranscription) {
              const text = msg.serverContent.inputTranscription.text;
              if (text) {
                 accumulatedTranscriptRef.current += text;
                 setTranscript(accumulatedTranscriptRef.current);
              }
            }
            if (msg.serverContent?.turnComplete) {
               // Optional: handle turn completion if needed
               // For continuous STT, we just keep accumulating or reset if desired.
               // Here we assume simple continuous transcription.
               accumulatedTranscriptRef.current += " "; // Space between turns
            }
          },
          onclose: () => {
            setIsListening(false);
          },
          onerror: (err) => {
            console.error("Gemini Live error:", err);
            setError(err.message || "Connection error");
            stop();
          }
        }
      });
      sessionRef.current = sessionPromise;
    } catch (e: any) {
      setError(e.message || "Failed to start Gemini Live");
      setIsListening(false);
    }
  }, [cleanup, stop]);

  useEffect(() => {
    return () => cleanup();
  }, [cleanup]);

  return { start, stop, transcript, isListening, error };
}
