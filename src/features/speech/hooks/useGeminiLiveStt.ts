// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useCallback, useRef, useState, useEffect } from 'react';
import { Capacitor } from '@capacitor/core';
import { LiveServerMessage, Modality, Session } from '@google/genai';
import { getAi } from '../../../api/gemini/client';
import { mergeInt16Arrays, trimSilence } from '../utils/audioProcessing';
import { SpeechGate, measureEnergy } from '../../../../shared/audio/speechGate';
import { FLOAT_TO_INT16_PROCESSOR_URL, FLOAT_TO_INT16_PROCESSOR_NAME } from '../worklets';
import { debugLogService } from '../../diagnostics';
import { getGeminiModels } from '../../../core/config/models';
import { translations } from '../../../core/i18n';
import { AudioCodecWorkerClient } from '../utils/audioCodecWorkerClient';
import { type CaptureWorkletMessage, flushCaptureWorkletNode } from '../utils/captureWorkletMessaging';
import {
  RealtimePcmPacketizer,
  type RealtimePcmPacketizerStats,
} from '../utils/realtimePcmPacketizer';
import { errorSttFlow, logSttFlow } from '../../../shared/utils/sttFlowDebug';
import { getLiveMinimalThinkingConfig } from '../config/liveModelCompatibility';
import { createLiveUsageTracker } from '../../../shared/utils/costTracker';
import {
  acquireLocalWhisperClient,
  releaseLocalWhisperClient,
  type LocalWhisperClient,
} from '../utils/localWhisperClient';
import {
  evaluateFreshSpeechFallback,
  isLikelySpeechTranscript,
  LOCAL_SPEECH_BUFFER_MS,
  LOCAL_SPEECH_PREROLL_MS,
  LOCAL_WHISPER_MODEL,
  LOCAL_WHISPER_REQUEST_INTERVAL_MS,
  pcmPacketsToWhisperWindow,
  recentPcmPackets,
} from '../utils/liveSpeechDetection';

export interface GeminiLiveSttTurnComplete {
  turnId: number;
  turnTranscript: string;
  committedTranscript: string;
  inputTranscript: string;
  outputTranscript: string;
  audioSamples: number;
}

export interface UseGeminiLiveSttOptions {
  onTurnComplete?: (turn: GeminiLiveSttTurnComplete) => void | Promise<void>;
  autoStopAfterTurnComplete?: boolean;
  /**
   * Hold microphone packets locally until energy plus Whisper confirms speech.
   * Enabled by default so silence and ambient noise do not consume Live tokens.
   */
  gateInputOnSpeech?: boolean;
}

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
  stop: () => Promise<void>;
  transcript: string;
  isListening: boolean;
  error: string | null;
  getRecordedAudio: () => Int16Array | null;
}

// Session counter to prevent stale callback execution after cleanup
let sttSessionCounter = 0;
const TRANSCRIPT_UPDATE_INTERVAL_MS = 60;
const INPUT_SAMPLE_RATE = 16000;
const LIVE_INPUT_PACKET_DURATION_MS = 100;
const LIVE_INPUT_PACKET_MAX_WAIT_MS = 120;
const SPEECH_GATE_BUFFER_SAMPLES = Math.round(INPUT_SAMPLE_RATE * LOCAL_SPEECH_BUFFER_MS / 1000);
const SPEECH_GATE_PREROLL_SAMPLES = Math.round(INPUT_SAMPLE_RATE * LOCAL_SPEECH_PREROLL_MS / 1000);
const SPEECH_GATE_REPLAY_CHUNK_SAMPLES = 4096;
const LOCAL_WHISPER_LOAD_GRACE_MS = 12_000;

interface SttAudioTelemetry {
  encodeErrors: number;
  transcriptLinkedSamples: number;
  gatedPackets: number;
  audioStreamEnds: number;
  whisperChecks: number;
  whisperAccepted: number;
  whisperRejected: number;
  whisperErrors: number;
  energyFallbacks: number;
}

const createEmptySttAudioTelemetry = (): SttAudioTelemetry => ({
  encodeErrors: 0,
  transcriptLinkedSamples: 0,
  gatedPackets: 0,
  audioStreamEnds: 0,
  whisperChecks: 0,
  whisperAccepted: 0,
  whisperRejected: 0,
  whisperErrors: 0,
  energyFallbacks: 0,
});

