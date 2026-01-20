// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * useUiBusyState - Hook bridge to Zustand store for UI busy state
 * 
 * This hook provides backward-compatible access to UI busy state.
 * All state is now managed by the uiSlice in the Zustand store.
 */

import { useCallback, useRef, useEffect } from 'react';
import { useShallow } from 'zustand/shallow';
import { useMaestroStore } from '../../store';

export interface UseUiBusyStateReturn {
  /** Array of unique task tags currently active */
  uiBusyTaskTags: string[];
  /** Count of non-reengagement UI tasks */
  externalUiTaskCount: number;
  /** Add a busy token and return it for later removal */
  addUiBusyToken: (token: string) => string;
  /** Remove a specific busy token */
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
  // Select state from store
  const { uiBusyTaskTags: uiBusyTaskTagsSet, externalUiTaskCount } = useMaestroStore(
    useShallow(state => ({
      uiBusyTaskTags: state.uiBusyTaskTags,
      externalUiTaskCount: state.externalUiTaskCount,
    }))
  );

  // Get actions from store (stable references)
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
  const uiBusyTaskTags = Array.from(uiBusyTaskTagsSet).map(tok => {
    const tag = (tok as string).split(':')[0];
    return tag;
  }).filter((tag, idx, arr) => arr.indexOf(tag) === idx);

  // Wrapper for addUiBusyToken that uses the tag directly as token prefix
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
    state.uiBusyTaskTags.forEach(token => {
      storeRemoveUiBusyToken(token);
    });
  }, [storeRemoveUiBusyToken]);

  const handleToggleHold = useCallback(() => {
    if (holdUiTokenRef.current) {
      removeUiBusyToken(holdUiTokenRef.current);
      holdUiTokenRef.current = null;
    } else {
      const token = addUiBusyToken('user-hold');
      holdUiTokenRef.current = token;
    }
  }, [addUiBusyToken, removeUiBusyToken]);

  const isReengagementToken = useCallback((token: string | null | undefined): boolean => {
    if (!token || typeof token !== 'string') return false;
    return token.startsWith('reengage-');
  }, []);

  return {
    uiBusyTaskTags,
    externalUiTaskCount,
    addUiBusyToken,
    removeUiBusyToken,
    clearUiBusyTokens,
    handleToggleHold,
    isReengagementToken,
    externalUiTaskCountRef,
  };
};

export default useUiBusyState;