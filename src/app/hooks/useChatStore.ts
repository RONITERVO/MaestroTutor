// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useState, useCallback, useRef, useEffect } from 'react';
import { ChatMessage, TtsAudioCacheEntry, ReplySuggestion } from '../../core/types';
import { 
  getChatHistoryDB, 
  safeSaveChatHistoryDB, 
  readBackupForPair, 
  getChatMetaDB,
  setChatMetaDB 
} from '../../features/chat/services/chatHistory';
import { setAppSettingsDB } from '../../features/session/services/settings';
import { isRealChatMessage } from '../../shared/utils/common';
import { upsertTtsCacheEntries } from '../../features/chat/utils/persistence';
import type { TranslationFunction } from './useTranslations';

const MAX_VISIBLE_MESSAGES_DEFAULT = 50;

export interface UseChatStoreConfig {
  t: TranslationFunction;
  /** Current language pair ID - used as prop for proper React deps tracking */
  selectedLanguagePairId: string | null;
  settingsRef: React.MutableRefObject<{
    selectedLanguagePairId: string | null;
    historyBookmarkMessageId?: string | null;
    maxVisibleMessages?: number;
  }>;
  setSettings: React.Dispatch<React.SetStateAction<any>>;
}

export interface UseChatStoreReturn {
  messages: ChatMessage[];
  messagesRef: React.MutableRefObject<ChatMessage[]>;
  isLoadingHistory: boolean;
  isLoadingHistoryRef: React.MutableRefObject<boolean>;
  addMessage: (message: Omit<ChatMessage, 'id' | 'timestamp'>) => string;
  updateMessage: (messageId: string, updates: Partial<ChatMessage>) => void;
  deleteMessage: (messageId: string) => void;
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>;
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
  setReplySuggestions: React.Dispatch<React.SetStateAction<ReplySuggestion[]>>;
  replySuggestionsRef: React.MutableRefObject<ReplySuggestion[]>;
  isLoadingSuggestions: boolean;
  setIsLoadingSuggestions: React.Dispatch<React.SetStateAction<boolean>>;
  isLoadingSuggestionsRef: React.MutableRefObject<boolean>;
}

const INLINE_CAP_AUDIO = 512 * 1024; // 512KB cap for inline audio

/**
 * Hook for managing chat message state with persistence to IndexedDB.
 * Handles message CRUD, history loading, and bookmark-based trimming.
 */