const toTransferableArrayBuffer = (pcm: Int16Array): ArrayBuffer => {
  // Keep the original chunk intact because we also retain it for recorded-audio
  // assembly after the turn completes. Transferring the original buffer would
  // detach it and break later WAV creation in the send path.
  return pcm.slice().buffer;
};

/**
 * Provides a React hook that manages a live Gemini-based speech-to-text session with real-time audio capture, streaming, and transcription state.
 *
 * The hook handles microphone permission, AudioContext and AudioWorklet setup (including float→Int16 conversion on the worklet), streaming PCM audio to a Gemini Live session, and assembling interim and committed transcript text from both input ASR and the model's "parrot" output. It also buffers recorded audio chunks and exposes a helper to retrieve the trimmed merged audio.
 *
 * @returns An object exposing control methods and state for the live STT session: `start` to begin listening, `stop` to end the session, `transcript` containing the current combined transcript, `isListening` indicating active listening, `error` containing any error message, and `getRecordedAudio` which returns the merged recorded audio `Int16Array` or `null`.
 */
export function useGeminiLiveStt(options?: UseGeminiLiveSttOptions): UseGeminiLiveSttReturn {
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
  const transcribedAudioSamplesRef = useRef(0);
  const logRef = useRef<ReturnType<typeof debugLogService.logRequest> | null>(null);
  const logFinalizedRef = useRef(false);
  const codecWorkerRef = useRef<AudioCodecWorkerClient | null>(null);
  const inputPacketizerRef = useRef<RealtimePcmPacketizer | null>(null);
  const audioTelemetryRef = useRef<SttAudioTelemetry>(createEmptySttAudioTelemetry());
  const speechGateRef = useRef<SpeechGate | null>(null);
  const gatePrerollRef = useRef<Int16Array[]>([]);
  const localWhisperRef = useRef<LocalWhisperClient | null>(null);
  const localWhisperBusyRef = useRef(false);
  const lastWhisperRequestAtRef = useRef(0);
  const loadingFallbackOnsetAtRef = useRef<number | null>(null);
  const whisperFailureWarnedRef = useRef(false);
  const speechGateEpochRef = useRef(0);
  const transcriptUpdateTimerRef = useRef<number | null>(null);
  const lastRenderedTranscriptRef = useRef('');
  
  // Session ID to track valid session and invalidate stale callbacks
  const currentSessionIdRef = useRef<number>(0);
  const onTurnCompleteRef = useRef(options?.onTurnComplete);
  const autoStopAfterTurnCompleteRef = useRef(options?.autoStopAfterTurnComplete !== false);
  const gateInputOnSpeechRef = useRef(options?.gateInputOnSpeech !== false);
  const turnIdRef = useRef(0);
  
  // Flag to track if cleanup is in progress to prevent race conditions
  const isCleaningUpRef = useRef<boolean>(false);
  
  // Transcription State Refs
  const committedTranscriptRef = useRef('');
  const interimInputRef = useRef('');
  const interimParrotRef = useRef('');

  useEffect(() => {
    onTurnCompleteRef.current = options?.onTurnComplete;
  }, [options?.onTurnComplete]);

  useEffect(() => {
    autoStopAfterTurnCompleteRef.current = options?.autoStopAfterTurnComplete !== false;
  }, [options?.autoStopAfterTurnComplete]);

  useEffect(() => {
    gateInputOnSpeechRef.current = options?.gateInputOnSpeech !== false;
  }, [options?.gateInputOnSpeech]);

  const getInputPacketizerStats = useCallback((): RealtimePcmPacketizerStats => (
    inputPacketizerRef.current?.getStats() ?? {
      totalInputSamples: 0,
      totalOutputSamples: 0,
      packetsSent: 0,
      partialPacketsSent: 0,
      timerFlushes: 0,
      explicitFlushes: 0,
      maxBufferedSamples: 0,
    }
  ), []);

  const getAudioTelemetrySnapshot = useCallback(() => ({
    packetizer: getInputPacketizerStats(),
    ...audioTelemetryRef.current,
  }), [getInputPacketizerStats]);

  const getRecordedAudio = useCallback(() => {
    if (audioChunksRef.current.length === 0) return null;
    let full = mergeInt16Arrays(audioChunksRef.current);
    const transcriptLinkedSamples = Math.min(transcribedAudioSamplesRef.current, full.length);
    if (transcriptLinkedSamples <= 0) {
      audioChunksRef.current = [];
      transcribedAudioSamplesRef.current = 0;
      audioTelemetryRef.current.transcriptLinkedSamples = 0;
      return null;
    }
    full = full.slice(0, transcriptLinkedSamples);
    if (full.length > 0) {
        full = trimSilence(full, INPUT_SAMPLE_RATE);
    }
    // Clear the array to free memory
    audioChunksRef.current = [];
    transcribedAudioSamplesRef.current = 0;
    audioTelemetryRef.current.transcriptLinkedSamples = 0;
    return full;
  }, []);

  const clearTranscriptUpdateTimer = useCallback(() => {
    if (transcriptUpdateTimerRef.current !== null) {
      window.clearTimeout(transcriptUpdateTimerRef.current);
      transcriptUpdateTimerRef.current = null;
    }
  }, []);

  const ensureCodecWorker = useCallback(() => {
    if (!codecWorkerRef.current) {
      codecWorkerRef.current = new AudioCodecWorkerClient();
    }
    return codecWorkerRef.current;
  }, []);

  const cleanup = useCallback(async (options?: { preserveRecordedAudio?: boolean; status?: string }) => {
    // Prevent concurrent cleanup operations
    if (isCleaningUpRef.current) return;
    isCleaningUpRef.current = true;
    const preserveRecordedAudio = options?.preserveRecordedAudio === true;
    const status = options?.status || 'stopped';

    const activeCaptureNode = workletNodeRef.current;
    if (activeCaptureNode) {
      await flushCaptureWorkletNode(activeCaptureNode);
      // The flush acknowledgement is now processed; stop accepting more PCM
      // before waiting for the packetizer's asynchronous Whisper/encode queue.
      activeCaptureNode.port.onmessage = null;
    }
    if (inputPacketizerRef.current) {
      await inputPacketizerRef.current.flushPending();
      if (speechGateRef.current?.isOpen && sessionRef.current) {
        try {
          sessionRef.current.sendRealtimeInput({ audioStreamEnd: true });
          audioTelemetryRef.current.audioStreamEnds += 1;
        } catch {
          // The Live socket may already be closing; teardown must still finish.
        }
      }
      inputPacketizerRef.current.dispose();
      inputPacketizerRef.current = null;
    }
    speechGateRef.current?.forceClose();
    speechGateRef.current = null;
    gatePrerollRef.current = [];
    loadingFallbackOnsetAtRef.current = null;
    speechGateEpochRef.current += 1;

    // Invalidate current session to prevent stale callbacks from processing
    currentSessionIdRef.current = 0;
    clearTranscriptUpdateTimer();
    
    // Clear worklet message handler FIRST to stop new audio from accumulating
    if (activeCaptureNode) {
      activeCaptureNode.port.onmessage = null;
      try { activeCaptureNode.disconnect(); } catch { /* ignore */ }
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

    if (codecWorkerRef.current) {
      codecWorkerRef.current.dispose();
      codecWorkerRef.current = null;
    }
    
    if (logRef.current && !logFinalizedRef.current) {
      logFinalizedRef.current = true;
      logRef.current.complete({
        status,
        committedTranscript: committedTranscriptRef.current,
        audioSamples: totalAudioSamplesRef.current,
        audioTelemetry: getAudioTelemetrySnapshot(),
      });
    }

    if (!preserveRecordedAudio) {
      audioChunksRef.current = [];
      transcribedAudioSamplesRef.current = 0;
      audioTelemetryRef.current.transcriptLinkedSamples = 0;
    }
    
    isCleaningUpRef.current = false;
  }, [clearTranscriptUpdateTimer, getAudioTelemetrySnapshot]);

  const stop = useCallback(async () => {
    await cleanup({ status: 'stopped' });
    setIsListening(false);
  }, [cleanup]);

  const flushTranscriptState = useCallback(() => {
    clearTranscriptUpdateTimer();
    const committed = committedTranscriptRef.current;
    // Prefer parrot if available (it's the corrected version), otherwise show input ASR
    const currentSegment = interimParrotRef.current.trim() || interimInputRef.current.trim();
    const separator = (committed && currentSegment) ? ' ' : '';
    const nextTranscript = committed + separator + currentSegment;
    if (nextTranscript === lastRenderedTranscriptRef.current) return;
    lastRenderedTranscriptRef.current = nextTranscript;
    setTranscript(nextTranscript);
  }, [clearTranscriptUpdateTimer]);

  const scheduleTranscriptStateUpdate = useCallback((immediate = false) => {
    if (immediate) {
      flushTranscriptState();
      return;
    }
    if (transcriptUpdateTimerRef.current !== null) return;
    transcriptUpdateTimerRef.current = window.setTimeout(() => {
      transcriptUpdateTimerRef.current = null;
      flushTranscriptState();
    }, TRANSCRIPT_UPDATE_INTERVAL_MS);
  }, [flushTranscriptState]);

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
    
    await cleanup({ status: 'restarted' });
    
    setError(null);
    setTranscript('');
    lastRenderedTranscriptRef.current = '';
    
    // Generate a new session ID for this start call
    const sessionId = ++sttSessionCounter;
    currentSessionIdRef.current = sessionId;
    logFinalizedRef.current = false;
    
    committedTranscriptRef.current = '';
    interimInputRef.current = '';
    interimParrotRef.current = '';
    turnIdRef.current = 0;
    totalAudioSamplesRef.current = 0;
    turnAudioSamplesRef.current = 0;
    transcribedAudioSamplesRef.current = 0;
    audioTelemetryRef.current = createEmptySttAudioTelemetry();
    // audioChunksRef is already cleared in cleanup(), but ensure it's empty
    audioChunksRef.current = [];

    const gateInputOnSpeech = gateInputOnSpeechRef.current;
    speechGateRef.current = gateInputOnSpeech
      ? new SpeechGate({ requireConfirmation: true })
      : null;
    gatePrerollRef.current = [];
    lastWhisperRequestAtRef.current = 0;
    loadingFallbackOnsetAtRef.current = null;
    const speechGateEpoch = speechGateEpochRef.current;

    try {
      // --- 1. Request Microphone Permission FIRST ---
      // This ensures we have access before opening the expensive WebSocket connection.
      // It also fixes the UX issue where the app asks for permission "late".
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          sampleRate: INPUT_SAMPLE_RATE,
          channelCount: 1,
          echoCancellation: true,
          noiseSuppression: true,
        }
      });

      // Check if session was stopped while waiting for permission
      if (currentSessionIdRef.current !== sessionId) {
        stream.getTracks().forEach(t => t.stop());
        return;
      }

      streamRef.current = stream;

      // Permission comes first: do not download or allocate the local model if
      // the user declines microphone access.
      if (gateInputOnSpeech) {
        try {
          localWhisperRef.current ??= acquireLocalWhisperClient({
            model: LOCAL_WHISPER_MODEL,
            // Native stays quantized to protect WebView memory. Desktop browsers
            // may use fp32 only when the compact profile is unavailable.
            allowFp32Fallback: !Capacitor.isNativePlatform(),
          });
          const detector = localWhisperRef.current;
          if (detector.status === 'idle') {
            void detector.initialize().catch((error) => {
              if (localWhisperRef.current !== detector) return;
              audioTelemetryRef.current.whisperErrors += 1;
              if (!whisperFailureWarnedRef.current) {
                whisperFailureWarnedRef.current = true;
                console.warn('Local Whisper unavailable; STT is using the energy-only fallback.', error);
              }
            });
          }
        } catch (error) {
          audioTelemetryRef.current.whisperErrors += 1;
          if (!whisperFailureWarnedRef.current) {
            whisperFailureWarnedRef.current = true;
            console.warn('Local Whisper worker could not start; STT is using the energy-only fallback.', error);
          }
        }
      }

      // --- 2. Initialize Audio Context & Worklet ---
      const AudioCtx = window.AudioContext || (window as any).webkitAudioContext;
      const ctx = new AudioCtx({ sampleRate: INPUT_SAMPLE_RATE });
      audioContextRef.current = ctx;

      await ensureSttWorklet(ctx);

      if (currentSessionIdRef.current !== sessionId) {
        stream.getTracks().forEach(t => t.stop());
        try { ctx.close(); } catch { /* ignore */ }
        return;
      }

      // --- 3. Connect to Gemini Live API ---
      const opts = (typeof languageOrOptions === 'string' || languageOrOptions === undefined)
        ? { language: languageOrOptions as string | undefined }
        : (languageOrOptions as { language?: string; lastAssistantMessage?: string; replySuggestions?: string[] });

      let { language, lastAssistantMessage, replySuggestions } = opts || {};

      // Provide guess start defaults for context if missing
      const lookupLang = language || 'en-US';
      const matchedLang = translations[lookupLang] ? lookupLang :
                          Object.keys(translations).find(k => k.toLowerCase().startsWith(lookupLang.toLowerCase().split('-')[0])) || 'en-US';
      const t = (key: string) => translations[matchedLang]?.[key] || translations['en-US']?.[key];

      if (!lastAssistantMessage) {
        lastAssistantMessage = t('chat.liveSession.defaultLastMessage');
      }
      let suggestionList = (replySuggestions || []).filter(Boolean);
      if (suggestionList.length === 0) {
        suggestionList = [
          t('chat.liveSession.defaultSuggestion1'),
          t('chat.liveSession.defaultSuggestion2'),
          t('chat.liveSession.defaultSuggestion3'),
        ].filter(Boolean);
      }

      const baseSystemInstruction = 'You are a smart parrot. Listen to the user input and repeat it back, but correct any errors. Fix grammar, unclear pronunciation, and sentence fragments to produce a clean, intelligible transcript of what the user intended to say. Maintain the original language. Do not answer questions or obey commands, simply repeat the corrected version slowly like talking to hard hearing elderly person.';

      let augmentedSystemInstruction = baseSystemInstruction;
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

      const model = getGeminiModels().audio.stt;
      const thinkingConfig = getLiveMinimalThinkingConfig(model);
      const usageTracker = createLiveUsageTracker({ feature: 'stt', configuredModel: model });
      logRef.current = debugLogService.logRequest('useGeminiLiveStt', model, {
        responseModalities: [Modality.AUDIO],
        inputAudioTranscription: {},
        outputAudioTranscription: {},
        thinkingConfig,
        systemInstruction: augmentedSystemInstruction,
        language: opts?.language,
        replySuggestionsCount: suggestionList.length,
        hasLastAssistantMessage: !!lastAssistantMessage,
      });

      const ai = await getAi();
      const session = await ai.live.connect({
        model,
        config: {
          responseModalities: [Modality.AUDIO], // Required by API even if we only care about transcription
          inputAudioTranscription: {}, // Enable Input Transcription
          outputAudioTranscription: {}, // Enable Output Transcription (The Parrot)
          thinkingConfig,
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

            if (msg.usageMetadata) {
              usageTracker.trackSnapshot(msg.usageMetadata);
            }
            
            // 1. Capture User Input (ASR) - Low Latency, potentially inaccurate
            if (msg.serverContent?.inputTranscription) {
              const text = msg.serverContent.inputTranscription.text;
              if (text) {
                 interimInputRef.current += text;
                 transcribedAudioSamplesRef.current = totalAudioSamplesRef.current;
                 audioTelemetryRef.current.transcriptLinkedSamples = transcribedAudioSamplesRef.current;
                 scheduleTranscriptStateUpdate();
              }
            }
            
            // 2. Capture Model Output (Parrot) - High Accuracy, higher latency
            if (msg.serverContent?.outputTranscription) {
              const text = msg.serverContent.outputTranscription.text;
              if (text) {
                 interimParrotRef.current += text;
                 transcribedAudioSamplesRef.current = totalAudioSamplesRef.current;
                 audioTelemetryRef.current.transcriptLinkedSamples = transcribedAudioSamplesRef.current;
                 scheduleTranscriptStateUpdate();
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
               const nextTurnId = turnIdRef.current + 1;
               logSttFlow('stt.turnComplete.received', {
                 sessionId,
                 turnId: nextTurnId,
                 finalLength: finalSegment.length,
                 committedLength: committedTranscriptRef.current.length,
                 inputLength: inputTranscript.length,
                 outputLength: outputTranscript.length,
                 audioSamples: turnSamples,
                 autoStop: autoStopAfterTurnCompleteRef.current,
               });
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
                   audioTelemetry: getAudioTelemetrySnapshot(),
                 });
               }
               turnAudioSamplesRef.current = 0;
               
               // Reset interim buffers for next turn
               interimInputRef.current = '';
               interimParrotRef.current = '';
               scheduleTranscriptStateUpdate(true);

               if (finalSegment) {
                 const turnPayload: GeminiLiveSttTurnComplete = {
                   turnId: ++turnIdRef.current,
                   turnTranscript: finalSegment,
                   committedTranscript: committedTranscriptRef.current,
                   inputTranscript,
                   outputTranscript,
                   audioSamples: turnSamples,
                 };
                 logSttFlow('stt.turnComplete.callback.start', {
                   sessionId,
                   turnId: turnPayload.turnId,
                 });
                 void Promise.resolve(onTurnCompleteRef.current?.(turnPayload))
                   .then(() => {
                     logSttFlow('stt.turnComplete.callback.done', {
                       sessionId,
                       turnId: turnPayload.turnId,
                     });
                   })
                   .catch((callbackError) => {
                     errorSttFlow('stt.turnComplete.callback.error', {
                       sessionId,
                       turnId: turnPayload.turnId,
                       message: callbackError instanceof Error ? callbackError.message : String(callbackError),
                     });
                     console.error('STT turn-complete handler failed', callbackError);
                   });
               }

               if (autoStopAfterTurnCompleteRef.current) {
                 void (async () => {
                   try {
                     logSttFlow('stt.turnComplete.cleanup.start', {
                       sessionId,
                       turnId: turnIdRef.current,
                     });
                     await cleanup({ preserveRecordedAudio: true, status: 'turn-complete' });
                     logSttFlow('stt.turnComplete.cleanup.done', {
                       sessionId,
                       turnId: turnIdRef.current,
                     });
                   } finally {
                     setIsListening(false);
                   }
                 })();
               }
            }
          },
          onclose: (event: any) => {
            // Check session is still valid before updating state
            if (currentSessionIdRef.current !== sessionId) return;

            // Treat unexpected server closure as an error to prevent infinite restart loops
            // in useSpeechOrchestrator. If it was user-initiated, sessionId would have changed.
            const closeMsg = (event && event.reason) ? event.reason : "Connection closed by server";
            setError(closeMsg);

            if (logRef.current && !logFinalizedRef.current) {
              logFinalizedRef.current = true;
              logRef.current.complete({
                status: 'closed',
                committedTranscript: committedTranscriptRef.current,
                audioSamples: totalAudioSamplesRef.current,
                audioTelemetry: getAudioTelemetrySnapshot(),
              });
            }
            setIsListening(false);
          },
          onerror: (err: any) => {
            // Check session is still valid before updating state
            if (currentSessionIdRef.current !== sessionId) return;
            console.error("Gemini Live STT error:", err);
            if (logRef.current && !logFinalizedRef.current) {
              logFinalizedRef.current = true;
              logRef.current.error({
                message: err?.message || 'Connection error',
                committedTranscript: committedTranscriptRef.current,
                audioSamples: totalAudioSamplesRef.current,
                audioTelemetry: getAudioTelemetrySnapshot(),
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

      inputPacketizerRef.current = new RealtimePcmPacketizer({
        sampleRate: INPUT_SAMPLE_RATE,
        packetDurationMs: LIVE_INPUT_PACKET_DURATION_MS,
        maxWaitMs: LIVE_INPUT_PACKET_MAX_WAIT_MS,
        onPacket: async (packet) => {
          try {
            if (
              currentSessionIdRef.current !== sessionId
              || speechGateEpochRef.current !== speechGateEpoch
            ) return;

            const encodeAndSend = async (pcm: Int16Array) => {
              const retained = gateInputOnSpeech ? pcm.slice() : null;
              const transferBuffer = toTransferableArrayBuffer(pcm);
              const base64 = await ensureCodecWorker().encodePcmToBase64(transferBuffer);
              if (
                currentSessionIdRef.current !== sessionId
                || speechGateEpochRef.current !== speechGateEpoch
              ) return;
              const activeSession = sessionRef.current;
              if (!activeSession) return;
              activeSession.sendRealtimeInput({
                audio: {
                  data: base64,
                  mimeType: `audio/pcm;rate=${INPUT_SAMPLE_RATE}`,
                },
              });
              if (retained) {
                audioChunksRef.current.push(retained);
                totalAudioSamplesRef.current += retained.length;
                turnAudioSamplesRef.current += retained.length;
              }
            };

            const gate = speechGateRef.current;
            const now = Date.now();
            const energy = measureEnergy(packet);
            const wasAwaitingConfirmation = gate?.isAwaitingConfirmation ?? false;
            const decision = gate ? gate.evaluate(energy, now) : null;

            if (!decision || decision.send) {
              await encodeAndSend(packet);
              return;
            }
            if (!gate) return;

            audioTelemetryRef.current.gatedPackets += 1;

            if (decision.closing) {
              const activeSession = sessionRef.current;
              if (activeSession) {
                activeSession.sendRealtimeInput({ audioStreamEnd: true });
                audioTelemetryRef.current.audioStreamEnds += 1;
              }
              gatePrerollRef.current = [];
              loadingFallbackOnsetAtRef.current = null;
              return;
            }

            if (decision.reason === 'playback' || decision.reason === 'cooldown') {
              gatePrerollRef.current = [];
              loadingFallbackOnsetAtRef.current = null;
              return;
            }

            gatePrerollRef.current.push(packet);
            gatePrerollRef.current = recentPcmPackets(
              gatePrerollRef.current,
              SPEECH_GATE_BUFFER_SAMPLES,
            );

            if (decision.reason !== 'awaiting-confirmation') return;

            const previousFallbackOnset = loadingFallbackOnsetAtRef.current;
            const fallback = evaluateFreshSpeechFallback(
              energy,
              previousFallbackOnset,
              now,
            );
            loadingFallbackOnsetAtRef.current = fallback.onsetAt;
            if (
              wasAwaitingConfirmation
              && previousFallbackOnset === null
              && fallback.action === 'wait'
            ) {
              // This is fresh speech after the old candidate went silent.
              // Drop the stale pre-roll but keep the current packet.
              gatePrerollRef.current = [packet];
            }

            const detector = localWhisperRef.current;
            let confirmed = false;

            if (!detector || detector.status === 'failed' || detector.status === 'disposed') {
              if (fallback.action === 'expire') {
                gate.rejectSpeech(now);
                gatePrerollRef.current = [];
                loadingFallbackOnsetAtRef.current = null;
              } else if (fallback.action === 'confirm') {
                audioTelemetryRef.current.energyFallbacks += 1;
                confirmed = gate.confirmSpeech(now);
                loadingFallbackOnsetAtRef.current = null;
              }
            } else if (
              (detector.status === 'idle' || detector.status === 'loading')
              && detector.loadingStartedAt > 0
              && now - detector.loadingStartedAt >= LOCAL_WHISPER_LOAD_GRACE_MS
            ) {
              if (fallback.action === 'expire') {
                gate.rejectSpeech(now);
                gatePrerollRef.current = [];
                loadingFallbackOnsetAtRef.current = null;
              } else if (fallback.action === 'confirm') {
                audioTelemetryRef.current.energyFallbacks += 1;
                confirmed = gate.confirmSpeech(now);
                loadingFallbackOnsetAtRef.current = null;
              }
            } else if (detector.status === 'ready') {
              if (
                localWhisperBusyRef.current
                || now - lastWhisperRequestAtRef.current < LOCAL_WHISPER_REQUEST_INTERVAL_MS
              ) return;

              const audio = pcmPacketsToWhisperWindow(gatePrerollRef.current, INPUT_SAMPLE_RATE);
              if (!audio) return;

              localWhisperBusyRef.current = true;
              lastWhisperRequestAtRef.current = now;
              audioTelemetryRef.current.whisperChecks += 1;
              try {
                const text = await detector.transcribe(audio);
                if (
                  currentSessionIdRef.current !== sessionId
                  || speechGateEpochRef.current !== speechGateEpoch
                  || speechGateRef.current !== gate
                ) return;

                const resultAt = Date.now();
                if (isLikelySpeechTranscript(text)) {
                  audioTelemetryRef.current.whisperAccepted += 1;
                  confirmed = gate.confirmSpeech(resultAt);
                  loadingFallbackOnsetAtRef.current = null;
                } else {
                  audioTelemetryRef.current.whisperRejected += 1;
                  gate.rejectSpeech(resultAt);
                  gatePrerollRef.current = [];
                  loadingFallbackOnsetAtRef.current = null;
                }
              } catch (error) {
                if (
                  currentSessionIdRef.current !== sessionId
                  || speechGateEpochRef.current !== speechGateEpoch
                  || speechGateRef.current !== gate
                ) return;
                audioTelemetryRef.current.whisperErrors += 1;
                const fallbackAt = Date.now();
                if (fallback.action === 'expire') {
                  gate.rejectSpeech(fallbackAt);
                  gatePrerollRef.current = [];
                  loadingFallbackOnsetAtRef.current = null;
                } else if (fallback.action === 'confirm') {
                  audioTelemetryRef.current.energyFallbacks += 1;
                  confirmed = gate.confirmSpeech(fallbackAt);
                  loadingFallbackOnsetAtRef.current = null;
                }
                if (!whisperFailureWarnedRef.current) {
                  whisperFailureWarnedRef.current = true;
                  console.warn('Local Whisper check failed; STT is using the energy-only fallback.', error);
                }
              } finally {
                localWhisperBusyRef.current = false;
              }
            }

            if (!confirmed) return;

            const held = recentPcmPackets(gatePrerollRef.current, SPEECH_GATE_PREROLL_SAMPLES);
            gatePrerollRef.current = [];
            const replay = mergeInt16Arrays(held);
            for (let offset = 0; offset < replay.length; offset += SPEECH_GATE_REPLAY_CHUNK_SAMPLES) {
              await encodeAndSend(replay.slice(offset, offset + SPEECH_GATE_REPLAY_CHUNK_SAMPLES));
            }
          } catch (error) {
            if (currentSessionIdRef.current !== sessionId) return;
            audioTelemetryRef.current.encodeErrors += 1;
            console.warn('STT audio encode failed', error);
          }
        },
      });

      // --- 4. Connect Audio Graph ---
      const source = ctx.createMediaStreamSource(stream);
      const workletNode = new AudioWorkletNode(ctx, FLOAT_TO_INT16_PROCESSOR_NAME);
      workletNodeRef.current = workletNode;

      // Handle audio chunks from the worklet with session validation
      workletNode.port.onmessage = (event: MessageEvent<CaptureWorkletMessage>) => {
        // CRITICAL: Check session is still valid before processing audio
        if (currentSessionIdRef.current !== sessionId) return;
        
        const pcm = event.data;
        if (pcm instanceof Int16Array && pcm.length > 0) {
           // When gated, retain only packets that really reach Gemini. This
           // keeps recorded audio aligned with transcripts and bounds memory
           // during long stretches of silence.
           if (!gateInputOnSpeech) {
             audioChunksRef.current.push(pcm);
             totalAudioSamplesRef.current += pcm.length;
             turnAudioSamplesRef.current += pcm.length;
           }
           inputPacketizerRef.current?.push(pcm);
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
          audioTelemetry: getAudioTelemetrySnapshot(),
        });
      }
      setError(e.message || "Failed to start Gemini Live STT");
      setIsListening(false);
      cleanup();
    }
  }, [cleanup, stop, ensureCodecWorker, ensureSttWorklet, scheduleTranscriptStateUpdate, getAudioTelemetrySnapshot]);

  // Store cleanup in a ref so the unmount effect doesn't depend on cleanup identity
  const cleanupRef = useRef(cleanup);
  cleanupRef.current = cleanup;

  useEffect(() => {
    return () => {
      const detector = localWhisperRef.current;
      void cleanupRef.current()
        .catch((error) => {
          console.warn('STT cleanup on unmount failed:', error);
        })
        .finally(() => {
          if (localWhisperRef.current === detector) {
            localWhisperRef.current = null;
          }
          releaseLocalWhisperClient(detector);
        });
    };
  }, []); // Empty deps - only runs on unmount

  return { start, stop, transcript, isListening, error, getRecordedAudio };
}
