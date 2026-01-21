// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * Chat Slice - manages chat messages, history, and reply suggestions
 * 
 * Responsibilities:
 * - Messages array and CRUD operations
 * - Reply suggestions state
 * - Loading states for history and suggestions
 * - TTS cache management for messages
 * - History persistence via chatHistory services
 */

import type { StateCreator } from 'zustand';
import type { ChatMessage, ReplySuggestion, TtsAudioCacheEntry, GroundingChunk } from '../../core/types';
import { 
  getChatHistoryDB, 
  safeSaveChatHistoryDB, 
  getChatMetaDB
} from '../../features/chat';
import { upsertTtsCacheEntries } from '../../features/chat';
import { isRealChatMessage } from '../../shared/utils/common';
import type { MaestroStore } from '../maestroStore';

const INLINE_CAP_AUDIO = 512 * 1024; // 512KB cap for inline audio

export interface ChatSlice {
  // State
  messages: ChatMessage[];
  isLoadingHistory: boolean;
  replySuggestions: ReplySuggestion[];
  isLoadingSuggestions: boolean;
  lastFetchedSuggestionsFor: string | null;
  isSending: boolean;
  sendPrep: { active: boolean; label: string; done?: number; total?: number; etaMs?: number } | null;
  isCreatingSuggestion: boolean;
  latestGroundingChunks: GroundingChunk[] | undefined;
  imageLoadDurations: number[];
  attachedImageBase64: string | null;
  attachedImageMimeType: string | null;
  
  // Actions
  loadHistoryForPair: (pairId: string, t: (key: string) => string) => Promise<void>;
  addMessage: (message: Omit<ChatMessage, 'id' | 'timestamp'>) => string;
  updateMessage: (messageId: string, updates: Partial<ChatMessage>) => void;
  deleteMessage: (messageId: string) => void;
  setMessages: (messages: ChatMessage[] | ((prev: ChatMessage[]) => ChatMessage[])) => void;
  setReplySuggestions: (suggestions: ReplySuggestion[] | ((prev: ReplySuggestion[]) => ReplySuggestion[])) => void;
  setIsLoadingSuggestions: (value: boolean | ((prev: boolean) => boolean)) => void;
  setLastFetchedSuggestionsFor: (messageId: string | null) => void;
  setIsSending: (value: boolean) => void;
  setSendPrep: (prep: { active: boolean; label: string; done?: number; total?: number; etaMs?: number } | null | ((prev: { active: boolean; label: string; done?: number; total?: number; etaMs?: number } | null) => { active: boolean; label: string; done?: number; total?: number; etaMs?: number } | null)) => void;
  setIsCreatingSuggestion: (value: boolean) => void;
  setLatestGroundingChunks: (chunks: GroundingChunk[] | undefined) => void;
  addImageLoadDuration: (duration: number) => void;
  setAttachedImage: (base64: string | null, mimeType: string | null) => void;
  upsertMessageTtsCache: (messageId: string, entry: TtsAudioCacheEntry) => void;
  upsertSuggestionTtsCache: (messageId: string, suggestionIndex: number, entry: TtsAudioCacheEntry) => void;
  
  // Utilities
  trimHistoryByBookmark: (arr: ChatMessage[]) => ChatMessage[];
  getHistoryRespectingBookmark: (arr: ChatMessage[]) => ChatMessage[];
  computeMaxMessagesForArray: (arr: ChatMessage[]) => number | undefined;
  getMessages: () => ChatMessage[];
}

export const createChatSlice: StateCreator<
  MaestroStore,
  [['zustand/subscribeWithSelector', never], ['zustand/devtools', never]],
  [],
  ChatSlice
