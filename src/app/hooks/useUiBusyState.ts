// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useState, useCallback, useRef, useEffect } from 'react';

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
 * Tracks active tasks that should block or influence UI behavior.
 */
export const useUiBusyState = (): UseUiBusyStateReturn => {
  const uiBusyTokensRef = useRef<Set<string>>(new Set());
  const holdUiTokenRef = useRef<string | null>(null);
  const externalUiTaskCountRef = useRef<number>(0);

  const [uiBusyTaskTags, setUiBusyTaskTags] = useState<string[]>([]);
  const [externalUiTaskCount, setExternalUiTaskCount] = useState<number>(0);

  const recomputeUiBusyState = useCallback(() => {
    const tags: string[] = [];
    let nonReengagementCount = 0;
    uiBusyTokensRef.current.forEach(tok => {
      if (typeof tok !== 'string') return;
      const tag = tok.split(':')[0];
      if (tag) tags.push(tag);
      if (!tok.startsWith('reengage-')) nonReengagementCount++;
    });
    const seen = new Set<string>();
    const uniqTags = tags.filter(t => (seen.has(t) ? false : (seen.add(t), true)));
    setUiBusyTaskTags(uniqTags);
    setExternalUiTaskCount(nonReengagementCount);
  }, []);

  useEffect(() => {
    externalUiTaskCountRef.current = externalUiTaskCount;
  }, [externalUiTaskCount]);

  const addUiBusyToken = useCallback((token: string): string => {
    uiBusyTokensRef.current.add(token);
    recomputeUiBusyState();
    return token;
  }, [recomputeUiBusyState]);

  const removeUiBusyToken = useCallback((token?: string | null) => {
    if (!token) return;
    uiBusyTokensRef.current.delete(token);
    recomputeUiBusyState();
  }, [recomputeUiBusyState]);

  const clearUiBusyTokens = useCallback(() => {
    uiBusyTokensRef.current.clear();
    recomputeUiBusyState();
  }, [recomputeUiBusyState]);

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
