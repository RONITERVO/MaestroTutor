// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * UI Slice - manages cross-component UI state
 * 
 * Responsibilities:
 * - Language selector state (open, temp selections)
 * - Shell state (topbar open)
 * - Unified Activity Token State (replaces scattered boolean flags)
 * - Maestro activity stage
 * - Loading assets (animations, avatar)
 * - Transition states
 * 
 * Activity Token System:
 * Tokens use format `{category}:{uniqueId}` where categories are:
 * - `tts` (speaking), `stt` (listening), `gen` (generation), `live` (live session), `ui` (user actions/popups)
 * All busy-state checks derive from token presence, eliminating race conditions.
 */

import type { StateCreator } from 'zustand';
import type { MaestroActivityStage } from '../../core/types';
import {
  TOKEN_CATEGORY,
  TOKEN_SUBTYPE,
  type TokenCategory,
  buildToken,
  getTokenSubtype,
  isReengagementToken,
  UI_TOKEN_DISPLAY,
} from '../../core/config/activityTokens';
import type { MaestroStore } from '../maestroStore';

export interface UiSlice {
  // Language Selector
  isLanguageSelectionOpen: boolean;
  tempNativeLangCode: string | null;
  tempTargetLangCode: string | null;
  languageSelectorLastInteraction: number;
  
  // Shell
  isTopbarOpen: boolean;
  
  // Unified Activity Token State
  // Replaces scattered boolean flags (isSpeaking, isListening, isSending) with a single Set
  // Token format: `{category}:{uniqueId}` - e.g., 'tts:speak-1234567', 'stt:listen-9876543'
  activityTokens: Set<string>;
  
  // Activity
  maestroActivityStage: MaestroActivityStage;
  
  // Assets
  loadingAnimations: string[];
  transitioningImageId: string | null;
  maestroAvatarUri: string | null;
  maestroAvatarMimeType: string | null;
  
  // Actions - Language Selector
  setIsLanguageSelectionOpen: (value: boolean) => void;
  setTempNativeLangCode: (code: string | null) => void;
  setTempTargetLangCode: (code: string | null) => void;
  updateLanguageSelectorInteraction: () => void;
  
  // Actions - Shell
  setIsTopbarOpen: (value: boolean) => void;
  toggleTopbar: () => void;
  
  // Actions - Unified Activity Tokens
  /**
   * Add an activity token. Returns the full token string for later removal.
   * Token format: `{category}:{subtype}` or `{category}:{timestamp}` if no subtype provided
   */
  addActivityToken: (category: TokenCategory, subtype?: string) => string;
  
  /**
   * Remove a specific activity token by its full string
   */
  removeActivityToken: (token: string) => void;
  
  /**
   * Check if any token of a given category exists
   */
  hasTokenCategory: (category: TokenCategory) => boolean;
  
  // Actions - Activity
  setMaestroActivityStage: (stage: MaestroActivityStage) => void;
  
  // Actions - Assets
  setLoadingAnimations: (animations: string[]) => void;
  setTransitioningImageId: (id: string | null) => void;
  setMaestroAvatar: (uri: string | null, mimeType: string | null) => void;
}

// ============================================================
// DERIVED SELECTORS - Computed from activity tokens
// These replace the old boolean flags in speechSlice and chatSlice
// ============================================================

/**
 * Check if any TTS (speaking) activity is happening
 */
export const selectIsSpeaking = (state: { activityTokens: Set<string> }): boolean =>
  [...state.activityTokens].some(t => t.startsWith(`${TOKEN_CATEGORY.TTS}:`));

/**
 * Check if any STT (listening) activity is happening
 */
export const selectIsListening = (state: { activityTokens: Set<string> }): boolean =>
  [...state.activityTokens].some(t => t.startsWith(`${TOKEN_CATEGORY.STT}:`));

/**
 * Check if any generation activity is happening (sending/generating)
 */
export const selectIsSending = (state: { activityTokens: Set<string> }): boolean =>
  [...state.activityTokens].some(t => t.startsWith(`${TOKEN_CATEGORY.GEN}:`));

/**
 * Check if loading suggestions specifically
 */
export const selectIsLoadingSuggestions = (state: { activityTokens: Set<string> }): boolean =>
  state.activityTokens.has(buildToken(TOKEN_CATEGORY.GEN, TOKEN_SUBTYPE.SUGGESTIONS));

/**
 * Check if creating a suggestion specifically
 */
export const selectIsCreatingSuggestion = (state: { activityTokens: Set<string> }): boolean =>
  state.activityTokens.has(buildToken(TOKEN_CATEGORY.GEN, TOKEN_SUBTYPE.CREATE_SUGGESTION));

/**
 * Check if live session is active
 */
export const selectIsLive = (state: { activityTokens: Set<string> }): boolean =>
  [...state.activityTokens].some(t => t.startsWith(`${TOKEN_CATEGORY.LIVE}:`));

