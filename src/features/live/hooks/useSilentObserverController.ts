// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Modality } from '@google/genai';
import { ChatMessage } from '../../../core/types';
import {
  useGeminiLiveConversation,
  LiveSessionState,
  type LiveTurnTranscriptUpdate,
} from '../../speech';
import { useMaestroStore } from '../../../store';
import { createSmartRef } from '../../../shared/utils/smartRef';
import { buildLiveSystemInstruction } from '../utils/liveSystemInstruction';
import { LIVE_OPEN_TRIGGER } from '../../../../shared/liveOpenReason';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../core/config/activityTokens';

const OBSERVER_RETRY_MS = 8000;
const OBSERVER_MANUAL_STOP_HOLD_MS = 5000;

export interface UseSilentObserverControllerConfig {
  enabled: boolean;
  isBlockingActivity: boolean;
  liveSessionState: LiveSessionState;
  liveVideoStream: MediaStream | null;
  visualContextVideoRef: React.RefObject<HTMLVideoElement | null>;
  currentSystemPromptText: string;
  resolveBookmarkContextSummary: () => string | null;
  computeHistorySubsetForMedia: (arr: ChatMessage[]) => ChatMessage[];
  onTurnComplete?: (
    userText: string,
    modelText: string,
    userAudioPcm?: Int16Array,
    modelAudioLines?: Int16Array[]
  ) => void | Promise<void>;
  onTurnTranscriptUpdate?: (update: LiveTurnTranscriptUpdate) => void;
}

export interface UseSilentObserverControllerReturn {
  silentObserverState: LiveSessionState;
  silentObserverError: string | null;
  stopSilentObserver: () => Promise<void>;
  resetSilentObserver: () => Promise<void>;
}

const readForegroundState = () => {
  if (typeof document === 'undefined') return true;
  const visible = !document.hidden;
  const focused = typeof document.hasFocus === 'function' ? document.hasFocus() : true;
  return visible && focused;
};