> = (set, get) => ({
  // Initial state
  messages: [],
  isLoadingHistory: true,
  replySuggestions: [],
  isLoadingSuggestions: false,
  lastFetchedSuggestionsFor: null,
  isSending: false,
  sendPrep: null,
  isCreatingSuggestion: false,
  latestGroundingChunks: undefined,
  imageLoadDurations: [],
  attachedImageBase64: null,
  attachedImageMimeType: null,
  
  // Actions
  loadHistoryForPair: async (pairId: string, t: (key: string) => string) => {
    set({ isLoadingHistory: true, messages: [], replySuggestions: [] });
    
    try {
      const history = await getChatHistoryDB(pairId);
      
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
      set({ messages: cleanedHistory });
      if (wasCleaned) await safeSaveChatHistoryDB(pairId, cleanedHistory);
      
      // Load chat meta for bookmark
      try {
        const meta = await getChatMetaDB(pairId);
        if (meta && meta.bookmarkMessageId) {
          get().updateSetting('historyBookmarkMessageId', meta.bookmarkMessageId);
        } else {
          get().updateSetting('historyBookmarkMessageId', null);
        }
      } catch (e) {
        // Ignore meta load errors
      }
    } catch (error) {
      console.error("Failed to load history from IndexedDB", error);
      set({ messages: [] });
    } finally {
      set({ isLoadingHistory: false });
    }
  },
  
  addMessage: (message: Omit<ChatMessage, 'id' | 'timestamp'>): string => {
    const newMessage = { ...message, id: crypto.randomUUID(), timestamp: Date.now() } as ChatMessage;
    set(state => ({ messages: [...state.messages, newMessage] }));
    return newMessage.id;
  },
  
  updateMessage: (messageId: string, updates: Partial<ChatMessage>) => {
    set(state => ({
      messages: state.messages.map(m => 
        m.id === messageId ? { ...m, ...updates, timestamp: Date.now() } : m
      )
    }));
  },
  
  deleteMessage: (messageId: string) => {
    set(state => ({
      messages: state.messages.filter(m => m.id !== messageId)
    }));
  },
  
  setMessages: (messagesOrUpdater) => {
    set(state => ({
      messages: typeof messagesOrUpdater === 'function' 
        ? messagesOrUpdater(state.messages) 
        : messagesOrUpdater
    }));
  },
  
  setReplySuggestions: (suggestionsOrUpdater) => {
    set(state => ({
      replySuggestions: typeof suggestionsOrUpdater === 'function'
        ? suggestionsOrUpdater(state.replySuggestions)
        : suggestionsOrUpdater
    }));
  },
  
  setIsLoadingSuggestions: (value: boolean | ((prev: boolean) => boolean)) => {
    set(state => ({
      isLoadingSuggestions: typeof value === 'function' ? value(state.isLoadingSuggestions) : value
    }));
  },
  
  setLastFetchedSuggestionsFor: (messageId: string | null) => {
    set({ lastFetchedSuggestionsFor: messageId });
  },
  
  setIsSending: (value: boolean) => {
    set({ isSending: value });
  },
  
  setSendPrep: (prep) => {
    set(state => ({
      sendPrep: typeof prep === 'function' ? prep(state.sendPrep) : prep
    }));
  },
  
  setIsCreatingSuggestion: (value: boolean) => {
    set({ isCreatingSuggestion: value });
  },
  
  setLatestGroundingChunks: (chunks) => {
    set({ latestGroundingChunks: chunks });
  },
  
  addImageLoadDuration: (duration: number) => {
    const MAX_IMAGE_LOAD_DURATIONS = 100;
    set(state => {
      const newDurations = [...state.imageLoadDurations, duration];
      // Keep only the most recent N entries to prevent unbounded growth
      return {
        imageLoadDurations: newDurations.length > MAX_IMAGE_LOAD_DURATIONS 
          ? newDurations.slice(-MAX_IMAGE_LOAD_DURATIONS) 
          : newDurations
      };
    });
  },
  
  setAttachedImage: (base64: string | null, mimeType: string | null) => {
    set({ attachedImageBase64: base64, attachedImageMimeType: mimeType });
  },
  
  upsertMessageTtsCache: (messageId: string, entry: TtsAudioCacheEntry) => {
    if (!entry || typeof entry.audioDataUrl !== 'string' || 
        entry.audioDataUrl.length === 0 || entry.audioDataUrl.length > INLINE_CAP_AUDIO) {
      return;
    }
    set(state => ({
      messages: state.messages.map(m => {
        if (m.id !== messageId) return m;
        const nextCache = upsertTtsCacheEntries(m.ttsAudioCache, entry);
        return { ...m, ttsAudioCache: nextCache };
      })
    }));
  },
  
  upsertSuggestionTtsCache: (messageId: string, suggestionIndex: number, entry: TtsAudioCacheEntry) => {
    if (!entry || typeof entry.audioDataUrl !== 'string' || 
        entry.audioDataUrl.length === 0 || entry.audioDataUrl.length > INLINE_CAP_AUDIO) {
      return;
    }
    set(state => {
      const newMessages = state.messages.map(m => {
        if (m.id !== messageId || !Array.isArray(m.replySuggestions)) return m;
        const nextSuggestions = m.replySuggestions.map((suggestion, idx) => {
          if (idx !== suggestionIndex) return suggestion;
          const nextCache = upsertTtsCacheEntries(suggestion.ttsAudioCache, entry);
          return { ...suggestion, ttsAudioCache: nextCache };
        });
        return { ...m, replySuggestions: nextSuggestions };
      });
      
      // Also sync local replySuggestions state when cache is upserted
      let newSuggestions = state.replySuggestions;
      if (state.lastFetchedSuggestionsFor === messageId) {
        newSuggestions = state.replySuggestions.map((suggestion, idx) => {
          if (idx !== suggestionIndex) return suggestion;
          const nextCache = upsertTtsCacheEntries(suggestion.ttsAudioCache, entry);
          return { ...suggestion, ttsAudioCache: nextCache };
        });
      }
      
      return { messages: newMessages, replySuggestions: newSuggestions };
    });
  },
  
  // Utilities
  trimHistoryByBookmark: (arr: ChatMessage[]): ChatMessage[] => {
    const bm = get().settings.historyBookmarkMessageId;
    if (!bm) return arr;
    if (!arr.some(m => m.id === bm)) return arr;
    const idx = arr.findIndex(m => m.id === bm && !m.thinking);
    if (idx === -1) return arr;
    return arr.slice(idx + 1);
  },
  
  getHistoryRespectingBookmark: (arr: ChatMessage[]): ChatMessage[] => {
    if (get().settings.historyBookmarkMessageId) {
      return get().trimHistoryByBookmark(arr);
    }
    return arr;
  },
  
  computeMaxMessagesForArray: (arr: ChatMessage[]): number | undefined => {
    const settings = get().settings;
    const realArr = arr.filter(isRealChatMessage);
    if (settings.historyBookmarkMessageId) {
      const idx = realArr.findIndex(m => m.id === settings.historyBookmarkMessageId);
      if (idx !== -1) {
        return Math.max(0, realArr.length - (idx + 1));
      }
    }
    return undefined;
  },
  
  getMessages: () => get().messages,
});
