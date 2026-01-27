// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * useTutorConversation - The main orchestration hook for the Maestro tutor.
 * 
 * This hook coordinates the core message sending logic including:
 * - User message processing with optional media
 * - Gemini API calls for text/image generation
 * - Reply suggestion generation
 * - Re-engagement triggers
 * - Translation and parsing of responses
 */

import { useCallback, useRef, useEffect, useMemo } from 'react';
import { 
  ChatMessage, 
  ReplySuggestion, 
  GroundingChunk,
  MaestroActivityStage,
  LanguagePair,
  AppSettings,
  RecordedUtterance 
} from '../../../core/types';
import { ApiError } from '../../../api/gemini/client';
import { generateGeminiResponse, translateText } from '../../../api/gemini/generative';
import { sanitizeHistoryWithVerifiedUris, uploadMediaToFiles, checkFileStatuses } from '../../../api/gemini/files';
import { generateImage } from '../../../api/gemini/vision';
import { getGlobalProfileDB, setGlobalProfileDB, setAppSettingsDB } from '../../session';
import { safeSaveChatHistoryDB, deriveHistoryForApi, INLINE_CAP_AUDIO } from '..';
import { processMediaForUpload, createKeyframeFromVideoDataUrl } from '../../vision';
import { 
  DEFAULT_TEXT_MODEL_ID, 
  IMAGE_GEN_CAMERA_ID,
  MAX_MEDIA_TO_KEEP 
} from '../../../core/config/app';
import { 
  DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE, 
  IMAGE_GEN_SYSTEM_INSTRUCTION, 
  IMAGE_GEN_USER_PROMPT_TEMPLATE,
  composeMaestroSystemInstruction 
} from '../../../core/config/prompts';
import { isRealChatMessage } from '../../../shared/utils/common';
import { getPrimarySubtag, getShortLangCodeForPrompt } from '../../../shared/utils/languageUtils';
import type { TranslationFunction } from '../../../app/hooks/useTranslations';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../core/config/activityTokens';
import { useMaestroStore } from '../../../store';
import { useShallow } from 'zustand/shallow';
import { selectIsSending, selectIsLoadingSuggestions, selectIsCreatingSuggestion, selectIsSpeaking } from '../../../store/slices/uiSlice';
import { selectSelectedLanguagePair } from '../../../store/slices/settingsSlice';

const AUX_TEXT_MODEL_ID = 'gemini-3-flash-preview';

export interface UseTutorConversationConfig {
  // Translation function
  t: TranslationFunction;
  
  // Settings
  /** @deprecated Store-backed ref is used internally - this field is ignored */
  settingsRef?: React.MutableRefObject<AppSettings>;
  setSettings: (settings: AppSettings | ((prev: AppSettings) => AppSettings)) => void;
  /** @deprecated Store-backed ref is used internally - this field is ignored */
  selectedLanguagePairRef?: React.MutableRefObject<LanguagePair | undefined>;
  
  // Chat store
  /** @deprecated Store-backed ref is used internally - this field is ignored */
  messagesRef?: React.MutableRefObject<ChatMessage[]>;
  addMessage: (message: Omit<ChatMessage, 'id' | 'timestamp'>) => string;
  updateMessage: (messageId: string, updates: Partial<ChatMessage>) => void;
  setMessages: (messages: ChatMessage[] | ((prev: ChatMessage[]) => ChatMessage[])) => void;
  /** @deprecated Store-backed ref is used internally - this field is ignored */
  isLoadingHistoryRef?: React.MutableRefObject<boolean>;
  getHistoryRespectingBookmark: (arr: ChatMessage[]) => ChatMessage[];
  computeMaxMessagesForArray: (arr: ChatMessage[]) => number | undefined;
  /** @deprecated Store-backed ref is used internally - this field is ignored */
  lastFetchedSuggestionsForRef?: React.MutableRefObject<string | null>;
  
  // Hardware
  captureSnapshot: (isForReengagement?: boolean) => Promise<{ base64: string; mimeType: string; storageOptimizedBase64: string; storageOptimizedMimeType: string } | null>;
  
  // Speech
  /** @deprecated Store-backed ref is used internally - this field is ignored */
  speechIsSpeakingRef?: React.MutableRefObject<boolean>;
  speakMessage: (message: ChatMessage) => void;
  isSpeechSynthesisSupported: boolean;
  isListening: boolean;
  stopListening: () => void;
  startListening: (lang: string) => void;
  clearTranscript: () => void;
  hasPendingQueueItems: () => boolean;
  claimRecordedUtterance: () => RecordedUtterance | null;
  /** @deprecated Store-backed ref is used internally - this field is ignored */
  sttInterruptedBySendRef?: React.MutableRefObject<boolean>;
  /** @deprecated Store-backed ref is used internally - this field is ignored */
  recordedUtterancePendingRef?: React.MutableRefObject<RecordedUtterance | null>;
  /** @deprecated Store-backed ref is used internally - this field is ignored */
  pendingRecordedAudioMessageRef?: React.MutableRefObject<string | null>;
  
  // Re-engagement - using refs to allow late binding
  scheduleReengagementRef: React.MutableRefObject<(reason: string, delayOverrideMs?: number) => void>;
  cancelReengagementRef: React.MutableRefObject<() => void>;
  
  // UI State
  transcript: string;
  
  // Prompts
  currentSystemPromptText: string;
  currentReplySuggestionsPromptText: string;
  
  // Reply suggestions (managed by useChatStore, passed through)
  setReplySuggestions: (suggestions: ReplySuggestion[] | ((prev: ReplySuggestion[]) => ReplySuggestion[])) => void;
  
  // Toggle suggestion mode callback - using ref to allow late binding
  handleToggleSuggestionModeRef?: React.MutableRefObject<((forceState?: boolean) => void) | undefined>;
  
  // Maestro avatar refs - passed from App.tsx where the avatar is loaded
  maestroAvatarUriRef: React.MutableRefObject<string | null>;
  maestroAvatarMimeTypeRef: React.MutableRefObject<string | null>;
  
  // Hardware errors
  setSnapshotUserError?: React.Dispatch<React.SetStateAction<string | null>>;
}

export interface UseTutorConversationReturn {
  // State
  isSending: boolean;
  isSendingRef: React.MutableRefObject<boolean>;
  sendPrep: { active: boolean; label: string; done?: number; total?: number; etaMs?: number } | null;
  latestGroundingChunks: GroundingChunk[] | undefined;
  maestroActivityStage: MaestroActivityStage;
  isCreatingSuggestion: boolean;
  imageLoadDurations: number[];
  
  // Main handlers
  handleSendMessageInternal: (
    text: string,
    passedImageBase64?: string,
    passedImageMimeType?: string,
    messageType?: 'user' | 'conversational-reengagement' | 'image-reengagement'
  ) => Promise<boolean>;
  handleSendMessageInternalRef: React.MutableRefObject<any>;
  
  // Suggestion handlers
  fetchAndSetReplySuggestions: (assistantMessageId: string, lastTutorMessage: string, history: ChatMessage[]) => Promise<void>;
  handleCreateSuggestion: (textToTranslate: string) => Promise<void>;
  handleSuggestionInteraction: (suggestion: ReplySuggestion, langType: 'target' | 'native') => void;
  
  // Activity stage
  setMaestroActivityStage: (stage: MaestroActivityStage) => void;
  
  // Parsing
  parseGeminiResponse: (responseText: string | undefined) => Array<{ target: string; native: string }>;
  
  // Utilities
  stripBracketedContent: (input: string | undefined | null) => string;
  resolveBookmarkContextSummary: () => string | null;
  ensureUrisForHistoryForSend: (arr: ChatMessage[], onProgress?: (done: number, total: number, etaMs?: number) => void) => Promise<Record<string, { oldUri?: string; newUri: string }>>;
  computeHistorySubsetForMedia: (arr: ChatMessage[]) => ChatMessage[];
  handleReengagementThresholdChange: (newThreshold: number) => void;
  calculateEstimatedImageLoadTime: () => number;
}

/**
 * Main orchestration hook for the Maestro Language Tutor.
 * Manages message sending, AI interactions, and conversation flow.
 */
