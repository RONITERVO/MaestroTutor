// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Modality } from '@google/genai';
import { ChatMessage } from '../../../core/types';
import { useGeminiLiveConversation, LiveSessionState } from '../../speech';
import { useMaestroStore } from '../../../store';
import { createSmartRef } from '../../../shared/utils/smartRef';
import { buildLiveSystemInstruction } from '../utils/liveSystemInstruction';

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
}

export interface UseSilentObserverControllerReturn {
  silentObserverState: LiveSessionState;
  silentObserverError: string | null;
  stopSilentObserver: () => Promise<void>;
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
}: UseSilentObserverControllerConfig): UseSilentObserverControllerReturn => {
  const [isForeground, setIsForeground] = useState<boolean>(() => readForegroundState());
  const [lifecycleTick, setLifecycleTick] = useState(0);

  const silentObserverState = useMaestroStore(state => state.silentObserverState);
  const silentObserverError = useMaestroStore(state => state.silentObserverError);
  const setSilentObserverState = useMaestroStore(state => state.setSilentObserverState);
  const setSilentObserverError = useMaestroStore(state => state.setSilentObserverError);

  const messagesRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.messages), []);
  const settingsRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.settings), []);

  const shouldRunRef = useRef(false);
  const suspendUntilRef = useRef<number>(0);
  const retryTimerRef = useRef<number | null>(null);
  const suspendWakeTimerRef = useRef<number | null>(null);
  const lastStartAttemptRef = useRef<number>(0);
  const resumptionHandleRef = useRef<string | undefined>(undefined);
  const stopObserverInternalRef = useRef<((reason: string, holdMs?: number) => Promise<void>) | null>(null);

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
    },
    onError: (message) => {
      setSilentObserverError(message);
    },
    onTurnComplete: (userText, modelText, userAudioPcm, modelAudioLines) => {
      if (!onTurnComplete) return;
      void Promise.resolve(onTurnComplete(userText, modelText, userAudioPcm, modelAudioLines)).catch((error) => {
        console.error('Silent observer turn handler failed:', error);
      });
    },
    onGoAway: () => {
      if (!shouldRunRef.current) return;
      void stopObserverInternalRef.current?.('observer-go-away', 0);
    },
    onSessionResumptionUpdate: (update) => {
      if (update.newHandle && update.resumable !== false) {
        resumptionHandleRef.current = update.newHandle;
      }
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

  const startObserverInternal = useCallback(async () => {
    clearRetryTimer();
    const startAttempt = Date.now();
    lastStartAttemptRef.current = startAttempt;

    try {
      const liveSystemInstruction = await buildObserverInstruction();
      if (!shouldRunRef.current || lastStartAttemptRef.current !== startAttempt) return;

      const voiceName = settingsRef.current.tts.voiceName || 'Kore';
      const activeStream = liveVideoStream && liveVideoStream.active ? liveVideoStream : null;
      const sessionResumption = resumptionHandleRef.current
        ? { handle: resumptionHandleRef.current }
        : {};

      await startObserverConversation({
        stream: activeStream,
        videoElement: visualContextVideoRef.current,
        systemInstruction: liveSystemInstruction,
        voiceName,
        responseModalities: [Modality.AUDIO],
        playModelAudio: true,
        emitTurns: Boolean(onTurnComplete),
        sessionResumption,
      });
    } catch (error) {
      if (!shouldRunRef.current || lastStartAttemptRef.current !== startAttempt) return;
      clearRetryTimer();
      const message = error instanceof Error ? error.message : String(error);
      setSilentObserverError(message);
      setSilentObserverState('error');
    }
  }, [
    buildObserverInstruction,
    clearRetryTimer,
    liveVideoStream,
    onTurnComplete,
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
    }
    clearRetryTimer();
    try {
      await stopObserverConversation();
    } catch {
      // Ignore stop errors; observer lifecycle will reconcile on next effect tick.
    }
  }, [clearRetryTimer, clearSuspendWakeTimer, stopObserverConversation]);

  useEffect(() => {
    stopObserverInternalRef.current = stopObserverInternal;
  }, [stopObserverInternal]);

  const stopSilentObserver = useCallback(async () => {
    await stopObserverInternal('manual-stop', OBSERVER_MANUAL_STOP_HOLD_MS);
  }, [stopObserverInternal]);

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
    if (silentObserverState !== 'active' && silentObserverState !== 'connecting') {
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
    };
  }, [clearRetryTimer, clearSuspendWakeTimer, stopObserverConversation]);

  return {
    silentObserverState,
    silentObserverError,
    stopSilentObserver,
  };
};

export default useSilentObserverController;
