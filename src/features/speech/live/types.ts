// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {
  type LiveServerGoAway,
  type LiveServerSessionResumptionUpdate,
  type Modality
} from '@google/genai';
import {
  type ConversationLiveOpenTrigger
} from '../../../../shared/liveOpenReason';
import {
  type LocalSpeechTriggerPhase
} from '../utils/localSpeechTrigger';

export type LiveSessionState = 'idle' | 'armed' | 'connecting' | 'active' | 'error';

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
  speechPreviewProgress?: number;
  modelText: string;
  reason: LiveTurnTranscriptUpdateReason;
  thinkingTrace?: string[];
  thinkingPhase?: string;
  thinkingStatusLine?: string;
}

export interface UseGeminiLiveConversationCallbacks {
  onStateChange?: (state: LiveSessionState) => void;
  onError?: (message: string) => void;
  /** Advance disconnect warning only; consumers must not close the session immediately. */
  onGoAway?: (goAway: LiveServerGoAway) => void;
  onSessionResumptionUpdate?: (update: LiveServerSessionResumptionUpdate) => void;
  onTurnTranscriptUpdate?: (update: LiveTurnTranscriptUpdate) => void;
  onLocalSpeechTriggerPhaseChange?: (phase: LocalSpeechTriggerPhase | null) => void;
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
  /** The audited product event that authorizes this paid Live transport. */
  liveOpenTrigger: ConversationLiveOpenTrigger;
  /**
   * Only stream microphone audio and video after local Whisper confirms that a
   * speech-like sound contains real words.
   *
   * For the silent observer, this keeps the app locally armed while idle and
   * opens a paid transport only after Whisper finds words. User-started camera
   * sessions leave this off: the click already declared intent.
   */
  gateInputOnSpeech?: boolean;
  /** Keep the paid transport open, but start/end continuous microphone turns locally. */
  gateAudioAfterConnect?: boolean;
  systemInstruction?: string;
  buildSystemInstruction?: () => Promise<string>;
  stream?: MediaStream | null;
  videoElement?: HTMLVideoElement | null;
  voiceName?: string;
  responseModalities?: Modality[];
  playModelAudio?: boolean;
  emitTurns?: boolean;
  /** Deliberately opt into barge-in. Disabled by default for Android reliability. */
  allowModelInterruptions?: boolean;
  costFeature?: 'liveConversation' | 'reengagement';
}

export const INPUT_SAMPLE_RATE = 16000;

export const OUTPUT_SAMPLE_RATE = 24000;

export const TRANSCRIPT_UPDATE_INTERVAL_MS = 60;

export const MAX_LIVE_FRAME_DIMENSION = 640;

export const LIVE_INPUT_PACKET_DURATION_MS = 100;

export const LIVE_INPUT_PACKET_MAX_WAIT_MS = 120;

export const SPEECH_GATE_REPLAY_CHUNK_SAMPLES = 4096;

export const OBSERVER_WHISPER_LOAD_GRACE_MS = 12_000;

export interface ModelAudioDecodeJob {
  jobId: number;
  sessionId: number;
  turnId: number;
  cancelled: boolean;
  promise: Promise<void>;
}

export interface ModelAudioDecodeCheckpoint {
  sessionId: number;
  turnId: number;
  lastJobId: number;
}

export interface LiveInputAudioTelemetry {
  encodeErrors: number;
  capturedSamples: number;
  gatedPackets: number;
  activityStarts: number;
  audioStreamEnds: number;
  speechTriggerSamples: number;
  connectionHandoffPackets: number;
  connectionHandoffSamples: number;
  whisperChecks: number;
  whisperWakeChecks: number;
  whisperBoundaryChecks: number;
  whisperAccepted: number;
  whisperRejected: number;
  whisperErrors: number;
  energyFallbacks: number;
}

export interface LivePlaybackTelemetry {
  decodeErrors: number;
  queueErrors: number;
  underruns: number;
  starts: number;
  resumes: number;
  outputSampleRate: number | null;
  resampledOutput: boolean;
  contextFallbackToDefaultRate: boolean;
  drains: number;
  drainCancellations: number;
  lastDrainWaitMs: number | null;
}

export const createEmptyInputAudioTelemetry = (): LiveInputAudioTelemetry => ({
  encodeErrors: 0,
  capturedSamples: 0,
  gatedPackets: 0,
  activityStarts: 0,
  audioStreamEnds: 0,
  speechTriggerSamples: 0,
  connectionHandoffPackets: 0,
  connectionHandoffSamples: 0,
  whisperChecks: 0,
  whisperWakeChecks: 0,
  whisperBoundaryChecks: 0,
  whisperAccepted: 0,
  whisperRejected: 0,
  whisperErrors: 0,
  energyFallbacks: 0,
});

export const createEmptyPlaybackTelemetry = (): LivePlaybackTelemetry => ({
  decodeErrors: 0,
  queueErrors: 0,
  underruns: 0,
  starts: 0,
  resumes: 0,
  outputSampleRate: null,
  resampledOutput: false,
  contextFallbackToDefaultRate: false,
  drains: 0,
  drainCancellations: 0,
  lastDrainWaitMs: null,
});

