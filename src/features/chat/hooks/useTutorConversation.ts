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
  AppSettings,
  RecordedUtterance 
} from '../../../core/types';
import { ApiError } from '../../../api/gemini/client';
import { generateGeminiResponse, translateText, type GeminiProgressEvent } from '../../../api/gemini/generative';
import { sanitizeHistoryWithVerifiedUris, uploadMediaToFiles, checkFileStatuses } from '../../../api/gemini/files';
import { generateImage } from '../../../api/gemini/vision';
import { ensureMaestroAvatarUris, invalidateMaestroAvatarCache } from '../../../api/gemini/maestroAvatarEnsure';
import { getGlobalProfileDB, setGlobalProfileDB, setAppSettingsDB } from '../../session';
import { safeSaveChatHistoryDB, deriveHistoryForApi, INLINE_CAP_AUDIO } from '..';
import { processMediaForUpload, createKeyframeFromVideoDataUrl } from '../../vision';
import { parseAssistantResponseForAttachment } from '../utils/assistantResponseAttachments';
import { getOfficePreview } from '../utils/officePreview';
import { isGoogleWorkspaceShortcutMimeType, isMicrosoftOfficeMimeType } from '../utils/fileAttachments';
import { 
  IMAGE_GEN_CAMERA_ID,
  MAX_MEDIA_TO_KEEP 
} from '../../../core/config/app';
import { getGeminiModels } from '../../../core/config/models';
import { 
  DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE, 
  IMAGE_GEN_SYSTEM_INSTRUCTION, 
  IMAGE_GEN_USER_PROMPT_TEMPLATE,
  IMAGE_GEN_COPYRIGHT_AVOIDANCE_INSTRUCTION,
  composeMaestroSystemInstruction 
} from '../../../core/config/prompts';
import { isRealChatMessage } from '../../../shared/utils/common';
import { trackTokenUsage, trackImageGeneration, hasShownCostWarning, setCostWarningShown } from '../../../shared/utils/costTracker';
import { createSmartRef } from '../../../shared/utils/smartRef';
import { getPrimarySubtag, getShortLangCodeForPrompt } from '../../../shared/utils/languageUtils';
import type { TranslationFunction } from '../../../app/hooks/useTranslations';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../core/config/activityTokens';
import { useMaestroStore } from '../../../store';
import { useShallow } from 'zustand/shallow';
import { selectIsSending, selectIsLoadingSuggestions, selectIsCreatingSuggestion, selectIsSpeaking } from '../../../store/slices/uiSlice';
import { selectSelectedLanguagePair } from '../../../store/slices/settingsSlice';

const parseApiErrorMessage = (message?: string): string => {
  if (!message) return '';
  const trimmed = message.trim();
  if (!trimmed) return '';
  try {
    const parsed = JSON.parse(trimmed);
    const nestedMessage = parsed?.error?.message;
    if (typeof nestedMessage === 'string' && nestedMessage.trim()) {
      return nestedMessage.trim();
    }
  } catch {
    // Ignore JSON parse errors
  }
  return trimmed;
};

const isQuotaError = (error: ApiError): boolean => {
  if (error.status === 429) return true;
  const code = (error.code ?? '').toString().toLowerCase();
  if (code === '429' || code.includes('resource_exhausted') || code.includes('quota')) return true;
  const msg = parseApiErrorMessage(error.message).toLowerCase();
  return (
    msg.includes('resource_exhausted') ||
    msg.includes('exceeded your current quota') ||
    msg.includes('rate limit') ||
    msg.includes('quota')
  );
};

const isInvalidApiKeyError = (error: ApiError): boolean => {
  const msg = (error.message || '').toLowerCase();
  // Check for both the RPC reason and the message content
  return msg.includes('api_key_invalid') || msg.includes('api key not valid');
};

const isSvgMimeType = (mimeType?: string | null): boolean => {
  return (mimeType || '').trim().toLowerCase() === 'image/svg+xml';
};

const MAX_THINKING_TRACE_LINES = 8;
const MAX_THINKING_DRAFT_CHARS = 12000;
const THINKING_DRAFT_FLUSH_INTERVAL_MS = 120;

const isOfficeMimeUnsupportedByGemini = (mimeType?: string | null): boolean => {
  const normalized = (mimeType || '').trim().toLowerCase();
  if (!normalized) return false;
  return isMicrosoftOfficeMimeType(normalized) || isGoogleWorkspaceShortcutMimeType(normalized);
};

const toUtf8Base64DataUrl = (mimeType: string, text: string): string => {
  const bytes = new TextEncoder().encode(text);
  let binary = '';
  for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
  return `data:${mimeType};charset=utf-8;base64,${btoa(binary)}`;
};

type MediaPayloadOverride = {
  oldUri?: string;
  newUri?: string;
  newMimeType?: string;
  transient?: boolean;
  omitFromPayload?: boolean;
};

export interface UseTutorConversationConfig {
  // Translation function
  t: TranslationFunction;
  
