// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {
  type SpeechGate
} from '../../../../shared/audio/speechGate';
import { type ContinuousLiveTurnBoundary } from '../../../core-sdk/media/continuousLiveTurnBoundary';
import {
  type SemanticSpeechCapture
} from '../../../core-sdk/media/observerSpeechDetection';
import { type PcmCaptureRouter } from '../../../core-sdk/media/pcmInput';
import {
  type RealtimePcmPacketizer
} from '../../../core-sdk/media/realtimePcmPacketizer';
import { type beginTurnTiming } from '../../../platform/browser/turnTiming';
import { type debugLogService } from '../../diagnostics';
import { type AudioCodecWorkerClient } from '../utils/audioCodecWorkerClient';
import {
  type LocalWhisperClient
} from '../utils/localWhisperClient';
import {
  WorkletPlaybackDrainCoordinator
} from '../utils/playbackDrain';
import { createEmptyInputAudioTelemetry, createEmptyPlaybackTelemetry, type LiveInputAudioTelemetry, type LivePlaybackTelemetry, type LiveTurnTranscriptUpdate, type ModelAudioDecodeJob, type UseGeminiLiveConversationCallbacks } from './types';

const cell = <T>(current: T): { current: T } => ({ current });

/** Per-hook session ownership, independent of React renders. Cells preserve the
 * original mutable read points across capture, provider and cleanup callbacks. */