export const useSilentObserverController = ({
  enabled,
  isBlockingActivity,
  liveSessionState,
  liveVideoStream,
  visualContextVideoRef,
  currentSystemPromptText,
  resolveBookmarkContextSummary,
  computeHistorySubsetForMedia,
  onTurnComplete,
  onTurnTranscriptUpdate,
}: UseSilentObserverControllerConfig): UseSilentObserverControllerReturn => {
  const [isForeground, setIsForeground] = useState<boolean>(() => readForegroundState());
  const [lifecycleTick, setLifecycleTick] = useState(0);

  const silentObserverState = useMaestroStore(state => state.silentObserverState);
  const silentObserverError = useMaestroStore(state => state.silentObserverError);
  const setSilentObserverState = useMaestroStore(state => state.setSilentObserverState);
  const setSilentObserverError = useMaestroStore(state => state.setSilentObserverError);
  const addActivityToken = useMaestroStore(state => state.addActivityToken);
  const removeActivityToken = useMaestroStore(state => state.removeActivityToken);

  const messagesRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.messages), []);
  const settingsRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.settings), []);

  const shouldRunRef = useRef(false);
  const suspendUntilRef = useRef<number>(0);
  const retryTimerRef = useRef<number | null>(null);
  const suspendWakeTimerRef = useRef<number | null>(null);
  const lastStartAttemptRef = useRef<number>(0);
  const startInFlightRef = useRef(false);
  const observerLiveTokenRef = useRef<string | null>(null);

  const clearRetryTimer = useCallback(() => {
    if (retryTimerRef.current !== null) {
      window.clearTimeout(retryTimerRef.current);
      retryTimerRef.current = null;
    }
  }, []);

  const clearSuspendWakeTimer = useCallback(() => {
    if (suspendWakeTimerRef.current !== null) {
      window.clearTimeout(suspendWakeTimerRef.current);
      suspendWakeTimerRef.current = null;
    }
  }, []);

  const {
    start: startObserverConversation,
    stop: stopObserverConversation,
    updateVideoInput: updateObserverVideoInput,
  } = useGeminiLiveConversation({
    onStateChange: (state) => {
      setSilentObserverState(state);
      if (state === 'connecting' || state === 'active') {
        setSilentObserverError(null);
      }
      if (state === 'active') {
        clearRetryTimer();
      }
      if (observerLiveTokenRef.current) {
        removeActivityToken(observerLiveTokenRef.current);
        observerLiveTokenRef.current = null;
      }
      if (state === 'connecting' || state === 'active') {
        observerLiveTokenRef.current = addActivityToken(
          TOKEN_CATEGORY.LIVE,
          state === 'connecting' ? TOKEN_SUBTYPE.OBSERVER_CONNECTING : TOKEN_SUBTYPE.OBSERVER_SESSION,
        );
      }
    },
    onError: (message) => {
      setSilentObserverError(message);
    },
    onTurnTranscriptUpdate: (update) => {
      onTurnTranscriptUpdate?.(update);
    },
    onTurnComplete: (userText, modelText, userAudioPcm, modelAudioLines) => {
      if (!onTurnComplete) return;
      return Promise.resolve(onTurnComplete(userText, modelText, userAudioPcm, modelAudioLines)).catch((error) => {
        console.error('Silent observer turn handler failed:', error);
      });
    },
    onGoAway: (notice) => {
      // This is an advance warning, not a command to close immediately. The
      // provider will close after its advertised grace period; the shared Live
      // lifecycle then drains queued model speech before observer reconnection.
      console.debug('Silent observer received Live disconnect warning; awaiting provider close.', notice.timeLeft);
    },
  });

  const buildObserverInstruction = useCallback(async () => {
    return buildLiveSystemInstruction({
      basePrompt: currentSystemPromptText,
      messages: messagesRef.current,
      computeHistorySubsetForMedia,
      resolveBookmarkContextSummary,
    });
  }, [computeHistorySubsetForMedia, currentSystemPromptText, messagesRef, resolveBookmarkContextSummary]);
  const instructionBuilderRef = useRef(buildObserverInstruction);
  instructionBuilderRef.current = buildObserverInstruction;

  const startObserverInternal = useCallback(async () => {
    if (startInFlightRef.current) return;
    startInFlightRef.current = true;
    clearRetryTimer();
    const startAttempt = Date.now();
    lastStartAttemptRef.current = startAttempt;

    try {
      const liveSystemInstruction = await buildObserverInstruction();
      if (!shouldRunRef.current || lastStartAttemptRef.current !== startAttempt) return;

      const voiceName = settingsRef.current.tts.voiceName || 'Kore';
      const activeStream = liveVideoStream && liveVideoStream.active ? liveVideoStream : null;

      await startObserverConversation({
        liveOpenTrigger: LIVE_OPEN_TRIGGER.WHISPER_OBSERVER,
        stream: activeStream,
        videoElement: visualContextVideoRef.current,
        systemInstruction: liveSystemInstruction,
        buildSystemInstruction: () => instructionBuilderRef.current(),
        voiceName,
        responseModalities: [Modality.AUDIO],
        playModelAudio: true,
        emitTurns: Boolean(onTurnComplete),
        costFeature: 'reengagement',
        // The observer is locally armed without a provider transport. VAD plus
        // Whisper must find words before it may open one.
        gateInputOnSpeech: true,
      });
    } catch (error) {
      if (!shouldRunRef.current || lastStartAttemptRef.current !== startAttempt) return;
      clearRetryTimer();
      const message = error instanceof Error ? error.message : String(error);
      setSilentObserverError(message);
      setSilentObserverState('error');
    } finally {
      startInFlightRef.current = false;
    }
  }, [
    buildObserverInstruction,
    clearRetryTimer,
    liveVideoStream,
    onTurnComplete,
    onTurnTranscriptUpdate,
    settingsRef,
    setSilentObserverError,
    setSilentObserverState,
    startObserverConversation,
    visualContextVideoRef,
  ]);

  const stopObserverInternal = useCallback(async (reason: string, holdMs = 0) => {
    void reason;
    if (holdMs > 0) {
      suspendUntilRef.current = Date.now() + holdMs;
      clearSuspendWakeTimer();
      suspendWakeTimerRef.current = window.setTimeout(() => {
        suspendWakeTimerRef.current = null;
        setLifecycleTick(prev => prev + 1);
      }, holdMs + 20);
    } else {
      suspendUntilRef.current = 0;
      clearSuspendWakeTimer();
    }
    clearRetryTimer();
    try {
      await stopObserverConversation();
    } catch {
      // Ignore stop errors; observer lifecycle will reconcile on next effect tick.
    }
  }, [clearRetryTimer, clearSuspendWakeTimer, stopObserverConversation]);

  const stopSilentObserver = useCallback(async () => {
    await stopObserverInternal('manual-stop', OBSERVER_MANUAL_STOP_HOLD_MS);
  }, [stopObserverInternal]);

  const resetSilentObserver = useCallback(async () => {
    setSilentObserverError(null);
    await stopObserverInternal('reset', 0);
    setLifecycleTick(prev => prev + 1);
  }, [setSilentObserverError, stopObserverInternal]);

  useEffect(() => {
    const syncForeground = () => setIsForeground(readForegroundState());
    document.addEventListener('visibilitychange', syncForeground);
    window.addEventListener('focus', syncForeground);
    window.addEventListener('blur', syncForeground);
    return () => {
      document.removeEventListener('visibilitychange', syncForeground);
      window.removeEventListener('focus', syncForeground);
      window.removeEventListener('blur', syncForeground);
    };
  }, []);

  useEffect(() => {
    const stream = liveVideoStream && liveVideoStream.active ? liveVideoStream : null;
    if (
      silentObserverState !== 'armed'
      && silentObserverState !== 'active'
      && silentObserverState !== 'connecting'
    ) {
      return;
    }
    void updateObserverVideoInput(stream, visualContextVideoRef.current);
  }, [liveVideoStream, silentObserverState, updateObserverVideoInput, visualContextVideoRef]);

  useEffect(() => {
    const now = Date.now();
    const suspended = now < suspendUntilRef.current;
    const shouldRun =
      enabled &&
      isForeground &&
      !suspended &&
      liveSessionState === 'idle' &&
      !isBlockingActivity;

    shouldRunRef.current = shouldRun;

    if (!shouldRun) {
      clearRetryTimer();
      if (silentObserverState !== 'idle') {
        void stopObserverInternal('auto-stop');
      }
      return;
    }

    if (silentObserverState === 'idle') {
      void startObserverInternal();
      return;
    }

    if (silentObserverState === 'error') {
      const elapsed = now - lastStartAttemptRef.current;
      const waitMs = Math.max(0, OBSERVER_RETRY_MS - elapsed);
      clearRetryTimer();
      retryTimerRef.current = window.setTimeout(() => {
        retryTimerRef.current = null;
        if (!shouldRunRef.current) return;
        void startObserverInternal();
      }, waitMs);
    }
  }, [
    clearRetryTimer,
    enabled,
    isBlockingActivity,
    isForeground,
    lifecycleTick,
    liveSessionState,
    silentObserverState,
    startObserverInternal,
    stopObserverInternal,
  ]);

  useEffect(() => {
    return () => {
      clearRetryTimer();
      clearSuspendWakeTimer();
      void stopObserverConversation();
      if (observerLiveTokenRef.current) {
        removeActivityToken(observerLiveTokenRef.current);
        observerLiveTokenRef.current = null;
      }
    };
  }, [clearRetryTimer, clearSuspendWakeTimer, removeActivityToken, stopObserverConversation]);

  return {
    silentObserverState,
    silentObserverError,
    stopSilentObserver,
    resetSilentObserver,
  };
};

export default useSilentObserverController;
