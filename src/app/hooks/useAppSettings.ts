// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useState, useCallback, useRef, useEffect } from 'react';
import { AppSettings, LanguagePair } from '../../core/types';
import { getAppSettingsDB, setAppSettingsDB } from '../../features/session/services/settings';
import { LOCAL_STORAGE_SETTINGS_KEY } from '../../core/config/app';
import { ALL_LANGUAGES, STT_LANGUAGES, DEFAULT_NATIVE_LANG_CODE, DEFAULT_TARGET_LANG_CODE } from '../../core/config/languages';
import { generateAllLanguagePairs, getPrimaryCode } from '../../shared/utils/languageUtils';

const MAX_VISIBLE_MESSAGES_DEFAULT = 50;

const allGeneratedLanguagePairs = generateAllLanguagePairs();
const DEFAULT_LANGUAGE_PAIR_ID = `${DEFAULT_TARGET_LANG_CODE}-${DEFAULT_NATIVE_LANG_CODE}`;

export const initialSettings: AppSettings = {
  selectedLanguagePairId: null,
  selectedCameraId: null,
  sendWithSnapshotEnabled: false,
  tts: {
    provider: 'browser',
    speakNative: true,
  },
  stt: {
    enabled: false,
    language: getPrimaryCode(ALL_LANGUAGES.find(l => l.langCode === DEFAULT_NATIVE_LANG_CODE)?.code || STT_LANGUAGES[0].code),
    provider: 'browser',
  },
  smartReengagement: {
    thresholdSeconds: 45,
    useVisualContext: false,
  },
  enableGoogleSearch: true,
  imageGenerationModeEnabled: true,
  imageFocusedModeEnabled: true,
  isSuggestionMode: false,
  historyBookmarkMessageId: null,
  maxVisibleMessages: undefined,
};

/**
 * Load settings from localStorage with proper merging of nested objects
 */
const loadFromLocalStorage = <T,>(key: string, defaultValue: T): T => {
  try {
    const item = window.localStorage.getItem(key);
    if (item) {
      const parsed = JSON.parse(item);
      if (key === LOCAL_STORAGE_SETTINGS_KEY && typeof defaultValue === 'object' && defaultValue !== null) {
        const mergedSettings = { ...defaultValue } as any;
        for (const k in parsed) {
          if (parsed.hasOwnProperty(k)) {
            if (typeof parsed[k] === 'object' && parsed[k] !== null && !Array.isArray(parsed[k]) && k in mergedSettings) {
              mergedSettings[k] = { ...mergedSettings[k], ...parsed[k] };
            } else {
              mergedSettings[k] = parsed[k];
            }
          }
        }
        // Ensure all keys from initialSettings exist
        Object.keys(initialSettings).forEach(initialKey => {
          if (!(initialKey in mergedSettings)) {
            mergedSettings[initialKey] = (initialSettings as any)[initialKey];
          }
          if (typeof mergedSettings.imageGenerationModeEnabled === 'undefined') {
            mergedSettings.imageGenerationModeEnabled = initialSettings.imageGenerationModeEnabled;
          }
          if (typeof mergedSettings.imageFocusedModeEnabled === 'undefined') {
            mergedSettings.imageFocusedModeEnabled = initialSettings.imageFocusedModeEnabled;
          }
          if (typeof mergedSettings.maxVisibleMessages === 'undefined' || mergedSettings.maxVisibleMessages === null) {
            mergedSettings.maxVisibleMessages = MAX_VISIBLE_MESSAGES_DEFAULT;
          }
        });
        // Ensure nested objects have all required keys
        ['tts', 'stt', 'smartReengagement'].forEach(nestedKey => {
          if (mergedSettings[nestedKey] && (initialSettings as any)[nestedKey]) {
            Object.keys((initialSettings as any)[nestedKey]).forEach(subKey => {
              if (!((mergedSettings as any)[nestedKey] as any).hasOwnProperty(subKey)) {
                (mergedSettings as any)[nestedKey][subKey] = (initialSettings as any)[nestedKey][subKey];
              }
            });
          }
        });
        if (!mergedSettings.tts || !mergedSettings.tts.provider) {
          mergedSettings.tts = { ...(mergedSettings.tts || {}), provider: 'browser' };
        }
        if (!mergedSettings.stt || !mergedSettings.stt.provider) {
          mergedSettings.stt = { ...(mergedSettings.stt || {}), provider: 'browser' };
        }
        // Validate language pair
        if (mergedSettings.selectedLanguagePairId && !allGeneratedLanguagePairs.some(p => p.id === mergedSettings.selectedLanguagePairId)) {
          mergedSettings.selectedLanguagePairId = null;
        }
        const activePairForStt = allGeneratedLanguagePairs.find(p => p.id === mergedSettings.selectedLanguagePairId) || allGeneratedLanguagePairs.find(p => p.id === DEFAULT_LANGUAGE_PAIR_ID)!;
        mergedSettings.stt.language = mergedSettings.stt?.language || getPrimaryCode(activePairForStt.targetLanguageCode) || getPrimaryCode(activePairForStt.nativeLanguageCode) || STT_LANGUAGES[0].code;

        if (typeof mergedSettings.enableGoogleSearch === 'undefined') {
          mergedSettings.enableGoogleSearch = initialSettings.enableGoogleSearch;
        }

        return mergedSettings as T;
      }
      return parsed;
    }
    return defaultValue;
  } catch (error) {
    console.warn(`Error reading localStorage key "${key}":`, error);
    return defaultValue;
  }
};

