// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * useSuggestionModeAutoRestart - restarts STT in suggestion mode.
 */

import { useEffect, useRef } from 'react';
import type { MutableRefObject } from 'react';

interface UseSuggestionModeAutoRestartConfig {
  isListening: boolean;
  settingsRef: MutableRefObject<{ stt: { enabled: boolean; language: string }; isSuggestionMode: boolean }>;
  startListening: (lang: string) => void;
}

export const useSuggestionModeAutoRestart = ({
  isListening,
  settingsRef,
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
      if (settingsRef.current.isSuggestionMode && settingsRef.current.stt.enabled) {
        restartTimeoutRef.current = window.setTimeout(() => {
          restartTimeoutRef.current = null;
          // Re-check conditions before starting - avoid re-entrancy
          if (!isListening && settingsRef.current.stt.enabled && settingsRef.current.isSuggestionMode) {
            startListening(settingsRef.current.stt.language);
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
  }, [isListening, settingsRef, startListening]);
};

export default useSuggestionModeAutoRestart;
