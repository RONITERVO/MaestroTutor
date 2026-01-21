// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * useIdleReengagement - schedules reengagement when idle.
 * 
 * Uses refs for callback functions to avoid infinite loops caused by
 * callback recreation triggering effect re-runs.
 */

import { useEffect, useRef } from 'react';
import type { ReengagementPhase } from '../../store';
import type { LanguagePair } from '../../core/types';

interface UseIdleReengagementConfig {
  selectedLanguagePair: LanguagePair | undefined;
  isSpeaking: boolean;
  isSending: boolean;
  isListening: boolean;
  isUserActive: boolean;
  reengagementPhase: ReengagementPhase;
  scheduleReengagement: (reason: string, delayOverrideMs?: number) => void;
  cancelReengagement: () => void;
}

export const useIdleReengagement = ({
  selectedLanguagePair,
  isSpeaking,
  isSending,
  isListening,
  isUserActive,
  reengagementPhase,
  scheduleReengagement,
  cancelReengagement,
}: UseIdleReengagementConfig) => {
  // Store callbacks in refs to avoid them triggering effect re-runs
  // when they are recreated (which happens due to their dependencies)
  const scheduleReengagementRef = useRef(scheduleReengagement);
  const cancelReengagementRef = useRef(cancelReengagement);
  
  // Keep refs updated with latest callbacks (combined into single effect)
  useEffect(() => {
    scheduleReengagementRef.current = scheduleReengagement;
    cancelReengagementRef.current = cancelReengagement;
  }, [scheduleReengagement, cancelReengagement]);

  // Main effect - only depends on state values, not callbacks
  useEffect(() => {
    if (!selectedLanguagePair) {
      cancelReengagementRef.current();
      return;
    }
    if (isSpeaking || isSending || isListening || isUserActive) {
      cancelReengagementRef.current();
      return;
    }
    if (reengagementPhase === 'idle') {
      scheduleReengagementRef.current('became-idle');
    }
  }, [selectedLanguagePair, isSpeaking, isSending, isListening, isUserActive, reengagementPhase]);
};

export default useIdleReengagement;
