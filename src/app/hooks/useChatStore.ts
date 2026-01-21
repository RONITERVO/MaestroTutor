// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * useChatStore - Hook bridge to Zustand store for chat state
 * 
 * This hook provides backward-compatible access to chat state.
 * All state is now managed by the chatSlice in the Zustand store.
 */

import { useCallback, useRef, useEffect } from 'react';
import { useShallow } from 'zustand/shallow';
import { useMaestroStore, MAX_VISIBLE_MESSAGES_DEFAULT } from '../../store';
import { ChatMessage, TtsAudioCacheEntry, ReplySuggestion } from '../../core/types';
import { safeSaveChatHistoryDB, setChatMetaDB } from '../../features/chat';
import { setAppSettingsDB } from '../../features/session';
import { isRealChatMessage } from '../../shared/utils/common';
import type { TranslationFunction } from './useTranslations';

export interface UseChatStoreConfig {
  t: TranslationFunction;
  /** Current language pair ID - used as prop for proper React deps tracking */
  selectedLanguagePairId: string | null;
  /** settingsRef is now optional - we read from store */
  settingsRef?: React.MutableRefObject<{
    selectedLanguagePairId: string | null;
    historyBookmarkMessageId?: string | null;
    maxVisibleMessages?: number;
  }>;
  /** setSettings is now optional - we use store actions */
  setSettings?: React.Dispatch<React.SetStateAction<any>>;
}

export interface UseChatStoreReturn {
  messages: ChatMessage[];
  messagesRef: React.MutableRefObject<ChatMessage[]>;
  isLoadingHistory: boolean;
  isLoadingHistoryRef: React.MutableRefObject<boolean>;
  addMessage: (message: Omit<ChatMessage, 'id' | 'timestamp'>) => string;
  updateMessage: (messageId: string, updates: Partial<ChatMessage>) => void;
  deleteMessage: (messageId: string) => void;
  setMessages: (messages: ChatMessage[] | ((prev: ChatMessage[]) => ChatMessage[])) => void;
  /** Trim history based on bookmark */
  trimHistoryByBookmark: (arr: ChatMessage[]) => ChatMessage[];
  /** Get history respecting bookmark settings */
  getHistoryRespectingBookmark: (arr: ChatMessage[]) => ChatMessage[];
  /** Upsert TTS cache entry for a message */
  upsertMessageTtsCache: (messageId: string, entry: TtsAudioCacheEntry) => void;
  /** Upsert TTS cache for a suggestion on a message */
  upsertSuggestionTtsCache: (messageId: string, suggestionIndex: number, entry: TtsAudioCacheEntry) => void;
  /** Compute max messages for API calls */
  computeMaxMessagesForArray: (arr: ChatMessage[]) => number | undefined;
  /** Get ref for checking last fetched suggestions */
  lastFetchedSuggestionsForRef: React.MutableRefObject<string | null>;
  /** Reply suggestions for the current assistant message */
  replySuggestions: ReplySuggestion[];
  setReplySuggestions: (suggestions: ReplySuggestion[] | ((prev: ReplySuggestion[]) => ReplySuggestion[])) => void;
  replySuggestionsRef: React.MutableRefObject<ReplySuggestion[]>;
  isLoadingSuggestions: boolean;
  setIsLoadingSuggestions: (value: boolean | ((prev: boolean) => boolean)) => void;
  isLoadingSuggestionsRef: React.MutableRefObject<boolean>;
}

/**
 * Hook for managing chat message state with persistence to IndexedDB.
 * Now backed by Zustand store - this is a thin wrapper for backward compatibility.
 */