export const useChatStore = (config: UseChatStoreConfig): UseChatStoreReturn => {
  const { t, selectedLanguagePairId, settingsRef, setSettings } = config;
  
  const messagesRef = useRef<ChatMessage[]>([]);
  const isLoadingHistoryRef = useRef<boolean>(true);
  const lastFetchedSuggestionsForRef = useRef<string | null>(null);
  const replySuggestionsRef = useRef<ReplySuggestion[]>([]);
  const isLoadingSuggestionsRef = useRef<boolean>(false);

  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isLoadingHistory, setIsLoadingHistory] = useState(true);
  const [replySuggestions, setReplySuggestions] = useState<ReplySuggestion[]>([]);
  const [isLoadingSuggestions, setIsLoadingSuggestions] = useState(false);

  // Sync refs with state
  useEffect(() => { messagesRef.current = messages; }, [messages]);
  useEffect(() => { isLoadingHistoryRef.current = isLoadingHistory; }, [isLoadingHistory]);
  useEffect(() => { replySuggestionsRef.current = replySuggestions; }, [replySuggestions]);
  useEffect(() => { isLoadingSuggestionsRef.current = isLoadingSuggestions; }, [isLoadingSuggestions]);

  // Load history when language pair changes
  useEffect(() => {
    const loadHistoryForPair = async (pairId: string) => {
      setIsLoadingHistory(true);
      try {
        let history = await getChatHistoryDB(pairId);
        if (!history || history.length === 0) {
          const backup = readBackupForPair(pairId);
          if (backup && backup.length > 0) {
            history = backup;
            safeSaveChatHistoryDB(pairId, backup);
          }
        }
        
        // Clean up interrupted states
        const cleanedHistory = (history || []).map(msg => {
          if (msg.isGeneratingImage || msg.thinking) {
            const newMsg = { ...msg };
            if (newMsg.isGeneratingImage) {
              newMsg.isGeneratingImage = false;
              newMsg.imageGenError = t('chat.error.imageGenInterrupted');
            }
            if (newMsg.thinking) {
              newMsg.thinking = false;
              if (!newMsg.text && !newMsg.translations?.length && !newMsg.rawAssistantResponse) {
                newMsg.role = 'error';
                newMsg.text = t('chat.error.thinkingInterrupted');
              }
            }
            return newMsg;
          }
          return msg;
        });

        const wasCleaned = (history || []).length > 0 && JSON.stringify(history) !== JSON.stringify(cleanedHistory);
        setMessages(cleanedHistory);
        if (wasCleaned) await safeSaveChatHistoryDB(pairId, cleanedHistory);
        
        // Load chat meta for bookmark
        try {
          const meta = await getChatMetaDB(pairId);
          if (meta && meta.bookmarkMessageId) {
            setSettings((prev: any) => {
              const next = {
                ...prev,
                historyBookmarkMessageId: meta.bookmarkMessageId ?? null,
              };
              settingsRef.current = next;
              setAppSettingsDB(next).catch(() => {});
              return next;
            });
          } else {
            setSettings((prev: any) => {
              const next = { ...prev, historyBookmarkMessageId: null };
              settingsRef.current = next;
              setAppSettingsDB(next).catch(() => {});
              return next;
            });
          }
        } catch (e) {
          // Ignore meta load errors
        }
      } catch (error) {
        console.error("Failed to load history from IndexedDB", error);
        setMessages([]);
      } finally {
        setIsLoadingHistory(false);
      }
    };

    const pairId = selectedLanguagePairId;
    if (pairId) {
      setMessages([]);
      setReplySuggestions([]);
      loadHistoryForPair(pairId);
    }
  }, [selectedLanguagePairId, t, setSettings, settingsRef]);

  // Auto-save messages when they change
  useEffect(() => {
    if (!isLoadingHistory && selectedLanguagePairId) {
      safeSaveChatHistoryDB(selectedLanguagePairId, messages);
    }
  }, [messages, selectedLanguagePairId, isLoadingHistory]);

  // Auto-update bookmark when messages exceed max visible
  useEffect(() => {
    if (isLoadingHistoryRef.current) return;
    const pairId = settingsRef.current.selectedLanguagePairId;
    if (!pairId) return;

    const arr = messagesRef.current;
    if (!arr || arr.length === 0) return;

    const isEligible = (m: ChatMessage) => isRealChatMessage(m);
    const eligibleIndices: number[] = [];
    for (let i = 0; i < arr.length; i++) {
      if (isEligible(arr[i])) eligibleIndices.push(i);
    }
    const maxVisible = (settingsRef.current.maxVisibleMessages ?? MAX_VISIBLE_MESSAGES_DEFAULT) + 2;
    if (eligibleIndices.length <= maxVisible) return;

    const bmId = settingsRef.current.historyBookmarkMessageId;
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
    if ((settingsRef.current.historyBookmarkMessageId || null) === (desiredBookmarkId || null)) return;

    setSettings((prev: any) => {
      const next = { ...prev, historyBookmarkMessageId: desiredBookmarkId };
      settingsRef.current = next;
      setAppSettingsDB(next).catch(() => {});
      return next;
    });
    (async () => { 
      try { await setChatMetaDB(pairId, { bookmarkMessageId: desiredBookmarkId }); } catch {} 
    })();
  }, [messages, selectedLanguagePairId, settingsRef, setSettings]);

  const addMessage = useCallback((message: Omit<ChatMessage, 'id' | 'timestamp'>): string => {
    const newMessage = { ...message, id: crypto.randomUUID(), timestamp: Date.now() };
    setMessages(prevMessages => [...prevMessages, newMessage]);
    return newMessage.id;
  }, []);

  const updateMessage = useCallback((messageId: string, updates: Partial<ChatMessage>) => {
    setMessages(prev => prev.map(m => 
      m.id === messageId ? { ...m, ...updates, timestamp: Date.now() } : m
    ));
  }, []);

  const deleteMessage = useCallback((messageId: string) => {
    setMessages(prev => prev.filter(m => m.id !== messageId));
  }, []);

  const trimHistoryByBookmark = useCallback((arr: ChatMessage[]): ChatMessage[] => {
    const bm = settingsRef.current.historyBookmarkMessageId;
    if (!bm) return arr;
    if (!messagesRef.current.some(m => m.id === bm)) return arr;
    const idx = arr.findIndex(m => m.id === bm && !m.thinking);
    if (idx === -1) return arr;
    return arr.slice(idx + 1);
  }, [settingsRef]);

  const getHistoryRespectingBookmark = useCallback((arr: ChatMessage[]): ChatMessage[] => {
    if (settingsRef.current.historyBookmarkMessageId) {
      return trimHistoryByBookmark(arr);
    }
    return arr;
  }, [settingsRef, trimHistoryByBookmark]);

  const upsertMessageTtsCache = useCallback((messageId: string, entry: TtsAudioCacheEntry) => {
    if (!entry || typeof entry.audioDataUrl !== 'string' || 
        entry.audioDataUrl.length === 0 || entry.audioDataUrl.length > INLINE_CAP_AUDIO) {
      return;
    }
    setMessages(prev => prev.map(m => {
      if (m.id !== messageId) return m;
      const nextCache = upsertTtsCacheEntries(m.ttsAudioCache, entry);
      return { ...m, ttsAudioCache: nextCache };
    }));
  }, []);

  const upsertSuggestionTtsCache = useCallback((messageId: string, suggestionIndex: number, entry: TtsAudioCacheEntry) => {
    if (!entry || typeof entry.audioDataUrl !== 'string' || 
        entry.audioDataUrl.length === 0 || entry.audioDataUrl.length > INLINE_CAP_AUDIO) {
      return;
    }
    setMessages(prev => prev.map(m => {
      if (m.id !== messageId || !Array.isArray(m.replySuggestions)) return m;
      const nextSuggestions = m.replySuggestions.map((suggestion, idx) => {
        if (idx !== suggestionIndex) return suggestion;
        const nextCache = upsertTtsCacheEntries(suggestion.ttsAudioCache, entry);
        return { ...suggestion, ttsAudioCache: nextCache };
      });
      return { ...m, replySuggestions: nextSuggestions };
    }));
    
    // CRITICAL: Also sync local replySuggestions state when cache is upserted
    // This matches original App.tsx lines 867-873
    if (lastFetchedSuggestionsForRef.current === messageId) {
      setReplySuggestions(prev => prev.map((suggestion, idx) => {
        if (idx !== suggestionIndex) return suggestion;
        const nextCache = upsertTtsCacheEntries(suggestion.ttsAudioCache, entry);
        return { ...suggestion, ttsAudioCache: nextCache };
      }));
    }
  }, []);

  const computeMaxMessagesForArray = useCallback((arr: ChatMessage[]): number | undefined => {
    const s = settingsRef.current;
    const realArr = arr.filter(isRealChatMessage);
    if (s.historyBookmarkMessageId) {
      const idx = realArr.findIndex(m => m.id === s.historyBookmarkMessageId);
      if (idx !== -1) {
        return Math.max(0, realArr.length - (idx + 1));
      }
    }
    return undefined;
  }, [settingsRef]);

  return {
    messages,
    messagesRef,
    isLoadingHistory,
    isLoadingHistoryRef,
    addMessage,
    updateMessage,
    deleteMessage,
    setMessages,
    trimHistoryByBookmark,
    getHistoryRespectingBookmark,
    upsertMessageTtsCache,
    upsertSuggestionTtsCache,
    computeMaxMessagesForArray,
    lastFetchedSuggestionsForRef,
    // Reply suggestions state (moved from useMaestroController)
    replySuggestions,
    setReplySuggestions,
    replySuggestionsRef,
    isLoadingSuggestions,
    setIsLoadingSuggestions,
    isLoadingSuggestionsRef,
  };
};

export default useChatStore;
