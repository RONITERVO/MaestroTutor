
import { useState, useRef, useEffect, useCallback } from 'react';
import { AppSettings } from '../types';

interface UseSmartReengagementProps {
  settings: AppSettings;
  isLoadingHistory: boolean;
  selectedLanguagePairId: string | null;
  isSending: boolean;
  isSpeaking: boolean;
  isVisualContextActive: boolean;
  externalUiTaskCount: number;
  triggerReengagementSequence: () => Promise<void>;
  addUiBusyToken: (token: string) => string;
  removeUiBusyToken: (token?: string | null) => void;
}

type ReengagementPhase = 'idle' | 'waiting' | 'watching' | 'countdown' | 'engaging';

export const useSmartReengagement = ({
  settings,
  isLoadingHistory,
  selectedLanguagePairId,
  isSending,
  isSpeaking,
  isVisualContextActive,
  externalUiTaskCount,
  triggerReengagementSequence,
  addUiBusyToken,
  removeUiBusyToken
}: UseSmartReengagementProps) => {
  const [reengagementPhase, setReengagementPhase] = useState<ReengagementPhase>('idle');
  const reengagementTimersRef = useRef<{ waitTimer: number | null; countdownTimer: number | null }>({ waitTimer: null, countdownTimer: null });
  const reengagementTokensRef = useRef<{ waitToken: string | null; countdownToken: string | null }>({ waitToken: null, countdownToken: null });
  const reengagementDeadlineRef = useRef<number | null>(null);
  const isUserActiveRef = useRef<boolean>(false);
  const beginCountdownRef = useRef<(reason: string) => void>(() => {});
  const triggerReengagementSequenceRef = useRef(triggerReengagementSequence);

  useEffect(() => { triggerReengagementSequenceRef.current = triggerReengagementSequence; }, [triggerReengagementSequence]);

  const isReengagementToken = (token: string | null | undefined): boolean => {
    if (!token || typeof token !== 'string') return false;
    return token.startsWith('reengage-');
  };

  const canScheduleReengagement = useCallback((): boolean => {
    if (isLoadingHistory) return false;
    if (!selectedLanguagePairId) return false;
    if (isSending) return false;
    if (isSpeaking) return false;
    if (isVisualContextActive) return false;
    if (externalUiTaskCount > 0) return false;
    return true;
  }, [isLoadingHistory, selectedLanguagePairId, isSending, isSpeaking, isVisualContextActive, externalUiTaskCount]);

  const cancelReengagement = useCallback(() => {
    const timers = reengagementTimersRef.current;
    if (timers.waitTimer) {
      clearTimeout(timers.waitTimer);
      timers.waitTimer = null;
    }
    if (timers.countdownTimer) {
      clearTimeout(timers.countdownTimer);
      timers.countdownTimer = null;
    }
    const tokens = reengagementTokensRef.current;
    if (tokens.waitToken) {
      removeUiBusyToken(tokens.waitToken);
      tokens.waitToken = null;
    }
    if (tokens.countdownToken) {
      removeUiBusyToken(tokens.countdownToken);
      tokens.countdownToken = null;
    }
    reengagementDeadlineRef.current = null;
    setReengagementPhase('idle');
  }, [removeUiBusyToken]);

  const startWaitTimer = useCallback((delayMs: number, reason: string) => {
    if (!canScheduleReengagement()) {
      cancelReengagement();
      return;
    }
    const timers = reengagementTimersRef.current;
    if (timers.waitTimer) {
      clearTimeout(timers.waitTimer);
      timers.waitTimer = null;
    }
    if (timers.countdownTimer) {
      clearTimeout(timers.countdownTimer);
      timers.countdownTimer = null;
    }
    const tokens = reengagementTokensRef.current;
    if (tokens.countdownToken) {
      removeUiBusyToken(tokens.countdownToken);
      tokens.countdownToken = null;
    }
    if (tokens.waitToken) {
      removeUiBusyToken(tokens.waitToken);
      tokens.waitToken = null;
    }
    const clampedDelay = Math.max(0, Math.floor(Number.isFinite(delayMs) ? delayMs : 0));
    const token = addUiBusyToken(`reengage-wait:${reason}:${Date.now()}`);
    tokens.waitToken = token;
    
    // Split the wait time into 'waiting' (Resting) and 'watching' (Observing) if long enough
    const splitThreshold = 10000; // 10 seconds
    
    if (clampedDelay > splitThreshold) {
        const firstPhase = Math.floor(clampedDelay * 0.6);
        const secondPhase = clampedDelay - firstPhase;
        
        reengagementDeadlineRef.current = Date.now() + clampedDelay;
        setReengagementPhase('waiting'); // Low attention / Resting
        
        timers.waitTimer = window.setTimeout(() => {
            setReengagementPhase('watching'); // Medium attention / Observing
            
            timers.waitTimer = window.setTimeout(() => {
                timers.waitTimer = null;
                beginCountdownRef.current('timer-elapsed');
            }, secondPhase);
            
        }, firstPhase);
        
    } else {
        reengagementDeadlineRef.current = Date.now() + clampedDelay;
        setReengagementPhase('watching'); // Short delay directly to observing
        timers.waitTimer = window.setTimeout(() => {
            timers.waitTimer = null;
            beginCountdownRef.current('timer-elapsed');
        }, clampedDelay);
    }

  }, [canScheduleReengagement, removeUiBusyToken, addUiBusyToken, cancelReengagement]);

  const scheduleReengagement = useCallback((reason: string, delayOverrideMs?: number) => {
    const defaultDelay = settings.smartReengagement.thresholdSeconds * 1000;
    const delay = typeof delayOverrideMs === 'number' && Number.isFinite(delayOverrideMs)
      ? delayOverrideMs
      : defaultDelay;
    if (!canScheduleReengagement()) {
      cancelReengagement();
      return;
    }
    startWaitTimer(delay, reason);
  }, [canScheduleReengagement, cancelReengagement, startWaitTimer, settings.smartReengagement.thresholdSeconds]);

  const beginCountdown = useCallback((reason: string) => {
    if (isUserActiveRef.current) {
      scheduleReengagement('user-active-during-countdown');
      return;
    }
    if (!canScheduleReengagement()) {
      scheduleReengagement('countdown-blocked');
      return;
    }
    const timers = reengagementTimersRef.current;
    if (timers.waitTimer) {
      clearTimeout(timers.waitTimer);
      timers.waitTimer = null;
    }
    const tokens = reengagementTokensRef.current;
    if (tokens.waitToken) {
      removeUiBusyToken(tokens.waitToken);
      tokens.waitToken = null;
    }
    if (tokens.countdownToken) {
      removeUiBusyToken(tokens.countdownToken);
      tokens.countdownToken = null;
    }
    if (timers.countdownTimer) {
      clearTimeout(timers.countdownTimer);
      timers.countdownTimer = null;
    }
    const token = addUiBusyToken(`reengage-countdown:${reason}:${Date.now()}`);
    tokens.countdownToken = token;
    reengagementDeadlineRef.current = null;
    setReengagementPhase('countdown');
    timers.countdownTimer = window.setTimeout(async () => {
      timers.countdownTimer = null;
      if (isUserActiveRef.current) {
        scheduleReengagement('user-active-during-countdown');
        return;
      }
      if (!canScheduleReengagement()) {
        scheduleReengagement('countdown-blocked');
        return;
      }
      cancelReengagement();
      await triggerReengagementSequenceRef.current();
    }, 5000);
  }, [canScheduleReengagement, scheduleReengagement, removeUiBusyToken, addUiBusyToken, cancelReengagement]);

  useEffect(() => {
    beginCountdownRef.current = beginCountdown;
  }, [beginCountdown]);

  const handleUserActivity = useCallback(() => {
    isUserActiveRef.current = true;
    cancelReengagement();
    setTimeout(() => { isUserActiveRef.current = false; }, 3000);
  }, [cancelReengagement]);

  return {
    reengagementPhase,
    scheduleReengagement,
    cancelReengagement,
    isReengagementToken,
    handleUserActivity,
    setReengagementPhase
  };
};
