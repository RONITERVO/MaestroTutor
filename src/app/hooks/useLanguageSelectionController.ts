// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * useLanguageSelectionController - Handles language selection UI flow.
 */

import { useCallback, useEffect } from 'react';
import type { MutableRefObject } from 'react';
import type { AppSettings, ChatMessage, LanguagePair } from '../../core/types';
import { ALL_LANGUAGES, DEFAULT_NATIVE_LANG_CODE } from '../../core/config/languages';
import { safeSaveChatHistoryDB } from '../../features/chat';

interface UseLanguageSelectionControllerConfig {
  isSettingsLoaded: boolean;
  settings: AppSettings;
  settingsRef: MutableRefObject<AppSettings>;
  isSendingRef: MutableRefObject<boolean>;
  languagePairs: LanguagePair[];
  handleSettingsChange: <K extends keyof AppSettings>(key: K, value: AppSettings[K]) => void;
  messagesRef: MutableRefObject<ChatMessage[]>;
  isLanguageSelectionOpen: boolean;
  tempNativeLangCode: string | null;
  tempTargetLangCode: string | null;
  languageSelectorLastInteraction: number;
  setIsLanguageSelectionOpen: (open: boolean) => void;
  setTempNativeLangCode: (code: string | null) => void;
  setTempTargetLangCode: (code: string | null) => void;
}

export const useLanguageSelectionController = ({
  isSettingsLoaded,
  settings,
  settingsRef,
  isSendingRef,
  languagePairs,
  handleSettingsChange,
  messagesRef,
  isLanguageSelectionOpen,
  tempNativeLangCode,
  tempTargetLangCode,
  languageSelectorLastInteraction,
  setIsLanguageSelectionOpen,
  setTempNativeLangCode,
  setTempTargetLangCode,
}: UseLanguageSelectionControllerConfig) => {
  const handleShowLanguageSelector = useCallback(() => {
    if (isSendingRef.current) return;
    setIsLanguageSelectionOpen(true);
    const currentPairId = settingsRef.current.selectedLanguagePairId;
    if (currentPairId) {
      const [target, native] = currentPairId.split('-');
      setTempNativeLangCode(native);
      setTempTargetLangCode(target);
    }
  }, [isSendingRef, settingsRef, setIsLanguageSelectionOpen, setTempNativeLangCode, setTempTargetLangCode]);

  const handleTempNativeSelect = useCallback((code: string | null) => {
    setTempNativeLangCode(code);
    if (code && code === tempTargetLangCode) {
      setTempTargetLangCode(null);
    }
  }, [setTempNativeLangCode, tempTargetLangCode, setTempTargetLangCode]);

  const handleTempTargetSelect = useCallback((code: string | null) => {
    setTempTargetLangCode(code);
  }, [setTempTargetLangCode]);

  const handleConfirmLanguageSelection = useCallback(async () => {
    if (!tempNativeLangCode || !tempTargetLangCode) return;
    const newPairId = `${tempTargetLangCode}-${tempNativeLangCode}`;
    const oldPairId = settingsRef.current.selectedLanguagePairId;
    const isDifferent = newPairId !== oldPairId;

    // Save current chat history before switching if changing to a different pair
    if (isDifferent && oldPairId) {
      try {
        await safeSaveChatHistoryDB(oldPairId, messagesRef.current);
      } catch (e) {
        console.error(`[useLanguageSelectionController] Failed to save chat history for pairId=${oldPairId}:`, e);
        // Continue with language switch even if save fails - user experience priority
      }
    }

    if (languagePairs.some(p => p.id === newPairId)) {
      handleSettingsChange('selectedLanguagePairId', newPairId);
    }
    setIsLanguageSelectionOpen(false);
  }, [tempNativeLangCode, tempTargetLangCode, languagePairs, handleSettingsChange, settingsRef, messagesRef, setIsLanguageSelectionOpen, safeSaveChatHistoryDB]);

  useEffect(() => {
    if (isSettingsLoaded && !settings.selectedLanguagePairId) {
      const browserLangCode = (typeof navigator !== 'undefined' && navigator.language || 'en').substring(0, 2);
      const defaultNative = ALL_LANGUAGES.find(l => l.langCode === browserLangCode) || 
                           ALL_LANGUAGES.find(l => l.langCode === DEFAULT_NATIVE_LANG_CODE)!;
      setTempNativeLangCode(defaultNative.langCode);
      setTempTargetLangCode(null);
      setIsLanguageSelectionOpen(true);
    }
  }, [isSettingsLoaded, settings.selectedLanguagePairId, setTempNativeLangCode, setTempTargetLangCode, setIsLanguageSelectionOpen]);

  useEffect(() => {
    let timeout: number;
    if (isLanguageSelectionOpen && tempNativeLangCode && tempTargetLangCode) {
      timeout = window.setTimeout(() => {
        const idleTime = Date.now() - languageSelectorLastInteraction;
        if (idleTime >= 4500) {
          handleConfirmLanguageSelection();
        }
      }, 5000);
    }
    return () => clearTimeout(timeout);
  }, [
    isLanguageSelectionOpen,
    tempNativeLangCode,
    tempTargetLangCode,
    handleConfirmLanguageSelection,
    languageSelectorLastInteraction,
  ]);

  return {
    handleShowLanguageSelector,
    handleTempNativeSelect,
    handleTempTargetSelect,
    handleConfirmLanguageSelection,
  };
};

export default useLanguageSelectionController;
