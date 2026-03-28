// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useCallback, useEffect, useRef, useState } from 'react';
import {
  GoogleGenAI,
  LiveServerMessage,
  Modality,
  LiveServerGoAway,
  LiveServerSessionResumptionUpdate,
  SessionResumptionConfig,
} from '@google/genai';
import { mergeInt16Arrays, trimSilence } from '../utils/audioProcessing';
import { countTranscriptNewlines } from '../utils/transcriptParsing';
import { debugLogService } from '../../diagnostics';
import { getGeminiModels } from '../../../core/config/models';
import {
  FLOAT_TO_INT16_PROCESSOR_URL,
  FLOAT_TO_INT16_PROCESSOR_NAME,
  PCM_PLAYBACK_PROCESSOR_URL,
  PCM_PLAYBACK_PROCESSOR_NAME,
} from '../worklets';
import { resolveLiveConnectApiKey } from '../../../api/gemini/client';
import { AudioCodecWorkerClient } from '../utils/audioCodecWorkerClient';
import { type CaptureWorkletMessage, flushCaptureWorkletNode } from '../utils/captureWorkletMessaging';
import {
  RealtimePcmPacketizer,
  type RealtimePcmPacketizerStats,
} from '../utils/realtimePcmPacketizer';

export type LiveSessionState = 'idle' | 'connecting' | 'active' | 'error';

export type LiveTurnTranscriptUpdateReason =
  | 'input'
  | 'output'
  | 'pending-user'
  | 'waiting-for-input'
  | 'no-model-response'
  | 'interrupted'
  | 'session-reset';

export interface LiveTurnTranscriptUpdate {
  userText: string;
  modelText: string;
  reason: LiveTurnTranscriptUpdateReason;
  thinkingTrace?: string[];
  thinkingPhase?: string;
  thinkingStatusLine?: string;
}

export interface UseGeminiLiveConversationCallbacks {
  onStateChange?: (state: LiveSessionState) => void;
  onError?: (message: string) => void;
  onGoAway?: (goAway: LiveServerGoAway) => void;
  onSessionResumptionUpdate?: (update: LiveServerSessionResumptionUpdate) => void;
  onTurnTranscriptUpdate?: (update: LiveTurnTranscriptUpdate) => void;
  /**
   * Called when a turn completes with consolidated transcripts and audio.
   * @param userText - The user's transcribed speech
   * @param modelText - The model's transcribed response
   * @param userAudioPcm - Optional user audio as Int16Array (16kHz)
   * @param modelAudioLines - Optional array of model audio segments (24kHz), split by transcript newlines.
   *                          Each element corresponds to a line in modelText (target line, then native translation line).
   *                          Splitting accounts for delay between audio arrival and transcript appearance.
   */
  onTurnComplete?: (
    userText: string,
    modelText: string,
    userAudioPcm?: Int16Array,
    modelAudioLines?: Int16Array[]
  ) => void | Promise<void>;
}

export interface StartLiveConversationOptions {
  systemInstruction?: string;
  stream?: MediaStream | null;
  videoElement?: HTMLVideoElement | null;
  voiceName?: string;
  responseModalities?: Modality[];
  playModelAudio?: boolean;
  emitTurns?: boolean;
  sessionResumption?: SessionResumptionConfig;
}

const INPUT_SAMPLE_RATE = 16000;
const OUTPUT_SAMPLE_RATE = 24000;
const TRANSCRIPT_UPDATE_INTERVAL_MS = 60;
const MAX_LIVE_FRAME_DIMENSION = 640;
const LIVE_INPUT_PACKET_DURATION_MS = 100;
const LIVE_INPUT_PACKET_MAX_WAIT_MS = 120;

// Session counter to prevent stale callback execution after cleanup
let liveConversationSessionCounter = 0;

const blobToBase64 = (blob: Blob): Promise<string> => new Promise((resolve, reject) => {
  const reader = new FileReader();
  reader.onloadend = () => {
    const result = reader.result as string;
    const base64 = result.substring(result.indexOf(',') + 1);
    resolve(base64 || '');
  };
  reader.onerror = reject;
  reader.readAsDataURL(blob);
});

const toTransferableArrayBuffer = (pcm: Int16Array): ArrayBuffer => {
  if (
    pcm.buffer instanceof ArrayBuffer
    && pcm.byteOffset === 0
    && pcm.byteLength === pcm.buffer.byteLength
  ) {
    return pcm.buffer;
  }
  return pcm.slice().buffer;
};

interface ModelAudioDecodeJob {
  jobId: number;
  sessionId: number;
  turnId: number;
  cancelled: boolean;
  promise: Promise<void>;
}

interface ModelAudioDecodeCheckpoint {
  sessionId: number;
  turnId: number;
  lastJobId: number;
}

interface LiveInputAudioTelemetry {
  encodeErrors: number;
}

interface LivePlaybackTelemetry {
  decodeErrors: number;
  queueErrors: number;
  underruns: number;
  starts: number;
  resumes: number;
  outputSampleRate: number | null;
  resampledOutput: boolean;
  contextFallbackToDefaultRate: boolean;
}

const createEmptyInputAudioTelemetry = (): LiveInputAudioTelemetry => ({
  encodeErrors: 0,
});

