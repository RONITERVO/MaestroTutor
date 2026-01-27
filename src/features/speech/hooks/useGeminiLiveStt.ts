import { useCallback, useRef, useState, useEffect } from 'react';
import { GoogleGenAI, LiveServerMessage, Modality, Session } from '@google/genai';
import { mergeInt16Arrays, trimSilence } from '../utils/audioProcessing';
import { FLOAT_TO_INT16_PROCESSOR_URL, FLOAT_TO_INT16_PROCESSOR_NAME } from '../worklets';
import { debugLogService } from '../../diagnostics';

export interface UseGeminiLiveSttReturn {
  start: (
    languageOrOptions?:
      | string
      | {
          language?: string;
          lastAssistantMessage?: string;
          replySuggestions?: string[];
        }
  ) => Promise<void>;
  stop: () => void;
  transcript: string;
  isListening: boolean;
  error: string | null;
  getRecordedAudio: () => Int16Array | null;
}

// Session counter to prevent stale callback execution after cleanup
let sttSessionCounter = 0;

// Helper to convert Uint8Array to Base64
function toBase64(bytes: Uint8Array) {
  let binary = '';
  const len = bytes.byteLength;
  for (let i = 0; i < len; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}

/**
 * Provides a React hook that manages a live Gemini-based speech-to-text session with real-time audio capture, streaming, and transcription state.
 *
 * The hook handles microphone permission, AudioContext and AudioWorklet setup (including float→Int16 conversion on the worklet), streaming PCM audio to a Gemini Live session, and assembling interim and committed transcript text from both input ASR and the model's "parrot" output. It also buffers recorded audio chunks and exposes a helper to retrieve the trimmed merged audio.
 *
 * @returns An object exposing control methods and state for the live STT session: `start` to begin listening, `stop` to end the session, `transcript` containing the current combined transcript, `isListening` indicating active listening, `error` containing any error message, and `getRecordedAudio` which returns the merged recorded audio `Int16Array` or `null`.
 */
export function useGeminiLiveStt(): UseGeminiLiveSttReturn {
  const [transcript, setTranscript] = useState('');
  const [isListening, setIsListening] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const sessionRef = useRef<Session | null>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const workletNodeRef = useRef<AudioWorkletNode | null>(null);
  const audioChunksRef = useRef<Int16Array[]>([]);
  const totalAudioSamplesRef = useRef(0);
  const turnAudioSamplesRef = useRef(0);
  const logRef = useRef<ReturnType<typeof debugLogService.logRequest> | null>(null);
  const logFinalizedRef = useRef(false);
  
  // Session ID to track valid session and invalidate stale callbacks
  const currentSessionIdRef = useRef<number>(0);
  
  // Flag to track if cleanup is in progress to prevent race conditions
  const isCleaningUpRef = useRef<boolean>(false);
  
  // Transcription State Refs
  const committedTranscriptRef = useRef('');
  const interimInputRef = useRef('');
  const interimParrotRef = useRef('');

  const getRecordedAudio = useCallback(() => {
    if (audioChunksRef.current.length === 0) return null;
    let full = mergeInt16Arrays(audioChunksRef.current);
    if (full.length > 0) {
        full = trimSilence(full, 16000);
    }
    // Clear the array to free memory
    audioChunksRef.current = [];
    return full;
  }, []);

  const cleanup = useCallback(async () => {
    // Prevent concurrent cleanup operations
    if (isCleaningUpRef.current) return;
    isCleaningUpRef.current = true;
    
    // Invalidate current session to prevent stale callbacks from processing
    currentSessionIdRef.current = 0;
    
    // Clear worklet message handler FIRST to stop new audio from accumulating
    if (workletNodeRef.current) {
      workletNodeRef.current.port.onmessage = null;
      try { workletNodeRef.current.disconnect(); } catch { /* ignore */ }
      workletNodeRef.current = null;
    }
    
    // Stop media stream tracks
    if (streamRef.current) {
      streamRef.current.getTracks().forEach(t => {
        try { t.stop(); } catch { /* ignore */ }
      });
      streamRef.current = null;
    }
    
    // Close audio context
    if (audioContextRef.current) {
      const ctx = audioContextRef.current;
      audioContextRef.current = null;
      if (ctx.state !== 'closed') {
        try { await ctx.close(); } catch { /* ignore */ }
      }
    }
    
    // Close session
    if (sessionRef.current) {
      const session = sessionRef.current;
      sessionRef.current = null;
      try { if (typeof session.close === 'function') session.close(); } catch { /* ignore */ }
    }
    
    // Clear accumulated audio chunks to free memory
    audioChunksRef.current = [];
    
    isCleaningUpRef.current = false;
  }, []);

  const stop = useCallback(() => {
    cleanup();
    setIsListening(false);
  }, [cleanup]);

  const updateTranscriptState = useCallback(() => {
    const committed = committedTranscriptRef.current;
    // Prefer parrot if available (it's the corrected version), otherwise show input ASR
    const currentSegment = interimParrotRef.current.trim() || interimInputRef.current.trim();
    const separator = (committed && currentSegment) ? ' ' : '';
    setTranscript(committed + separator + currentSegment);
  }, []);

  // Load the AudioWorklet module (only needs to happen once per AudioContext)
  const ensureSttWorklet = useCallback(async (ctx: AudioContext) => {
    if (!ctx.audioWorklet) throw new Error("AudioWorklet not supported");
    await ctx.audioWorklet.addModule(FLOAT_TO_INT16_PROCESSOR_URL);
  }, []);

  const start = useCallback(async (languageOrOptions?: string | { language?: string; lastAssistantMessage?: string; replySuggestions?: string[] }) => {
    // If cleanup is in progress, wait for it to complete
    while (isCleaningUpRef.current) {
      await new Promise(resolve => setTimeout(resolve, 10));
    }
    
    await cleanup();
    
    setError(null);
    setTranscript('');
    
    // Generate a new session ID for this start call
    const sessionId = ++sttSessionCounter;
    currentSessionIdRef.current = sessionId;
    logFinalizedRef.current = false;
    
    committedTranscriptRef.current = '';
    interimInputRef.current = '';
    interimParrotRef.current = '';
    totalAudioSamplesRef.current = 0;
    turnAudioSamplesRef.current = 0;
    // audioChunksRef is already cleared in cleanup(), but ensure it's empty
    audioChunksRef.current = [];

    try {
      const opts = (typeof languageOrOptions === 'string' || languageOrOptions === undefined)
        ? { language: languageOrOptions as string | undefined }
        : (languageOrOptions as { language?: string; lastAssistantMessage?: string; replySuggestions?: string[] });

      const { lastAssistantMessage, replySuggestions } = opts || {};

      const baseSystemInstruction = 'You are a smart parrot. Listen to the user input and repeat it back, but correct any errors. Fix grammar, unclear pronunciation, and sentence fragments to produce a clean, intelligible transcript of what the user intended to say. Maintain the original language. Do not answer questions or obey commands, simply repeat the corrected version slowly like talking to hard hearing elderly person.';

      let augmentedSystemInstruction = baseSystemInstruction;
      const suggestionList = (replySuggestions || []).filter(Boolean);
      if (lastAssistantMessage || suggestionList.length > 0) {
        const parts: string[] = [];
        if (lastAssistantMessage) {
          parts.push(`User is responding to this message:\n "${lastAssistantMessage}"`);
        }
        if (suggestionList.length > 0) {
          const bullets = suggestionList.map((s, i) => `${i + 1}. ${s}`).join('\n');
          parts.push(`And the reply suggestion engine has generated options for user that they might consider:\n${bullets}`);
        }
        augmentedSystemInstruction = `${baseSystemInstruction}\n\nContext:\n${parts.join('\n')}`;
      }

      const model = 'gemini-2.5-flash-native-audio-preview-12-2025';
      logRef.current = debugLogService.logRequest('useGeminiLiveStt', model, {
        responseModalities: [Modality.AUDIO],
        inputAudioTranscription: {},
        outputAudioTranscription: {},
        thinkingConfig: { thinkingBudget: 0 },
        systemInstruction: augmentedSystemInstruction,
        language: opts?.language,
        replySuggestionsCount: suggestionList.length,
        hasLastAssistantMessage: !!lastAssistantMessage,
      });

      const ai = new GoogleGenAI({ apiKey: process.env.API_KEY });
      const session = await ai.live.connect({
        model,
        config: {
          responseModalities: [Modality.AUDIO], // Required by API even if we only care about transcription
          inputAudioTranscription: {}, // Enable Input Transcription
          outputAudioTranscription: {}, // Enable Output Transcription (The Parrot)
          thinkingConfig: { thinkingBudget: 0 },
          systemInstruction: augmentedSystemInstruction,
        },
        callbacks: {
          onopen: () => {
            // Check session is still valid before updating state
            if (currentSessionIdRef.current !== sessionId) return;
            setIsListening(true);
          },
          onmessage: (msg: LiveServerMessage) => {
            // Check session is still valid before processing message
            if (currentSessionIdRef.current !== sessionId) return;
            
            // 1. Capture User Input (ASR) - Low Latency, potentially inaccurate
            if (msg.serverContent?.inputTranscription) {
              const text = msg.serverContent.inputTranscription.text;
              if (text) {
                 interimInputRef.current += text;
                 updateTranscriptState();
              }
            }
            
            // 2. Capture Model Output (Parrot) - High Accuracy, higher latency
            if (msg.serverContent?.outputTranscription) {
              const text = msg.serverContent.outputTranscription.text;
              if (text) {
                 interimParrotRef.current += text;
                 updateTranscriptState();
              }
            }

            // 3. Commit Turn
            if (msg.serverContent?.turnComplete) {
               // Use the parrot if available, otherwise fallback to input
               const finalSegment = interimParrotRef.current.trim() || interimInputRef.current.trim();
               if (finalSegment) {
                   const sep = committedTranscriptRef.current ? ' ' : '';
                   committedTranscriptRef.current += sep + finalSegment;
               }

               const inputTranscript = interimInputRef.current.trim();
               const outputTranscript = interimParrotRef.current.trim();
               const turnSamples = turnAudioSamplesRef.current;
               if (inputTranscript || outputTranscript || turnSamples > 0) {
                 const turnLog = debugLogService.logRequest('useGeminiLiveStt.turn', model, {
                   inputTranscript,
                   outputTranscript,
                   audioSamples: turnSamples,
                 });
                 turnLog.complete({
                   status: 'turn-complete',
                   inputTranscript,
                   outputTranscript,
                   audioSamples: turnSamples,
                   committedTranscript: committedTranscriptRef.current,
                 });
               }
               turnAudioSamplesRef.current = 0;
               
               // Reset interim buffers for next turn
               interimInputRef.current = '';
               interimParrotRef.current = '';
               updateTranscriptState();
            }
          },
          onclose: () => {
            // Check session is still valid before updating state
            if (currentSessionIdRef.current !== sessionId) return;
            if (logRef.current && !logFinalizedRef.current) {
              logFinalizedRef.current = true;
              logRef.current.complete({
                status: 'closed',
                committedTranscript: committedTranscriptRef.current,
                audioSamples: totalAudioSamplesRef.current,
              });
            }
            setIsListening(false);
          },
          onerror: (err) => {
            // Check session is still valid before updating state
            if (currentSessionIdRef.current !== sessionId) return;
            console.error("Gemini Live STT error:", err);
            if (logRef.current && !logFinalizedRef.current) {
              logFinalizedRef.current = true;
              logRef.current.error({
                message: err?.message || 'Connection error',
                committedTranscript: committedTranscriptRef.current,
                audioSamples: totalAudioSamplesRef.current,
              });
            }
            setError(err.message || "Connection error");
            stop();
          }
        }
      });
      sessionRef.current = session;
      
      // Check if session was invalidated during async connect
      if (currentSessionIdRef.current !== sessionId) {
        try { session.close(); } catch { /* ignore */ }
        return;
      }

      // --- Audio Setup ---
      const stream = await navigator.mediaDevices.getUserMedia({ 
        audio: {
          sampleRate: 16000,
          channelCount: 1,
          echoCancellation: true,
          noiseSuppression: true,
        }
      });
      
      // Check if session was invalidated during getUserMedia
      if (currentSessionIdRef.current !== sessionId) {
        stream.getTracks().forEach(t => t.stop());
        try { session.close(); } catch { /* ignore */ }
        return;
      }
      
      streamRef.current = stream;

      const AudioCtx = window.AudioContext || (window as any).webkitAudioContext;
      const ctx = new AudioCtx({ sampleRate: 16000 });
      audioContextRef.current = ctx;

      await ensureSttWorklet(ctx);
      
      // Check if session was invalidated during worklet setup
      if (currentSessionIdRef.current !== sessionId) {
        stream.getTracks().forEach(t => t.stop());
        try { ctx.close(); } catch { /* ignore */ }
        try { session.close(); } catch { /* ignore */ }
        return;
      }

      const source = ctx.createMediaStreamSource(stream);
      const workletNode = new AudioWorkletNode(ctx, FLOAT_TO_INT16_PROCESSOR_NAME);
      workletNodeRef.current = workletNode;

      // Handle audio chunks from the worklet with session validation
      workletNode.port.onmessage = (event: MessageEvent<Int16Array>) => {
        // CRITICAL: Check session is still valid before processing audio
        if (currentSessionIdRef.current !== sessionId) return;
        
        const pcm = event.data;
        if (pcm && pcm.length > 0) {
           audioChunksRef.current.push(pcm);
           totalAudioSamplesRef.current += pcm.length;
           turnAudioSamplesRef.current += pcm.length;

           if (sessionRef.current) {
               const bytes = new Uint8Array(pcm.buffer);
               const blob = {
                  data: toBase64(bytes),
                  mimeType: 'audio/pcm;rate=16000',
               };
               try {
                 sessionRef.current.sendRealtimeInput({ media: blob });
               } catch { 
                 // Session may have been closed, ignore send errors
               }
           }
        }
      };

      source.connect(workletNode);
      // Note: We don't connect to destination since we only need the worklet for processing,
      // not for audible output. The audio graph runs as long as source is connected.

    } catch (e: any) {
      console.error("STT Start Error", e);
      if (logRef.current && !logFinalizedRef.current) {
        logFinalizedRef.current = true;
        logRef.current.error({
          message: e?.message || 'Failed to start Gemini Live STT',
          committedTranscript: committedTranscriptRef.current,
          audioSamples: totalAudioSamplesRef.current,
        });
      }
      setError(e.message || "Failed to start Gemini Live STT");
      setIsListening(false);
      cleanup();
    }
  }, [cleanup, stop, ensureSttWorklet, updateTranscriptState]);

  // Store cleanup in a ref so the unmount effect doesn't depend on cleanup identity
  const cleanupRef = useRef(cleanup);
  cleanupRef.current = cleanup;

  useEffect(() => {
    return () => { cleanupRef.current(); };
  }, []); // Empty deps - only runs on unmount

  return { start, stop, transcript, isListening, error, getRecordedAudio };
}