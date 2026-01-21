// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * useUiBusyState - Hook bridge to Zustand store for UI busy state
 * 
 * This hook provides access to UI busy state via the unified activity token system.
 * All state is managed by the uiSlice in the Zustand store.
 */

import { useCallback, useRef, useEffect } from 'react';
import { useShallow } from 'zustand/shallow';
import { useMaestroStore } from '../../store';
import type { ActivityTokenCategory } from '../../store/slices/uiSlice';

export interface UseUiBusyStateReturn {
  /** The raw Set of activity tokens (unified state) */
  activityTokens: Set<string>;
  /** Array of unique task tags currently active (legacy compatibility) */
  uiBusyTaskTags: string[];
  /** Count of non-reengagement UI tasks */
  externalUiTaskCount: number;
  /** Add an activity token with category and optional subtype. Returns the token for removal. */
  addActivityToken: (category: ActivityTokenCategory, subtype?: string) => string;
  /** Remove a specific activity token */
  removeActivityToken: (token: string) => void;
  /** Legacy: Add a busy token and return it for later removal */
  addUiBusyToken: (token: string) => string;
  /** Legacy: Remove a specific busy token */
  removeUiBusyToken: (token?: string | null) => void;
  /** Clear all busy tokens */
  clearUiBusyTokens: () => void;
  /** Toggle hold state (user-initiated pause) */
  handleToggleHold: () => void;
  /** Check if a token is a reengagement token */
  isReengagementToken: (token: string | null | undefined) => boolean;
  /** Ref to access external task count synchronously */
  externalUiTaskCountRef: React.MutableRefObject<number>;
}

/**
 * Hook for managing UI busy state and task tracking.
 * Now backed by Zustand store - this is a thin wrapper for backward compatibility.
 */
export const useUiBusyState = (): UseUiBusyStateReturn => {
  // Select state from store - use activityTokens (the new unified state)
  const { activityTokens, externalUiTaskCount } = useMaestroStore(
    useShallow(state => ({
      activityTokens: state.activityTokens,
      externalUiTaskCount: state.externalUiTaskCount,
    }))
  );

  // Get actions from store (stable references)
  const storeAddActivityToken = useMaestroStore(state => state.addActivityToken);
  const storeRemoveActivityToken = useMaestroStore(state => state.removeActivityToken);
  const storeAddUiBusyToken = useMaestroStore(state => state.addUiBusyToken);
  const storeRemoveUiBusyToken = useMaestroStore(state => state.removeUiBusyToken);

  // Local ref for hold token
  const holdUiTokenRef = useRef<string | null>(null);
  const externalUiTaskCountRef = useRef<number>(externalUiTaskCount);

  // Keep ref in sync
  useEffect(() => {
    externalUiTaskCountRef.current = externalUiTaskCount;
  }, [externalUiTaskCount]);

  // Convert Set to array of unique tags for backward compatibility
  // Maps new token format to legacy tag names for UI components
  const uiBusyTaskTags = Array.from(activityTokens).map(tok => {
    const token = tok as string;
    // Map new token format to legacy tag names
    if (token === 'ui:hold' || token.startsWith('user-hold')) {
      return 'user-hold';
    } else if (token.startsWith('live:')) {
      return 'live-session';
    } else if (token.startsWith('tts:')) {
      return 'tts';
    } else if (token.startsWith('stt:')) {
      return 'stt';
    } else if (token.startsWith('gen:')) {
      return 'gen';
    } else if (token.startsWith('ui:reengage') || token.startsWith('reengage-')) {
      return 'reengage';
    } else {
      // For legacy tokens and ui:* tokens, use the subtype or full token
      const parts = token.split(':');
      if (parts[0] === 'ui' && parts[1]) {
        return parts[1]; // e.g., 'ui:save-popup' → 'save-popup'
      }
      return parts[0]; // Legacy format: 'video-play:123' → 'video-play'
    }
  }).filter((tag, idx, arr) => arr.indexOf(tag) === idx);

  // New unified activity token API
  const addActivityToken = useCallback((category: ActivityTokenCategory, subtype?: string): string => {
    return storeAddActivityToken(category, subtype);
  }, [storeAddActivityToken]);

  const removeActivityToken = useCallback((token: string) => {
    storeRemoveActivityToken(token);
  }, [storeRemoveActivityToken]);

  // Legacy: Wrapper for addUiBusyToken that uses the tag directly as token prefix
  const addUiBusyToken = useCallback((tag: string): string => {
    // The store generates a unique token with timestamp
    return storeAddUiBusyToken(tag);
  }, [storeAddUiBusyToken]);

  const removeUiBusyToken = useCallback((token?: string | null) => {
    if (!token) return;
    storeRemoveUiBusyToken(token);
  }, [storeRemoveUiBusyToken]);

  const clearUiBusyTokens = useCallback(() => {
    // Clear all tokens by getting current set and removing each
    const state = useMaestroStore.getState();
    state.activityTokens.forEach(token => {
      storeRemoveActivityToken(token);
    });
  }, [storeRemoveActivityToken]);

  const handleToggleHold = useCallback(() => {
    if (holdUiTokenRef.current) {
      removeActivityToken(holdUiTokenRef.current);
      holdUiTokenRef.current = null;
    } else {
      const token = addActivityToken('ui', 'hold');
      holdUiTokenRef.current = token;
    }
  }, [addActivityToken, removeActivityToken]);

  const isReengagementToken = useCallback((token: string | null | undefined): boolean => {
    if (!token || typeof token !== 'string') return false;
    return token.startsWith('reengage-') || token.startsWith('ui:reengage');
  }, []);

  return {
    activityTokens,
    uiBusyTaskTags,
    externalUiTaskCount,
    addActivityToken,
    removeActivityToken,
    addUiBusyToken,
    removeUiBusyToken,
    clearUiBusyTokens,
    handleToggleHold,
    isReengagementToken,
    externalUiTaskCountRef,
  };
};

export default useUiBusyState;