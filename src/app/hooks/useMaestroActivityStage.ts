// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * useMaestroActivityStage - syncs activity stage based on state.
 * 
 * Uses ref for setMaestroActivityStage to avoid infinite loops.
 */

import { useEffect, useRef } from 'react';
import type { MaestroActivityStage } from '../../core/types';
import type { ReengagementPhase } from '../../store';

interface UseMaestroActivityStageConfig {
  isSpeaking: boolean;
  isSending: boolean;
  isListening: boolean;
  reengagementPhase: ReengagementPhase;
  setMaestroActivityStage: (stage: MaestroActivityStage) => void;
}

export const useMaestroActivityStage = ({
  isSpeaking,
  isSending,
  isListening,
  reengagementPhase,
  setMaestroActivityStage,
}: UseMaestroActivityStageConfig) => {
  // Store setter in ref to avoid it triggering effect re-runs
  const setMaestroActivityStageRef = useRef(setMaestroActivityStage);
  
  useEffect(() => {
    setMaestroActivityStageRef.current = setMaestroActivityStage;
  }, [setMaestroActivityStage]);

  useEffect(() => {
    if (isSpeaking) {
      setMaestroActivityStageRef.current('speaking');
    } else if (isSending) {
      setMaestroActivityStageRef.current('typing');
    } else if (isListening) {
      setMaestroActivityStageRef.current('listening');
    } else if (reengagementPhase === 'countdown' || reengagementPhase === 'engaging') {
      setMaestroActivityStageRef.current('observing_high');
    } else if (reengagementPhase === 'watching') {
      setMaestroActivityStageRef.current('observing_medium');
    } else if (reengagementPhase === 'waiting') {
      setMaestroActivityStageRef.current('observing_low');
    } else {
      setMaestroActivityStageRef.current('idle');
    }
  }, [isSpeaking, isSending, isListening, reengagementPhase]);
};

export default useMaestroActivityStage;