export const useChatStore = (config: UseChatStoreConfig): UseChatStoreReturn => {
  const { t, selectedLanguagePairId } = config;

  // Select state from store
  const {
    messages,
    isLoadingHistory,
    replySuggestions,
    isLoadingSuggestions,
    lastFetchedSuggestionsFor,
  } = useMaestroStore(useShallow(state => ({
    messages: state.messages,
    isLoadingHistory: state.isLoadingHistory,
    replySuggestions: state.replySuggestions,
    isLoadingSuggestions: state.isLoadingSuggestions,
    lastFetchedSuggestionsFor: state.lastFetchedSuggestionsFor,
  })));

  // Get actions from store (stable references)
  const loadHistoryForPair = useMaestroStore(state => state.loadHistoryForPair);
  const addMessage = useMaestroStore(state => state.addMessage);
  const updateMessage = useMaestroStore(state => state.updateMessage);
  const deleteMessage = useMaestroStore(state => state.deleteMessage);
  const setMessages = useMaestroStore(state => state.setMessages);
  const setReplySuggestions = useMaestroStore(state => state.setReplySuggestions);
  const setIsLoadingSuggestions = useMaestroStore(state => state.setIsLoadingSuggestions);
  const upsertMessageTtsCache = useMaestroStore(state => state.upsertMessageTtsCache);
  const upsertSuggestionTtsCache = useMaestroStore(state => state.upsertSuggestionTtsCache);
  const trimHistoryByBookmark = useMaestroStore(state => state.trimHistoryByBookmark);
  const getHistoryRespectingBookmark = useMaestroStore(state => state.getHistoryRespectingBookmark);
  const computeMaxMessagesForArray = useMaestroStore(state => state.computeMaxMessagesForArray);

  // Refs for imperative access (synced with store state)
  const messagesRef = useRef<ChatMessage[]>(messages);
  const isLoadingHistoryRef = useRef<boolean>(isLoadingHistory);
  const replySuggestionsRef = useRef<ReplySuggestion[]>(replySuggestions);
  const isLoadingSuggestionsRef = useRef<boolean>(isLoadingSuggestions);
  const lastFetchedSuggestionsForRef = useRef<string | null>(lastFetchedSuggestionsFor);

  // Keep refs in sync with store state
  useEffect(() => { messagesRef.current = messages; }, [messages]);
  useEffect(() => { isLoadingHistoryRef.current = isLoadingHistory; }, [isLoadingHistory]);
  useEffect(() => { replySuggestionsRef.current = replySuggestions; }, [replySuggestions]);
  useEffect(() => { isLoadingSuggestionsRef.current = isLoadingSuggestions; }, [isLoadingSuggestions]);
  useEffect(() => { lastFetchedSuggestionsForRef.current = lastFetchedSuggestionsFor; }, [lastFetchedSuggestionsFor]);

  // Track previous language pair to detect changes
  const prevPairIdRef = useRef<string | null>(null);

  // Load history when language pair changes
  useEffect(() => {
    if (selectedLanguagePairId && selectedLanguagePairId !== prevPairIdRef.current) {
      prevPairIdRef.current = selectedLanguagePairId;
      loadHistoryForPair(selectedLanguagePairId, t);
    }
  }, [selectedLanguagePairId, t, loadHistoryForPair]);

  // Get settings from store for auto-bookmark functionality
  // Select only the specific values we need, not the whole settings object
  const historyBookmarkMessageId = useMaestroStore(state => state.settings.historyBookmarkMessageId);
  const maxVisibleMessages = useMaestroStore(state => state.settings.maxVisibleMessages);
  const storeSetSettings = useMaestroStore(state => state.setSettings);
  
  // Store refs for stable access in effects
  const storeSetSettingsRef = useRef(storeSetSettings);
  useEffect(() => { storeSetSettingsRef.current = storeSetSettings; }, [storeSetSettings]);

  // CRITICAL: Auto-save messages when they change (parity with baseline)
  useEffect(() => {
    if (!isLoadingHistory && selectedLanguagePairId) {
      safeSaveChatHistoryDB(selectedLanguagePairId, messages);
    }
  }, [messages, selectedLanguagePairId, isLoadingHistory]);

  // CRITICAL: Auto-update bookmark when messages exceed max visible (parity with baseline)
  // Use refs for settings to avoid effect re-runs when settings change
  const historyBookmarkMessageIdRef = useRef(historyBookmarkMessageId);
  const maxVisibleMessagesRef = useRef(maxVisibleMessages);
  useEffect(() => { historyBookmarkMessageIdRef.current = historyBookmarkMessageId; }, [historyBookmarkMessageId]);
  useEffect(() => { maxVisibleMessagesRef.current = maxVisibleMessages; }, [maxVisibleMessages]);
  
  useEffect(() => {
    if (isLoadingHistoryRef.current) return;
    const pairId = selectedLanguagePairId;
    if (!pairId) return;

    const arr = messagesRef.current;
    if (!arr || arr.length === 0) return;

    const isEligible = (m: ChatMessage) => isRealChatMessage(m);
    const eligibleIndices: number[] = [];
    for (let i = 0; i < arr.length; i++) {
      if (isEligible(arr[i])) eligibleIndices.push(i);
    }
    const maxVisible = (maxVisibleMessagesRef.current ?? MAX_VISIBLE_MESSAGES_DEFAULT) + 2;
    if (eligibleIndices.length <= maxVisible) return;

    const bmId = historyBookmarkMessageIdRef.current;
    let bmIndex = -1;
    if (bmId) {
      bmIndex = arr.findIndex(m => m.id === bmId);
    }

    const startForCount = bmIndex >= 0 ? bmIndex : 0;
    let currentVisibleIgnoringCtx = 0;
    for (let i = startForCount; i < arr.length; i++) {
      if (isEligible(arr[i])) currentVisibleIgnoringCtx++;
    }
    if (currentVisibleIgnoringCtx <= maxVisible) return;

    const cutoffPosInEligible = Math.max(0, eligibleIndices.length - maxVisible);
    const firstEligibleIdx = eligibleIndices[cutoffPosInEligible];

    let desiredBookmarkIdx = -1;
    for (let i = firstEligibleIdx; i < arr.length; i++) {
      if (arr[i].role === 'assistant' && !arr[i].thinking) {
        desiredBookmarkIdx = i;
        break;
      }
    }
    if (desiredBookmarkIdx === -1) return;

    const desiredBookmarkId = arr[desiredBookmarkIdx].id;
    if ((historyBookmarkMessageIdRef.current || null) === (desiredBookmarkId || null)) return;

    // Update settings in store using an updater function
    storeSetSettingsRef.current(prev => ({ ...prev, historyBookmarkMessageId: desiredBookmarkId }));
    // Persist full updated settings to DB
    const fullSettings = useMaestroStore.getState().settings;
    const updatedSettings = { ...fullSettings, historyBookmarkMessageId: desiredBookmarkId };
    setAppSettingsDB(updatedSettings).catch(() => {});
    setChatMetaDB(pairId, { bookmarkMessageId: desiredBookmarkId }).catch(() => {});
  }, [messages, selectedLanguagePairId]);

  // Wrapped addMessage to maintain interface compatibility
  const addMessageWrapper = useCallback((message: Omit<ChatMessage, 'id' | 'timestamp'>): string => {
    return addMessage(message);
  }, [addMessage]);

  return {
    messages,
    messagesRef,
    isLoadingHistory,
    isLoadingHistoryRef,
    addMessage: addMessageWrapper,
    updateMessage,
    deleteMessage,
    setMessages,
    trimHistoryByBookmark,
    getHistoryRespectingBookmark,
    upsertMessageTtsCache,
    upsertSuggestionTtsCache,
    computeMaxMessagesForArray,
    lastFetchedSuggestionsForRef,
    replySuggestions,
    setReplySuggestions,
    replySuggestionsRef,
    isLoadingSuggestions,
    setIsLoadingSuggestions,
    isLoadingSuggestionsRef,
  };
};

export default useChatStore;
