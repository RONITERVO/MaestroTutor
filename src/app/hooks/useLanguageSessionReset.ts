// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useEffect, useRef } from 'react';
import type { LanguageChangePorts } from '../coordinators/contracts';
import { beginLanguageChangeReset } from '../coordinators/languageChange';
import { readSpeechRoutingState } from '../speechRoutingState';

type LanguageSessionResetConfig = Omit<LanguageChangePorts, 'readState' | 'delay'> & {
  selectedLanguagePairId: string | null;
};

/** React owns pair-transition detection and effect cancellation; the coordinator
 * owns stop/restart ordering. Initial selection from null is a real transition. */
export const useLanguageSessionReset = ({
  selectedLanguagePairId, settingsRef, cancelReengagement, clearTranscript,
  handleStopLiveSession, resetSilentObserver, setSttError, startListening,
  stopListening, stopSpeaking,
}: LanguageSessionResetConfig) => {
  const previousLanguagePairIdRef = useRef<string | null>(selectedLanguagePairId);
  useEffect(() => {
    if (selectedLanguagePairId === previousLanguagePairIdRef.current) return;
    previousLanguagePairIdRef.current = selectedLanguagePairId;
    return beginLanguageChangeReset({
      cancelReengagement, settingsRef, stopSpeaking, stopListening, clearTranscript,
      setSttError, resetSilentObserver, handleStopLiveSession, startListening,
      readState: readSpeechRoutingState,
      delay: (callback, milliseconds) => window.setTimeout(callback, milliseconds),
    });
  }, [
    cancelReengagement, clearTranscript, handleStopLiveSession, resetSilentObserver,
    selectedLanguagePairId, settingsRef, setSttError, startListening, stopListening, stopSpeaking,
  ]);
};
