
import { useCallback, useRef, useState, useEffect } from 'react';
import { GoogleGenAI, LiveServerMessage, Modality, Blob as GenAIBlob } from '@google/genai';

export interface UseGeminiLiveSttReturn {
  start: (language?: string) => Promise<void>;
  stop: () => void;
  transcript: string;
  isListening: boolean;
  error: string | null;
}

const STT_WORKLET_NAME = 'gemini-stt-processor';

// Helper to convert Uint8Array to Base64
function toBase64(bytes: Uint8Array) {
  let binary = '';
  const len = bytes.byteLength;
  for (let i = 0; i < len; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}

// Convert Float32 audio data to Int16 PCM Blob for Gemini
function encodeAudioChunkToBlob(float32Data: Float32Array): GenAIBlob {
  const l = float32Data.length;
  const int16 = new Int16Array(l);
  for (let i = 0; i < l; i++) {
    // Clamp and convert to 16-bit PCM
    let s = Math.max(-1, Math.min(1, float32Data[i]));
    int16[i] = s < 0 ? s * 0x8000 : s * 0x7FFF;
  }
  const bytes = new Uint8Array(int16.buffer);
  return {
    data: toBase64(bytes),
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
  const workletNodeRef = useRef<AudioWorkletNode | null>(null);
  const accumulatedTranscriptRef = useRef('');
  
  // Track registered contexts to prevent re-registering the worklet module
  const registeredContextsRef = useRef<WeakSet<AudioContext>>(new WeakSet());

  const cleanup = useCallback(async () => {
    if (workletNodeRef.current) {
      workletNodeRef.current.port.onmessage = null;
      workletNodeRef.current.disconnect();
      workletNodeRef.current = null;
    }
    if (streamRef.current) {
      streamRef.current.getTracks().forEach(t => t.stop());
      streamRef.current = null;
    }
    if (audioContextRef.current) {
      try { await audioContextRef.current.close(); } catch { /* ignore */ }
      audioContextRef.current = null;
    }
    if (sessionRef.current) {
      // Use close if available on the session object
      try { if (typeof sessionRef.current.close === 'function') sessionRef.current.close(); } catch { /* ignore */ }
      sessionRef.current = null;
    }
  }, []);

  const stop = useCallback(() => {
    cleanup();
    setIsListening(false);
  }, [cleanup]);

  // Ensure the AudioWorklet is loaded
  const ensureSttWorklet = useCallback(async (ctx: AudioContext) => {
    if (!ctx.audioWorklet) throw new Error("AudioWorklet not supported");
    if (registeredContextsRef.current.has(ctx)) return;

    const blob = new Blob(
      [
        `class GeminiSttProcessor extends AudioWorkletProcessor {
          process(inputs) {
            const input = inputs[0];
            if (input && input[0] && input[0].length > 0) {
              this.port.postMessage(input[0]);
            }
            return true;
          }
        }
        registerProcessor("${STT_WORKLET_NAME}", GeminiSttProcessor);`
      ],
      { type: "application/javascript" }
    );
    
    const url = URL.createObjectURL(blob);
    try {
      await ctx.audioWorklet.addModule(url);
      registeredContextsRef.current.add(ctx);
    } finally {
      URL.revokeObjectURL(url);
    }
  }, []);

  const start = useCallback(async (language?: string) => {
    await cleanup();
    setError(null);
    setTranscript('');
    accumulatedTranscriptRef.current = '';

    try {
      const ai = new GoogleGenAI({ apiKey: process.env.API_KEY });
      const session = await ai.live.connect({
        model: 'gemini-2.5-flash-native-audio-preview-12-2025',
        config: {
          responseModalities: [Modality.AUDIO], // Required by API even if we only care about transcription
          inputAudioTranscription: {}, // Enable Input Transcription
        },
        callbacks: {
          onopen: () => {
            setIsListening(true);
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
               accumulatedTranscriptRef.current += " "; 
            }
          },
          onclose: () => {
            setIsListening(false);
          },
          onerror: (err) => {
            console.error("Gemini Live STT error:", err);
            setError(err.message || "Connection error");
            stop();
          }
        }
      });
      sessionRef.current = session;

      // --- Audio Setup ---
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

      await ensureSttWorklet(ctx);

      const source = ctx.createMediaStreamSource(stream);
      const workletNode = new AudioWorkletNode(ctx, STT_WORKLET_NAME);
      workletNodeRef.current = workletNode;

      // Handle audio chunks from the worklet
      workletNode.port.onmessage = (event) => {
        const float32Data = event.data;
        if (sessionRef.current) {
           const blob = encodeAudioChunkToBlob(float32Data);
           // Using sendRealtimeInput is non-blocking here
           sessionRef.current.sendRealtimeInput({ media: blob });
        }
      };

      source.connect(workletNode);
      // Connect to destination to ensure the graph runs (muted)
      workletNode.connect(ctx.destination); 

    } catch (e: any) {
      console.error("STT Start Error", e);
      setError(e.message || "Failed to start Gemini Live STT");
      setIsListening(false);
      cleanup();
    }
  }, [cleanup, stop, ensureSttWorklet]);

  useEffect(() => {
    return () => { cleanup(); };
  }, [cleanup]);

  return { start, stop, transcript, isListening, error };
}
