// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { type flushCaptureWorkletNode } from '../utils/captureWorkletMessaging';
import type { createLiveActivity } from './activity';
import type { createBrowserLiveVideo } from './browserVideo';
import type { createLiveModelAudio } from './modelAudio';
import type { LiveSessionData } from './state';
import type { createLiveTelemetry } from './telemetry';
import type { createLiveTranscripts } from './transcripts';

export function createLiveLifecycle(state: Pick<LiveSessionData,
  'sessionRef' | 'inputAudioContextRef' | 'outputAudioContextRef'
  | 'microphoneStreamRef' | 'canvasRef' | 'workletNodeRef'
  | 'playbackNodeRef' | 'logRef' | 'logFinalizedRef'
  | 'pendingUserTurnRef' | 'videoUpdateVersionRef' | 'videoFrameInFlightRef'
  | 'pendingTranscriptUpdateRef' | 'serverMessageQueueRef' | 'inputCodecWorkerRef'
  | 'outputCodecWorkerRef' | 'inputPacketizerRef' | 'pcmCaptureRouterRef'
  | 'currentSessionIdRef' | 'speechTriggerAbortRef' | 'isCleaningUpRef'
  | 'currentInputTranscriptionRef' | 'localSpeechPendingRef' | 'currentOutputTranscriptionRef'
  | 'currentUserAudioChunksRef' | 'currentModelAudioChunksRef' | 'currentUserAudioTotalLengthRef'
  | 'currentUserTranscriptAudioLengthRef' | 'currentModelThinkingTraceRef' | 'currentModelThinkingPhaseRef'
  | 'currentModelThinkingStatusLineRef' | 'currentTurnWaitingForInputRef' | 'currentModelAudioTotalLengthRef'
  | 'modelAudioSplitPointsRef' | 'lastNewlineCountRef' | 'lastTranscriptUpdateRef'
  | 'playbackDrainCoordinatorRef' | 'playbackPendingRef' | 'speechGateRef'
  | 'speechTurnBoundaryRef' | 'semanticSpeechCaptureRef' | 'loadingFallbackOnsetAtRef'
  | 'speechGateEpochRef' | 'playbackUntilRef' | 'playbackActiveRef'
  | 'awaitingModelTurnRef' | 'boundaryClosePromiseRef'
>, ports: ReturnType<typeof createLiveActivity> & ReturnType<typeof createLiveModelAudio> & ReturnType<typeof createLiveTranscripts> & ReturnType<typeof createBrowserLiveVideo> & ReturnType<typeof createLiveTelemetry> & { flushCaptureWorkletNode: typeof flushCaptureWorkletNode }) {
  const {
    sessionRef, inputAudioContextRef, outputAudioContextRef,
    microphoneStreamRef, canvasRef, workletNodeRef,
    playbackNodeRef, logRef, logFinalizedRef,
    pendingUserTurnRef, videoUpdateVersionRef, videoFrameInFlightRef,
    pendingTranscriptUpdateRef, serverMessageQueueRef, inputCodecWorkerRef,
    outputCodecWorkerRef, inputPacketizerRef, pcmCaptureRouterRef,
    currentSessionIdRef, speechTriggerAbortRef, isCleaningUpRef,
    currentInputTranscriptionRef, localSpeechPendingRef, currentOutputTranscriptionRef,
    currentUserAudioChunksRef, currentModelAudioChunksRef, currentUserAudioTotalLengthRef,
    currentUserTranscriptAudioLengthRef, currentModelThinkingTraceRef, currentModelThinkingPhaseRef,
    currentModelThinkingStatusLineRef, currentTurnWaitingForInputRef, currentModelAudioTotalLengthRef,
    modelAudioSplitPointsRef, lastNewlineCountRef, lastTranscriptUpdateRef,
    playbackDrainCoordinatorRef, playbackPendingRef, speechGateRef,
    speechTurnBoundaryRef, semanticSpeechCaptureRef, loadingFallbackOnsetAtRef,
    speechGateEpochRef, playbackUntilRef, playbackActiveRef,
    awaitingModelTurnRef, boundaryClosePromiseRef,
  } = state;
  const {
    cancelModelAudioDecodeJobs, clearTranscriptUpdateTimer, detachCaptureVideo,
    emitTurnTranscriptReset, setLocalSpeechTriggerPhase, setVadActivity,
    startNextModelAudioTurn, stopAllAudio, stopVideoFrameLoop,
    updateState, getAudioTelemetrySnapshot, flushCaptureWorkletNode,
  } = ports;
  const cleanup = async () => {
    // Prevent concurrent cleanup operations
    if (isCleaningUpRef.current) return;
    isCleaningUpRef.current = true;

    speechTriggerAbortRef.current?.abort();
    speechTriggerAbortRef.current = null;
    setLocalSpeechTriggerPhase(null);
    setVadActivity(false, false);

    const wasSpeechGated = speechGateRef.current !== null;
    if (wasSpeechGated) {
      // Do not make a foreground/user-session transition wait for local
      // inference. Epoch invalidation makes any in-flight result harmless.
      speechGateEpochRef.current += 1;
    }
    const activeCaptureNode = workletNodeRef.current;
    if (activeCaptureNode && !wasSpeechGated) {
      await flushCaptureWorkletNode(activeCaptureNode);
    }
    if (inputPacketizerRef.current) {
      if (!wasSpeechGated) {
        await inputPacketizerRef.current.flushPending();
      }
      inputPacketizerRef.current.dispose();
      inputPacketizerRef.current = null;
    }
    if (pcmCaptureRouterRef.current) {
      await pcmCaptureRouterRef.current.stop();
      pcmCaptureRouterRef.current = null;
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
      playbackDrainCoordinatorRef.current.cancelAll();
      playbackPendingRef.current = false;
      try { playbackNodeRef.current.port.postMessage({ type: 'reset' }); } catch { }
      try { playbackNodeRef.current.disconnect(); } catch { }
      playbackNodeRef.current = null;
    }
    playbackUntilRef.current = 0;
    playbackActiveRef.current = false;
    speechGateRef.current = null;
    speechTurnBoundaryRef.current = null;
    semanticSpeechCaptureRef.current?.reset();
    semanticSpeechCaptureRef.current = null;
    loadingFallbackOnsetAtRef.current = null;
    awaitingModelTurnRef.current = false;
    boundaryClosePromiseRef.current = null;
    if (!wasSpeechGated) {
      speechGateEpochRef.current += 1;
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
      try { if (typeof session.close === 'function') session.close(); } catch { }
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
    localSpeechPendingRef.current = false;
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
  };

  const stop = async () => {
    updateState('idle');
    if (inputPacketizerRef.current && !speechGateRef.current) {
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
  };
  return { cleanup, stop };
}
