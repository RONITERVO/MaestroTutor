// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * useAppSettings - Hook bridge to Zustand store for app settings
 * 
 * This hook provides backward-compatible access to settings state.
 * All state is now managed by the settingsSlice in the Zustand store.
 */

import { useCallback, useEffect, useRef } from 'react';
import { useShallow } from 'zustand/shallow';
import { useMaestroStore, getStoreState } from '../../store';
import type { AppSettings, LanguagePair } from '../../core/types';
import { ALL_LANGUAGES, DEFAULT_NATIVE_LANG_CODE } from '../../core/config/languages';

// Re-export constants from the slice for backward compatibility
export {
  initialSettings,
  MAX_VISIBLE_MESSAGES_DEFAULT,
  allGeneratedLanguagePairs,
  DEFAULT_LANGUAGE_PAIR_ID
} from '../../store/slices/settingsSlice';

export interface UseAppSettingsReturn {
  settings: AppSettings;
  settingsRef: React.MutableRefObject<AppSettings>;
  handleSettingsChange: <K extends keyof AppSettings>(key: K, value: AppSettings[K]) => void;
  setSettings: (settings: AppSettings | ((prev: AppSettings) => AppSettings)) => void;
  languagePairs: LanguagePair[];
  selectedLanguagePair: LanguagePair | undefined;
  selectedLanguagePairRef: React.MutableRefObject<LanguagePair | undefined>;
  isSettingsLoaded: boolean;
  /** Whether language selection UI should be open */
  needsLanguageSelection: boolean;
  setNeedsLanguageSelection: (value: boolean) => void;
}

export interface UseAppSettingsConfig {
  onLanguageSelectionRequired?: (defaultNativeLangCode: string) => void;
  onSettingsLoaded?: (settings: AppSettings) => void;
}

/**
 * Hook for managing application settings with persistence to IndexedDB.
 * Now backed by Zustand store - this is a thin wrapper for backward compatibility.
 */
export const useAppSettings = (config?: UseAppSettingsConfig): UseAppSettingsReturn => {
  // Select state from store with shallow comparison for objects
  // Compute selectedLanguagePair in the selector to avoid Zustand getter issues
  const {
    settings,
    languagePairs,
    isSettingsLoaded,
    needsLanguageSelection,
    selectedLanguagePair,
  } = useMaestroStore(useShallow(state => ({
    settings: state.settings,
    languagePairs: state.languagePairs,
    isSettingsLoaded: state.isSettingsLoaded,
    needsLanguageSelection: state.needsLanguageSelection,
    // Compute the derived value directly in the selector
    selectedLanguagePair: state.languagePairs.find(p => p.id === state.settings.selectedLanguagePairId),
  })));

  // Get actions from store (stable references, no need for shallow)
  const initSettings = useMaestroStore(state => state.initSettings);
  const updateSetting = useMaestroStore(state => state.updateSetting);
  const setSettings = useMaestroStore(state => state.setSettings);
  const setNeedsLanguageSelection = useMaestroStore(state => state.setNeedsLanguageSelection);

  // Refs for imperative access (synced with store state)
  const settingsRef = useRef<AppSettings>(settings);
  const selectedLanguagePairRef = useRef<LanguagePair | undefined>(selectedLanguagePair);

  // Keep refs in sync with store state
  useEffect(() => { settingsRef.current = settings; }, [settings]);
  useEffect(() => { selectedLanguagePairRef.current = selectedLanguagePair; }, [selectedLanguagePair]);

  // Track if callbacks have been fired
  const callbacksFiredRef = useRef(false);

  // Initialize settings on mount (only once)
  useEffect(() => {
    const currentState = getStoreState();
    if (!currentState.isSettingsLoaded) {
      initSettings();
    }
  }, [initSettings]);

  // Fire callbacks when settings are loaded
  useEffect(() => {
    if (isSettingsLoaded && !callbacksFiredRef.current) {
      callbacksFiredRef.current = true;

      // Call onSettingsLoaded callback
      config?.onSettingsLoaded?.(settings);

      // Call onLanguageSelectionRequired if needed
      if (needsLanguageSelection) {
        const browserLangCode = (typeof navigator !== 'undefined' && navigator.language || 'en').substring(0, 2);
        const defaultNative = ALL_LANGUAGES.find((l: any) => l.langCode === browserLangCode)
          || ALL_LANGUAGES.find((l: any) => l.langCode === DEFAULT_NATIVE_LANG_CODE);
        config?.onLanguageSelectionRequired?.(defaultNative?.langCode || 'en');
      }
    }
  }, [isSettingsLoaded, needsLanguageSelection, settings, config]);

  // Wrapper for handleSettingsChange to match old interface
  const handleSettingsChange = useCallback(<K extends keyof AppSettings>(key: K, value: AppSettings[K]) => {
    updateSetting(key, value);
  }, [updateSetting]);

  return {
    settings,
    settingsRef,
    handleSettingsChange,
    setSettings,
    languagePairs,
    selectedLanguagePair,
    selectedLanguagePairRef,
    isSettingsLoaded,
    needsLanguageSelection,
    setNeedsLanguageSelection,
  };
};

export default useAppSettings;