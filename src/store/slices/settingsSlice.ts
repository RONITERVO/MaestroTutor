// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * Settings Slice - manages app settings, language pairs, and user preferences
 * 
 * Responsibilities:
 * - AppSettings state (language, camera, TTS/STT config, etc.)
 * - Language pair selection and derived data
 * - Settings persistence via getAppSettingsDB/setAppSettingsDB
 */

import type { StateCreator } from 'zustand';
import type { AppSettings, LanguagePair } from '../../core/types';
import { getAppSettingsDB, setAppSettingsDB } from '../../features/session';
import { LOCAL_STORAGE_SETTINGS_KEY } from '../../core/config/app';
import { ALL_LANGUAGES, STT_LANGUAGES, DEFAULT_NATIVE_LANG_CODE, DEFAULT_TARGET_LANG_CODE } from '../../core/config/languages';
import { generateAllLanguagePairs, getPrimaryCode } from '../../shared/utils/languageUtils';
import type { MaestroStore } from '../maestroStore';

// Generate language pairs once
const allGeneratedLanguagePairs = generateAllLanguagePairs();
const DEFAULT_LANGUAGE_PAIR_ID = `${DEFAULT_TARGET_LANG_CODE}-${DEFAULT_NATIVE_LANG_CODE}`;
const MAX_VISIBLE_MESSAGES_DEFAULT = 50;

// Initial settings values
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

// Load and merge from localStorage
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
        });
        if (typeof mergedSettings.maxVisibleMessages === 'undefined' || mergedSettings.maxVisibleMessages === null) {
          mergedSettings.maxVisibleMessages = MAX_VISIBLE_MESSAGES_DEFAULT;
        }
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

export interface SettingsSlice {
  // State
  settings: AppSettings;
  languagePairs: LanguagePair[];
  isSettingsLoaded: boolean;
  needsLanguageSelection: boolean;
  
  // Derived (computed via selector functions)
  selectedLanguagePair: LanguagePair | undefined;
  currentSystemPromptText: string;
  currentReplySuggestionsPromptText: string;
  
  // Selector functions for derived state
  getSelectedLanguagePair: () => LanguagePair | undefined;
  getCurrentSystemPromptText: () => string;
  getCurrentReplySuggestionsPromptText: () => string;
  
  // Actions
  initSettings: () => Promise<void>;
  updateSetting: <K extends keyof AppSettings>(key: K, value: AppSettings[K]) => void;
  setSettings: (settings: AppSettings | ((prev: AppSettings) => AppSettings)) => void;
  setNeedsLanguageSelection: (value: boolean) => void;
  setIsSettingsLoaded: (value: boolean) => void;
}

export const createSettingsSlice: StateCreator<
  MaestroStore,
  [['zustand/subscribeWithSelector', never], ['zustand/devtools', never]],
  [],
  SettingsSlice
> = (set, get) => ({
  // Initial state
  settings: initialSettings,
  languagePairs: allGeneratedLanguagePairs,
  isSettingsLoaded: false,
  needsLanguageSelection: false,
  
  // Computed properties - these are initialized as undefined/empty
  // and will be computed via selectors when accessed
  selectedLanguagePair: undefined,
  currentSystemPromptText: '',
  currentReplySuggestionsPromptText: '',
  
  // Selector functions for derived state (call these to compute derived values)
  getSelectedLanguagePair: () => {
    const state = get();
    return state.languagePairs.find(p => p.id === state.settings.selectedLanguagePairId);
  },
  
  getCurrentSystemPromptText: () => {
    const pair = get().getSelectedLanguagePair();
    return pair?.baseSystemPrompt || '';
  },
  
  getCurrentReplySuggestionsPromptText: () => {
    const pair = get().getSelectedLanguagePair();
    return pair?.baseReplySuggestionsPrompt || '';
  },
  
  // Actions
  initSettings: async () => {
    try {
      const fromDb = await getAppSettingsDB();
      let effective = fromDb || loadFromLocalStorage(LOCAL_STORAGE_SETTINGS_KEY, initialSettings);
      
      if (!effective.selectedLanguagePairId || !allGeneratedLanguagePairs.some(p => p.id === effective.selectedLanguagePairId)) {
        effective = { ...effective, selectedLanguagePairId: null };
        set({ needsLanguageSelection: true });
      }
      
      set({ 
        settings: effective, 
        isSettingsLoaded: true 
      });
      
      // Persist to DB
      try { await setAppSettingsDB(effective); } catch {}
    } catch (e) {
      console.error('Failed to load settings:', e);
      set({ settings: initialSettings, isSettingsLoaded: true });
    }
  },
  
  updateSetting: <K extends keyof AppSettings>(key: K, value: AppSettings[K]) => {
    set(state => {
      const next = { ...state.settings, [key]: value };
      setAppSettingsDB(next).catch(() => {});
      return { settings: next };
    });
  },
  
  setSettings: (settingsOrUpdater) => {
    set(state => {
      const next = typeof settingsOrUpdater === 'function' 
        ? settingsOrUpdater(state.settings) 
        : settingsOrUpdater;
      setAppSettingsDB(next).catch(() => {});
      return { settings: next };
    });
  },
  
  setNeedsLanguageSelection: (value: boolean) => {
    set({ needsLanguageSelection: value });
  },
  
  setIsSettingsLoaded: (value: boolean) => {
    set({ isSettingsLoaded: value });
  },
});

// Export constants
export { MAX_VISIBLE_MESSAGES_DEFAULT, allGeneratedLanguagePairs, DEFAULT_LANGUAGE_PAIR_ID };