/**
 * Check if user has manually paused (hold)
 */
export const selectIsUserHold = (state: { activityTokens: Set<string> }): boolean =>
  state.activityTokens.has(buildToken(TOKEN_CATEGORY.UI, TOKEN_SUBTYPE.HOLD));

/**
 * Check if any activity is happening at all
 */
export const selectIsBusy = (state: { activityTokens: Set<string> }): boolean =>
  state.activityTokens.size > 0;

/**
 * Check if there's any non-reengagement activity (blocks scheduling reengagement)
 */
export const selectNonReengagementBusy = (state: { activityTokens: Set<string> }): boolean =>
  [...state.activityTokens].some(t => !isReengagementToken(t));

/**
 * Get sorted list of active UI tokens for display.
 */
export const selectActiveUiTokens = (state: { activityTokens: Set<string> }): string[] =>
  [...state.activityTokens]
    .filter(token => token.startsWith(`${TOKEN_CATEGORY.UI}:`) && !isReengagementToken(token))
    .sort((a, b) => {
      const subtypeA = getTokenSubtype(a);
      const subtypeB = getTokenSubtype(b);
      const priorityA = UI_TOKEN_DISPLAY[subtypeA]?.priority ?? 100;
      const priorityB = UI_TOKEN_DISPLAY[subtypeB]?.priority ?? 100;
      return priorityA - priorityB;
    });

export const createUiSlice: StateCreator<
  MaestroStore,
  [['zustand/subscribeWithSelector', never], ['zustand/devtools', never]],
  [],
  UiSlice
> = (set, get) => ({
  // Initial Language Selector state
  isLanguageSelectionOpen: false,
  tempNativeLangCode: null,
  tempTargetLangCode: null,
  languageSelectorLastInteraction: 0,
  
  // Initial Shell state
  isTopbarOpen: false,
  
  // Initial Activity Token State - unified busy state management
  activityTokens: new Set<string>(),
  
  // Initial Activity state
  maestroActivityStage: 'idle',
  
  // Initial Assets state
  loadingAnimations: [],
  transitioningImageId: null,
  maestroAvatarUri: null,
  maestroAvatarMimeType: null,
  
  // Language Selector Actions
  setIsLanguageSelectionOpen: (value: boolean) => {
    set({ isLanguageSelectionOpen: value });
  },
  
  setTempNativeLangCode: (code: string | null) => {
    set({ tempNativeLangCode: code, languageSelectorLastInteraction: Date.now() });
  },
  
  setTempTargetLangCode: (code: string | null) => {
    set({ tempTargetLangCode: code, languageSelectorLastInteraction: Date.now() });
  },
  
  updateLanguageSelectorInteraction: () => {
    set({ languageSelectorLastInteraction: Date.now() });
  },
  
  // Shell Actions
  setIsTopbarOpen: (value: boolean) => {
    set({ isTopbarOpen: value });
  },
  
  toggleTopbar: () => {
    set(state => ({ isTopbarOpen: !state.isTopbarOpen }));
  },
  
  // ============================================================
  // UNIFIED ACTIVITY TOKEN ACTIONS
  // ============================================================
  
  /**
   * Add an activity token. Returns the full token string for later removal.
   * Token format: `{category}:{subtype}` or `{category}:{timestamp}` if no subtype provided
   */
  addActivityToken: (category: TokenCategory, subtype?: string): string => {
    const token = subtype ? buildToken(category, subtype) : `${category}:${Date.now()}`;
    set(state => {
      const newTokens = new Set(state.activityTokens);
      newTokens.add(token);
      return { activityTokens: newTokens };
    });
    return token;
  },
  
  /**
   * Remove a specific activity token by its full string
   */
  removeActivityToken: (token: string) => {
    if (!token) return;
    set(state => {
      const newTokens = new Set(state.activityTokens);
      newTokens.delete(token);
      return { activityTokens: newTokens };
    });
  },
  
  /**
   * Check if any token of a given category exists
   */
  hasTokenCategory: (category: TokenCategory): boolean => {
    const tokens = get().activityTokens;
    const prefix = category + ':';
    return [...tokens].some(t => t.startsWith(prefix));
  },
  
  // Activity Actions
  setMaestroActivityStage: (stage: MaestroActivityStage) => {
    set({ maestroActivityStage: stage });
  },
  
  // Assets Actions
  setLoadingAnimations: (animations: string[]) => {
    set({ loadingAnimations: animations });
  },
  
  setTransitioningImageId: (id: string | null) => {
    set({ transitioningImageId: id });
  },
  
  setMaestroAvatar: (uri: string | null, mimeType: string | null) => {
    set({ maestroAvatarUri: uri, maestroAvatarMimeType: mimeType });
  },
});
