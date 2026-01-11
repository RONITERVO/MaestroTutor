
import { useCallback, useEffect, useRef, useState } from 'react';
import { GoogleGenAI, LiveServerMessage, Modality, Blob as GenAIBlob } from '@google/genai';

export type LiveSessionState = 'idle' | 'connecting' | 'active' | 'error';

export interface UseGeminiLiveConversationCallbacks {
  onStateChange?: (state: LiveSessionState) => void;
  onError?: (message: string) => void;
}

const CAPTURE_WORKLET_NAME = 'gemini-live-mic-capture';
const INPUT_SAMPLE_RATE = 16000;
const OUTPUT_SAMPLE_RATE = 24000;

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

export function useGeminiLiveConversation(
  callbacks: UseGeminiLiveConversationCallbacks = {}
) {
  const [state, setState] = useState<LiveSessionState>('idle');
  
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
  const audioWorkletRegisteredContextsRef = useRef<WeakSet<AudioContext>>(new WeakSet());

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
    if (frameIntervalRef.current) {
      window.clearInterval(frameIntervalRef.current);
      frameIntervalRef.current = null;
    }
    if (workletNodeRef.current) {
        try { workletNodeRef.current.port.onmessage = null; workletNodeRef.current.disconnect(); } catch { }
        workletNodeRef.current = null;
    }
    if (microphoneStreamRef.current) {
      microphoneStreamRef.current.getTracks().forEach(t => t.stop());
      microphoneStreamRef.current = null;
    }
    if (inputAudioContextRef.current) {
      try { await inputAudioContextRef.current.close(); } catch { }
      inputAudioContextRef.current = null;
    }
    if (outputAudioContextRef.current) {
      stopAllAudio();
      try { await outputAudioContextRef.current.close(); } catch { }
      outputAudioContextRef.current = null;
    }
    
    if (captureVideoRef.current) {
        try {
            captureVideoRef.current.pause();
            captureVideoRef.current.srcObject = null;
            if (captureVideoRef.current.parentElement === document.body) {
                document.body.removeChild(captureVideoRef.current);
            }
        } catch { }
        captureVideoRef.current = null;
    }
    canvasRef.current = null;

    if (sessionRef.current) {
        try { if (typeof sessionRef.current.close === 'function') sessionRef.current.close(); } catch {}
        sessionRef.current = null;
    }
  }, [stopAllAudio]);

  const ensureCaptureWorklet = useCallback(async (ctx: AudioContext) => {
    if (!ctx.audioWorklet || typeof ctx.audioWorklet.addModule !== 'function') {
      throw new Error('AudioWorklet is not supported');
    }
    const registry = audioWorkletRegisteredContextsRef.current;
    if (registry.has(ctx)) return;
    const source = `class GeminiLiveMicCaptureProcessor extends AudioWorkletProcessor {\n  process(inputs){\n    const input = inputs[0];\n    if (input && input[0] && input[0].length){\n      this.port.postMessage(input[0].slice());\n    }\n    return true;\n  }\n}\nregisterProcessor('${CAPTURE_WORKLET_NAME}', GeminiLiveMicCaptureProcessor);`;
    const blob = new Blob([source], { type: 'application/javascript' });
    const url = URL.createObjectURL(blob);
    try {
      await ctx.audioWorklet.addModule(url);
      registry.add(ctx);
    } finally {
      URL.revokeObjectURL(url);
    }
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
    updateState('connecting');
    await cleanup();

    try {
      if (!stream || !stream.active) throw new Error('No active stream provided');

      // Video Setup
      const video = await ensureVideoElementReady(stream, videoElement);
      const canvas = document.createElement('canvas');
      canvasRef.current = canvas;

      // Audio Setup
      const AudioContextCtor: typeof AudioContext = (window.AudioContext || (window as any).webkitAudioContext);
      const inputCtx = new AudioContextCtor({ sampleRate: INPUT_SAMPLE_RATE });
      inputAudioContextRef.current = inputCtx;
      const micStream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true } });
      microphoneStreamRef.current = micStream;
      const source = inputCtx.createMediaStreamSource(micStream);
      await ensureCaptureWorklet(inputCtx);
      const workletNode = new AudioWorkletNode(inputCtx, CAPTURE_WORKLET_NAME, { numberOfInputs: 1, numberOfOutputs: 0 });
      workletNodeRef.current = workletNode;

      const outputCtx = new AudioContextCtor({ sampleRate: OUTPUT_SAMPLE_RATE });
      outputAudioContextRef.current = outputCtx;

      const ai = new GoogleGenAI({ apiKey: process.env.API_KEY });
      const session = await ai.live.connect({
        model: 'gemini-2.5-flash-native-audio-preview-12-2025',
        config: {
          responseModalities: [Modality.AUDIO],
          systemInstruction: systemInstruction,
        },
        callbacks: {
          onopen: () => {
            updateState('active');
          },
          onmessage: async (msg: LiveServerMessage) => {
             const inlineAudio = msg.serverContent?.modelTurn?.parts?.[0]?.inlineData?.data;
             if (inlineAudio && outputAudioContextRef.current) {
                 try {
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
             if (msg.serverContent?.interrupted) {
                 stopAllAudio();
             }
          },
          onclose: () => {
            sessionRef.current = null;
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
      
      sessionRef.current = session;

      // Audio Streaming Loop (Worklet)
      workletNode.port.onmessage = (event: MessageEvent<Float32Array>) => {
          const floatData = event.data;
          if (!(floatData instanceof Float32Array) || !floatData.length) return;
          const pcm = new Int16Array(floatData.length);
          for(let i=0; i<floatData.length; i++) {
              let s = Math.max(-1, Math.min(1, floatData[i]));
              pcm[i] = s < 0 ? s * 0x8000 : s * 0x7FFF;
          }
          try {
              sessionRef.current?.sendRealtimeInput({ media: encodeInt16ToBlob(pcm) });
          } catch {}
      };
      source.connect(workletNode);

      // Video Streaming Loop (Canvas Resize)
      frameIntervalRef.current = window.setInterval(() => {
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
      callbacksRef.current.onError?.(e instanceof Error ? e.message : String(e));
      cleanup();
    }
  }, [updateState, cleanup, stopAllAudio, ensureCaptureWorklet, ensureVideoElementReady]);

  const stop = useCallback(async () => {
    updateState('idle');
    await cleanup();
  }, [cleanup, updateState]);

  useEffect(() => {
      return () => { cleanup(); };
  }, [cleanup]);

  return { start, stop };
}
