
import { useRef, useEffect, useCallback } from 'react';
import type { MutableRefObject } from 'react';
import { AppSettings } from '../../../core/types';
import { useMaestroStore } from '../../../store';

interface UseSmartReengagementProps {
  settings: AppSettings;
  isLoadingHistory: boolean;
  selectedLanguagePairId: string | null;
  isSending: boolean;
  isSpeaking: boolean;
  // Optional refs for real-time state checking (used when values might be stale)
  isSendingRef?: MutableRefObject<boolean>;
  isSpeakingRef?: MutableRefObject<boolean>;
  isVisualContextActive: boolean;
  externalUiTaskCount: number;
  triggerReengagementSequence: () => Promise<void>;
  addUiBusyToken: (token: string) => string;
  removeUiBusyToken: (token?: string | null) => void;
}

export const useSmartReengagement = ({
  settings,
  isLoadingHistory,
  selectedLanguagePairId,
  isSending,
  isSpeaking,
  isSendingRef,
  isSpeakingRef,
  isVisualContextActive,
  externalUiTaskCount,
  triggerReengagementSequence,
  addUiBusyToken,
  removeUiBusyToken
}: UseSmartReengagementProps) => {
  const reengagementPhase = useMaestroStore(state => state.reengagementPhase);
  const setReengagementPhase = useMaestroStore(state => state.setReengagementPhase);
  const setReengagementDeadline = useMaestroStore(state => state.setReengagementDeadline);
  const setIsUserActive = useMaestroStore(state => state.setIsUserActive);
  
  // Timer and token refs
  const reengagementTimersRef = useRef<{ waitTimer: number | null; countdownTimer: number | null }>({ waitTimer: null, countdownTimer: null });
  const reengagementTokensRef = useRef<{ waitToken: string | null; countdownToken: string | null }>({ waitToken: null, countdownToken: null });
  const reengagementDeadlineRef = useRef<number | null>(null);
  const isUserActiveRef = useRef<boolean>(false);
  
  // Refs to store the latest versions of interdependent functions
  // This breaks the circular dependency chain by allowing functions to call
  // each other through refs without being in each other's dependency arrays
  const scheduleReengagementRef = useRef<(reason: string, delayOverrideMs?: number) => void>(() => {});
  const cancelReengagementRef = useRef<() => void>(() => {});
  const beginCountdownRef = useRef<(reason: string) => void>(() => {});
  const canScheduleReengagementRef = useRef<() => boolean>(() => false);
  const triggerReengagementSequenceRef = useRef(triggerReengagementSequence);
  
  // Refs for stable access to props
  const settingsRef = useRef(settings);
  const addUiBusyTokenRef = useRef(addUiBusyToken);
  const removeUiBusyTokenRef = useRef(removeUiBusyToken);

  // Keep refs updated
  useEffect(() => { triggerReengagementSequenceRef.current = triggerReengagementSequence; }, [triggerReengagementSequence]);
  useEffect(() => { settingsRef.current = settings; }, [settings]);
  useEffect(() => { addUiBusyTokenRef.current = addUiBusyToken; }, [addUiBusyToken]);
  useEffect(() => { removeUiBusyTokenRef.current = removeUiBusyToken; }, [removeUiBusyToken]);

  const isReengagementToken = (token: string | null | undefined): boolean => {
    if (!token || typeof token !== 'string') return false;
    return token.startsWith('reengage-');
  };

  // canScheduleReengagement - checks if reengagement can be scheduled
  // Uses refs for state values that might be stale
  const canScheduleReengagement = useCallback((): boolean => {
    if (isLoadingHistory) return false;
    if (!selectedLanguagePairId) return false;
    // Use refs if available for real-time values
    if (isSendingRef?.current ?? isSending) return false;
    if (isSpeakingRef?.current ?? isSpeaking) return false;
    if (isVisualContextActive) return false;
    if (externalUiTaskCount > 0) return false;
    return true;
  }, [isLoadingHistory, selectedLanguagePairId, isSending, isSending, isSpeaking, isSendingRef, isSpeakingRef, isVisualContextActive, externalUiTaskCount]);

  // Keep canScheduleReengagement ref updated
  useEffect(() => { canScheduleReengagementRef.current = canScheduleReengagement; }, [canScheduleReengagement]);

  // cancelReengagement - cancels any pending reengagement
  // Uses refs for callbacks to avoid circular dependencies
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
      removeUiBusyTokenRef.current(tokens.waitToken);
      tokens.waitToken = null;
    }
    if (tokens.countdownToken) {
      removeUiBusyTokenRef.current(tokens.countdownToken);
      tokens.countdownToken = null;
    }
    reengagementDeadlineRef.current = null;
    setReengagementDeadline(null);
    setReengagementPhase('idle');
  }, [setReengagementDeadline, setReengagementPhase]);

  // Keep cancelReengagement ref updated
  useEffect(() => { cancelReengagementRef.current = cancelReengagement; }, [cancelReengagement]);

  // startWaitTimer - starts the wait timer before countdown
  // Uses refs for interdependent callbacks to avoid circular dependencies
  const startWaitTimer = useCallback((delayMs: number, reason: string) => {
    if (!canScheduleReengagementRef.current()) {
      cancelReengagementRef.current();
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
      removeUiBusyTokenRef.current(tokens.countdownToken);
      tokens.countdownToken = null;
    }
    if (tokens.waitToken) {
      removeUiBusyTokenRef.current(tokens.waitToken);
      tokens.waitToken = null;
    }
    const clampedDelay = Math.max(0, Math.floor(Number.isFinite(delayMs) ? delayMs : 0));
    const token = addUiBusyTokenRef.current(`reengage-wait:${reason}:${Date.now()}`);
    tokens.waitToken = token;
    
    // Split the wait time into 'waiting' (Resting) and 'watching' (Observing) if long enough
    const splitThreshold = 10000; // 10 seconds
    
    if (clampedDelay > splitThreshold) {
        const firstPhase = Math.floor(clampedDelay * 0.6);
        const secondPhase = clampedDelay - firstPhase;
        
        reengagementDeadlineRef.current = Date.now() + clampedDelay;
        setReengagementDeadline(reengagementDeadlineRef.current);
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
        setReengagementDeadline(reengagementDeadlineRef.current);
        setReengagementPhase('watching'); // Short delay directly to observing
        timers.waitTimer = window.setTimeout(() => {
            timers.waitTimer = null;
            beginCountdownRef.current('timer-elapsed');
        }, clampedDelay);
    }

  }, [setReengagementDeadline, setReengagementPhase]);

  // scheduleReengagement - main entry point to schedule reengagement
  // Uses refs for interdependent callbacks
  const scheduleReengagement = useCallback((reason: string, delayOverrideMs?: number) => {
    const defaultDelay = settingsRef.current.smartReengagement.thresholdSeconds * 1000;
    const delay = typeof delayOverrideMs === 'number' && Number.isFinite(delayOverrideMs)
      ? delayOverrideMs
      : defaultDelay;
    if (!canScheduleReengagementRef.current()) {
      cancelReengagementRef.current();
      return;
    }
    startWaitTimer(delay, reason);
  }, [startWaitTimer]);

  // Keep scheduleReengagement ref updated
  useEffect(() => { scheduleReengagementRef.current = scheduleReengagement; }, [scheduleReengagement]);

  // beginCountdown - starts the final countdown before triggering reengagement
  // Uses refs for circular dependencies with scheduleReengagement
  const beginCountdown = useCallback((reason: string) => {
    if (isUserActiveRef.current) {
      scheduleReengagementRef.current('user-active-during-countdown');
      return;
    }
    if (!canScheduleReengagementRef.current()) {
      scheduleReengagementRef.current('countdown-blocked');
      return;
    }
    const timers = reengagementTimersRef.current;
    if (timers.waitTimer) {
      clearTimeout(timers.waitTimer);
      timers.waitTimer = null;
    }
    const tokens = reengagementTokensRef.current;
    if (tokens.waitToken) {
      removeUiBusyTokenRef.current(tokens.waitToken);
      tokens.waitToken = null;
    }
    if (tokens.countdownToken) {
      removeUiBusyTokenRef.current(tokens.countdownToken);
      tokens.countdownToken = null;
    }
    if (timers.countdownTimer) {
      clearTimeout(timers.countdownTimer);
      timers.countdownTimer = null;
    }
    const token = addUiBusyTokenRef.current(`reengage-countdown:${reason}:${Date.now()}`);
    tokens.countdownToken = token;
    reengagementDeadlineRef.current = null;
    setReengagementDeadline(null);
    setReengagementPhase('countdown');
    timers.countdownTimer = window.setTimeout(async () => {
      timers.countdownTimer = null;
      if (isUserActiveRef.current) {
        scheduleReengagementRef.current('user-active-during-countdown');
        return;
      }
      if (!canScheduleReengagementRef.current()) {
        scheduleReengagementRef.current('countdown-blocked');
        return;
      }
      cancelReengagementRef.current();
      await triggerReengagementSequenceRef.current();
    }, 5000);
  }, [setReengagementDeadline, setReengagementPhase]);

  // Keep beginCountdown ref updated
  useEffect(() => { beginCountdownRef.current = beginCountdown; }, [beginCountdown]);

  // handleUserActivity - called when user interacts, cancels reengagement
  const handleUserActivity = useCallback(() => {
    isUserActiveRef.current = true;
    setIsUserActive(true);
    cancelReengagementRef.current();
    setTimeout(() => { 
      isUserActiveRef.current = false; 
      setIsUserActive(false);
    }, 3000);
  }, [setIsUserActive]);

  return {
    reengagementPhase,
    scheduleReengagement,
    cancelReengagement,
    isReengagementToken,
    handleUserActivity,
    setReengagementPhase
  };
};