  // Settings
  setSettings: (settings: AppSettings | ((prev: AppSettings) => AppSettings)) => void;
  
  // Chat store
  addMessage: (message: Omit<ChatMessage, 'id' | 'timestamp'>) => string;
  updateMessage: (messageId: string, updates: Partial<ChatMessage>) => void;
  setMessages: (messages: ChatMessage[] | ((prev: ChatMessage[]) => ChatMessage[])) => void;
  getHistoryRespectingBookmark: (arr: ChatMessage[]) => ChatMessage[];
  computeMaxMessagesForArray: (arr: ChatMessage[]) => number | undefined;
  
  // Hardware
  captureSnapshot: (isForReengagement?: boolean) => Promise<{ base64: string; mimeType: string; storageOptimizedBase64: string; storageOptimizedMimeType: string } | null>;
  
  // Speech
  speakMessage: (message: ChatMessage) => void;
  isSpeechSynthesisSupported: boolean;
  isListening: boolean;
  stopListening: () => void;
  startListening: (lang: string) => void;
  clearTranscript: () => void;
  hasPendingQueueItems: () => boolean;
  claimRecordedUtterance: () => RecordedUtterance | null;
  
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

  // Api key gate
  onApiKeyGateOpen?: (options?: { reason?: 'missing' | 'invalid' | 'quota'; instructionIndex?: number }) => void;
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
  resolveBookmarkContextSummary: () => string | null;
  ensureUrisForHistoryForSend: (arr: ChatMessage[], onProgress?: (done: number, total: number, etaMs?: number) => void) => Promise<Record<string, MediaPayloadOverride>>;
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
    onApiKeyGateOpen,
  } = config;

  const setLastFetchedSuggestionsFor = useMaestroStore(state => state.setLastFetchedSuggestionsFor);
  const setRecordedUtterancePending = useMaestroStore(state => state.setRecordedUtterancePending);
  const setPendingRecordedAudioMessageId = useMaestroStore(state => state.setPendingRecordedAudioMessageId);
  const setSttInterruptedBySend = useMaestroStore(state => state.setSttInterruptedBySend);

  // Smart refs - always return fresh state from store (no stale closures)
  const settingsRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.settings), []);
  const selectedLanguagePairRef = useMemo(() => createSmartRef(useMaestroStore.getState, selectSelectedLanguagePair), []);
  const messagesRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.messages), []);
  const isLoadingHistoryRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.isLoadingHistory), []);
  const speechIsSpeakingRef = useMemo(() => createSmartRef(useMaestroStore.getState, selectIsSpeaking), []);

  // Smart refs with setters - these need custom implementation for write support
  const lastFetchedSuggestionsForRef = useMemo<React.MutableRefObject<string | null>>(() => ({
    get current() {
      return useMaestroStore.getState().lastFetchedSuggestionsFor;
    },
    set current(value) {
      setLastFetchedSuggestionsFor(value);
    },
  }), [setLastFetchedSuggestionsFor]);

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
    attachedFileName,
  } = useMaestroStore(useShallow(state => ({
    sendPrep: state.sendPrep,
    latestGroundingChunks: state.latestGroundingChunks,
    maestroActivityStage: state.maestroActivityStage,
    imageLoadDurations: state.imageLoadDurations,
    attachedImageBase64: state.attachedImageBase64,
    attachedImageMimeType: state.attachedImageMimeType,
    attachedFileName: state.attachedFileName,
  })));

  // Derive activity states from tokens using selectors
  const isSending = useMaestroStore(selectIsSending);
  // Note: isSpeaking derived from tokens is available via speechIsSpeakingRef passed from props
  const isLoadingSuggestions = useMaestroStore(selectIsLoadingSuggestions);
  const isCreatingSuggestion = useMaestroStore(selectIsCreatingSuggestion);

  const setSendPrep = useMaestroStore(state => state.setSendPrep);
  const setLatestGroundingChunks = useMaestroStore(state => state.setLatestGroundingChunks);
  const setSuggestionsLoadingStreamText = useMaestroStore(state => state.setSuggestionsLoadingStreamText);
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

  // Invalidate avatar cache when avatar changes
  useEffect(() => {
    const handler = () => { invalidateMaestroAvatarCache(); };
    window.addEventListener('maestro-avatar-updated', handler);
    return () => window.removeEventListener('maestro-avatar-updated', handler);
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

  const appendThinkingTrace = useCallback((messageId: string, line: string) => {
    const cleanedLine = line.trim();
    if (!cleanedLine) return;

    const current = messagesRef.current.find(m => m.id === messageId);
    if (!current || !current.thinking) return;

    const prevTrace = Array.isArray(current.thinkingTrace)
      ? current.thinkingTrace.filter(item => typeof item === 'string' && item.trim().length > 0)
      : [];
    if (prevTrace[prevTrace.length - 1] === cleanedLine) return;

    const nextTrace = [...prevTrace, cleanedLine].slice(-MAX_THINKING_TRACE_LINES);
    updateMessage(messageId, { thinkingTrace: nextTrace });
  }, [messagesRef, updateMessage]);

  const formatGeminiProgressLine = useCallback((event: GeminiProgressEvent): string | undefined => {
    const elapsedSeconds = typeof event.elapsedMs === 'number'
      ? Math.max(0, Math.floor(event.elapsedMs / 1000))
      : undefined;

    switch (event.phase) {
      case 'attempt-start':
        return `Connecting to ${event.model} (attempt ${event.attempt}/${event.totalAttempts})...`;
      case 'attempt-processing':
        if (typeof elapsedSeconds !== 'number') return undefined;
        return `Model is processing... ${elapsedSeconds}s elapsed.`;
      case 'high-demand':
        return 'High demand detected. Request is queued on Google servers.';
      case 'fallback-switch':
        return `Switching to fallback model ${event.model}.`;
      case 'retry-scheduled':
        return `Retrying in ${Math.ceil((event.retryInMs || 0) / 1000)}s...`;
      case 'success':
        if (typeof elapsedSeconds === 'number' && elapsedSeconds > 0) {
          return `Response received after ${elapsedSeconds}s.`;
        }
        return 'Response received.';
      default:
        return undefined;
    }
  }, []);

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
  ): Promise<Record<string, MediaPayloadOverride>> => {
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
      const cachedMimeRequiresUpgrade0 = isSvgMimeType(cachedMime0) || isOfficeMimeUnsupportedByGemini(cachedMime0);
      const missing = !(cachedUri0 && cachedMime0) || cachedMimeRequiresUpgrade0;
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

    const updatedUriMap: Record<string, MediaPayloadOverride> = {};
    for (let idx = 0; idx < candidates.length; idx++) {
      if (!keepMediaIdx.has(idx)) continue;
      const m = candidates[idx];
      const llmUrl = (m as any).storageOptimizedImageUrl as string | undefined;
      const llmMime = (m as any).storageOptimizedImageMimeType as string | undefined;
      const uiUrl = m.imageUrl as string | undefined;
      const uiMime = m.imageMimeType as string | undefined;
      const cachedUri = (m as any).uploadedFileUri as string | undefined;
      const cachedMime = (m as any).uploadedFileMimeType as string | undefined;
      const cachedMimeRequiresUpgrade = isSvgMimeType(cachedMime) || isOfficeMimeUnsupportedByGemini(cachedMime);
      
      // Check if we have any data URL to potentially re-upload from
      const hasDataUrl = !!(llmUrl && llmMime) || !!(uiUrl && uiMime);
      
      // Check if the cached URI is expired/deleted
      let cachedDeleted = false;
      if (cachedUri && cachedMime) {
        try {
          const st = cachedStatuses && cachedStatuses[cachedUri];
          cachedDeleted = !!(st && st.deleted);
        } catch { cachedDeleted = false; }
      }
      
      // CRITICAL: If URI is expired but we have no data URL to re-upload, media will be lost
      if (cachedDeleted && !hasDataUrl) {
        console.warn(`[ensureUris] Message ${m.id} has expired uploadedFileUri but no data URL to re-upload. Media will be omitted from API payload.`);
        updatedUriMap[m.id] = { oldUri: cachedUri, transient: true, omitFromPayload: true };
        continue;
      }
      if (cachedMimeRequiresUpgrade && !hasDataUrl) {
        console.warn(`[ensureUris] Message ${m.id} has unsupported upload MIME metadata but no local data URL for conversion. Omitting media from this payload only.`);
        updatedUriMap[m.id] = { oldUri: cachedUri, transient: true, omitFromPayload: true };
        continue;
      }
      
      // Skip if no data URL available (nothing to upload)
      if (!hasDataUrl) continue;
      
      // Skip if we have a valid (non-expired) cached URI
      if (cachedUri && cachedMime && !cachedDeleted && !cachedMimeRequiresUpgrade) continue;

      let dataForUpload: { dataUrl: string; mimeType: string } | null = null;
      try {
        if (llmUrl && llmMime) {
          dataForUpload = { dataUrl: llmUrl, mimeType: llmMime };
        } else if (uiUrl && uiMime) {
          // Upload ORIGINAL to API for best quality, optimize only for local storage
          dataForUpload = { dataUrl: uiUrl, mimeType: uiMime };
          // Keep original SVG payload intact to avoid reload-time fidelity/encoding loss.
          if (!isSvgMimeType(uiMime)) {
            try {
              const optimized = await processMediaForUpload(uiUrl, uiMime, { t });
              updateMessage(m.id, { storageOptimizedImageUrl: optimized.dataUrl, storageOptimizedImageMimeType: optimized.mimeType });
            } catch { /* optimization for storage is optional */ }
          }
        }
        if (dataForUpload) {
          if (isOfficeMimeUnsupportedByGemini(dataForUpload.mimeType)) {
            try {
              const preview = await getOfficePreview(dataForUpload.dataUrl, dataForUpload.mimeType, m.attachmentName);
              const extracted = (preview.text || '').trim();
              if (!extracted) {
                throw new Error(preview.note || 'No extracted Office text available for Gemini upload conversion.');
              }
              dataForUpload = {
                dataUrl: toUtf8Base64DataUrl('text/plain', extracted),
                mimeType: 'text/plain',
              };
            } catch (officeErr) {
              console.warn(`[ensureUris] Failed to convert Office document to text for message ${m.id}; omitting this media from API payload.`, officeErr);
              updatedUriMap[m.id] = { oldUri: cachedUri, transient: true, omitFromPayload: true };
              continue;
            }
          }
          if (isSvgMimeType(dataForUpload.mimeType)) {
            try {
              const rasterized = await processMediaForUpload(dataForUpload.dataUrl, dataForUpload.mimeType, { t });
              if (!rasterized.dataUrl || !rasterized.mimeType || !rasterized.mimeType.startsWith('image/') || isSvgMimeType(rasterized.mimeType)) {
                throw new Error(`SVG rasterization did not produce a supported image MIME: ${rasterized.mimeType}`);
              }
              dataForUpload = { dataUrl: rasterized.dataUrl, mimeType: rasterized.mimeType };
            } catch (svgErr) {
              console.warn(`[ensureUris] Failed to convert SVG to JPEG for message ${m.id}; omitting this media from API payload.`, svgErr);
              updatedUriMap[m.id] = { oldUri: cachedUri, transient: true, omitFromPayload: true };
              continue;
            }
          }
          const up = await uploadMediaToFiles(
            dataForUpload.dataUrl,
            dataForUpload.mimeType,
            m.attachmentName || 'send-history'
          );
          // Persist all successful uploads (including SVG/Office surrogates) so we do not
          // re-convert/re-upload the same message media on every send.
          updateMessage(m.id, { uploadedFileUri: up.uri, uploadedFileMimeType: up.mimeType });
          (m as any).uploadedFileUri = up.uri;
          (m as any).uploadedFileMimeType = up.mimeType;
          updatedUriMap[m.id] = { oldUri: cachedUri, newUri: up.uri, newMimeType: up.mimeType };
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
      setAppSettingsDB(next).catch(() => {});
      return next;
    });
  }, [setSettings]);

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
      setSuggestionsLoadingStreamText('');
      if (suggestionsTokenRef.current) {
        removeActivityToken(suggestionsTokenRef.current);
        suggestionsTokenRef.current = null;
      }
      return;
    }
    if (!lastTutorMessage.trim() || !selectedLanguagePairRef.current) {
      setReplySuggestions([]);
      setSuggestionsLoadingStreamText('');
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
          setSuggestionsLoadingStreamText('');
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
    setSuggestionsLoadingStreamText('');

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
        let thoughtText = '';
        let outputText = '';
        const flushSuggestionsLoadingText = () => {
          // Show whichever stream is latest, condensed to a short single-line status
          const condensedThought = thoughtText.replace(/\s+/g, ' ').trim();
          const condensedOutput = outputText.replace(/\s+/g, ' ').trim();
          // Prefer output stream if available, otherwise show thought stream
          const active = condensedOutput || condensedThought;
          if (active) {
            const label = condensedOutput ? '' : 'thinking: ';
            const display = active.length > 48 ? `\u2026${active.slice(-48)}` : active;
            setSuggestionsLoadingStreamText(`${label}${display}`);
          }
        };

        const response = await generateGeminiResponse(
          getGeminiModels().text.aux,
          suggestionPrompt,
          [],
          undefined,
          undefined,
          undefined,
          undefined,
          false,
          { responseMimeType: "application/json" },
          undefined,
          {
            onProgress: (event) => {
              const progressLine = formatGeminiProgressLine(event);
              if (progressLine && !thoughtText.trim() && !outputText.trim()) {
                setSuggestionsLoadingStreamText(progressLine);
              }
            },
            onThoughtDelta: (_deltaThought, fullThought) => {
              thoughtText = fullThought || thoughtText;
              flushSuggestionsLoadingText();
            },
            onTextDelta: (_deltaText, fullText) => {
              outputText = fullText || outputText;
              flushSuggestionsLoadingText();
            },
          }
        );

        trackTokenUsage(getGeminiModels().text.aux, response.usageMetadata);

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

    setSuggestionsLoadingStreamText('');
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
    setSuggestionsLoadingStreamText,
    formatGeminiProgressLine,
    updateMessage
  ]);

  const handleCreateSuggestion = useCallback(async (textToTranslate: string) => {

    if (!textToTranslate || !selectedLanguagePairRef.current) return;

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
      const { translatedText, usageMetadata } = await translateText(textToTranslate, fromLangName, toLangName);
      trackTokenUsage(getGeminiModels().text.translation, usageMetadata);
      const newSuggestion: ReplySuggestion = {
        target: originalTextIsTarget ? textToTranslate : translatedText,
        native: originalTextIsTarget ? translatedText : textToTranslate,
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
  }, [addMessage, t, selectedLanguagePairRef, settingsRef, lastFetchedSuggestionsForRef, messagesRef, setMessages, setReplySuggestions, handleToggleSuggestionModeRef, addActivityToken, removeActivityToken]);

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
    text: string;
    passedImageBase64?: string;
    passedImageMimeType?: string;
    messageType: 'user' | 'conversational-reengagement' | 'image-reengagement';
    shouldGenerateUserImage: boolean;
    currentSettingsVal: AppSettings;
  }) => {
    let userMessageId: string | null = null;
    let userMessageText = params.text;
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
        setSendPrep({ active: true, label: t('chat.sendPrep.optimizingImage') || 'Optimizing...' });
        const optimized = await processMediaForUpload(attachedImageBase64, attachedImageMimeType, {
          t,
          onProgress: (label, done, total, etaMs) => {
            setSendPrep({ active: true, label, done, total, etaMs });
          }
        });
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
      attachmentName: attachedFileName || undefined,
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
    attachedFileName,
    captureSnapshot,
    claimRecordedUtterance,
    recordedUtterancePendingRef,
    sendWithFileUploadInProgressRef,
    setSendPrep,
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
    let lastProcessingBucket = -1;
    let streamingDraftText = '';
    let lastDraftFlushAt = 0;
    let thoughtBuffer = '';
    let lastThoughtFlushAt = 0;
    let currentPhaseLabel = '';

    const flushThinkingDraft = (force = false) => {
      const now = Date.now();
      if (!force && now - lastDraftFlushAt < THINKING_DRAFT_FLUSH_INTERVAL_MS) return;
      lastDraftFlushAt = now;
      const draftToShow = streamingDraftText.slice(-MAX_THINKING_DRAFT_CHARS);
      const current = messagesRef.current.find(m => m.id === params.thinkingMessageId);
      if (!current || !current.thinking) return;
      if ((current.thinkingDraftText || '') === draftToShow) return;
      updateMessage(params.thinkingMessageId, { thinkingDraftText: draftToShow });
    };

    const flushThoughtTrace = (force = false) => {
      const condensed = thoughtBuffer.replace(/\s+/g, ' ').trim();
      if (!condensed) return;
      const now = Date.now();
      if (!force && condensed.length < 80 && now - lastThoughtFlushAt < 2000) return;
      appendThinkingTrace(params.thinkingMessageId, `Model thought: ${condensed.slice(0, 220)}`);
      thoughtBuffer = '';
      lastThoughtFlushAt = now;
    };

    const response = await generateGeminiResponse(
      getGeminiModels().text.default,
      params.geminiPromptText,
      params.sanitizedDerivedHistory,
      params.systemInstructionForGemini,
      undefined,
      params.imageForGeminiContextMimeType,
      params.imageForGeminiContextFileUri,
      params.currentSettingsVal.enableGoogleSearch,
      undefined,
      undefined,
      {
        onProgress: (event) => {
          if (event.phase === 'attempt-processing') {
            const bucket = Math.floor((event.elapsedMs || 0) / 12000);
            if (bucket <= 0 || bucket === lastProcessingBucket) return;
            lastProcessingBucket = bucket;
          }
          const line = formatGeminiProgressLine(event);
          if (line) {
            appendThinkingTrace(params.thinkingMessageId, line);
            if (line !== currentPhaseLabel) {
              currentPhaseLabel = line;
              updateMessage(params.thinkingMessageId, { thinkingPhase: line });
            }
          }
        },
        onTextDelta: (_deltaText, fullText) => {
          streamingDraftText = fullText || streamingDraftText;
          if (currentPhaseLabel !== 'Final response') {
            currentPhaseLabel = 'Final response';
            updateMessage(params.thinkingMessageId, { thinkingPhase: 'Final response' });
          }
          flushThinkingDraft(false);
        },
        onThoughtDelta: (deltaThought) => {
          thoughtBuffer += deltaThought;
          if (currentPhaseLabel !== 'Thinking') {
            currentPhaseLabel = 'Thinking';
            updateMessage(params.thinkingMessageId, { thinkingPhase: 'Thinking' });
          }
          flushThoughtTrace(false);
        },
      }
    );

    flushThinkingDraft(true);
    flushThoughtTrace(true);

    trackTokenUsage(getGeminiModels().text.default, response.usageMetadata);

    const accumulatedFullText = response.text || "";
    const parsedAttachment = parseAssistantResponseForAttachment(accumulatedFullText);
    const responseTextForConversation = parsedAttachment.cleanedText;
    const parsedTranslationsOnComplete = parseGeminiResponse(responseTextForConversation);
    const groundingChunks = response.candidates?.[0]?.groundingMetadata?.groundingChunks as GroundingChunk[] | undefined;
    if (groundingChunks?.length) {
      setLatestGroundingChunks(groundingChunks);
    }

    const finalMessageUpdates: Partial<ChatMessage> = {
      thinking: false,
      thinkingTrace: undefined,
      thinkingDraftText: undefined,
      thinkingPhase: undefined,
      translations: parsedTranslationsOnComplete.length > 0 ? parsedTranslationsOnComplete : undefined,
      rawAssistantResponse: responseTextForConversation,
      text: parsedTranslationsOnComplete.length === 0 ? responseTextForConversation : undefined,
    };
    if (parsedAttachment.attachment) {
      finalMessageUpdates.imageUrl = parsedAttachment.attachment.dataUrl;
      finalMessageUpdates.imageMimeType = parsedAttachment.attachment.mimeType;
      finalMessageUpdates.attachmentName = parsedAttachment.attachment.fileName;
      finalMessageUpdates.storageOptimizedImageUrl = undefined;
      finalMessageUpdates.storageOptimizedImageMimeType = undefined;
      finalMessageUpdates.uploadedFileUri = undefined;
      finalMessageUpdates.uploadedFileMimeType = undefined;
    }
    updateMessage(params.thinkingMessageId, finalMessageUpdates);

    return {
      accumulatedFullText: responseTextForConversation,
      finalMessageUpdates,
      hasParsedAttachment: Boolean(parsedAttachment.attachment),
    };
  }, [appendThinkingTrace, formatGeminiProgressLine, messagesRef, parseGeminiResponse, setLatestGroundingChunks, updateMessage]);

  const runUserImageGeneration = useCallback(async (params: {
    shouldGenerateUserImage: boolean;
    currentSettingsVal: AppSettings;
    messageType: 'user' | 'conversational-reengagement' | 'image-reengagement';
    userMessageText: string;
    userMessageId: string | null;
    userImageToProcessBase64?: string;
    sanitizedDerivedHistory: any[];
  }) => {
    if (!params.shouldGenerateUserImage || !params.currentSettingsVal.imageGenerationModeEnabled ||
      !params.currentSettingsVal.sendWithSnapshotEnabled || params.messageType !== 'user' ||
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

    for (let attempt = 0; attempt < 7; attempt++) {
      const prompt = IMAGE_GEN_USER_PROMPT_TEMPLATE.replace("{TEXT}", params.userMessageText + (attempt !== 0 ? IMAGE_GEN_COPYRIGHT_AVOIDANCE_INSTRUCTION : ""));
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
      if (attempt < 6) await new Promise(r => setTimeout(r, 1500));
    }

    if (finalResult && 'base64Image' in finalResult) {
      const duration = Date.now() - userImageGenStartTime;
      addImageLoadDuration(duration);
      trackImageGeneration();
      if (!hasShownCostWarning()) {
        setCostWarningShown();
        addMessage({ role: 'error', text: t('error.imageGenCostWarning'), errorAction: 'imageGenCost' });
      }
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
    } else {
      updateMessage(params.userMessageId, {
        isGeneratingImage: false,
        imageGenerationStartTime: undefined
      });
    }

    return {};
  }, [
    addMessage,
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
    const existing = messagesRef.current.find((m) => m.id === params.thinkingMessageId);
    if (existing && ((existing.imageUrl && existing.imageMimeType) || (existing.uploadedFileUri && existing.uploadedFileMimeType))) {
      return;
    }

    const assistantStartTime = Date.now();
    updateMessage(params.thinkingMessageId, {
      isGeneratingImage: true,
      imageGenerationStartTime: assistantStartTime
    });

    // Exclude the latest assistant message since it's injected into the image-gen prompt as a user turn.
    const baseForEnsure: ChatMessage[] = getHistoryRespectingBookmark(messagesRef.current)
      .filter(m => m.id !== params.thinkingMessageId);

    let historyForAssistantImageGen: ChatMessage[] | undefined = undefined;
    try {
      sendWithFileUploadInProgressRef.current = true;
      setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...', done: 0, total: 0 });
      const ensuredUpdates = await ensureUrisForHistoryForSend(baseForEnsure, (done, total, etaMs) => {
        setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...', done, total, etaMs });
      });
      historyForAssistantImageGen = baseForEnsure.map(m => {
        const upd = ensuredUpdates[m.id];
        if (!upd) return m;
        if (upd.omitFromPayload) {
          return {
            ...m,
            uploadedFileUri: undefined,
            uploadedFileMimeType: undefined,
            imageFileUri: undefined,
            imageMimeType: undefined,
          } as ChatMessage;
        }
        if (upd.newUri) {
          const nextMime = upd.newMimeType || (m as any).uploadedFileMimeType;
          if ((m as any).uploadedFileUri !== upd.newUri || (upd.newMimeType && (m as any).uploadedFileMimeType !== upd.newMimeType)) {
            return { ...m, uploadedFileUri: upd.newUri, uploadedFileMimeType: nextMime } as ChatMessage;
          }
        }
        return m;
      });
      await new Promise(r => setTimeout(r, 0));
    } catch { /* ignore */ }

    for (let attempt = 0; attempt < 7; attempt++) {
      const histForAssistantImgBase = historyForAssistantImageGen || baseForEnsure;
      let gpTextForAssistant: string | undefined = undefined;
      try {
        const gp3 = await getGlobalProfileDB();
        gpTextForAssistant = gp3?.text || undefined;
      } catch {}

      const assistantHistory = deriveHistoryForApi(histForAssistantImgBase, {
        maxMessages: computeMaxMessagesForArray(baseForEnsure.filter((m: ChatMessage) => m.role === 'user' || m.role === 'assistant')),
        maxMediaToKeep: MAX_MEDIA_TO_KEEP,
        contextSummary: resolveBookmarkContextSummary() || undefined,
        globalProfileText: gpTextForAssistant,
        placeholderLatestUserMessage: DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE,
      });
      const sanitizedAssistantHistoryForImage = await sanitizeHistoryWithVerifiedUris(assistantHistory as any);

      const prompt = IMAGE_GEN_USER_PROMPT_TEMPLATE.replace("{TEXT}", params.accumulatedFullText + (attempt !== 0 ? IMAGE_GEN_COPYRIGHT_AVOIDANCE_INSTRUCTION : ""));
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
        trackImageGeneration();
        if (!hasShownCostWarning()) {
          setCostWarningShown();
          addMessage({ role: 'error', text: t('error.imageGenCostWarning'), errorAction: 'imageGenCost' });
        }
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
      } else if (attempt < 6) {
        await new Promise(r => setTimeout(r, 1500));
      } else {
        updateMessage(params.thinkingMessageId, {
          isGeneratingImage: false,
          imageGenerationStartTime: undefined
        });
      }
    }
  }, [
    addMessage,
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
    if (!text && !passedImageBase64 && messageType === 'user') return false;
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
    setSuggestionsLoadingStreamText('');
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
      text,
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

    const thinkingMessageId = addMessage({
      role: 'assistant',
      thinking: true,
      thinkingTrace: ['Preparing request context...'],
      thinkingDraftText: '',
    });

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
        let uploadSource = {
          dataUrl: imageForGeminiContextBase64,
          mimeType: imageForGeminiContextMimeType,
        };
        if (isOfficeMimeUnsupportedByGemini(uploadSource.mimeType)) {
          const officePreview = await getOfficePreview(uploadSource.dataUrl, uploadSource.mimeType, attachedFileName);
          const extracted = (officePreview.text || '').trim();
          if (!extracted) {
            throw new Error(`Failed to extract Office text for Gemini context upload. ${officePreview.note || ''}`.trim());
          }
          uploadSource = {
            dataUrl: toUtf8Base64DataUrl('text/plain', extracted),
            mimeType: 'text/plain',
          };
        }
        if (isSvgMimeType(uploadSource.mimeType)) {
          const rasterized = await processMediaForUpload(uploadSource.dataUrl, uploadSource.mimeType, { t });
          if (!rasterized.dataUrl || !rasterized.mimeType || !rasterized.mimeType.startsWith('image/') || isSvgMimeType(rasterized.mimeType)) {
            throw new Error(`Failed to rasterize SVG upload for Gemini context. Result MIME: ${rasterized.mimeType}`);
          }
          uploadSource = {
            dataUrl: rasterized.dataUrl,
            mimeType: rasterized.mimeType,
          };
        }
        const up = await uploadMediaToFiles(
          uploadSource.dataUrl,
          uploadSource.mimeType,
          attachedFileName || 'current-user-media'
        );
        // CRITICAL: Use the normalized MIME type returned from upload to avoid mismatch errors
        // The upload normalizes audio/webm;codecs=opus -> audio/webm, so we must use up.mimeType
        imageForGeminiContextMimeType = up.mimeType;
        if (messageType === 'user' && userMessageId) {
          const existing = (messagesRef.current || []).find(m => m.id === userMessageId);
          const existingUri = (existing as any)?.uploadedFileUri as string | undefined;
          const existingMime = (existing as any)?.uploadedFileMimeType as string | undefined;
          const hasReusableExisting = !!(
            existingUri &&
            existingMime &&
            !isSvgMimeType(existingMime) &&
            !isOfficeMimeUnsupportedByGemini(existingMime)
          );
          if (!hasReusableExisting) {
            updateMessage(userMessageId, {
              uploadedFileUri: up.uri,
              uploadedFileMimeType: up.mimeType,
            });
            imageForGeminiContextFileUri = up.uri;
          } else {
            imageForGeminiContextFileUri = existingUri;
            // Also use the existing mime type if we're reusing an existing URI
            imageForGeminiContextMimeType = existingMime || imageForGeminiContextMimeType;
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
      
      let ensuredUpdates: Record<string, MediaPayloadOverride> = {};
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
          const upd = ensuredUpdates[m.id];
          if (!upd) return m;
          if (upd.omitFromPayload) {
            return {
              ...m,
              uploadedFileUri: undefined,
              uploadedFileMimeType: undefined,
              imageFileUri: undefined,
              imageMimeType: undefined,
            } as ChatMessage;
          }
          if (upd.newUri) {
            const nextMime = upd.newMimeType || (m as any).uploadedFileMimeType;
            if ((m as any).uploadedFileUri !== upd.newUri || (upd.newMimeType && (m as any).uploadedFileMimeType !== upd.newMimeType)) {
              return { ...m, uploadedFileUri: upd.newUri, uploadedFileMimeType: nextMime } as ChatMessage;
            }
          }
          return m;
        });

      let globalProfileText: string | undefined = undefined;
      try {
        const gp2 = await getGlobalProfileDB();
        globalProfileText = gp2?.text || undefined;
      } catch {}

      // Ensure Maestro avatar URIs are valid before sending
      let avatarOverlayFileUri: string | undefined = undefined;
      let avatarOverlayMimeType: string | undefined = undefined;
      try {
        const avatarResult = await ensureMaestroAvatarUris();
        if (avatarResult.rawUri) {
          maestroAvatarUriRef.current = avatarResult.rawUri;
          maestroAvatarMimeTypeRef.current = avatarResult.rawMimeType;
        }
        avatarOverlayFileUri = avatarResult.overlayUri || undefined;
        avatarOverlayMimeType = avatarResult.overlayMimeType || undefined;
      } catch (e) {
        console.warn('Failed to ensure Maestro avatar URIs:', e);
      }

      const derivedHistory = deriveHistoryForApi(historySubsetForSendFinal, {
        maxMessages: computeMaxMessagesForArray(historySubsetForSendFinal.filter((m: ChatMessage) => m.role === 'user' || m.role === 'assistant')),
        maxMediaToKeep: MAX_MEDIA_TO_KEEP,
        contextSummary: resolveBookmarkContextSummary() || undefined,
        globalProfileText,
        avatarOverlayFileUri,
        avatarOverlayMimeType,
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

      const { accumulatedFullText, finalMessageUpdates, hasParsedAttachment } = await handleGeminiResponse({
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
        if (text === (transcript || '') && (transcript || '').length > 0) {
          clearTranscript();
        }
      }

      await new Promise(resolve => setTimeout(resolve, 0));

      // Image generation for assistant response
      if (!hasParsedAttachment) {
        await runAssistantImageGeneration({
          thinkingMessageId,
          accumulatedFullText,
          currentSettingsVal,
        });
      }

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
        if (error.code === 'MISSING_API_KEY') {
          errorMessage = t('error.apiKeyMissing');
          onApiKeyGateOpen?.({ reason: 'missing', instructionIndex: 0 });
        } else if (isInvalidApiKeyError(error)) {
          errorMessage = t('error.apiKeyInvalid');
          onApiKeyGateOpen?.({ reason: 'invalid', instructionIndex: 0 });
        } else if (isQuotaError(error)) {
          errorMessage = t('error.apiQuotaExceeded');
        } else {
          const parsedMessage = parseApiErrorMessage(error.message);
          errorMessage = parsedMessage || error.code || `HTTP ${error.status}`;
        }
      } else if (error instanceof Error) {
        errorMessage = error.message;
      }
      const isQuota = error instanceof ApiError && isQuotaError(error);
      updateMessage(thinkingMessageId, {
        thinking: false,
        thinkingTrace: undefined,
        thinkingDraftText: undefined,
        thinkingPhase: undefined,
        role: 'error',
        text: errorMessage,
        rawAssistantResponse: undefined,
        translations: undefined,
        ...(isQuota ? { errorAction: 'quota' } : {}),
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
      setSuggestionsLoadingStreamText('');
      // Clear any suggestions token on error
      if (suggestionsTokenRef.current) {
        removeActivityToken(suggestionsTokenRef.current);
        suggestionsTokenRef.current = null;
      }
      return false;
    }
  }, [
    t,
    addMessage,
    updateMessage,
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
    speakMessage,
    isSpeechSynthesisSupported,
    isListening,
    stopListening,
    startListening,
    clearTranscript,
    hasPendingQueueItems,
    currentSystemPromptText,
    attachedImageBase64,
    attachedImageMimeType,
    attachedFileName,
    setAttachedImage,
    addActivityToken,
    removeActivityToken,
    setLatestGroundingChunks,
    setReplySuggestions,
    setSuggestionsLoadingStreamText,
    setSendPrep,
    setSnapshotUserError,
    onApiKeyGateOpen,
    transcript,
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
    resolveBookmarkContextSummary,
    ensureUrisForHistoryForSend,
    computeHistorySubsetForMedia,
    handleReengagementThresholdChange,
    calculateEstimatedImageLoadTime,
  };
};

export default useTutorConversation;