export interface UseAppSettingsReturn {
  settings: AppSettings;
  settingsRef: React.MutableRefObject<AppSettings>;
  handleSettingsChange: <K extends keyof AppSettings>(key: K, value: AppSettings[K]) => void;
  setSettings: React.Dispatch<React.SetStateAction<AppSettings>>;
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
 * Handles loading from DB/localStorage, merging defaults, and saving changes.
 */
export const useAppSettings = (config?: UseAppSettingsConfig): UseAppSettingsReturn => {
  const settingsRef = useRef<AppSettings>(initialSettings);
  const selectedLanguagePairRef = useRef<LanguagePair | undefined>(undefined);
  
  const [settings, setSettings] = useState<AppSettings>(initialSettings);
  const [languagePairs] = useState<LanguagePair[]>(allGeneratedLanguagePairs);
  const [isSettingsLoaded, setIsSettingsLoaded] = useState(false);
  const [needsLanguageSelection, setNeedsLanguageSelection] = useState(false);

  const selectedLanguagePair = languagePairs.find(p => p.id === settings.selectedLanguagePairId);

  // Sync refs with state
  useEffect(() => { settingsRef.current = settings; }, [settings]);
  useEffect(() => { selectedLanguagePairRef.current = selectedLanguagePair; }, [selectedLanguagePair]);

  // Load settings from DB on mount
  useEffect(() => {
    (async () => {
      try {
        const fromDb = await getAppSettingsDB();
        let effective = fromDb || loadFromLocalStorage(LOCAL_STORAGE_SETTINGS_KEY, initialSettings);
        
        if (!effective.selectedLanguagePairId || !allGeneratedLanguagePairs.some(p => p.id === effective.selectedLanguagePairId)) {
          effective = { ...effective, selectedLanguagePairId: null };
          const browserLangCode = (typeof navigator !== 'undefined' && navigator.language || 'en').substring(0, 2);
          const defaultNative = ALL_LANGUAGES.find(l => l.langCode === browserLangCode) || ALL_LANGUAGES.find(l => l.langCode === DEFAULT_NATIVE_LANG_CODE)!;
          
          setNeedsLanguageSelection(true);
          config?.onLanguageSelectionRequired?.(defaultNative.langCode);
        }
        
        setSettings(effective);
        settingsRef.current = effective;
        setIsSettingsLoaded(true);
        config?.onSettingsLoaded?.(effective);
        
        try { await setAppSettingsDB(effective); } catch {}
      } catch (e) {
        setSettings(initialSettings);
        setIsSettingsLoaded(true);
      }
    })();
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const handleSettingsChange = useCallback(<K extends keyof AppSettings>(key: K, value: AppSettings[K]) => {
    setSettings(prev => {
      const next = { ...prev, [key]: value };
      settingsRef.current = next;
      setAppSettingsDB(next).catch(() => {});
      return next;
    });
  }, []);

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

export { MAX_VISIBLE_MESSAGES_DEFAULT, allGeneratedLanguagePairs, DEFAULT_LANGUAGE_PAIR_ID };
export default useAppSettings;
