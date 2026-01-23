// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * useSuggestionModeAutoRestart - restarts STT in suggestion mode.
 */

import { useEffect, useRef } from 'react';
import type { MutableRefObject } from 'react';
import { useMaestroStore } from '../../../store';

export interface UseSuggestionModeAutoRestartConfig {
  isListening: boolean;
  settingsRef: MutableRefObject<{ stt: { enabled: boolean; language: string }; isSuggestionMode: boolean }>;
  startListening: (lang: string) => void;
}

export const useSuggestionModeAutoRestart = ({
  isListening,
  startListening,
}: UseSuggestionModeAutoRestartConfig) => {
  const prevIsListeningRef = useRef<boolean>(false);
  const restartTimeoutRef = useRef<number | null>(null);

  useEffect(() => {
    const wasListening = prevIsListeningRef.current;
    prevIsListeningRef.current = isListening;

    // Clear any pending restart timeout when effect re-runs
    if (restartTimeoutRef.current !== null) {
      clearTimeout(restartTimeoutRef.current);
      restartTimeoutRef.current = null;
    }

    if (wasListening && !isListening) {
      const { settings } = useMaestroStore.getState();
      if (settings.isSuggestionMode && settings.stt.enabled) {
        restartTimeoutRef.current = window.setTimeout(() => {
          restartTimeoutRef.current = null;
          // Re-check conditions before starting - avoid re-entrancy
          const state = useMaestroStore.getState();
          if (!isListening && state.settings.stt.enabled && state.settings.isSuggestionMode) {
            startListening(state.settings.stt.language);
          }
        }, 100);
      }
    }

    // Cleanup on unmount or re-run
    return () => {
      if (restartTimeoutRef.current !== null) {
        clearTimeout(restartTimeoutRef.current);
        restartTimeoutRef.current = null;
      }
    };
  }, [isListening, startListening]);
};

export default useSuggestionModeAutoRestart;