const createEmptyPlaybackTelemetry = (): LivePlaybackTelemetry => ({
  decodeErrors: 0,
  queueErrors: 0,
  underruns: 0,
  starts: 0,
  resumes: 0,
  outputSampleRate: null,
  resampledOutput: false,
  contextFallbackToDefaultRate: false,
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
  const playbackNodeRef = useRef<AudioWorkletNode | null>(null);
  const logRef = useRef<ReturnType<typeof debugLogService.logRequest> | null>(null);
  const logFinalizedRef = useRef<boolean>(false);
  const modelRef = useRef<string>('');
  const pendingUserTurnRef = useRef<{ text: string; transcript: string; audio: Int16Array } | null>(null);
  const videoUpdateVersionRef = useRef<number>(0);
  const videoFrameInFlightRef = useRef(false);
  const transcriptUpdateTimerRef = useRef<number | null>(null);
  const pendingTranscriptUpdateRef = useRef<LiveTurnTranscriptUpdate | null>(null);
  const serverMessageQueueRef = useRef<Promise<void>>(Promise.resolve());
  const inputCodecWorkerRef = useRef<AudioCodecWorkerClient | null>(null);
  const outputCodecWorkerRef = useRef<AudioCodecWorkerClient | null>(null);
  const inputPacketizerRef = useRef<RealtimePcmPacketizer | null>(null);
  
  // Session ID to track valid session and invalidate stale callbacks
  const currentSessionIdRef = useRef<number>(0);
  
  // Flag to track if cleanup is in progress to prevent race conditions
  const isCleaningUpRef = useRef<boolean>(false);

  // Transcription & Audio Accumulators
  const currentInputTranscriptionRef = useRef<string>('');
  const currentOutputTranscriptionRef = useRef<string>('');
  const currentUserAudioChunksRef = useRef<Int16Array[]>([]);
  const currentModelAudioChunksRef = useRef<Int16Array[]>([]);
  const currentUserAudioTotalLengthRef = useRef<number>(0);
  const currentUserTranscriptAudioLengthRef = useRef<number>(0);
  const currentModelThinkingTraceRef = useRef<string[]>([]);
  const currentModelThinkingPhaseRef = useRef<string | undefined>(undefined);
  const currentModelThinkingStatusLineRef = useRef<string | undefined>(undefined);
  const currentTurnWaitingForInputRef = useRef<boolean>(false);
  
  // Audio-Transcript Synchronization Tracking
  // These track the correlation between streaming audio and delayed transcripts
  // Split points are recorded when newlines appear in transcript, using current audio length
  const currentModelAudioTotalLengthRef = useRef<number>(0);
  const modelAudioSplitPointsRef = useRef<number[]>([]);
  const lastNewlineCountRef = useRef<number>(0);
  const lastTranscriptUpdateRef = useRef<LiveTurnTranscriptUpdate | null>(null);
  const currentModelAudioTurnIdRef = useRef<number>(0);
  const nextModelAudioTurnIdRef = useRef<number>(1);
  const nextModelAudioDecodeJobIdRef = useRef<number>(1);
  const pendingModelAudioDecodeJobsRef = useRef<Map<number, ModelAudioDecodeJob>>(new Map());
  const inputAudioTelemetryRef = useRef<LiveInputAudioTelemetry>(createEmptyInputAudioTelemetry());
  const playbackTelemetryRef = useRef<LivePlaybackTelemetry>(createEmptyPlaybackTelemetry());

  const callbacksRef = useRef(callbacks);
  useEffect(() => { callbacksRef.current = callbacks; }, [callbacks]);

  const updateState = useCallback((s: LiveSessionState) => {
    setState(s);
    callbacksRef.current.onStateChange?.(s);
  }, []);

  const resetAudioTelemetry = useCallback(() => {
    inputAudioTelemetryRef.current = createEmptyInputAudioTelemetry();
    playbackTelemetryRef.current = createEmptyPlaybackTelemetry();
  }, []);

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
    input: {
      packetizer: getInputPacketizerStats(),
      ...inputAudioTelemetryRef.current,
    },
    playback: {
      ...playbackTelemetryRef.current,
    },
  }), [getInputPacketizerStats]);

  const getTranscriptLinkedUserAudio = useCallback(() => {
    const merged = mergeInt16Arrays(currentUserAudioChunksRef.current);
    if (merged.length === 0) {
      return new Int16Array(0);
    }

    const transcriptLinkedSamples = Math.min(
      currentUserTranscriptAudioLengthRef.current,
      merged.length
    );
    if (transcriptLinkedSamples <= 0) {
      return new Int16Array(0);
    }

    const transcriptLinkedAudio = merged.slice(0, transcriptLinkedSamples);
    return trimSilence(transcriptLinkedAudio, INPUT_SAMPLE_RATE);
  }, []);

  const clearTranscriptUpdateTimer = useCallback(() => {
    if (transcriptUpdateTimerRef.current !== null) {
      window.clearTimeout(transcriptUpdateTimerRef.current);
      transcriptUpdateTimerRef.current = null;
    }
  }, []);

  const flushPendingTranscriptUpdate = useCallback(() => {
    clearTranscriptUpdateTimer();
    const pendingUpdate = pendingTranscriptUpdateRef.current;
    if (!pendingUpdate) return;
    pendingTranscriptUpdateRef.current = null;
    lastTranscriptUpdateRef.current = pendingUpdate;
    callbacksRef.current.onTurnTranscriptUpdate?.(pendingUpdate);
  }, [clearTranscriptUpdateTimer]);

  const emitTurnTranscriptUpdate = useCallback((reason: LiveTurnTranscriptUpdateReason) => {
    const pendingUserText = pendingUserTurnRef.current?.text?.trim() ?? '';
    const currentUserText = currentInputTranscriptionRef.current.trim();
    const thinkingTrace = currentModelThinkingTraceRef.current.length > 0
      ? [...currentModelThinkingTraceRef.current]
      : undefined;
    const nextUpdate: LiveTurnTranscriptUpdate = {
      userText: [pendingUserText, currentUserText].filter(Boolean).join('\n').trim(),
      modelText: currentOutputTranscriptionRef.current.trim(),
      reason,
      thinkingTrace,
      thinkingPhase: currentModelThinkingPhaseRef.current,
      thinkingStatusLine: currentModelThinkingStatusLineRef.current,
    };
    const previousUpdate = pendingTranscriptUpdateRef.current || lastTranscriptUpdateRef.current;
    if (
      previousUpdate
      && previousUpdate.userText === nextUpdate.userText
      && previousUpdate.modelText === nextUpdate.modelText
      && previousUpdate.reason === nextUpdate.reason
      && (previousUpdate.thinkingPhase || '') === (nextUpdate.thinkingPhase || '')
      && (previousUpdate.thinkingStatusLine || '') === (nextUpdate.thinkingStatusLine || '')
      && (previousUpdate.thinkingTrace || []).join('\n') === (nextUpdate.thinkingTrace || []).join('\n')
    ) {
      return;
    }
    pendingTranscriptUpdateRef.current = nextUpdate;
    if (
      reason === 'session-reset'
      || reason === 'interrupted'
      || reason === 'pending-user'
      || reason === 'waiting-for-input'
      || reason === 'no-model-response'
    ) {
      flushPendingTranscriptUpdate();
      return;
    }
    if (transcriptUpdateTimerRef.current !== null) {
      return;
    }
    transcriptUpdateTimerRef.current = window.setTimeout(() => {
      transcriptUpdateTimerRef.current = null;
      flushPendingTranscriptUpdate();
    }, TRANSCRIPT_UPDATE_INTERVAL_MS);
  }, [flushPendingTranscriptUpdate]);

  const emitTurnTranscriptReset = useCallback(() => {
    clearTranscriptUpdateTimer();
    pendingTranscriptUpdateRef.current = null;
    lastTranscriptUpdateRef.current = null;
    callbacksRef.current.onTurnTranscriptUpdate?.({
      userText: '',
      modelText: '',
      reason: 'session-reset',
      thinkingTrace: undefined,
      thinkingPhase: undefined,
      thinkingStatusLine: undefined,
    });
  }, [clearTranscriptUpdateTimer]);

  const startNextModelAudioTurn = useCallback((sessionId: number) => {
    currentModelAudioTurnIdRef.current = sessionId > 0 ? nextModelAudioTurnIdRef.current++ : 0;
  }, []);

  const getModelAudioDecodeCheckpoint = useCallback((): ModelAudioDecodeCheckpoint => ({
    sessionId: currentSessionIdRef.current,
    turnId: currentModelAudioTurnIdRef.current,
    lastJobId: nextModelAudioDecodeJobIdRef.current - 1,
  }), []);

  const cancelModelAudioDecodeJobs = useCallback((sessionId?: number, turnId?: number) => {
    for (const [jobId, job] of pendingModelAudioDecodeJobsRef.current.entries()) {
      if (
        (sessionId === undefined || job.sessionId === sessionId)
        && (turnId === undefined || job.turnId === turnId)
      ) {
        job.cancelled = true;
        pendingModelAudioDecodeJobsRef.current.delete(jobId);
      }
    }
  }, []);

  const waitForModelAudioDecodeCheckpoint = useCallback(async (
    checkpoint: ModelAudioDecodeCheckpoint
  ) => {
    if (checkpoint.turnId === 0 || checkpoint.lastJobId <= 0) {
      return;
    }

    const pendingJobs = Array.from(pendingModelAudioDecodeJobsRef.current.values())
      .filter((job) => (
        job.sessionId === checkpoint.sessionId
        && job.turnId === checkpoint.turnId
        && job.jobId <= checkpoint.lastJobId
      ))
      .map((job) => job.promise);

    if (pendingJobs.length > 0) {
      await Promise.allSettled(pendingJobs);
    }
  }, []);

  const stopAllAudio = useCallback(() => {
    if (playbackNodeRef.current) {
      try {
        playbackNodeRef.current.port.postMessage({ type: 'reset' });
      } catch {
        // Ignore reset failures during teardown/interruption.
      }
    }
  }, []);

  const stopVideoFrameLoop = useCallback(() => {
    if (frameIntervalRef.current !== null) {
      window.clearInterval(frameIntervalRef.current);
      frameIntervalRef.current = null;
    }
  }, []);

  const detachCaptureVideo = useCallback(() => {
    if (!captureVideoRef.current) return;
    try {
      // Only fully detach if we created this hidden element.
      if (captureVideoRef.current.parentElement === document.body && captureVideoRef.current.style.position === 'fixed') {
        captureVideoRef.current.pause();
        captureVideoRef.current.srcObject = null;
        document.body.removeChild(captureVideoRef.current);
      }
    } catch {
      // Ignore detach errors.
    }
    captureVideoRef.current = null;
  }, []);

  const cleanup = useCallback(async () => {
    // Prevent concurrent cleanup operations
    if (isCleaningUpRef.current) return;
    isCleaningUpRef.current = true;

    const activeCaptureNode = workletNodeRef.current;
    if (activeCaptureNode) {
      await flushCaptureWorkletNode(activeCaptureNode);
    }
    if (inputPacketizerRef.current) {
      await inputPacketizerRef.current.flushPending();
      inputPacketizerRef.current.dispose();
      inputPacketizerRef.current = null;
    }

    // Invalidate current session to prevent stale callbacks from processing
    currentSessionIdRef.current = 0;
    cancelModelAudioDecodeJobs();
    startNextModelAudioTurn(0);

    stopVideoFrameLoop();
    clearTranscriptUpdateTimer();
    serverMessageQueueRef.current = Promise.resolve();
    pendingTranscriptUpdateRef.current = null;
    videoFrameInFlightRef.current = false;
    
    // Clear worklet message handler FIRST to stop new audio from accumulating
    if (activeCaptureNode) {
        try { activeCaptureNode.port.onmessage = null; activeCaptureNode.disconnect(); } catch { }
        workletNodeRef.current = null;
    }
    if (playbackNodeRef.current) {
      try { playbackNodeRef.current.port.postMessage({ type: 'reset' }); } catch { }
      try { playbackNodeRef.current.disconnect(); } catch { }
      playbackNodeRef.current = null;
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
    
    // Invalidate any in-flight async video setup so stale calls cannot repopulate refs after teardown.
    videoUpdateVersionRef.current += 1;
    detachCaptureVideo();
    canvasRef.current = null;

    if (sessionRef.current) {
        const session = sessionRef.current;
        sessionRef.current = null;
        try { if (typeof session.close === 'function') session.close(); } catch {}
    }

    if (inputCodecWorkerRef.current) {
      inputCodecWorkerRef.current.dispose();
      inputCodecWorkerRef.current = null;
    }
    if (outputCodecWorkerRef.current) {
      outputCodecWorkerRef.current.dispose();
      outputCodecWorkerRef.current = null;
    }

    emitTurnTranscriptReset();
    
    // Clear all accumulators to free memory
    currentInputTranscriptionRef.current = '';
    currentOutputTranscriptionRef.current = '';
    currentUserAudioChunksRef.current = [];
    currentUserAudioTotalLengthRef.current = 0;
    currentUserTranscriptAudioLengthRef.current = 0;
    currentModelThinkingTraceRef.current = [];
    currentModelThinkingPhaseRef.current = undefined;
    currentModelThinkingStatusLineRef.current = undefined;
    currentTurnWaitingForInputRef.current = false;
    currentModelAudioChunksRef.current = [];
    currentModelAudioTotalLengthRef.current = 0;
    modelAudioSplitPointsRef.current = [];
    lastNewlineCountRef.current = 0;
    lastTranscriptUpdateRef.current = null;
    pendingUserTurnRef.current = null;
    
    isCleaningUpRef.current = false;
  }, [
    cancelModelAudioDecodeJobs,
    clearTranscriptUpdateTimer,
    detachCaptureVideo,
    emitTurnTranscriptReset,
    startNextModelAudioTurn,
    stopAllAudio,
    stopVideoFrameLoop,
  ]);

  // Load the AudioWorklet module (only needs to happen once per AudioContext)
  const ensureCaptureWorklet = useCallback(async (ctx: AudioContext) => {
    if (!ctx.audioWorklet || typeof ctx.audioWorklet.addModule !== 'function') {
      throw new Error('AudioWorklet is not supported');
    }
    await ctx.audioWorklet.addModule(FLOAT_TO_INT16_PROCESSOR_URL);
  }, []);

  const ensurePlaybackWorklet = useCallback(async (ctx: AudioContext) => {
    if (!ctx.audioWorklet || typeof ctx.audioWorklet.addModule !== 'function') {
      throw new Error('AudioWorklet is not supported');
    }
    await ctx.audioWorklet.addModule(PCM_PLAYBACK_PROCESSOR_URL);
  }, []);

  const ensureInputCodecWorker = useCallback(() => {
    if (!inputCodecWorkerRef.current) {
      inputCodecWorkerRef.current = new AudioCodecWorkerClient();
    }
    return inputCodecWorkerRef.current;
  }, []);

  const ensureOutputCodecWorker = useCallback(() => {
    if (!outputCodecWorkerRef.current) {
      outputCodecWorkerRef.current = new AudioCodecWorkerClient();
    }
    return outputCodecWorkerRef.current;
  }, []);

  const ensureVideoElementReady = useCallback(async (stream: MediaStream, providedElement?: HTMLVideoElement | null) => {
    if (
      providedElement &&
      providedElement.srcObject === stream &&
      providedElement.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA &&
      providedElement.videoWidth > 0 &&
      providedElement.videoHeight > 0
    ) {
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

  const startVideoFrameLoop = useCallback((sessionId: number) => {
    stopVideoFrameLoop();
    frameIntervalRef.current = window.setInterval(() => {
      // Check session is still valid
      if (currentSessionIdRef.current !== sessionId) return;

      const activeSession = sessionRef.current;
      const activeVideo = captureVideoRef.current;
      const activeCanvas = canvasRef.current;
      if (!activeSession || !activeVideo || !activeCanvas) return;
      if (activeVideo.videoWidth === 0) return;
      if (videoFrameInFlightRef.current) return;

      const ctx = activeCanvas.getContext('2d');
      if (!ctx) return;
      const scale = Math.min(1, MAX_LIVE_FRAME_DIMENSION / Math.max(activeVideo.videoWidth, activeVideo.videoHeight));
      activeCanvas.width = Math.max(1, Math.round(activeVideo.videoWidth * scale));
      activeCanvas.height = Math.max(1, Math.round(activeVideo.videoHeight * scale));
      ctx.imageSmoothingEnabled = true;
      ctx.imageSmoothingQuality = 'medium';
      ctx.drawImage(activeVideo, 0, 0, activeCanvas.width, activeCanvas.height);

      videoFrameInFlightRef.current = true;
      activeCanvas.toBlob((blob) => {
        void (async () => {
          try {
            if (blob && sessionRef.current && currentSessionIdRef.current === sessionId) {
              const b64 = await blobToBase64(blob);
              if (currentSessionIdRef.current !== sessionId) return;
              sessionRef.current.sendRealtimeInput({ media: { data: b64, mimeType: 'image/jpeg' } });
            }
          } finally {
            videoFrameInFlightRef.current = false;
          }
        })();
      }, 'image/jpeg', 0.5);
    }, 1000);
  }, [stopVideoFrameLoop]);

  const updateVideoInput = useCallback(async (
    stream?: MediaStream | null,
    providedElement?: HTMLVideoElement | null
  ) => {
    const updateVersion = ++videoUpdateVersionRef.current;
    stopVideoFrameLoop();
    detachCaptureVideo();

    if (!stream || !stream.active) {
      return;
    }

    await ensureVideoElementReady(stream, providedElement);
    if (updateVersion !== videoUpdateVersionRef.current) {
      detachCaptureVideo();
      return;
    }

    if (!canvasRef.current) {
      canvasRef.current = document.createElement('canvas');
    }

    const activeSessionId = currentSessionIdRef.current;
    if (activeSessionId) {
      startVideoFrameLoop(activeSessionId);
    }
  }, [detachCaptureVideo, ensureVideoElementReady, startVideoFrameLoop, stopVideoFrameLoop]);

  const start = useCallback(async (opts: StartLiveConversationOptions = {}) => {
    const {
      stream,
      videoElement,
      systemInstruction,
      voiceName,
      responseModalities = [Modality.AUDIO],
      playModelAudio = true,
      emitTurns = true,
      sessionResumption,
    } = opts;
    
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
    startNextModelAudioTurn(sessionId);
    serverMessageQueueRef.current = Promise.resolve();
    resetAudioTelemetry();

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
      if (stream && stream.active) {
        // Video setup is optional in observer mode (audio-only fallback).
        await ensureVideoElementReady(stream, videoElement);
        if (await abortIfInvalidated()) return;
        if (!canvasRef.current) {
          canvasRef.current = document.createElement('canvas');
        }
      }

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

      if (playModelAudio) {
        let outputCtx: AudioContext;
        try {
          outputCtx = new AudioContextCtor({ sampleRate: OUTPUT_SAMPLE_RATE });
        } catch (error) {
          console.warn('Failed to create 24kHz playback AudioContext, retrying with default device rate', error);
          playbackTelemetryRef.current.contextFallbackToDefaultRate = true;
          try {
            outputCtx = new AudioContextCtor();
          } catch (fallbackError) {
            console.error('Failed to create playback AudioContext', fallbackError);
            throw new Error('Failed to initialize model audio playback');
          }
        }

        if (await abortIfInvalidated()) {
          try { await outputCtx.close(); } catch { }
          return;
        }

        playbackTelemetryRef.current.outputSampleRate = outputCtx.sampleRate;
        if (outputCtx.sampleRate !== OUTPUT_SAMPLE_RATE) {
          playbackTelemetryRef.current.resampledOutput = true;
          console.warn(
            `Playback AudioContext sample rate mismatch: expected ${OUTPUT_SAMPLE_RATE}, got ${outputCtx.sampleRate}. Falling back to worklet resampling.`
          );
        }

        outputAudioContextRef.current = outputCtx;
        await ensurePlaybackWorklet(outputCtx);
        if (await abortIfInvalidated()) return;
        const playbackNode = new AudioWorkletNode(outputCtx, PCM_PLAYBACK_PROCESSOR_NAME, {
          numberOfInputs: 0,
          numberOfOutputs: 1,
          outputChannelCount: [1],
        });
        playbackNode.port.onmessage = (event: MessageEvent<{
          type?: string;
          event?: 'started' | 'resumed' | 'underrun';
        }>) => {
          const telemetryMessage = event.data;
          if (!telemetryMessage || telemetryMessage.type !== 'telemetry') return;
          if (telemetryMessage.event === 'started') {
            playbackTelemetryRef.current.starts += 1;
            return;
          }
          if (telemetryMessage.event === 'resumed') {
            playbackTelemetryRef.current.resumes += 1;
            return;
          }
          if (telemetryMessage.event === 'underrun') {
            playbackTelemetryRef.current.underruns += 1;
          }
        };
        playbackNode.connect(outputCtx.destination);
        playbackNodeRef.current = playbackNode;
      }

      const model = getGeminiModels().audio.live;
      modelRef.current = model;
      logFinalizedRef.current = false;
      logRef.current = debugLogService.logRequest('useGeminiLiveConversation', model, {
        responseModalities,
        systemInstruction: systemInstruction || '',
        inputAudioTranscription: {},
        outputAudioTranscription: {},
      });

      const apiKey = await resolveLiveConnectApiKey({ purpose: 'live' });
      const ai = new GoogleGenAI({ apiKey });
      const session = await ai.live.connect({
        model,
        config: {
          responseModalities,
          systemInstruction: systemInstruction,
          // Empty config objects to enable transcription without specifying parameters causing invalid argument errors
          inputAudioTranscription: {},
          outputAudioTranscription: {},
          thinkingConfig: {
            includeThoughts: true,
          },
          // Voice configuration for the live conversation
          speechConfig: voiceName ? { voiceConfig: { prebuiltVoiceConfig: { voiceName } } } : undefined,
          sessionResumption,
        },
        callbacks: {
          onopen: () => {
            // Check session is still valid before updating state
            if (currentSessionIdRef.current !== sessionId) return;
            updateState('active');
          },
          onmessage: (msg: LiveServerMessage) => {
            serverMessageQueueRef.current = serverMessageQueueRef.current
              .catch(() => undefined)
              .then(async () => {
                // Check session is still valid before processing message
                if (currentSessionIdRef.current !== sessionId) return;

                if (msg.goAway) {
                  callbacksRef.current.onGoAway?.(msg.goAway);
                }
                if (msg.sessionResumptionUpdate) {
                  callbacksRef.current.onSessionResumptionUpdate?.(msg.sessionResumptionUpdate);
                }

                // 1. Handle Audio Output
                const modelTurnParts = msg.serverContent?.modelTurn?.parts ?? [];
                const thoughtTexts = modelTurnParts
                  .map((part) => (
                    part?.thought && typeof part.text === 'string'
                      ? part.text.trim()
                      : ''
                  ))
                  .filter((text): text is string => text.length > 0);

                if (thoughtTexts.length > 0) {
                  currentTurnWaitingForInputRef.current = false;
                  let thoughtTraceChanged = false;
                  for (const thoughtText of thoughtTexts) {
                    const previousThought = currentModelThinkingTraceRef.current[
                      currentModelThinkingTraceRef.current.length - 1
                    ];
                    if (previousThought === thoughtText) {
                      continue;
                    }
                    currentModelThinkingTraceRef.current = [
                      ...currentModelThinkingTraceRef.current,
                      thoughtText,
                    ].slice(-8);
                    thoughtTraceChanged = true;
                  }
                  currentModelThinkingPhaseRef.current = 'Thinking';
                  currentModelThinkingStatusLineRef.current = undefined;
                  if (thoughtTraceChanged) {
                    emitTurnTranscriptUpdate('output');
                  }
                }

                const inlineAudioParts = msg.serverContent?.modelTurn?.parts
                  ?.map((part) => part.inlineData?.data)
                  .filter((data): data is string => typeof data === 'string' && data.length > 0)
                  ?? [];

                for (const inlineAudio of inlineAudioParts) {
                  const turnId = currentModelAudioTurnIdRef.current;
                  const jobId = nextModelAudioDecodeJobIdRef.current++;
                  const job: ModelAudioDecodeJob = {
                    jobId,
                    sessionId,
                    turnId,
                    cancelled: false,
                    promise: Promise.resolve(),
                  };

                  const isJobActive = () => (
                    !job.cancelled
                    && currentSessionIdRef.current === sessionId
                    && currentModelAudioTurnIdRef.current === turnId
                  );

                  job.promise = ensureOutputCodecWorker().decodeBase64ToPcmBuffer(inlineAudio)
                    .then((buffer) => {
                      if (!isJobActive()) return;

                      const pcm16 = new Int16Array(buffer);
                      if (!pcm16.length) return;

                      currentModelAudioChunksRef.current.push(pcm16);
                      currentModelAudioTotalLengthRef.current += pcm16.length;

                      if (playModelAudio && playbackNodeRef.current) {
                        try {
                          playbackNodeRef.current.port.postMessage({
                            type: 'push',
                            pcm: pcm16,
                            inputSampleRate: OUTPUT_SAMPLE_RATE,
                          });
                        } catch (error) {
                          playbackTelemetryRef.current.queueErrors += 1;
                          console.warn('Playback worklet queue failed', error);
                        }
                      }
                    })
                    .catch((error) => {
                      if (!isJobActive()) return;
                      playbackTelemetryRef.current.decodeErrors += 1;
                      console.warn('Audio decode failed', error);
                    })
                    .finally(() => {
                      pendingModelAudioDecodeJobsRef.current.delete(jobId);
                    });

                  pendingModelAudioDecodeJobsRef.current.set(jobId, job);
                }

                // 2. Handle Transcript Accumulation & Split Point Detection
                if (msg.serverContent?.inputTranscription?.text) {
                  currentInputTranscriptionRef.current += msg.serverContent.inputTranscription.text;
                  currentUserTranscriptAudioLengthRef.current = currentUserAudioTotalLengthRef.current;
                  emitTurnTranscriptUpdate('input');
                }
                if (msg.serverContent?.waitingForInput === true) {
                  currentTurnWaitingForInputRef.current = true;
                }
                if (msg.serverContent?.outputTranscription?.text) {
                  const textPart = msg.serverContent.outputTranscription.text;
                  currentOutputTranscriptionRef.current += textPart;
                  currentTurnWaitingForInputRef.current = false;
                  currentModelThinkingPhaseRef.current = 'Final response';
                  currentModelThinkingStatusLineRef.current = undefined;

                  const currentText = currentOutputTranscriptionRef.current;
                  const newlineCount = countTranscriptNewlines(currentText);

                  if (newlineCount > lastNewlineCountRef.current) {
                    const decodeCheckpoint = getModelAudioDecodeCheckpoint();
                    await waitForModelAudioDecodeCheckpoint(decodeCheckpoint);
                    if (
                      currentSessionIdRef.current !== sessionId
                      || currentModelAudioTurnIdRef.current !== decodeCheckpoint.turnId
                    ) {
                      return;
                    }

                    const committedNewlineCount = lastNewlineCountRef.current;
                    if (newlineCount > committedNewlineCount) {
                      const diff = newlineCount - committedNewlineCount;
                      for (let i = 0; i < diff; i++) {
                        modelAudioSplitPointsRef.current.push(currentModelAudioTotalLengthRef.current);
                      }
                      lastNewlineCountRef.current = newlineCount;
                    }
                  }
                  emitTurnTranscriptUpdate('output');
                }

                // 3. Handle Turn Completion (Exchange Finished)
                if (msg.serverContent?.turnComplete) {
                  const modelAudioCheckpoint = getModelAudioDecodeCheckpoint();
                  await waitForModelAudioDecodeCheckpoint(modelAudioCheckpoint);
                  if (
                    currentSessionIdRef.current !== sessionId
                    || currentModelAudioTurnIdRef.current !== modelAudioCheckpoint.turnId
                  ) {
                    return;
                  }

                  const completedTurnId = modelAudioCheckpoint.turnId;
                  const userText = currentInputTranscriptionRef.current.trim();
                  const modelText = currentOutputTranscriptionRef.current.trim();

                  const userAudioFull = getTranscriptLinkedUserAudio();

                  const modelAudioFull = mergeInt16Arrays(currentModelAudioChunksRef.current);
                  const modelAudioLines: Int16Array[] = [];

                  if (modelAudioSplitPointsRef.current.length > 0 && modelAudioFull.length > 0) {
                    let startSample = 0;
                    const points = modelAudioSplitPointsRef.current
                      .slice()
                      .sort((a, b) => a - b)
                      .filter((point) => point < modelAudioFull.length);

                    for (const point of points) {
                      if (point > startSample) {
                        modelAudioLines.push(modelAudioFull.slice(startSample, point));
                        startSample = point;
                      } else {
                        modelAudioLines.push(new Int16Array(0));
                      }
                    }

                    if (startSample < modelAudioFull.length) {
                      modelAudioLines.push(modelAudioFull.slice(startSample));
                    }
                  } else if (modelAudioFull.length > 0) {
                    modelAudioLines.push(modelAudioFull);
                  }

                  const inputTranscript = currentInputTranscriptionRef.current;
                  const outputTranscript = currentOutputTranscriptionRef.current;

                  if (!modelText) {
                    const completionReason: LiveTurnTranscriptUpdateReason = currentTurnWaitingForInputRef.current
                      ? 'waiting-for-input'
                      : 'no-model-response';
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
                        status: completionReason,
                        userText,
                        inputTranscript,
                        modelAudioLinesCount: modelAudioLines.length,
                        userAudioSamples: userAudioFull.length,
                        audioTelemetry: getAudioTelemetrySnapshot(),
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

                    if (emitTurns) {
                      try {
                        const callbackResult = callbacksRef.current.onTurnComplete?.(
                          finalUserText,
                          modelText,
                          finalUserAudio,
                          modelAudioLines
                        );
                        if (callbackResult instanceof Promise) {
                          void callbackResult.catch((error) => {
                            console.error('Live turn completion callback rejected:', error);
                          });
                        }
                      } catch (error) {
                        console.error('Live turn completion callback failed:', error);
                      }
                    }
                    if (logRef.current && !logFinalizedRef.current) {
                      logRef.current.complete({
                        status: 'turn-complete',
                        userText: finalUserText,
                        modelText,
                        inputTranscript: finalInputTranscript,
                        outputTranscript,
                        modelAudioLinesCount: modelAudioLines.length,
                        userAudioSamples: finalUserAudio.length,
                        audioTelemetry: getAudioTelemetrySnapshot(),
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
                      audioTelemetry: getAudioTelemetrySnapshot(),
                    });
                  }

                  cancelModelAudioDecodeJobs(sessionId, completedTurnId);
                  currentInputTranscriptionRef.current = '';
                  currentOutputTranscriptionRef.current = '';
                  currentUserAudioChunksRef.current = [];
                  currentUserAudioTotalLengthRef.current = 0;
                  currentUserTranscriptAudioLengthRef.current = 0;
                  currentModelAudioChunksRef.current = [];
                  currentModelAudioTotalLengthRef.current = 0;
                  modelAudioSplitPointsRef.current = [];
                  lastNewlineCountRef.current = 0;
                  lastTranscriptUpdateRef.current = null;
                  startNextModelAudioTurn(sessionId);

                  if (!modelText) {
                    const completionReason: LiveTurnTranscriptUpdateReason = currentTurnWaitingForInputRef.current
                      ? 'waiting-for-input'
                      : 'no-model-response';
                    emitTurnTranscriptUpdate(completionReason);
                    currentModelThinkingTraceRef.current = [];
                    currentModelThinkingPhaseRef.current = undefined;
                    currentModelThinkingStatusLineRef.current = undefined;
                    currentTurnWaitingForInputRef.current = false;
                  } else {
                    currentModelThinkingTraceRef.current = [];
                    currentModelThinkingPhaseRef.current = undefined;
                    currentModelThinkingStatusLineRef.current = undefined;
                    currentTurnWaitingForInputRef.current = false;
                  }
                }

                // 4. Handle Interruption
                if (msg.serverContent?.interrupted) {
                  const interruptedTurnId = currentModelAudioTurnIdRef.current;
                  cancelModelAudioDecodeJobs(sessionId, interruptedTurnId);
                  if (playModelAudio) {
                    stopAllAudio();
                  }
                  currentOutputTranscriptionRef.current = '';
                  currentModelThinkingTraceRef.current = [];
                  currentModelThinkingPhaseRef.current = undefined;
                  currentModelThinkingStatusLineRef.current = undefined;
                  currentModelAudioChunksRef.current = [];
                  currentModelAudioTotalLengthRef.current = 0;
                  modelAudioSplitPointsRef.current = [];
                  lastNewlineCountRef.current = 0;
                  currentTurnWaitingForInputRef.current = false;
                  startNextModelAudioTurn(sessionId);
                  emitTurnTranscriptUpdate('interrupted');
                }
              })
              .catch((error) => {
                if (currentSessionIdRef.current !== sessionId) return;
                console.warn('Live server message processing failed', error);
              });
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
                audioTelemetry: getAudioTelemetrySnapshot(),
              });
            }
            updateState('idle');
            void cleanup().catch((error) => {
              console.warn('Live cleanup after close failed:', error);
            });
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
                audioTelemetry: getAudioTelemetrySnapshot(),
              });
            }
            callbacksRef.current.onError?.(message);
            void cleanup().catch((error) => {
              console.warn('Live cleanup after error failed:', error);
            });
          }
        }
      });
      
      sessionRef.current = session;
      
      // Check if session was invalidated during async connect
      if (currentSessionIdRef.current !== sessionId) {
        try { session.close(); } catch {}
        return;
      }

      inputPacketizerRef.current = new RealtimePcmPacketizer({
        sampleRate: INPUT_SAMPLE_RATE,
        packetDurationMs: LIVE_INPUT_PACKET_DURATION_MS,
        maxWaitMs: LIVE_INPUT_PACKET_MAX_WAIT_MS,
        onPacket: async (packet) => {
          try {
            const transferBuffer = toTransferableArrayBuffer(packet);
            const base64 = await ensureInputCodecWorker().encodePcmToBase64(transferBuffer);
            if (currentSessionIdRef.current !== sessionId) return;
            sessionRef.current?.sendRealtimeInput({
              media: {
                data: base64,
                mimeType: `audio/pcm;rate=${INPUT_SAMPLE_RATE}`,
              },
            });
          } catch (error) {
            if (currentSessionIdRef.current !== sessionId) return;
            inputAudioTelemetryRef.current.encodeErrors += 1;
            console.warn('Audio encode failed', error);
          }
        },
      });

      // Audio Streaming Loop (Worklet) with session validation
      workletNode.port.onmessage = (event: MessageEvent<CaptureWorkletMessage>) => {
          // CRITICAL: Check session is still valid before processing audio
          if (currentSessionIdRef.current !== sessionId) return;
          
          const pcm = event.data;
          if (!(pcm instanceof Int16Array) || !pcm.length) return;
          
          // Accumulate User Audio for history saving
          currentUserAudioChunksRef.current.push(pcm);
          currentUserAudioTotalLengthRef.current += pcm.length;
          inputPacketizerRef.current?.push(pcm);
      };
      source.connect(workletNode);

      if (stream && stream.active) {
        startVideoFrameLoop(sessionId);
      }

    } catch (e) {
      updateState('error');
      if (logRef.current && !logFinalizedRef.current) {
        logFinalizedRef.current = true;
        logRef.current.error({
          message: e instanceof Error ? e.message : String(e),
          inputTranscript: currentInputTranscriptionRef.current,
          outputTranscript: currentOutputTranscriptionRef.current,
          audioTelemetry: getAudioTelemetrySnapshot(),
        });
      }
      callbacksRef.current.onError?.(e instanceof Error ? e.message : String(e));
      await cleanup();
    }
  }, [
    cancelModelAudioDecodeJobs,
    updateState,
    cleanup,
    getAudioTelemetrySnapshot,
    getModelAudioDecodeCheckpoint,
    getTranscriptLinkedUserAudio,
    resetAudioTelemetry,
    stopAllAudio,
    startNextModelAudioTurn,
    ensureCaptureWorklet,
    ensureInputCodecWorker,
    ensureOutputCodecWorker,
    ensurePlaybackWorklet,
    ensureVideoElementReady,
    startVideoFrameLoop,
    waitForModelAudioDecodeCheckpoint,
  ]);

  const stop = useCallback(async () => {
    updateState('idle');
    if (inputPacketizerRef.current) {
      await inputPacketizerRef.current.flushPending();
    }
    if (logRef.current && !logFinalizedRef.current) {
      logFinalizedRef.current = true;
      logRef.current.complete({
        status: 'stopped',
        inputTranscript: currentInputTranscriptionRef.current,
        outputTranscript: currentOutputTranscriptionRef.current,
        audioTelemetry: getAudioTelemetrySnapshot(),
      });
    }
    await cleanup();
  }, [cleanup, getAudioTelemetrySnapshot, updateState]);

  // Store cleanup in a ref so the unmount effect doesn't depend on cleanup identity
  const cleanupRef = useRef(cleanup);
  cleanupRef.current = cleanup;

  useEffect(() => {
      return () => {
        void cleanupRef.current().catch((error) => {
          console.warn('Live cleanup on unmount failed:', error);
        });
      };
  }, []); // Empty deps - only runs on unmount

  return { start, stop, updateVideoInput };
}