export const useTutorConversation = (config: UseTutorConversationConfig): UseTutorConversationReturn => {
  const {
    t,
    setSettings,
    addMessage,
    updateMessage,
    setMessages,
    getHistoryRespectingBookmark,
    computeMaxMessagesForArray,
    captureSnapshot,
    speakMessage,
    isSpeechSynthesisSupported,
    isListening,
    stopListening,
    startListening,
    clearTranscript,
    hasPendingQueueItems,
    claimRecordedUtterance,
    scheduleReengagementRef,
    cancelReengagementRef,
    transcript,
    currentSystemPromptText,
    currentReplySuggestionsPromptText,
    setReplySuggestions,
    handleToggleSuggestionModeRef,
    maestroAvatarUriRef,
    maestroAvatarMimeTypeRef,
    setSnapshotUserError,
  } = config;

  const setLastFetchedSuggestionsFor = useMaestroStore(state => state.setLastFetchedSuggestionsFor);
  const setRecordedUtterancePending = useMaestroStore(state => state.setRecordedUtterancePending);
  const setPendingRecordedAudioMessageId = useMaestroStore(state => state.setPendingRecordedAudioMessageId);
  const setSttInterruptedBySend = useMaestroStore(state => state.setSttInterruptedBySend);

  const settingsRef = useMemo<React.MutableRefObject<AppSettings>>(() => ({
    get current() {
      return useMaestroStore.getState().settings;
    },
    set current(_value) {},
  }), []);

  const selectedLanguagePairRef = useMemo<React.MutableRefObject<LanguagePair | undefined>>(() => ({
    get current() {
      return selectSelectedLanguagePair(useMaestroStore.getState());
    },
    set current(_value) {},
  }), []);

  const messagesRef = useMemo<React.MutableRefObject<ChatMessage[]>>(() => ({
    get current() {
      return useMaestroStore.getState().messages;
    },
    set current(_value) {},
  }), []);

  const isLoadingHistoryRef = useMemo<React.MutableRefObject<boolean>>(() => ({
    get current() {
      return useMaestroStore.getState().isLoadingHistory;
    },
    set current(_value) {},
  }), []);

  const lastFetchedSuggestionsForRef = useMemo<React.MutableRefObject<string | null>>(() => ({
    get current() {
      return useMaestroStore.getState().lastFetchedSuggestionsFor;
    },
    set current(value) {
      setLastFetchedSuggestionsFor(value);
    },
  }), [setLastFetchedSuggestionsFor]);

  const speechIsSpeakingRef = useMemo<React.MutableRefObject<boolean>>(() => ({
    get current() {
      return selectIsSpeaking(useMaestroStore.getState());
    },
    set current(_value) {},
  }), []);

  const recordedUtterancePendingRef = useMemo<React.MutableRefObject<RecordedUtterance | null>>(() => ({
    get current() {
      return useMaestroStore.getState().recordedUtterancePending;
    },
    set current(value) {
      setRecordedUtterancePending(value);
    },
  }), [setRecordedUtterancePending]);

  const pendingRecordedAudioMessageRef = useMemo<React.MutableRefObject<string | null>>(() => ({
    get current() {
      return useMaestroStore.getState().pendingRecordedAudioMessageId;
    },
    set current(value) {
      setPendingRecordedAudioMessageId(value);
    },
  }), [setPendingRecordedAudioMessageId]);

  const sttInterruptedBySendRef = useMemo<React.MutableRefObject<boolean>>(() => ({
    get current() {
      return useMaestroStore.getState().sttInterruptedBySend;
    },
    set current(value) {
      setSttInterruptedBySend(value);
    },
  }), [setSttInterruptedBySend]);

  const {
    sendPrep,
    latestGroundingChunks,
    maestroActivityStage,
    imageLoadDurations,
    attachedImageBase64,
    attachedImageMimeType,
  } = useMaestroStore(useShallow(state => ({
    sendPrep: state.sendPrep,
    latestGroundingChunks: state.latestGroundingChunks,
    maestroActivityStage: state.maestroActivityStage,
    imageLoadDurations: state.imageLoadDurations,
    attachedImageBase64: state.attachedImageBase64,
    attachedImageMimeType: state.attachedImageMimeType,
  })));

  // Derive activity states from tokens using selectors
  const isSending = useMaestroStore(selectIsSending);
  // Note: isSpeaking derived from tokens is available via speechIsSpeakingRef passed from props
  const isLoadingSuggestions = useMaestroStore(selectIsLoadingSuggestions);
  const isCreatingSuggestion = useMaestroStore(selectIsCreatingSuggestion);

  const setSendPrep = useMaestroStore(state => state.setSendPrep);
  const setLatestGroundingChunks = useMaestroStore(state => state.setLatestGroundingChunks);
  const addImageLoadDuration = useMaestroStore(state => state.addImageLoadDuration);
  const setAttachedImage = useMaestroStore(state => state.setAttachedImage);
  const setMaestroActivityStage = useMaestroStore(state => state.setMaestroActivityStage);
  
  // Token-based activity tracking for unified busy state management
  const addActivityToken = useMaestroStore(state => state.addActivityToken);
  const removeActivityToken = useMaestroStore(state => state.removeActivityToken);

  // Refs
  const isSendingRef = useRef(false);
  const sendWithFileUploadInProgressRef = useRef(false);
  // maestroAvatarUriRef and maestroAvatarMimeTypeRef are now passed via config
  const handleSendMessageInternalRef = useRef<any>(null);
  const sendPrepRef = useRef<{ active: boolean; label: string; done?: number; total?: number; etaMs?: number } | null>(null);
  const isMountedRef = useRef(true);
  const sendingTokenRef = useRef<string | null>(null);
  const suggestionsTokenRef = useRef<string | null>(null);
  const createSuggestionTokenRef = useRef<string | null>(null);

  // Sync refs with state (derive isSending from tokens)
  useEffect(() => { isSendingRef.current = isSending; }, [isSending]);
  useEffect(() => { sendPrepRef.current = sendPrep; }, [sendPrep]);
  useEffect(() => () => { isMountedRef.current = false; }, []);

  // Utility functions
  const stripBracketedContent = useCallback((input: string | undefined | null): string => {
    if (typeof input !== 'string') return '';
    const without = input.replace(/\[[^\]]*\]/g, ' ');
    return without.replace(/\s+/g, ' ').trim();
  }, []);

  const parseGeminiResponse = useCallback((responseText: string | undefined): Array<{ target: string; native: string }> => {
    if (typeof responseText !== 'string' || !responseText.trim() || !selectedLanguagePairRef.current) {
      return [];
    }
    const lines = responseText.split('\n').map(line => line.trim()).filter(line => line);
    const translations: Array<{ target: string; native: string }> = [];
    const nativeLangPrefix = `[${getShortLangCodeForPrompt(selectedLanguagePairRef.current.nativeLanguageCode)}]`;

    for (let i = 0; i < lines.length; i++) {
      const currentLine = lines[i];
      if (!currentLine.startsWith(nativeLangPrefix)) {
        let targetContent = currentLine;
        let nativeContent = "";
        if (i + 1 < lines.length && lines[i + 1].startsWith(nativeLangPrefix)) {
          nativeContent = lines[i + 1].substring(nativeLangPrefix.length).trim();
          i++;
        }
        translations.push({ target: targetContent, native: nativeContent });
      } else {
        translations.push({ target: "", native: currentLine.substring(nativeLangPrefix.length).trim() });
      }
    }

    if (translations.length === 0 && responseText.trim()) {
      translations.push({ target: responseText.trim(), native: "" });
    }
    return translations;
  }, [selectedLanguagePairRef]);

  const resolveBookmarkContextSummary = useCallback((): string | null => {
    const bm = settingsRef.current.historyBookmarkMessageId;
    if (!bm) return null;
    const full = messagesRef.current;
    const bmIndex = full.findIndex(m => m.id === bm);
    if (bmIndex === -1) return null;
    let summary: string | undefined = (full[bmIndex] && typeof full[bmIndex].chatSummary === 'string')
      ? full[bmIndex].chatSummary!.trim()
      : undefined;
    if (!summary) {
      for (let i = bmIndex; i >= 0; i--) {
        const m = full[i];
        if (m.role === 'assistant' && typeof m.chatSummary === 'string' && m.chatSummary.trim()) { 
          summary = m.chatSummary.trim(); 
          break; 
        }
      }
    }
    if (!summary || !summary.trim()) return null;
    return summary.trim();
  }, [settingsRef, messagesRef]);

  const computeHistorySubsetForMedia = useCallback((arr: ChatMessage[]): ChatMessage[] => {
    let base = getHistoryRespectingBookmark(arr).filter(isRealChatMessage);
    const max = computeMaxMessagesForArray(base);
    if (typeof max === 'number') {
      base = base.slice(-Math.max(0, max));
    }
    return base;
  }, [getHistoryRespectingBookmark, computeMaxMessagesForArray]);

  const ensureUrisForHistoryForSend = useCallback(async (
    arr: ChatMessage[], 
    onProgress?: (done: number, total: number, etaMs?: number) => void
  ): Promise<Record<string, { oldUri?: string; newUri: string }>> => {
    const candidates = computeHistorySubsetForMedia(arr);

    const mediaIndices: number[] = [];
    for (let i = 0; i < candidates.length; i++) {
      const m = candidates[i];
      const hasMedia = !!((m as any).storageOptimizedImageUrl && (m as any).storageOptimizedImageMimeType) || 
                       !!(m.imageUrl && m.imageMimeType) || 
                       !!((m as any).uploadedFileUri && (m as any).uploadedFileMimeType);
      if (hasMedia) mediaIndices.push(i);
    }
    const maxMedia = MAX_MEDIA_TO_KEEP;
    const keepMediaIdx = new Set<number>(mediaIndices.slice(-maxMedia));

    const cachedUrisToCheck: string[] = [];
    for (let i = 0; i < candidates.length; i++) {
      if (!keepMediaIdx.has(i)) continue;
      const m0 = candidates[i];
      const cachedUri0 = (m0 as any).uploadedFileUri as string | undefined;
      const cachedMime0 = (m0 as any).uploadedFileMimeType as string | undefined;
      if (cachedUri0 && cachedMime0) cachedUrisToCheck.push(cachedUri0);
    }
    let cachedStatuses: Record<string, { deleted: boolean }> = {};
    try {
      const uniqUris = Array.from(new Set(cachedUrisToCheck));
      if (uniqUris.length) cachedStatuses = await checkFileStatuses(uniqUris);
    } catch { cachedStatuses = {}; }

    let totalToEnsure = 0;
    for (let i = 0; i < candidates.length; i++) {
      if (!keepMediaIdx.has(i)) continue;
      const m0 = candidates[i];
      const llmUrl0 = (m0 as any).storageOptimizedImageUrl as string | undefined;
      const llmMime0 = (m0 as any).storageOptimizedImageMimeType as string | undefined;
      const uiUrl0 = m0.imageUrl as string | undefined;
      const uiMime0 = m0.imageMimeType as string | undefined;
      const cachedUri0 = (m0 as any).uploadedFileUri as string | undefined;
      const cachedMime0 = (m0 as any).uploadedFileMimeType as string | undefined;
      const hasAnyMedia0 = !!((llmUrl0 && llmMime0) || (uiUrl0 && uiMime0));
      const missing = !(cachedUri0 && cachedMime0);
      const deleted = !!(cachedUri0 && cachedStatuses[cachedUri0]?.deleted);
      if ((missing || deleted) && hasAnyMedia0) totalToEnsure++;
    }

    let doneCount = 0;
    const startTs = Date.now();
    const tick = () => {
      if (!onProgress) return;
      const elapsed = Date.now() - startTs;
      const avg = doneCount > 0 ? elapsed / doneCount : undefined;
      const remaining = Math.max(0, totalToEnsure - doneCount);
      const eta = avg !== undefined ? Math.round(avg * remaining) : undefined;
      onProgress(doneCount, totalToEnsure, eta);
    };

    const updatedUriMap: Record<string, { oldUri?: string; newUri: string }> = {};
    for (let idx = 0; idx < candidates.length; idx++) {
      if (!keepMediaIdx.has(idx)) continue;
      const m = candidates[idx];
      const llmUrl = (m as any).storageOptimizedImageUrl as string | undefined;
      const llmMime = (m as any).storageOptimizedImageMimeType as string | undefined;
      const uiUrl = m.imageUrl as string | undefined;
      const uiMime = m.imageMimeType as string | undefined;
      if (!(llmUrl && llmMime) && !(uiUrl && uiMime)) continue;

      const cachedUri = (m as any).uploadedFileUri as string | undefined;
      const cachedMime = (m as any).uploadedFileMimeType as string | undefined;
      let cachedDeleted = false;
      if (cachedUri && cachedMime) {
        try {
          const st = cachedStatuses && cachedStatuses[cachedUri];
          cachedDeleted = !!(st && st.deleted);
        } catch { cachedDeleted = false; }
        if (!cachedDeleted) continue;
      }

      let dataForUpload: { dataUrl: string; mimeType: string } | null = null;
      try {
        if (llmUrl && llmMime) {
          dataForUpload = { dataUrl: llmUrl, mimeType: llmMime };
        } else if (uiUrl && uiMime) {
          // Upload ORIGINAL to API for best quality, optimize only for local storage
          dataForUpload = { dataUrl: uiUrl, mimeType: uiMime };
          try {
            const optimized = await processMediaForUpload(uiUrl, uiMime, { t });
            updateMessage(m.id, { storageOptimizedImageUrl: optimized.dataUrl, storageOptimizedImageMimeType: optimized.mimeType });
          } catch { /* optimization for storage is optional */ }
        }
        if (dataForUpload) {
          const up = await uploadMediaToFiles(dataForUpload.dataUrl, dataForUpload.mimeType, 'send-history');
          updateMessage(m.id, { uploadedFileUri: up.uri, uploadedFileMimeType: up.mimeType });
          (m as any).uploadedFileUri = up.uri;
          (m as any).uploadedFileMimeType = up.mimeType;
          updatedUriMap[m.id] = { oldUri: cachedUri, newUri: up.uri };
        }
      } catch (e) {
        console.warn('Pre-send URI ensure failed for message', m.id, e);
      }
      try { doneCount++; tick(); } catch {}
      await new Promise(r => setTimeout(r, 0));
    }
    return updatedUriMap;
  }, [computeHistorySubsetForMedia, updateMessage, t]);

  const handleReengagementThresholdChange = useCallback((newThreshold: number) => {
    setSettings(prev => {
      const next = {
        ...prev,
        smartReengagement: {
          ...prev.smartReengagement,
          thresholdSeconds: newThreshold,
        }
      };
      settingsRef.current = next;
      setAppSettingsDB(next).catch(() => {});
      return next;
    });
  }, [setSettings, settingsRef]);

  const calculateEstimatedImageLoadTime = useCallback((): number => {
    if (imageLoadDurations.length > 0) {
      const sum = imageLoadDurations.reduce((a, b) => a + b, 0);
      return sum / imageLoadDurations.length / 1000;
    }
    return 15;
  }, [imageLoadDurations]);

  // Reply suggestions - token-based loading state management
  const fetchAndSetReplySuggestions = useCallback(async (
    assistantMessageId: string, 
    lastTutorMessage: string, 
    history: ChatMessage[]
  ) => {
    // Check if already loading using token state
    if (isLoadingSuggestions) {
      setReplySuggestions([]);
      if (suggestionsTokenRef.current) {
        removeActivityToken(suggestionsTokenRef.current);
        suggestionsTokenRef.current = null;
      }
      return;
    }
    if (!lastTutorMessage.trim() || !selectedLanguagePairRef.current) {
      setReplySuggestions([]);
      return;
    }

    // Check if suggestions already exist on message
    {
      const allMsgs = messagesRef.current;
      const targetIdx = allMsgs.findIndex(m => m.id === assistantMessageId);
      if (targetIdx !== -1) {
        const target = allMsgs[targetIdx];
        if (target && Array.isArray((target as any).replySuggestions) && (target as any).replySuggestions.length > 0) {
          setReplySuggestions((target as any).replySuggestions as ReplySuggestion[]);
          if (suggestionsTokenRef.current) {
            removeActivityToken(suggestionsTokenRef.current);
            suggestionsTokenRef.current = null;
          }
          return;
        }
      }
    }

    // Add token for loading suggestions
    suggestionsTokenRef.current = addActivityToken(TOKEN_CATEGORY.GEN, TOKEN_SUBTYPE.SUGGESTIONS);
    setReplySuggestions([]);

    const historyForPrompt = getHistoryRespectingBookmark(history)
      .filter(msg => msg.role === 'user' || msg.role === 'assistant')
      .slice(-6)
      .map(msg => {
        if (msg.role === 'user') {
          return `User: ${msg.text || '(sent an image)'}`;
        }
        return `Tutor: ${msg.translations?.[0]?.target || msg.rawAssistantResponse || msg.text || '(sent an image)'}`;
      })
      .join('\n');

    const allMsgs = messagesRef.current;
    let previousChatSummary = '';
    {
      const idx = allMsgs.findIndex(m => m.id === assistantMessageId);
      const searchEnd = idx === -1 ? allMsgs.length - 1 : idx - 1;
      for (let i = searchEnd; i >= 0; i--) {
        const m = allMsgs[i];
        if (m.role === 'assistant' && typeof m.chatSummary === 'string' && m.chatSummary.trim()) {
          previousChatSummary = m.chatSummary.trim();
          break;
        }
      }
    }

    // Fetch existing global profile for the prompt (single API call optimization)
    let existingGlobalProfile = '';
    try {
      existingGlobalProfile = (await getGlobalProfileDB())?.text || '';
    } catch {
      existingGlobalProfile = '';
    }

    let suggestionPrompt = currentReplySuggestionsPromptText
      .replace("{tutor_message_placeholder}", lastTutorMessage)
      .replace("{conversation_history_placeholder}", historyForPrompt || "No history yet.")
      .replace("{previous_chat_summary_placeholder}", previousChatSummary || "")
      .replace("{existing_global_profile_placeholder}", existingGlobalProfile || "(none)");

    const MAX_RETRIES = 2;

    for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
      try {
        const response = await generateGeminiResponse(
          AUX_TEXT_MODEL_ID,
          suggestionPrompt,
          [],
          undefined,
          undefined,
          undefined,
          undefined,
          false,
          { responseMimeType: "application/json" }
        );

        let jsonStr = (response.text || '').trim();
        const fenceRegex = /^```(\w*)?\s*\n?(.*?)\n?\s*```$/s;
        const fenceMatch = jsonStr.match(fenceRegex);

        if (fenceMatch && fenceMatch[2]) {
          jsonStr = fenceMatch[2].trim();
        } else {
          const firstBrace = jsonStr.indexOf('{');
          const lastBrace = jsonStr.lastIndexOf('}');
          if (firstBrace !== -1 && lastBrace !== -1) {
            jsonStr = jsonStr.substring(firstBrace, lastBrace + 1);
          }
        }

        const parsedResponse = JSON.parse(jsonStr);

        if (Array.isArray(parsedResponse.suggestions) &&
          parsedResponse.suggestions.every((s: any) => typeof s === 'object' && s !== null && 'target' in s && 'native' in s && typeof s.target === 'string' && typeof s.native === 'string')) {
          const suggestions = parsedResponse.suggestions as ReplySuggestion[];
          setReplySuggestions(suggestions);
          updateMessage(assistantMessageId, { replySuggestions: suggestions });
          try { 
            const pid = settingsRef.current.selectedLanguagePairId; 
            if (pid) { await safeSaveChatHistoryDB(pid, messagesRef.current); } 
          } catch {}
        } else {
          console.warn("Parsed suggestions not in expected format:", parsedResponse.suggestions);
          setReplySuggestions([]);
        }

        if (typeof parsedResponse.reengagementSeconds === 'number' && parsedResponse.reengagementSeconds >= 5) {
          handleReengagementThresholdChange(parsedResponse.reengagementSeconds);
        }

        // Update chat summary on the message
        const newChatSummary = typeof parsedResponse.chatSummary === 'string' ? parsedResponse.chatSummary.trim() : '';
        if (newChatSummary) {
          updateMessage(assistantMessageId, { chatSummary: newChatSummary });
        }

        // Update global profile directly from the single API response (no second API call needed)
        try {
          const newGlobalProfile = typeof parsedResponse.globalProfile === 'string' ? parsedResponse.globalProfile.trim().slice(0, 10000) : '';
          if (newGlobalProfile) {
            await setGlobalProfileDB(newGlobalProfile);
            // Notify UI components that the global profile was updated
            try { window.dispatchEvent(new CustomEvent('globalProfileUpdated')); } catch {}
          }
        } catch (e) {
          console.warn('Failed to update global profile:', e);
        }

        break;

      } catch (error) {
        console.error(`Error fetching reply suggestions (attempt ${attempt + 1}/${MAX_RETRIES + 1}):`, error);
        if (attempt < MAX_RETRIES) {
          await new Promise(resolve => setTimeout(resolve, 500 * (attempt + 1)));
        } else {
          setReplySuggestions([]);
        }
      }
    }

    // Remove suggestions loading token
    if (suggestionsTokenRef.current) {
      removeActivityToken(suggestionsTokenRef.current);
      suggestionsTokenRef.current = null;
    }
  }, [
    currentReplySuggestionsPromptText, 
    handleReengagementThresholdChange, 
    getHistoryRespectingBookmark,
    messagesRef,
    selectedLanguagePairRef,
    settingsRef,
    isLoadingSuggestions,
    addActivityToken,
    removeActivityToken,
    setReplySuggestions,
    updateMessage
  ]);

  const handleCreateSuggestion = useCallback(async (textToTranslate: string) => {
    const sanitized = stripBracketedContent(textToTranslate);
    if (!sanitized || !selectedLanguagePairRef.current) return;

    // Add token for creating suggestion
    createSuggestionTokenRef.current = addActivityToken(TOKEN_CATEGORY.GEN, TOKEN_SUBTYPE.CREATE_SUGGESTION);

    const sttLang = settingsRef.current.stt.language;
    const sttLangCode = getPrimarySubtag(sttLang);
    const targetLangCode = getPrimarySubtag(selectedLanguagePairRef.current.targetLanguageCode);

    let fromLangName: string;
    let toLangName: string;
    let originalTextIsTarget: boolean;

    if (sttLangCode === targetLangCode) {
      fromLangName = selectedLanguagePairRef.current.targetLanguageName;
      toLangName = selectedLanguagePairRef.current.nativeLanguageName;
      originalTextIsTarget = true;
    } else {
      fromLangName = selectedLanguagePairRef.current.nativeLanguageName;
      toLangName = selectedLanguagePairRef.current.targetLanguageName;
      originalTextIsTarget = false;
    }

    try {
      const { translatedText } = await translateText(sanitized, fromLangName, toLangName);
      const newSuggestion: ReplySuggestion = {
        target: originalTextIsTarget ? sanitized : translatedText,
        native: originalTextIsTarget ? translatedText : sanitized,
      };

      const isDuplicate = (s: ReplySuggestion) => s.target === newSuggestion.target && s.native === newSuggestion.native;

      setReplySuggestions(prev => {
        if (prev.some(isDuplicate)) return prev;
        return [newSuggestion, ...prev];
      });

      const targetMsgId = lastFetchedSuggestionsForRef.current ||
        messagesRef.current.slice().reverse().find(m => m.role === 'assistant' && !m.thinking)?.id;

      if (targetMsgId) {
        if (!lastFetchedSuggestionsForRef.current) {
          lastFetchedSuggestionsForRef.current = targetMsgId;
        }
        setMessages(prev => prev.map(m => {
          if (m.id === targetMsgId) {
            const existing = m.replySuggestions || [];
            if (existing.some(isDuplicate)) return m;
            return { ...m, replySuggestions: [newSuggestion, ...existing] };
          }
          return m;
        }));
      }

    } catch (error) {
      console.error("Failed to create suggestion via translation:", error);
      addMessage({ role: 'error', text: t('error.translationFailed') });
    } finally {
      // Remove creating suggestion token
      if (createSuggestionTokenRef.current) {
        removeActivityToken(createSuggestionTokenRef.current);
        createSuggestionTokenRef.current = null;
      }
      // Exit suggestion mode after creating suggestion (matches original behavior)
      if (handleToggleSuggestionModeRef?.current) {
        handleToggleSuggestionModeRef.current(false);
      }
    }
  }, [addMessage, t, stripBracketedContent, selectedLanguagePairRef, settingsRef, lastFetchedSuggestionsForRef, messagesRef, setMessages, setReplySuggestions, handleToggleSuggestionModeRef, addActivityToken, removeActivityToken]);

  const handleSuggestionInteraction = useCallback((suggestion: ReplySuggestion, langType: 'target' | 'native') => {
    if (!selectedLanguagePairRef.current) return;
    if (speechIsSpeakingRef.current) return;
    if (!suggestion.target && !suggestion.native) return;
    void langType;
    // Speech handled by App-level speakWrapper.
  }, [selectedLanguagePairRef, speechIsSpeakingRef]);

  const requestReplySuggestions = useCallback((assistantMessageId: string, lastTutorMessage: string, history: ChatMessage[]) => {
    fetchAndSetReplySuggestions(assistantMessageId, lastTutorMessage, history);
    lastFetchedSuggestionsForRef.current = assistantMessageId;
  }, [fetchAndSetReplySuggestions, lastFetchedSuggestionsForRef]);

  const optimizeAndUploadMedia = useCallback(async (params: {
    dataUrl: string;
    mimeType: string;
    displayName: string;
    onProgress?: (label: string, done?: number, total?: number, etaMs?: number) => void;
    setUploadPrepLabel?: boolean;
  }) => {
    // Create optimized version for local storage (reduces DB size)
    const optimized = await processMediaForUpload(params.dataUrl, params.mimeType, { t, onProgress: params.onProgress });
    sendWithFileUploadInProgressRef.current = true;
    if (params.setUploadPrepLabel !== false) {
      setSendPrep(prev => (prev && prev.active
        ? { ...prev, label: t('chat.sendPrep.uploadingMedia') || 'Uploading media...' }
        : { active: true, label: t('chat.sendPrep.uploadingMedia') || 'Uploading media...' }));
    }
    // CRITICAL: Upload the ORIGINAL full-resolution data to the LLM for best quality
    // The optimized version is only used for local storage to reduce DB backup/reload size
    const upload = await uploadMediaToFiles(params.dataUrl, params.mimeType, params.displayName);
    return { optimized, upload };
  }, [t, setSendPrep]);

  const createUserMessage = useCallback(async (params: {
    sanitizedText: string;
    passedImageBase64?: string;
    passedImageMimeType?: string;
    messageType: 'user' | 'conversational-reengagement' | 'image-reengagement';
    shouldGenerateUserImage: boolean;
    currentSettingsVal: AppSettings;
  }) => {
    let userMessageId: string | null = null;
    let userMessageText = params.sanitizedText;
    let recordedSpeechForMessage: RecordedUtterance | null = null;
    let userImageToProcessBase64: string | undefined = (typeof params.passedImageBase64 === 'string' && params.passedImageBase64)
      ? params.passedImageBase64
      : undefined;
    let userImageToProcessMimeType: string | undefined = (typeof params.passedImageMimeType === 'string' && params.passedImageMimeType)
      ? params.passedImageMimeType
      : undefined;
    let userImageToProcessStorageOptimizedBase64: string | undefined = undefined;
    let userImageToProcessStorageOptimizedMimeType: string | undefined = undefined;

    if (params.messageType !== 'user') {
      return {
        userMessageId,
        userMessageText,
        recordedSpeechForMessage,
        userImageToProcessBase64,
        userImageToProcessMimeType,
        userImageToProcessStorageOptimizedBase64,
        userImageToProcessStorageOptimizedMimeType,
      };
    }

    for (let attempt = 0; attempt < 2; attempt++) {
      const claimed = typeof claimRecordedUtterance === 'function' ? claimRecordedUtterance() : null;
      if (claimed && typeof claimed.dataUrl === 'string' && claimed.dataUrl.length > 0) {
        recordedSpeechForMessage = claimed;
        recordedUtterancePendingRef.current = null;
        break;
      }
      if (recordedUtterancePendingRef.current && typeof recordedUtterancePendingRef.current.dataUrl === 'string' && recordedUtterancePendingRef.current.dataUrl.length > 0) {
        recordedSpeechForMessage = recordedUtterancePendingRef.current;
        recordedUtterancePendingRef.current = null;
        break;
      }
      if (attempt === 0) {
        await new Promise((resolve) => setTimeout(resolve, 60));
      }
    }
    if (recordedSpeechForMessage && recordedSpeechForMessage.dataUrl.length > INLINE_CAP_AUDIO) {
      recordedSpeechForMessage = null;
    }

    if (params.currentSettingsVal.sendWithSnapshotEnabled && !userImageToProcessBase64 && !params.shouldGenerateUserImage) {
      const snapshotResult = await captureSnapshot(false);
      if (snapshotResult) {
        userImageToProcessBase64 = snapshotResult.base64;
        userImageToProcessMimeType = snapshotResult.mimeType;
        userImageToProcessStorageOptimizedBase64 = snapshotResult.storageOptimizedBase64;
        userImageToProcessStorageOptimizedMimeType = snapshotResult.storageOptimizedMimeType;
      }
    }

    // Handle video keyframe extraction
    const keyframeSrcBase64 = (typeof params.passedImageBase64 === 'string' && params.passedImageBase64)
      ? params.passedImageBase64
      : attachedImageBase64;
    const keyframeSrcMime = (typeof params.passedImageMimeType === 'string' && params.passedImageMimeType)
      ? params.passedImageMimeType
      : attachedImageMimeType;
    if (keyframeSrcBase64 && keyframeSrcMime && keyframeSrcMime.startsWith('video/')) {
      try {
        const kf = await createKeyframeFromVideoDataUrl(keyframeSrcBase64, { at: 'start', maxDim: 768, quality: 0.75, outputMime: 'image/jpeg' });
        const kfId = addMessage({ role: 'user', text: userMessageText, imageUrl: kf.dataUrl, imageMimeType: kf.mimeType });
        try {
          if (!sendWithFileUploadInProgressRef.current) {
            sendWithFileUploadInProgressRef.current = true;
          }
          updateMessage(kfId, { storageOptimizedImageUrl: kf.dataUrl, storageOptimizedImageMimeType: kf.mimeType });
          const up = await uploadMediaToFiles(kf.dataUrl, kf.mimeType, 'keyframe-image');
          updateMessage(kfId, { uploadedFileUri: up.uri, uploadedFileMimeType: up.mimeType });
        } catch (e) {
          // Ignore upload errors for keyframe
        }
        userMessageText = '';
        userImageToProcessBase64 = keyframeSrcBase64;
        userImageToProcessMimeType = keyframeSrcMime;
      } catch (e) {
        console.warn('Failed to create keyframe from video; proceeding without keyframe image message', e);
      }
    }

    if (!userImageToProcessStorageOptimizedBase64 && attachedImageBase64 && attachedImageMimeType) {
      try {
        if (!sendWithFileUploadInProgressRef.current) {
          sendWithFileUploadInProgressRef.current = true;
        }
        const optimized = await processMediaForUpload(attachedImageBase64, attachedImageMimeType, { t });
        userImageToProcessStorageOptimizedBase64 = optimized.dataUrl;
        userImageToProcessStorageOptimizedMimeType = optimized.mimeType;
      } catch {}
    }

    userMessageId = addMessage({
      role: 'user',
      text: userMessageText,
      recordedUtterance: recordedSpeechForMessage || undefined,
      imageUrl: userImageToProcessBase64,
      imageMimeType: userImageToProcessMimeType,
      storageOptimizedImageUrl: userImageToProcessStorageOptimizedBase64,
      storageOptimizedImageMimeType: userImageToProcessStorageOptimizedMimeType,
    });

    return {
      userMessageId,
      userMessageText,
      recordedSpeechForMessage,
      userImageToProcessBase64,
      userImageToProcessMimeType,
      userImageToProcessStorageOptimizedBase64,
      userImageToProcessStorageOptimizedMimeType,
    };
  }, [
    addMessage,
    attachedImageBase64,
    attachedImageMimeType,
    captureSnapshot,
    claimRecordedUtterance,
    recordedUtterancePendingRef,
    sendWithFileUploadInProgressRef,
    t,
    updateMessage,
  ]);

  const handleGeminiResponse = useCallback(async (params: {
    thinkingMessageId: string;
    geminiPromptText: string;
    sanitizedDerivedHistory: any[];
    systemInstructionForGemini: string;
    imageForGeminiContextMimeType?: string;
    imageForGeminiContextFileUri?: string;
    currentSettingsVal: AppSettings;
  }) => {
    const response = await generateGeminiResponse(
      DEFAULT_TEXT_MODEL_ID,
      params.geminiPromptText,
      params.sanitizedDerivedHistory,
      params.systemInstructionForGemini,
      undefined,
      params.imageForGeminiContextMimeType,
      params.imageForGeminiContextFileUri,
      params.currentSettingsVal.enableGoogleSearch,
      undefined
    );

    const accumulatedFullText = response.text || "";
    const parsedTranslationsOnComplete = parseGeminiResponse(accumulatedFullText);
    const groundingChunks = response.candidates?.[0]?.groundingMetadata?.groundingChunks as GroundingChunk[] | undefined;
    if (groundingChunks?.length) {
      setLatestGroundingChunks(groundingChunks);
    }

    const finalMessageUpdates = {
      thinking: false,
      translations: parsedTranslationsOnComplete.length > 0 ? parsedTranslationsOnComplete : undefined,
      rawAssistantResponse: accumulatedFullText,
      text: parsedTranslationsOnComplete.length === 0 ? accumulatedFullText : undefined,
    };
    updateMessage(params.thinkingMessageId, finalMessageUpdates);

    return { accumulatedFullText, finalMessageUpdates };
  }, [parseGeminiResponse, setLatestGroundingChunks, updateMessage]);

  const runUserImageGeneration = useCallback(async (params: {
    shouldGenerateUserImage: boolean;
    currentSettingsVal: AppSettings;
    messageType: 'user' | 'conversational-reengagement' | 'image-reengagement';
    userMessageText: string;
    userMessageId: string | null;
    userImageToProcessBase64?: string;
    sanitizedDerivedHistory: any[];
  }) => {
    if (!params.shouldGenerateUserImage || !params.currentSettingsVal.sendWithSnapshotEnabled || params.messageType !== 'user' ||
      !params.userMessageText.trim() || !params.userMessageId || params.userImageToProcessBase64) {
      return {};
    }

    const userImageGenStartTime = Date.now();
    updateMessage(params.userMessageId, {
      isGeneratingImage: true,
      imageGenerationStartTime: userImageGenStartTime,
      imageUrl: undefined,
      imageMimeType: undefined,
    });

    const sanitizedUserHistoryForImage = params.sanitizedDerivedHistory as any;
    let finalResult: any = null;

    for (let attempt = 0; attempt < 3; attempt++) {
      const prompt = IMAGE_GEN_USER_PROMPT_TEMPLATE.replace("{TEXT}", params.userMessageText);
      const userImgGenResult = await generateImage({
        history: sanitizedUserHistoryForImage,
        latestMessageText: prompt,
        latestMessageRole: 'user',
        systemInstruction: IMAGE_GEN_SYSTEM_INSTRUCTION,
        maestroAvatarUri: maestroAvatarUriRef.current || undefined,
        maestroAvatarMimeType: maestroAvatarMimeTypeRef.current || undefined,
      });
      finalResult = userImgGenResult;
      if ('base64Image' in userImgGenResult) break;
      if (attempt < 2) await new Promise(r => setTimeout(r, 1500));
    }

    if (finalResult && 'base64Image' in finalResult) {
      const duration = Date.now() - userImageGenStartTime;
      addImageLoadDuration(duration);
      try {
        const { optimized, upload } = await optimizeAndUploadMedia({
          dataUrl: finalResult.base64Image as string,
          mimeType: finalResult.mimeType as string,
          displayName: 'user-generated',
        });

        updateMessage(params.userMessageId, {
          imageUrl: finalResult.base64Image,
          imageMimeType: finalResult.mimeType,
          storageOptimizedImageUrl: optimized.dataUrl,
          storageOptimizedImageMimeType: optimized.mimeType,
          uploadedFileUri: upload.uri,
          uploadedFileMimeType: upload.mimeType,
          isGeneratingImage: false,
          imageGenError: null,
          imageGenerationStartTime: undefined
        });
        return { imageForGeminiContextFileUri: upload.uri, imageForGeminiContextMimeType: upload.mimeType };
      } catch (e) {
        updateMessage(params.userMessageId, {
          imageUrl: finalResult.base64Image,
          imageMimeType: finalResult.mimeType,
          isGeneratingImage: false,
          imageGenError: null,
          imageGenerationStartTime: undefined
        });
      } finally {
        setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...' } : prev));
      }
    } else if (finalResult) {
      updateMessage(params.userMessageId, {
        imageGenError: (finalResult as any).error,
        isGeneratingImage: false,
        imageGenerationStartTime: undefined
      });
    }

    return {};
  }, [
    generateImage,
    maestroAvatarMimeTypeRef,
    maestroAvatarUriRef,
    optimizeAndUploadMedia,
    addImageLoadDuration,
    setSendPrep,
    t,
    updateMessage,
  ]);

  const runAssistantImageGeneration = useCallback(async (params: {
    thinkingMessageId: string;
    accumulatedFullText: string;
    currentSettingsVal: AppSettings;
  }) => {
    if (!params.currentSettingsVal.imageGenerationModeEnabled || !params.accumulatedFullText.trim()) return;

    const assistantStartTime = Date.now();
    updateMessage(params.thinkingMessageId, {
      isGeneratingImage: true,
      imageGenerationStartTime: assistantStartTime
    });

    let historyForAssistantImageGen: ChatMessage[] | undefined = undefined;
    try {
      sendWithFileUploadInProgressRef.current = true;
      const baseForEnsure: ChatMessage[] = getHistoryRespectingBookmark(messagesRef.current);
      setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...', done: 0, total: 0 });
      const ensuredUpdates = await ensureUrisForHistoryForSend(baseForEnsure, (done, total, etaMs) => {
        setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...', done, total, etaMs });
      });
      historyForAssistantImageGen = baseForEnsure.map(m => {
        const upd = ensuredUpdates[m.id];
        if (upd && upd.newUri && (m as any).uploadedFileUri !== upd.newUri) {
          return { ...m, uploadedFileUri: upd.newUri } as ChatMessage;
        }
        return m;
      });
      await new Promise(r => setTimeout(r, 0));
    } catch { /* ignore */ }

    for (let attempt = 0; attempt < 3; attempt++) {
      const histForAssistantImgBase = historyForAssistantImageGen || getHistoryRespectingBookmark(messagesRef.current);
      let gpTextForAssistant: string | undefined = undefined;
      try {
        const gp3 = await getGlobalProfileDB();
        gpTextForAssistant = gp3?.text || undefined;
      } catch {}

      const assistantHistory = deriveHistoryForApi(histForAssistantImgBase, {
        maxMessages: computeMaxMessagesForArray(getHistoryRespectingBookmark(messagesRef.current).filter((m: ChatMessage) => m.role === 'user' || m.role === 'assistant')),
        maxMediaToKeep: MAX_MEDIA_TO_KEEP,
        contextSummary: resolveBookmarkContextSummary() || undefined,
        globalProfileText: gpTextForAssistant,
        placeholderLatestUserMessage: DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE,
      });
      const sanitizedAssistantHistoryForImage = await sanitizeHistoryWithVerifiedUris(assistantHistory as any);

      const prompt = IMAGE_GEN_USER_PROMPT_TEMPLATE.replace("{TEXT}", params.accumulatedFullText);
      const assistantImgGenResult = await generateImage({
        history: sanitizedAssistantHistoryForImage,
        latestMessageText: prompt,
        latestMessageRole: 'user',
        systemInstruction: IMAGE_GEN_SYSTEM_INSTRUCTION,
        maestroAvatarUri: maestroAvatarUriRef.current || undefined,
        maestroAvatarMimeType: maestroAvatarMimeTypeRef.current || undefined,
      });

      if ('base64Image' in assistantImgGenResult) {
        const duration = Date.now() - assistantStartTime;
        addImageLoadDuration(duration);
        try {
          const { optimized, upload } = await optimizeAndUploadMedia({
            dataUrl: assistantImgGenResult.base64Image as string,
            mimeType: assistantImgGenResult.mimeType as string,
            displayName: 'assistant-generated',
            setUploadPrepLabel: false,
          });

          updateMessage(params.thinkingMessageId, {
            imageUrl: assistantImgGenResult.base64Image,
            imageMimeType: assistantImgGenResult.mimeType,
            storageOptimizedImageUrl: optimized.dataUrl,
            storageOptimizedImageMimeType: optimized.mimeType,
            uploadedFileUri: upload.uri,
            uploadedFileMimeType: upload.mimeType,
            isGeneratingImage: false,
            imageGenError: null,
            imageGenerationStartTime: undefined
          });
        } catch (e) {
          updateMessage(params.thinkingMessageId, {
            imageUrl: assistantImgGenResult.base64Image,
            imageMimeType: assistantImgGenResult.mimeType,
            isGeneratingImage: false,
            imageGenError: null,
            imageGenerationStartTime: undefined
          });
        }
        break;
      } else if (attempt < 2) {
        await new Promise(r => setTimeout(r, 1500));
      } else {
        updateMessage(params.thinkingMessageId, {
          imageGenError: (assistantImgGenResult as any).error,
          isGeneratingImage: false,
          imageGenerationStartTime: undefined
        });
      }
    }
  }, [
    computeMaxMessagesForArray,
    ensureUrisForHistoryForSend,
    generateImage,
    getHistoryRespectingBookmark,
    maestroAvatarMimeTypeRef,
    maestroAvatarUriRef,
    messagesRef,
    optimizeAndUploadMedia,
    resolveBookmarkContextSummary,
    addImageLoadDuration,
    setSendPrep,
    t,
    updateMessage,
  ]);


  // Main send message handler
  const handleSendMessageInternal = useCallback(async (
    text: string,
    passedImageBase64?: string,
    passedImageMimeType?: string,
    messageType: 'user' | 'conversational-reengagement' | 'image-reengagement' = 'user'
  ): Promise<boolean> => {
    if (isLoadingHistoryRef.current) return false;
    const sanitizedText = stripBracketedContent(text);
    if (!sanitizedText && !passedImageBase64 && messageType === 'user') return false;
    if (!selectedLanguagePairRef.current) {
      console.error("No language pair selected, cannot send message.");
      addMessage({ role: 'error', text: t('error.noLanguagePair') });
      return false;
    }

    if (isSendingRef.current || speechIsSpeakingRef.current) {
      return false;
    }

    // Add sending token for unified busy state tracking (replaces setIsSending(true))
    sendingTokenRef.current = addActivityToken(TOKEN_CATEGORY.GEN, TOKEN_SUBTYPE.RESPONSE);
    if (settingsRef.current.stt.enabled && isListening) {
      try { stopListening(); } catch { /* ignore */ }
      sttInterruptedBySendRef.current = true;
      clearTranscript();
    } else {
      sttInterruptedBySendRef.current = false;
    }
    sendWithFileUploadInProgressRef.current = true;
    setReplySuggestions([]);
    // Clear any lingering suggestions token
    if (suggestionsTokenRef.current) {
      removeActivityToken(suggestionsTokenRef.current);
      suggestionsTokenRef.current = null;
    }
    lastFetchedSuggestionsForRef.current = null;

    if (messageType === 'user') {
      // Clear any previous snapshot errors
      if (setSnapshotUserError) setSnapshotUserError(null);
    }
    pendingRecordedAudioMessageRef.current = null;

    const currentSettingsVal = settingsRef.current;
    const shouldGenerateUserImage = currentSettingsVal.selectedCameraId === IMAGE_GEN_CAMERA_ID;
    const userMessageContext = await createUserMessage({
      sanitizedText,
      passedImageBase64,
      passedImageMimeType,
      messageType,
      shouldGenerateUserImage,
      currentSettingsVal,
    });
    let {
      userMessageId,
      userMessageText,
      userImageToProcessBase64,
      userImageToProcessMimeType,
      userImageToProcessStorageOptimizedBase64,
      userImageToProcessStorageOptimizedMimeType,
    } = userMessageContext;

    const thinkingMessageId = addMessage({ role: 'assistant', thinking: true });

    cancelReengagementRef.current();

    let historyForGemini = messagesRef.current.filter(m => m.id !== thinkingMessageId);
    if (messageType === 'user' && userMessageId) {
      historyForGemini = historyForGemini.filter(m => m.id !== userMessageId);
    }

    let geminiPromptText: string;
    let systemInstructionForGemini: string = currentSystemPromptText;
    try {
      await getGlobalProfileDB();
    } finally {
      systemInstructionForGemini = composeMaestroSystemInstruction(systemInstructionForGemini);
    }

    // Optimize user image if needed
    if (messageType === 'user' && userImageToProcessBase64 && !userImageToProcessStorageOptimizedBase64 && userImageToProcessMimeType) {
      if (!sendWithFileUploadInProgressRef.current) {
        sendWithFileUploadInProgressRef.current = true;
      }
      try {
        setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...' });
        const optimized = await processMediaForUpload(userImageToProcessBase64, userImageToProcessMimeType, {
          t,
          onProgress: (label, done, total, etaMs) => setSendPrep({ active: true, label, done, total, etaMs })
        });
        userImageToProcessStorageOptimizedBase64 = optimized.dataUrl;
        userImageToProcessStorageOptimizedMimeType = optimized.mimeType;

        if (messageType === 'user' && userMessageId) {
          updateMessage(userMessageId, { storageOptimizedImageUrl: optimized.dataUrl, storageOptimizedImageMimeType: optimized.mimeType });
        }
      } catch (e) { 
        console.warn('Failed to derive low-res for current user media, will omit persistence media', e); 
      } finally { 
        setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...' } : prev)); 
      }
    }

    // Decide which image to upload to Gemini
    let imageForGeminiContextBase64: string | undefined;
    let imageForGeminiContextMimeType: string | undefined;

    if (messageType === 'user') {
      if (userImageToProcessBase64) {
        imageForGeminiContextBase64 = userImageToProcessBase64;
        imageForGeminiContextMimeType = userImageToProcessMimeType;
      } else {
        imageForGeminiContextBase64 = userImageToProcessStorageOptimizedBase64;
        imageForGeminiContextMimeType = userImageToProcessStorageOptimizedMimeType;
      }
    } else {
      imageForGeminiContextBase64 = (typeof passedImageBase64 === 'string' && passedImageBase64) ? passedImageBase64 : undefined;
      imageForGeminiContextMimeType = (typeof passedImageMimeType === 'string' && passedImageMimeType) ? passedImageMimeType : undefined;
    }

    let imageForGeminiContextFileUri: string | undefined = undefined;

    switch (messageType) {
      case 'image-reengagement':
        geminiPromptText = "...";
        break;
      case 'conversational-reengagement':
        geminiPromptText = "...";
        imageForGeminiContextBase64 = undefined;
        imageForGeminiContextMimeType = undefined;
        break;
      case 'user':
      default:
        geminiPromptText = userMessageText;
        break;
    }

    // For image-reengagement, use original image for API (full quality)
    // Note: re-engagement images are transient (not persisted in chat), so no storage optimization needed
    if (messageType === 'image-reengagement') {
      if (typeof passedImageBase64 === 'string' && passedImageBase64 && typeof passedImageMimeType === 'string' && passedImageMimeType) {
        imageForGeminiContextBase64 = passedImageBase64;
        imageForGeminiContextMimeType = passedImageMimeType;
      }
    }

    // Upload current image to Files API
    if (imageForGeminiContextBase64 && imageForGeminiContextMimeType) {
      try {
        if (!sendWithFileUploadInProgressRef.current) {
          sendWithFileUploadInProgressRef.current = true;
        }
        setSendPrep({ active: true, label: t('chat.sendPrep.uploadingMedia') || 'Uploading media...' });
        const up = await uploadMediaToFiles(imageForGeminiContextBase64, imageForGeminiContextMimeType, 'current-user-media');
        // CRITICAL: Use the normalized MIME type returned from upload to avoid mismatch errors
        // The upload normalizes audio/webm;codecs=opus -> audio/webm, so we must use up.mimeType
        imageForGeminiContextMimeType = up.mimeType;
        if (messageType === 'user' && userMessageId) {
          const existing = (messagesRef.current || []).find(m => m.id === userMessageId);
          const hasExisting = !!(existing && typeof (existing as any).uploadedFileUri === 'string' && (existing as any).uploadedFileUri);
          if (!hasExisting) {
            updateMessage(userMessageId, {
              uploadedFileUri: up.uri,
              uploadedFileMimeType: up.mimeType,
            });
            imageForGeminiContextFileUri = up.uri;
          } else {
            imageForGeminiContextFileUri = (existing as any).uploadedFileUri as string;
            // Also use the existing mime type if we're reusing an existing URI
            imageForGeminiContextMimeType = (existing as any).uploadedFileMimeType as string || imageForGeminiContextMimeType;
          }
        } else {
          imageForGeminiContextFileUri = up.uri;
        }
      } catch (e) {
        console.warn('Failed to upload current media to Files API; will send without media');
        imageForGeminiContextFileUri = undefined;
      } finally {
        setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...' } : prev));
      }
    }

    setLatestGroundingChunks(undefined);

    try {
      const historySubsetForSend: ChatMessage[] = getHistoryRespectingBookmark(historyForGemini);
      setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...', done: 0, total: 0 });
      
      let ensuredUpdates: Record<string, { oldUri?: string; newUri: string }> = {};
      try {
        ensuredUpdates = await ensureUrisForHistoryForSend(historySubsetForSend, (done, total, etaMs) => {
          setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...', done, total, etaMs });
        });
      } finally {
        setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.finalizing') || 'Finalizing...' } : prev));
      }

      let historyForGeminiPostEnsure = messagesRef.current.filter(m => m.id !== thinkingMessageId);
      if (messageType === 'user' && userMessageId) {
        historyForGeminiPostEnsure = historyForGeminiPostEnsure.filter(m => m.id !== userMessageId);
      }
      const historySubsetForSendFinal: ChatMessage[] = getHistoryRespectingBookmark(historyForGeminiPostEnsure)
        .map((m: ChatMessage) => {
          if (ensuredUpdates[m.id]?.newUri) {
            const nu = ensuredUpdates[m.id].newUri;
            if ((m as any).uploadedFileUri !== nu) {
              return { ...m, uploadedFileUri: nu } as ChatMessage;
            }
          }
          return m;
        });

      try {
        for (const m of historySubsetForSendFinal) {
          const upd = ensuredUpdates[m.id];
          if (upd && upd.newUri && (m as any).uploadedFileUri !== upd.newUri) {
            (m as any).uploadedFileUri = upd.newUri;
          }
        }
      } catch {}

      let globalProfileText: string | undefined = undefined;
      try {
        const gp2 = await getGlobalProfileDB();
        globalProfileText = gp2?.text || undefined;
      } catch {}

      const derivedHistory = deriveHistoryForApi(historySubsetForSendFinal, {
        maxMessages: computeMaxMessagesForArray(historySubsetForSendFinal.filter((m: ChatMessage) => m.role === 'user' || m.role === 'assistant')),
        maxMediaToKeep: MAX_MEDIA_TO_KEEP,
        contextSummary: resolveBookmarkContextSummary() || undefined,
        globalProfileText,
      });
      const sanitizedDerivedHistory = await sanitizeHistoryWithVerifiedUris(derivedHistory as any);

      // User image generation for AI Camera mode
      const userImageContext = await runUserImageGeneration({
        shouldGenerateUserImage,
        currentSettingsVal,
        messageType,
        userMessageText,
        userMessageId,
        userImageToProcessBase64,
        sanitizedDerivedHistory,
      });
      if (userImageContext.imageForGeminiContextFileUri) {
        imageForGeminiContextFileUri = userImageContext.imageForGeminiContextFileUri;
        imageForGeminiContextMimeType = userImageContext.imageForGeminiContextMimeType;
      }

      const { accumulatedFullText, finalMessageUpdates } = await handleGeminiResponse({
        thinkingMessageId,
        geminiPromptText,
        sanitizedDerivedHistory,
        systemInstructionForGemini,
        imageForGeminiContextMimeType,
        imageForGeminiContextFileUri,
        currentSettingsVal,
      });

      // Early suggestion fetch
      try {
        const textForSuggestionsEarly = finalMessageUpdates.rawAssistantResponse || (finalMessageUpdates.translations?.find(tr => tr.target)?.target) || "";
        // Check if already loading suggestions via token
        if (!suggestionsTokenRef.current && textForSuggestionsEarly.trim()) {
          const historyWithFinalAssistant = messagesRef.current.map(m =>
            m.id === thinkingMessageId ? ({ ...m, ...finalMessageUpdates }) : m
          );
          requestReplySuggestions(thinkingMessageId, textForSuggestionsEarly, getHistoryRespectingBookmark(historyWithFinalAssistant));
        }
      } catch (e) {
        console.warn('Failed to prefetch suggestions before TTS:', e);
      }

      // Speak the response
      const originalMessage = messagesRef.current.find(m => m.id === thinkingMessageId);
      if (originalMessage) {
        const finalMessageForSpeech = { ...originalMessage, ...finalMessageUpdates };
        speakMessage(finalMessageForSpeech);
      }

      if (messageType === 'user') {
        setAttachedImage(null, null);
        if (sanitizedText === stripBracketedContent(transcript || '') && (transcript || '').length > 0) {
          clearTranscript();
        }
      }

      await new Promise(resolve => setTimeout(resolve, 0));

      // Image generation for assistant response
      await runAssistantImageGeneration({
        thinkingMessageId,
        accumulatedFullText,
        currentSettingsVal,
      });

      try {
        sendWithFileUploadInProgressRef.current = false;
      } catch { /* ignore */ }
      // Remove sending token (replaces setIsSending(false))
      if (sendingTokenRef.current) {
        removeActivityToken(sendingTokenRef.current);
        sendingTokenRef.current = null;
      }
      setSendPrep(null);
      scheduleReengagementRef.current('send-complete');

      // Resume STT if needed
      const isSpeechActive = speechIsSpeakingRef.current || (typeof hasPendingQueueItems === 'function' && hasPendingQueueItems());
      if (sttInterruptedBySendRef.current && settingsRef.current.stt.enabled && !isSpeechActive) {
        try {
          startListening(settingsRef.current.stt.language);
        } finally {
          sttInterruptedBySendRef.current = false;
        }
      }

      // Fetch suggestions if TTS not supported
      if (!isSpeechSynthesisSupported) {
        const finalAssistantMessage = messagesRef.current.find(m => m.id === thinkingMessageId);
        if (finalAssistantMessage && finalAssistantMessage.role === 'assistant' &&
          (finalAssistantMessage.rawAssistantResponse || (finalAssistantMessage.translations && finalAssistantMessage.translations.length > 0)) &&
          !suggestionsTokenRef.current &&
          finalAssistantMessage.id !== lastFetchedSuggestionsForRef.current) {
          const textForSuggestions = finalAssistantMessage.rawAssistantResponse ||
            (finalAssistantMessage.translations?.find(tr => tr.target)?.target) || "";
          if (textForSuggestions.trim()) {
            requestReplySuggestions(finalAssistantMessage.id, textForSuggestions, getHistoryRespectingBookmark(messagesRef.current));
          }
        }
      }

      return true;

    } catch (error) {
      console.error("Error sending message (stream consumer):", error);
      let errorMessage = t('general.error');
      if (error instanceof ApiError) {
        errorMessage = error.message || error.code || `HTTP ${error.status}`;
      } else if (error instanceof Error) {
        errorMessage = error.message;
      }
      updateMessage(thinkingMessageId, {
        thinking: false, 
        role: 'error', 
        text: errorMessage, 
        rawAssistantResponse: undefined, 
        translations: undefined,
      });
      // Remove sending token on error (replaces setIsSending(false))
      if (sendingTokenRef.current) {
        removeActivityToken(sendingTokenRef.current);
        sendingTokenRef.current = null;
      }
      setSendPrep(null);
      try {
        sendWithFileUploadInProgressRef.current = false;
      } catch { /* ignore */ }

      // Resume STT on error
      if (sttInterruptedBySendRef.current && settingsRef.current.stt.enabled && !speechIsSpeakingRef.current) {
        try {
          startListening(settingsRef.current.stt.language);
        } finally {
          sttInterruptedBySendRef.current = false;
        }
      }

      scheduleReengagementRef.current('send-error');

      if (messageType === 'user') {
        setAttachedImage(null, null);
      }
      setReplySuggestions([]);
      // Clear any suggestions token on error
      if (suggestionsTokenRef.current) {
        removeActivityToken(suggestionsTokenRef.current);
        suggestionsTokenRef.current = null;
      }
      if (isMountedRef.current) {
        speechIsSpeakingRef.current = false;
      }
      return false;
    }
  }, [
    t,
    addMessage,
    updateMessage,
    settingsRef,
    selectedLanguagePairRef,
    messagesRef,
    isLoadingHistoryRef,
    createUserMessage,
    cancelReengagementRef,
    scheduleReengagementRef,
    getHistoryRespectingBookmark,
    computeMaxMessagesForArray,
    ensureUrisForHistoryForSend,
    resolveBookmarkContextSummary,
    handleGeminiResponse,
    runUserImageGeneration,
    runAssistantImageGeneration,
    requestReplySuggestions,
    stripBracketedContent,
    speakMessage,
    isSpeechSynthesisSupported,
    isListening,
    stopListening,
    startListening,
    clearTranscript,
    hasPendingQueueItems,
    sttInterruptedBySendRef,
    pendingRecordedAudioMessageRef,
    speechIsSpeakingRef,
    currentSystemPromptText,
    attachedImageBase64,
    attachedImageMimeType,
    setAttachedImage,
    addActivityToken,
    removeActivityToken,
    setLatestGroundingChunks,
    setReplySuggestions,
    setSendPrep,
    setSnapshotUserError,
    transcript,
    lastFetchedSuggestionsForRef,
  ]);

  // Keep ref updated
  useEffect(() => {
    handleSendMessageInternalRef.current = handleSendMessageInternal;
  }, [handleSendMessageInternal]);

  return {
    isSending,
    isSendingRef,
    sendPrep,
    latestGroundingChunks,
    maestroActivityStage,
    isCreatingSuggestion,
    imageLoadDurations,
    
    handleSendMessageInternal,
    handleSendMessageInternalRef,
    
    fetchAndSetReplySuggestions,
    handleCreateSuggestion,
    handleSuggestionInteraction,
    
    setMaestroActivityStage,
    
    parseGeminiResponse,
    stripBracketedContent,
    resolveBookmarkContextSummary,
    ensureUrisForHistoryForSend,
    computeHistorySubsetForMedia,
    handleReengagementThresholdChange,
    calculateEstimatedImageLoadTime,
  };
};

export default useTutorConversation;
