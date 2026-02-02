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
import { ALL_LANGUAGES, STT_LANGUAGES, DEFAULT_NATIVE_LANG_CODE, DEFAULT_TARGET_LANG_CODE, type LanguageDefinition } from '../../core/config/languages';
import { generateAllLanguagePairs, getPrimaryCode, parseLanguagePairId } from '../../shared/utils/languageUtils';
import { IMAGE_GEN_CAMERA_ID } from '../../core/config/app';
import type { MaestroStore } from '../maestroStore';

// Generate language pairs once
const allGeneratedLanguagePairs = generateAllLanguagePairs();
const DEFAULT_LANGUAGE_PAIR_ID = `${DEFAULT_TARGET_LANG_CODE}-${DEFAULT_NATIVE_LANG_CODE}`;
const MAX_VISIBLE_MESSAGES_DEFAULT = 50;

// Initial settings values
export const initialSettings: AppSettings = {
  selectedLanguagePairId: null,
  selectedCameraId: IMAGE_GEN_CAMERA_ID,
  sendWithSnapshotEnabled: true,
  tts: {
    provider: 'gemini-live',
    speakNative: true,
    voiceName: 'Kore',
  },
  stt: {
    enabled: false,
    language: getPrimaryCode(ALL_LANGUAGES.find(l => l.langCode === DEFAULT_NATIVE_LANG_CODE)?.code || STT_LANGUAGES[0].code),
    provider: 'gemini',
  },
  smartReengagement: {
    thresholdSeconds: 45,
    useVisualContext: true,
  },
  enableGoogleSearch: true,
  imageGenerationModeEnabled: true,
  imageFocusedModeEnabled: true,
  isSuggestionMode: false,
  historyBookmarkMessageId: null,
  maxVisibleMessages: undefined,
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

// ============================================================
// DERIVED SELECTORS
// ============================================================

export const selectSettings = (state: Pick<SettingsSlice, 'settings'>) => state.settings;

export const selectSelectedLanguagePair = (
  state: Pick<SettingsSlice, 'languagePairs' | 'settings'>
): LanguagePair | undefined =>
  state.languagePairs.find(p => p.id === state.settings.selectedLanguagePairId);

export const selectCurrentSystemPromptText = (
  state: Pick<SettingsSlice, 'languagePairs' | 'settings'>
): string => selectSelectedLanguagePair(state)?.baseSystemPrompt || '';

export const selectCurrentReplySuggestionsPromptText = (
  state: Pick<SettingsSlice, 'languagePairs' | 'settings'>
): string => selectSelectedLanguagePair(state)?.baseReplySuggestionsPrompt || '';

const resolveLanguageCodes = (state: Pick<SettingsSlice, 'settings'>) => {
  const pairId = state.settings.selectedLanguagePairId;
  if (pairId && typeof pairId === 'string') {
    const parsed = parseLanguagePairId(pairId);
    if (parsed) {
      return parsed;
    }
  }
  return { targetCode: DEFAULT_TARGET_LANG_CODE, nativeCode: DEFAULT_NATIVE_LANG_CODE };
};

// Return type is non-optional since we always fall back to ALL_LANGUAGES[0]
const findLanguageDef = (langCode: string, fallbackCode: string): LanguageDefinition =>
  ALL_LANGUAGES.find(lang => lang.langCode === langCode)
  || ALL_LANGUAGES.find(lang => lang.langCode === fallbackCode)
  || ALL_LANGUAGES[0];

export const selectTargetLanguageDef = (
  state: Pick<SettingsSlice, 'settings'>
): LanguageDefinition => {
  const { targetCode } = resolveLanguageCodes(state);
  return findLanguageDef(targetCode, DEFAULT_TARGET_LANG_CODE);
};

export const selectNativeLanguageDef = (
  state: Pick<SettingsSlice, 'settings'>
): LanguageDefinition => {
  const { nativeCode } = resolveLanguageCodes(state);
  return findLanguageDef(nativeCode, DEFAULT_NATIVE_LANG_CODE);
};

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
      let effective = fromDb || initialSettings;

      if (!effective.tts || effective.tts.provider !== 'gemini-live') {
        effective = { ...effective, tts: { ...effective.tts, provider: 'gemini-live' } };
      }

      // Ensure voiceName is set (migration for existing users without this field)
      if (!effective.tts?.voiceName) {
        effective = { ...effective, tts: { ...effective.tts, voiceName: 'Kore' } };
      }

      if (!effective.stt || effective.stt.provider !== 'gemini') {
        effective = { ...effective, stt: { ...effective.stt, provider: 'gemini' } };
      }
      
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