export function createLiveSessionState(callbacks: UseGeminiLiveConversationCallbacks) {
  const speechTriggerActivityTokenRef = cell<string | null>(null);
  const vadActivityTokenRef = cell<{ token: string; observer: boolean } | null>(null);
  const sessionRef = cell<any>(null);
  const frameIntervalRef = cell<number | null>(null);
  const inputAudioContextRef = cell<AudioContext | null>(null);
  const outputAudioContextRef = cell<AudioContext | null>(null);
  const microphoneStreamRef = cell<MediaStream | null>(null);
  const captureVideoRef = cell<HTMLVideoElement | null>(null);
  const canvasRef = cell<HTMLCanvasElement | null>(null);
  const workletNodeRef = cell<AudioWorkletNode | null>(null);
  const playbackNodeRef = cell<AudioWorkletNode | null>(null);
  const logRef = cell<ReturnType<typeof debugLogService.logRequest> | null>(null);
  const logFinalizedRef = cell<boolean>(false);
  const modelRef = cell<string>('');
  const pendingUserTurnRef = cell<{ text: string; transcript: string; audio: Int16Array } | null>(null);
  const videoUpdateVersionRef = cell<number>(0);
  const videoFrameInFlightRef = cell(false);
  const transcriptUpdateTimerRef = cell<number | null>(null);
  const pendingTranscriptUpdateRef = cell<LiveTurnTranscriptUpdate | null>(null);
  const serverMessageQueueRef = cell<Promise<void>>(Promise.resolve());
  const inputCodecWorkerRef = cell<AudioCodecWorkerClient | null>(null);
  const outputCodecWorkerRef = cell<AudioCodecWorkerClient | null>(null);
  const inputPacketizerRef = cell<RealtimePcmPacketizer | null>(null);
  const pcmCaptureRouterRef = cell<PcmCaptureRouter | null>(null);
  const currentSessionIdRef = cell<number>(0);
  const speechTriggerAbortRef = cell<AbortController | null>(null);
  const isCleaningUpRef = cell<boolean>(false);
  const currentInputTranscriptionRef = cell<string>('');
  const localSpeechPendingRef = cell(false);
  const concealedSpeechProgressRef = cell(0);
  const concealedSpeechSamplesRef = cell(0);
  const turnTimingRef = cell<ReturnType<typeof beginTurnTiming> | null>(null);
  const currentOutputTranscriptionRef = cell<string>('');
  const currentUserAudioChunksRef = cell<Int16Array[]>([]);
  const currentModelAudioChunksRef = cell<Int16Array[]>([]);
  const currentUserAudioTotalLengthRef = cell<number>(0);
  const currentUserTranscriptAudioLengthRef = cell<number>(0);
  const currentModelThinkingTraceRef = cell<string[]>([]);
  const currentModelThinkingPhaseRef = cell<string | undefined>(undefined);
  const currentModelThinkingStatusLineRef = cell<string | undefined>(undefined);
  const currentTurnWaitingForInputRef = cell<boolean>(false);
  const currentModelAudioTotalLengthRef = cell<number>(0);
  const modelAudioSplitPointsRef = cell<number[]>([]);
  const lastNewlineCountRef = cell<number>(0);
  const lastTranscriptUpdateRef = cell<LiveTurnTranscriptUpdate | null>(null);
  const currentModelAudioTurnIdRef = cell<number>(0);
  const nextModelAudioTurnIdRef = cell<number>(1);
  const nextModelAudioDecodeJobIdRef = cell<number>(1);
  const pendingModelAudioDecodeJobsRef = cell<Map<number, ModelAudioDecodeJob>>(new Map());
  const inputAudioTelemetryRef = cell<LiveInputAudioTelemetry>(createEmptyInputAudioTelemetry());
  const playbackTelemetryRef = cell<LivePlaybackTelemetry>(createEmptyPlaybackTelemetry());
  const playbackDrainCoordinatorRef = cell(new WorkletPlaybackDrainCoordinator());
  const playbackPendingRef = cell(false);
  const speechGateRef = cell<SpeechGate | null>(null);
  const speechTurnBoundaryRef = cell<ContinuousLiveTurnBoundary | null>(null);
  const semanticSpeechCaptureRef = cell<SemanticSpeechCapture | null>(null);
  const observerWhisperRef = cell<LocalWhisperClient | null>(null);
  const observerWhisperBusyRef = cell(false);
  const lastWhisperRequestAtRef = cell(0);
  const loadingFallbackOnsetAtRef = cell<number | null>(null);
  const whisperFailureWarnedRef = cell(false);
  const speechGateEpochRef = cell(0);
  const playbackUntilRef = cell(0);
  const playbackActiveRef = cell(false);
  const awaitingModelTurnRef = cell(false);
  const inputClosedByServerRef = cell(false);
  const boundaryClosePromiseRef = cell<Promise<void> | null>(null);
  const callbacksRef = cell(callbacks);
  return {
    speechTriggerActivityTokenRef, vadActivityTokenRef, sessionRef,
    frameIntervalRef, inputAudioContextRef, outputAudioContextRef,
    microphoneStreamRef, captureVideoRef, canvasRef,
    workletNodeRef, playbackNodeRef, logRef,
    logFinalizedRef, modelRef, pendingUserTurnRef,
    videoUpdateVersionRef, videoFrameInFlightRef, transcriptUpdateTimerRef,
    pendingTranscriptUpdateRef, serverMessageQueueRef, inputCodecWorkerRef,
    outputCodecWorkerRef, inputPacketizerRef, pcmCaptureRouterRef,
    currentSessionIdRef, speechTriggerAbortRef, isCleaningUpRef,
    currentInputTranscriptionRef, localSpeechPendingRef, concealedSpeechProgressRef,
    concealedSpeechSamplesRef, turnTimingRef, currentOutputTranscriptionRef,
    currentUserAudioChunksRef, currentModelAudioChunksRef, currentUserAudioTotalLengthRef,
    currentUserTranscriptAudioLengthRef, currentModelThinkingTraceRef, currentModelThinkingPhaseRef,
    currentModelThinkingStatusLineRef, currentTurnWaitingForInputRef, currentModelAudioTotalLengthRef,
    modelAudioSplitPointsRef, lastNewlineCountRef, lastTranscriptUpdateRef,
    currentModelAudioTurnIdRef, nextModelAudioTurnIdRef, nextModelAudioDecodeJobIdRef,
    pendingModelAudioDecodeJobsRef, inputAudioTelemetryRef, playbackTelemetryRef,
    playbackDrainCoordinatorRef, playbackPendingRef, speechGateRef,
    speechTurnBoundaryRef, semanticSpeechCaptureRef, observerWhisperRef,
    observerWhisperBusyRef, lastWhisperRequestAtRef, loadingFallbackOnsetAtRef,
    whisperFailureWarnedRef, speechGateEpochRef, playbackUntilRef,
    playbackActiveRef, awaitingModelTurnRef, inputClosedByServerRef,
    boundaryClosePromiseRef, callbacksRef,
  };
}
export type LiveSessionData = ReturnType<typeof createLiveSessionState>;
