// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { getPrimaryCode } from '../../shared/utils/languageUtils';
import { STT_RESTART_DELAY_MS, type SpeechModePorts } from './contracts';

/** User-driven language/microphone actions shared by the app's UI handoffs. */
export const createSpeechModeActions = ({
  pendingEnableRef,
  isListening, stopListening, startListening, clearTranscript, settingsRef,
  selectedLanguagePairRef, setSettings, stopSilentObserverRef, setSttError, delay, warn,
}: SpeechModePorts) => {
  const handleToggleSuggestionMode = (forceState?: boolean) => {
    const newIsSuggestionMode = typeof forceState === 'boolean' ? forceState : !settingsRef.current.isSuggestionMode;
    if (newIsSuggestionMode === settingsRef.current.isSuggestionMode) return;

    const currentSttSettings = settingsRef.current.stt;
    const sttShouldBeActive = currentSttSettings.enabled;
    let newSttLang = currentSttSettings.language;

    if (selectedLanguagePairRef.current) {
      newSttLang = newIsSuggestionMode
        ? getPrimaryCode(selectedLanguagePairRef.current.nativeLanguageCode)
        : getPrimaryCode(selectedLanguagePairRef.current.targetLanguageCode);
    }

    const langDidChange = newSttLang !== currentSttSettings.language;

    setSettings(prev => ({
      ...prev,
      isSuggestionMode: newIsSuggestionMode,
      stt: {
        ...prev.stt,
        language: newSttLang
      }
    }));

    if (langDidChange && sttShouldBeActive && isListening) {
      stopListening();
      delay(() => {
        if (settingsRef.current.stt.enabled) {
          clearTranscript();
          startListening(settingsRef.current.stt.language);
        }
      }, STT_RESTART_DELAY_MS);
    } else if (langDidChange) {
      clearTranscript();
    }

  };
  const sttMasterToggle = async () => {
    // A second click cancels an enable that is still waiting for the observer.
    // The owner survives coordinator recreation and fences older continuations.
    if (pendingEnableRef.current !== null) {
      pendingEnableRef.current = null;
      return;
    }
    // If enabled, turn it OFF (regardless of error state). This allows clearing stuck states.
    if (settingsRef.current.stt.enabled) {
      const nextSettings = { ...settingsRef.current, stt: { ...settingsRef.current.stt, enabled: false } };
      setSettings(nextSettings);

      // Also clear any STT Error when manually turning off
      setSttError(null);

      stopListening();
      return;
    }

    // If disabled, turn it ON.
    const enableOwner = Symbol('stt-enable');
    pendingEnableRef.current = enableOwner;
    try {
      await Promise.resolve(stopSilentObserverRef.current?.());
    } catch (error) {
      warn('Failed to stop silent observer before STT start', error);
    }
    if (pendingEnableRef.current !== enableOwner) return;
    pendingEnableRef.current = null;
    const currentSttSettings = settingsRef.current.stt;
    const nextSettings = { ...settingsRef.current, stt: { ...currentSttSettings, enabled: true } };
    setSettings(nextSettings);

    // Clear old error messages before starting fresh
    setSttError(null);
    clearTranscript();
    startListening(currentSttSettings.language);

  };
  return { handleToggleSuggestionMode, sttMasterToggle };
};
