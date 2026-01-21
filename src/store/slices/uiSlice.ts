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
 * - Loading assets (gifs, avatar)
 * - Transition states
 * 
 * Activity Token System:
 * Tokens use format `{category}:{uniqueId}` where categories are:
 * - `tts` (speaking), `stt` (listening), `gen` (generation), `live` (live session), `ui` (user actions/popups)
 * All busy-state checks derive from token presence, eliminating race conditions.
 */

import type { StateCreator } from 'zustand';
import type { MaestroActivityStage } from '../../core/types';
import type { MaestroStore } from '../maestroStore';

/**
 * Activity token categories for the unified busy state system
 */
export type ActivityTokenCategory = 'tts' | 'stt' | 'gen' | 'live' | 'ui';

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
  
  // Computed count: tokens that are NOT reengagement tokens (used to block reengagement)
  // This is kept as a derived value for backwards compatibility
  externalUiTaskCount: number;
  
  // Activity
  maestroActivityStage: MaestroActivityStage;
  
  // Assets
  loadingGifs: string[];
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
  addActivityToken: (category: ActivityTokenCategory, subtype?: string) => string;
  
  /**
   * Remove a specific activity token by its full string
   */
  removeActivityToken: (token: string) => void;
  
  /**
   * Check if any token of a given category exists
   */
  hasTokenCategory: (category: string) => boolean;
  
  // Legacy compatibility - maps to addActivityToken/removeActivityToken internally
  addUiBusyToken: (tag: string) => string;
  removeUiBusyToken: (tag?: string | null) => void;
  setExternalUiTaskCount: (count: number) => void;
  isUiBusy: () => boolean;
  
  // Actions - Activity
  setMaestroActivityStage: (stage: MaestroActivityStage) => void;
  
  // Actions - Assets
  setLoadingGifs: (gifs: string[]) => void;
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
  [...state.activityTokens].some(t => t.startsWith('tts:'));

/**
 * Check if any STT (listening) activity is happening
 */
export const selectIsListening = (state: { activityTokens: Set<string> }): boolean =>
  [...state.activityTokens].some(t => t.startsWith('stt:'));

/**
 * Check if any generation activity is happening (sending/generating)
 */
export const selectIsSending = (state: { activityTokens: Set<string> }): boolean =>
  [...state.activityTokens].some(t => t.startsWith('gen:'));

/**
 * Check if loading suggestions specifically
 */
export const selectIsLoadingSuggestions = (state: { activityTokens: Set<string> }): boolean =>
  state.activityTokens.has('gen:suggestions');

/**
 * Check if creating a suggestion specifically
 */
export const selectIsCreatingSuggestion = (state: { activityTokens: Set<string> }): boolean =>
  state.activityTokens.has('gen:create-suggestion');

/**
 * Check if live session is active
 */
export const selectIsLive = (state: { activityTokens: Set<string> }): boolean =>
  [...state.activityTokens].some(t => t.startsWith('live:'));

/**
 * Check if user has manually paused (hold)
 */
export const selectIsUserHold = (state: { activityTokens: Set<string> }): boolean =>
  state.activityTokens.has('ui:hold') || state.activityTokens.has('user-hold');

/**
 * Check if any activity is happening at all
 */
export const selectIsBusy = (state: { activityTokens: Set<string> }): boolean =>
  state.activityTokens.size > 0;

/**
 * Check if there's any non-reengagement activity (blocks scheduling reengagement)
 */
export const selectNonReengagementBusy = (state: { activityTokens: Set<string> }): boolean =>
  [...state.activityTokens].some(t => !t.startsWith('ui:reengage') && !t.startsWith('reengage-'));

/**
 * Compute external UI task count (tokens that don't start with 'reengage-' or 'ui:reengage')
 */
const computeExternalUiTaskCount = (tokens: Set<string>): number => {
  let count = 0;
  tokens.forEach(t => {
    if (!t.startsWith('reengage-') && !t.startsWith('ui:reengage')) {
      count++;
    }
  });
  return count;
};

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
  externalUiTaskCount: 0,
  
  // Initial Activity state
  maestroActivityStage: 'idle',
  
  // Initial Assets state
  loadingGifs: [],
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
  addActivityToken: (category: ActivityTokenCategory, subtype?: string): string => {
    const token = subtype ? `${category}:${subtype}` : `${category}:${Date.now()}`;
    set(state => {
      const newTokens = new Set(state.activityTokens);
      newTokens.add(token);
      return { 
        activityTokens: newTokens, 
        externalUiTaskCount: computeExternalUiTaskCount(newTokens) 
      };
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
      return { 
        activityTokens: newTokens, 
        externalUiTaskCount: computeExternalUiTaskCount(newTokens) 
      };
    });
  },
  
  /**
   * Check if any token of a given category exists
   */
  hasTokenCategory: (category: string): boolean => {
    const tokens = get().activityTokens;
    const prefix = category + ':';
    return [...tokens].some(t => t.startsWith(prefix));
  },
  
  // ============================================================
  // LEGACY COMPATIBILITY ACTIONS
  // These maintain backwards compatibility with existing code
  // ============================================================
  
  /**
   * Legacy: Add a UI busy token (maps to activityTokens internally)
   * CRITICAL: This must accept the full token and store it as-is
   * The token is generated by the caller (e.g. InputArea.tsx) and must be returned unchanged
   * so that removeUiBusyToken can later find and remove the exact same token
   */
  addUiBusyToken: (token: string): string => {
    set(state => {
      const newTokens = new Set(state.activityTokens);
      newTokens.add(token);
      return { 
        activityTokens: newTokens, 
        externalUiTaskCount: computeExternalUiTaskCount(newTokens) 
      };
    });
    return token;
  },
  
  /**
   * Legacy: Remove a UI busy token
   */
  removeUiBusyToken: (tag?: string | null) => {
    if (!tag) return;
    set(state => {
      const newTokens = new Set(state.activityTokens);
      newTokens.delete(tag);
      return { 
        activityTokens: newTokens, 
        externalUiTaskCount: computeExternalUiTaskCount(newTokens) 
      };
    });
  },
  
  /**
   * Legacy: Set external UI task count directly (rarely used, prefer tokens)
   */
  setExternalUiTaskCount: (count: number) => {
    set({ externalUiTaskCount: count });
  },
  
  /**
   * Legacy: Check if UI is busy
   */
  isUiBusy: (): boolean => {
    const state = get();
    return state.activityTokens.size > 0 || state.externalUiTaskCount > 0;
  },
  
  // Activity Actions
  setMaestroActivityStage: (stage: MaestroActivityStage) => {
    set({ maestroActivityStage: stage });
  },
  
  // Assets Actions
  setLoadingGifs: (gifs: string[]) => {
    set({ loadingGifs: gifs });
  },
  
  setTransitioningImageId: (id: string | null) => {
    set({ transitioningImageId: id });
  },
  
  setMaestroAvatar: (uri: string | null, mimeType: string | null) => {
    set({ maestroAvatarUri: uri, maestroAvatarMimeType: mimeType });
  },
});
