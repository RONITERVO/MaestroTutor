// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { mergeInt16Arrays, trimSilence } from '../../../core-sdk/media/audioProcessing';
import type { LiveTimerPorts } from './ports';
import type { LiveSessionData } from './state';
import { INPUT_SAMPLE_RATE, type LiveTurnTranscriptUpdate, type LiveTurnTranscriptUpdateReason, TRANSCRIPT_UPDATE_INTERVAL_MS } from './types';

export function createLiveTranscripts(state: Pick<LiveSessionData,
  'pendingUserTurnRef' | 'transcriptUpdateTimerRef' | 'pendingTranscriptUpdateRef'
  | 'currentInputTranscriptionRef' | 'localSpeechPendingRef' | 'concealedSpeechProgressRef'
  | 'currentOutputTranscriptionRef' | 'currentUserAudioChunksRef' | 'currentUserTranscriptAudioLengthRef'
  | 'currentModelThinkingTraceRef' | 'currentModelThinkingPhaseRef' | 'currentModelThinkingStatusLineRef'
  | 'lastTranscriptUpdateRef' | 'speechGateRef' | 'callbacksRef'
>, ports: LiveTimerPorts) {
  const {
    pendingUserTurnRef, transcriptUpdateTimerRef, pendingTranscriptUpdateRef,
    currentInputTranscriptionRef, localSpeechPendingRef, concealedSpeechProgressRef,
    currentOutputTranscriptionRef, currentUserAudioChunksRef, currentUserTranscriptAudioLengthRef,
    currentModelThinkingTraceRef, currentModelThinkingPhaseRef, currentModelThinkingStatusLineRef,
    lastTranscriptUpdateRef, speechGateRef, callbacksRef,
  } = state;
  const { setTimeout, clearTimeout } = ports;
  const getTranscriptLinkedUserAudio = () => {
    const merged = mergeInt16Arrays(currentUserAudioChunksRef.current);
    if (merged.length === 0) {
      return new Int16Array(0);
    }

    if (speechGateRef.current) {
      // Gated chunks are exactly what reached Live. Preserve the confirmed
      // three-second lead-in instead of trimming it a second time for storage.
      return merged;
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
  };

  const clearTranscriptUpdateTimer = () => {
    if (transcriptUpdateTimerRef.current !== null) {
      clearTimeout(transcriptUpdateTimerRef.current);
      transcriptUpdateTimerRef.current = null;
    }
  };

  const flushPendingTranscriptUpdate = () => {
    clearTranscriptUpdateTimer();
    const pendingUpdate = pendingTranscriptUpdateRef.current;
    if (!pendingUpdate) return;
    pendingTranscriptUpdateRef.current = null;
    lastTranscriptUpdateRef.current = pendingUpdate;
    callbacksRef.current.onTurnTranscriptUpdate?.(pendingUpdate);
  };

  const emitTurnTranscriptUpdate = (reason: LiveTurnTranscriptUpdateReason) => {
    const pendingUserText = pendingUserTurnRef.current?.text?.trim() ?? '';
    const currentUserText = (
      currentInputTranscriptionRef.current.trim()
    );
    const thinkingTrace = currentModelThinkingTraceRef.current.length > 0
      ? [...currentModelThinkingTraceRef.current]
      : undefined;
    const nextUpdate: LiveTurnTranscriptUpdate = {
      userText: [pendingUserText, currentUserText].filter(Boolean).join('\n').trim(),
      speechPreviewProgress: !currentUserText && localSpeechPendingRef.current
        ? Math.max(3, concealedSpeechProgressRef.current) : undefined,
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
      && previousUpdate.speechPreviewProgress === nextUpdate.speechPreviewProgress
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
    transcriptUpdateTimerRef.current = setTimeout(() => {
      transcriptUpdateTimerRef.current = null;
      flushPendingTranscriptUpdate();
    }, TRANSCRIPT_UPDATE_INTERVAL_MS);
  };

  const emitTurnTranscriptReset = () => {
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
  };
  return { getTranscriptLinkedUserAudio, clearTranscriptUpdateTimer, flushPendingTranscriptUpdate, emitTurnTranscriptUpdate, emitTurnTranscriptReset };
}
