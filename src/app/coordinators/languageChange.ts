// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { STT_RESTART_DELAY_MS, type LanguageChangePorts } from './contracts';

/** Begin a language-context handoff and return its cancellation fence. The
 * scheduled restart rereads state after both Live owners have stopped. */
export const beginLanguageChangeReset = ({
  cancelReengagement, settingsRef, stopSpeaking, stopListening, clearTranscript,
  setSttError, resetSilentObserver, handleStopLiveSession, startListening, readState, delay,
}: LanguageChangePorts): (() => void) => {
  let cancelled = false;

  const resetLiveSystemsForLanguageChange = async () => {
    cancelReengagement();

    const shouldRestartStt = settingsRef.current.stt.enabled;
    stopSpeaking();
    stopListening();
    clearTranscript();
    setSttError(null);

    await Promise.allSettled([
      Promise.resolve(resetSilentObserver()),
      handleStopLiveSession({ scheduleReengagement: false }),
    ]);

    if (!shouldRestartStt || cancelled) return;

    delay(() => {
      if (cancelled) return;
      const state = readState();
      const liveState = state.liveSessionState;
      if (state.settings.stt.enabled && (liveState === 'idle' || liveState === 'error')) {
        startListening(settingsRef.current.stt.language);
      }
    }, STT_RESTART_DELAY_MS);
  };

  void resetLiveSystemsForLanguageChange();

  return () => {
    cancelled = true;
  };
};
