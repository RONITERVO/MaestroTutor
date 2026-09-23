import { createTextResponseCoordinator } from '../coordinators/textResponse';
import { createUserMessageCoordinator } from '../coordinators/userMessage';
// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { createAttachmentUploads, getMessageAttachmentSource, type HistoryMediaOverride } from '../coordinators/attachmentUploads';
import type { UseTutorConversationConfig, UseTutorConversationReturn } from '../coordinators/conversationContracts';
export type { UseTutorConversationConfig, UseTutorConversationReturn } from '../coordinators/conversationContracts';
import { createSuggestionCoordinator } from '../coordinators/suggestions';
import { REENGAGEMENT_PROMPT } from '../../../core/config/prompts';
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
  AppSettings,
  RecordedUtterance,
} from '../../../core/types';
import { ApiError } from '../../../api/gemini/client';
import { translateText, type GeminiProgressEvent } from '../../../api/gemini/generative';
import { sanitizeHistoryWithVerifiedUris, uploadMediaToFiles, checkFileStatuses } from '../../../api/gemini/files';
import { generateMusic } from '../../../api/gemini/music';
import { ensureMaestroAvatarUris, invalidateMaestroAvatarCache } from '../../../api/gemini/maestroAvatarEnsure';
import { getGlobalProfileDB, setGlobalProfileDB, setAppSettingsDB } from '../../session';
import { safeSaveChatHistoryDB, deriveHistoryForApi, INLINE_CAP_AUDIO } from '..';
import { processMediaForUpload, createKeyframeFromVideoDataUrl } from '../../vision';
import { extractOfficeTextForUpload } from '../../../core-sdk/chat/officeTextExtraction';
import { buildAttachmentUploadPlans as buildCoreAttachmentUploadPlans } from '../../../core-sdk/chat/attachmentUploadPlans';
import {
  getVisibleAssistantMessageText,
} from '../../../core-sdk/chat/assistantMessageContext';
import {
  parseStrictTutorResponseText,
  type StrictParsedTutorResponse,
} from '../../../core-sdk/chat/tutorResponse';
import { runTutorTextTurn, runReplySuggestions, runMaestroImageGeneration } from '../../../api/gemini/journeys';
import {
  executeSuggestionToolRequest,
  normalizeSuggestionCreatorToolRequest as normalizeCoreSuggestionCreatorToolRequest,
} from '../../../core-sdk/chat/suggestionAftersteps';
import { normalizeSuggestionCreatorArtifact as normalizeCoreSuggestionCreatorArtifact } from '../../../platform/browser/assistantArtifacts';
import { deriveBrowserTutorHistory } from '../../../core-sdk/chat/history';
import {
  buildUploadedAttachmentState,
  inferUploadedAttachmentTargetsForMimeType,
  PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID,
} from '../../../core-sdk/chat/uploadedAttachmentVariants';
import { 
  IMAGE_GEN_CAMERA_ID,
  MAX_MEDIA_TO_KEEP 
} from '../../../core/config/app';
import { getGeminiModels } from '../../../core/config/models';
import { 
  DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE, 
  composeMaestroSystemInstruction 
} from '../../../core/config/prompts';
import { isRealChatMessage } from '../../../shared/utils/common';
import { trackGeminiUsage, hasShownCostWarning, setCostWarningShown } from '../../../shared/utils/costTracker';
import { createSmartRef } from '../../../shared/utils/smartRef';
import { getPrimarySubtag } from '../../../shared/utils/languageUtils';
import type { TranslationFunction } from '../../../app/hooks/useTranslations';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../core/config/activityTokens';
import { synthesizeGeminiAudioNote } from '../../speech/services/geminiLiveAudioNote';
import { useMaestroStore } from '../../../store';
import { useShallow } from 'zustand/shallow';
import { selectIsListening, selectIsResponsePending, selectIsLoadingSuggestions, selectIsCreatingSuggestion, selectIsSpeaking } from '../../../store/slices/uiSlice';
import { selectSelectedLanguagePair } from '../../../store/slices/settingsSlice';
import { errorSttFlow, logSttFlow, warnSttFlow } from '../../../shared/utils/sttFlowDebug';

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



const buildAttachmentUploadPlans = (
  source: { dataUrl: string; mimeType: string; attachmentName?: string },
  t: TranslationFunction
) => buildCoreAttachmentUploadPlans(source, {
  createVideoKeyframe: async mediaSource => createKeyframeFromVideoDataUrl(mediaSource.dataUrl, {
    at: 'start',
    maxDim: 768,
    quality: 0.75,
    outputMime: 'image/jpeg',
  }),
  extractOfficeText: async mediaSource => {
    return extractOfficeTextForUpload({
      dataUrl: mediaSource.dataUrl,
      mimeType: mediaSource.mimeType,
      fileName: mediaSource.attachmentName,
    });
  },
  rasterizeSvg: async mediaSource => processMediaForUpload(
    mediaSource.dataUrl,
    mediaSource.mimeType,
    { t },
  ),
});

type ToolAttachmentPhase = NonNullable<ChatMessage['toolAttachmentPhase']>;
type MaestroToolKind = NonNullable<ChatMessage['maestroToolKind']>;

const truncateForToolPrompt = (value: string, maxChars: number = 420): string => {
  const normalized = (value || '').replace(/\s+/g, ' ').trim();
  if (!normalized) return '';
  return normalized.length > maxChars ? `${normalized.slice(0, maxChars - 1)}…` : normalized;
};

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
    stopListening,
    startListening,
    clearTranscript,
    hasPendingQueueItems,
    claimRecordedUtterance,
    scheduleReengagementRef,
    cancelReengagementRef,
    transcript,
    currentSystemPromptText,
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
  const isSending = useMaestroStore(selectIsResponsePending);
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
  const isResponsePendingRef = useRef(false);
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
  useEffect(() => { isResponsePendingRef.current = isSending; }, [isSending]);
  useEffect(() => { sendPrepRef.current = sendPrep; }, [sendPrep]);
  useEffect(() => () => { isMountedRef.current = false; }, []);

  // Invalidate avatar cache when avatar changes
  useEffect(() => {
    const handler = () => { invalidateMaestroAvatarCache(); };
    window.addEventListener('maestro-avatar-updated', handler);
    return () => window.removeEventListener('maestro-avatar-updated', handler);
  }, []);

  const parseStrictTutorResponse = useCallback((responseText: string | undefined): StrictParsedTutorResponse => {
    return parseStrictTutorResponseText(
      responseText,
      selectedLanguagePairRef.current?.nativeLanguageCode
    );
  }, [selectedLanguagePairRef]);

  const parseGeminiResponse = useCallback((responseText: string | undefined): Array<{ target: string; native: string }> => {
    return parseStrictTutorResponse(responseText).translations;
  }, [parseStrictTutorResponse]);

  const normalizeSuggestionCreatorArtifact = useCallback((artifact: unknown) => {
    return normalizeCoreSuggestionCreatorArtifact(artifact);
  }, []);

  const normalizeSuggestionCreatorToolRequest = useCallback((toolRequest: unknown, assistantMessageId: string) => {
    const assistantMessage = messagesRef.current.find(message => message.id === assistantMessageId);
    const fallbackText = truncateForToolPrompt(getVisibleAssistantMessageText(assistantMessage), 500);
    return normalizeCoreSuggestionCreatorToolRequest(toolRequest, fallbackText);
  }, [messagesRef, settingsRef]);

  

  

  const formatGeminiStatusLine = useCallback((event: GeminiProgressEvent): string | undefined => {
    const elapsedSeconds = typeof event.elapsedMs === 'number'
      ? Math.max(0, Math.floor(event.elapsedMs / 1000))
      : undefined;

    switch (event.phase) {
      case 'attempt-start':
        return t('streaming.connectingTo', { model: event.model, attempt: event.attempt, total: event.totalAttempts }) || `Connecting to ${event.model} (attempt ${event.attempt}/${event.totalAttempts})...`;
      case 'attempt-processing':
        if (typeof elapsedSeconds !== 'number') return undefined;
        return t('streaming.waitingForOutput', { seconds: elapsedSeconds }) || `Waiting for model output... ${elapsedSeconds}s elapsed.`;
      case 'high-demand':
        if (event.reason === 'no-output-timeout' && typeof elapsedSeconds === 'number' && elapsedSeconds > 0) {
          return t('streaming.noOutputAfter', { seconds: elapsedSeconds }) || `No model output after ${elapsedSeconds}s. Request is likely queued on Google servers.`;
        }
        return t('streaming.highDemand') || 'High demand detected. Request is queued on Google servers.';
      case 'fallback-switch':
        return t('streaming.switchingFallback', { model: event.model }) || `Switching to fallback model ${event.model}.`;
      case 'retry-scheduled':
        return t('streaming.retryingIn', { seconds: Math.ceil((event.retryInMs || 0) / 1000) }) || `Retrying in ${Math.ceil((event.retryInMs || 0) / 1000)}s...`;
      case 'success':
        return undefined;
      default:
        return undefined;
    }
  }, [t]);

  const formatGeminiPhaseLabel = useCallback((event: GeminiProgressEvent): string | undefined => {
    switch (event.phase) {
      case 'attempt-start':
        return t('streaming.phaseConnecting') || 'Connecting';
      case 'attempt-processing':
        return t('streaming.phaseProcessing') || 'Processing';
      case 'high-demand':
        return t('streaming.phaseHighDemand') || 'High demand';
      case 'fallback-switch':
        return t('streaming.phaseSwitchingModel') || 'Switching model';
      case 'retry-scheduled':
        return t('streaming.phaseRetrying') || 'Retrying';
      case 'success':
        return t('streaming.phaseFinalizing') || 'Finalizing';
      default:
        return undefined;
    }
  }, [t]);

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

  const { ensureUploadedAttachmentVariantsForMessage, uploadAttachmentVariantsForSource, ensureUrisForHistoryForSend } = useMemo(() => createAttachmentUploads({
    t, updateMessage, computeHistorySubsetForMedia, checkFileStatuses, uploadMediaToFiles, buildAttachmentUploadPlans,
  }), [t, updateMessage, computeHistorySubsetForMedia]);

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

  // React/store adapter: orchestration is independently owned by the coordinator.
  const fetchAndSetReplySuggestions = useMemo(() => createSuggestionCoordinator({
    state: {
      getMessages: () => messagesRef.current,
      getLanguagePair: () => selectedLanguagePairRef.current,
      getPairId: () => settingsRef.current.selectedLanguagePairId,
      // Preserve the render-time loading guard; fresh reads remain explicit above.
      isLoading: () => isLoadingSuggestions,
      setSuggestionOwner: id => { lastFetchedSuggestionsForRef.current = id; },
    },
    activity: {
      begin: () => { suggestionsTokenRef.current = addActivityToken(TOKEN_CATEGORY.GEN, TOKEN_SUBTYPE.SUGGESTIONS); },
      finish: () => {
        if (suggestionsTokenRef.current) {
          removeActivityToken(suggestionsTokenRef.current);
          suggestionsTokenRef.current = null;
        }
      },
    },
    persistence: {
      getProfile: getGlobalProfileDB, saveHistory: safeSaveChatHistoryDB, saveProfile: setGlobalProfileDB,
      notifyProfileUpdated: () => { window.dispatchEvent(new CustomEvent('globalProfileUpdated')); },
    },
    runReplySuggestions, normalizeSuggestionCreatorArtifact, normalizeSuggestionCreatorToolRequest,
    executeAssistantToolRequest, addMessage, updateMessage, setReplySuggestions,
    setSuggestionsLoadingStreamText, getHistoryRespectingBookmark, handleReengagementThresholdChange,
    formatGeminiStatusLine, trackGeminiUsage,
  }), [
    executeAssistantToolRequest, handleReengagementThresholdChange, getHistoryRespectingBookmark,
    messagesRef, normalizeSuggestionCreatorToolRequest, normalizeSuggestionCreatorArtifact,
    selectedLanguagePairRef, settingsRef, isLoadingSuggestions, addMessage, addActivityToken,
    removeActivityToken, setReplySuggestions, setSuggestionsLoadingStreamText, formatGeminiStatusLine,
    lastFetchedSuggestionsForRef, updateMessage,
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
      const { translatedText, usageMetadata, modelVersion, modelUsed } = await translateText(textToTranslate, fromLangName, toLangName);
      trackGeminiUsage({
        feature: 'translation',
        configuredModel: modelUsed || getGeminiModels().text.translation,
        modelVersion,
        usageMetadata,
      });
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

  const attachGeneratedToolMedia = useCallback(async (params: {
    messageId: string;
    toolKind: MaestroToolKind;
    dataUrl: string;
    mimeType: string;
    attachmentName: string;
  }) => {
    try {
      const { optimized, upload } = await optimizeAndUploadMedia({
        dataUrl: params.dataUrl,
        mimeType: params.mimeType,
        displayName: params.attachmentName,
        setUploadPrepLabel: false,
      });
      const uploadedAttachmentState = buildUploadedAttachmentState([
        {
          id: PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID,
          uri: upload.uri,
          mimeType: upload.mimeType,
          targets: inferUploadedAttachmentTargetsForMimeType(upload.mimeType),
          source: 'original',
          order: 10,
        },
      ]);

      updateMessage(params.messageId, {
        imageUrl: params.dataUrl,
        imageMimeType: params.mimeType,
        attachmentName: params.attachmentName,
        storageOptimizedImageUrl: optimized.dataUrl,
        storageOptimizedImageMimeType: optimized.mimeType,
        ...uploadedAttachmentState,
        isGeneratingToolAttachment: false,
        toolAttachmentStartTime: undefined,
        toolAttachmentPhase: undefined,
        maestroToolKind: params.toolKind,
      });
    } catch (error) {
      console.warn(`[MaestroTool] Failed to upload ${params.toolKind} attachment.`, error);
      updateMessage(params.messageId, {
        imageUrl: params.dataUrl,
        imageMimeType: params.mimeType,
        attachmentName: params.attachmentName,
        isGeneratingToolAttachment: false,
        toolAttachmentStartTime: undefined,
        toolAttachmentPhase: undefined,
        maestroToolKind: params.toolKind,
      });
    }
  }, [optimizeAndUploadMedia, updateMessage]);

  async function executeAssistantToolRequest(
    assistantMessageId: string,
    toolRequest: ReturnType<typeof normalizeSuggestionCreatorToolRequest>
  ) {
    const existing = messagesRef.current.find(message => message.id === assistantMessageId);
    updateMessage(assistantMessageId, {
      isLoadingArtifact: false,
      artifactLoadStartTime: undefined,
    });

    if (existing && ((existing.imageUrl && existing.imageMimeType) || (existing.uploadedFileVariants && existing.uploadedFileVariants.length > 0))) {
      return;
    }

    if (!toolRequest) {
      return;
    }

    try {
      await executeSuggestionToolRequest(toolRequest, {
        image: async request => {
          const assistantMessage = messagesRef.current.find(m => m.id === assistantMessageId);
          const fullRawText = assistantMessage?.llmRawResponse
            || request.prompt
            || assistantMessage?.rawAssistantResponse
            || getVisibleAssistantMessageText(assistantMessage);
          await runAssistantImageGeneration({
            thinkingMessageId: assistantMessageId,
            accumulatedFullText: fullRawText,
          });
        },
        audioNote: async request => {
          updateMessage(assistantMessageId, {
            isGeneratingToolAttachment: true,
            toolAttachmentStartTime: Date.now(),
            toolAttachmentPhase: 'pending' as ToolAttachmentPhase,
            maestroToolKind: 'audio-note',
          });
          const selectedLanguagePair = selectedLanguagePairRef.current;
          const langCode = getPrimarySubtag(selectedLanguagePair?.targetLanguageCode || settingsRef.current.stt.language || 'en');
          const audioNote = await synthesizeGeminiAudioNote({
            text: truncateForToolPrompt(request.text, 500),
            langCode,
            voiceName: settingsRef.current.tts.voiceName || 'Kore',
          });
          await attachGeneratedToolMedia({
            messageId: assistantMessageId,
            toolKind: 'audio-note',
            dataUrl: audioNote.dataUrl,
            mimeType: audioNote.mimeType,
            attachmentName: 'maestro-audio-note.wav',
          });
        },
        music: async request => {
          updateMessage(assistantMessageId, {
            isGeneratingToolAttachment: true,
            toolAttachmentStartTime: Date.now(),
            toolAttachmentPhase: 'pending' as ToolAttachmentPhase,
            maestroToolKind: 'music',
          });
          const music = await generateMusic({
            prompt: request.prompt,
            durationSeconds: request.durationSeconds,
            onStreamPlaybackStart: () => {
              updateMessage(assistantMessageId, {
                isGeneratingToolAttachment: false,
                toolAttachmentStartTime: undefined,
                toolAttachmentPhase: 'streaming' as ToolAttachmentPhase,
                maestroToolKind: 'music',
              });
            },
          });
          updateMessage(assistantMessageId, {
            isGeneratingToolAttachment: false,
            toolAttachmentStartTime: undefined,
            toolAttachmentPhase: 'finalizing' as ToolAttachmentPhase,
            maestroToolKind: 'music',
          });
          await attachGeneratedToolMedia({
            messageId: assistantMessageId,
            toolKind: 'music',
            dataUrl: music.dataUrl,
            mimeType: music.mimeType,
            attachmentName: 'maestro-music.wav',
          });
        },
      });
    } catch (error) {
      console.warn(`[MaestroTool] ${toolRequest.tool} generation failed.`, error);
      updateMessage(assistantMessageId, {
        isGeneratingToolAttachment: false,
        toolAttachmentStartTime: undefined,
        toolAttachmentPhase: undefined,
      });
    }
  }

  const createUserMessage = useMemo(() => createUserMessageCoordinator({ t, addMessage, captureSnapshot, claimRecordedUtterance, attachedImageBase64, attachedImageMimeType, attachedFileName, recordedUtterancePendingRef, sendWithFileUploadInProgressRef, setSendPrep, processMediaForUpload, INLINE_CAP_AUDIO }), [t, addMessage, captureSnapshot, claimRecordedUtterance, attachedImageBase64, attachedImageMimeType, attachedFileName, recordedUtterancePendingRef, sendWithFileUploadInProgressRef, setSendPrep, processMediaForUpload, INLINE_CAP_AUDIO]);

  const handleGeminiResponse = useMemo(() => createTextResponseCoordinator({ t, setSettings, updateMessage, messagesRef, selectedLanguagePairRef, runTutorTextTurn, trackGeminiUsage, setLatestGroundingChunks, formatGeminiPhaseLabel, formatGeminiStatusLine }), [t, setSettings, updateMessage, messagesRef, selectedLanguagePairRef, runTutorTextTurn, trackGeminiUsage, setLatestGroundingChunks, formatGeminiPhaseLabel, formatGeminiStatusLine]);

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
    const finalResult = await runMaestroImageGeneration({
      contextText: params.userMessageText,
      history: sanitizedUserHistoryForImage,
      maestroAvatarUri: maestroAvatarUriRef.current || undefined,
      maestroAvatarMimeType: maestroAvatarMimeTypeRef.current || undefined,
    });

    if (finalResult && 'base64Image' in finalResult) {
      const duration = Date.now() - userImageGenStartTime;
      addImageLoadDuration(duration);
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
        const uploadedAttachmentState = buildUploadedAttachmentState([
          {
            id: PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID,
            uri: upload.uri,
            mimeType: upload.mimeType,
            targets: inferUploadedAttachmentTargetsForMimeType(upload.mimeType),
            source: 'original',
            order: 10,
          },
        ]);

        updateMessage(params.userMessageId, {
          imageUrl: finalResult.base64Image,
          imageMimeType: finalResult.mimeType,
          storageOptimizedImageUrl: optimized.dataUrl,
          storageOptimizedImageMimeType: optimized.mimeType,
          ...uploadedAttachmentState,
          isGeneratingImage: false,
          imageGenError: null,
          imageGenerationStartTime: undefined
        });
        return {
          imageForGeminiContextFileUri: [{ fileUri: upload.uri, mimeType: upload.mimeType }],
        };
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
  }) => {
    if (!params.accumulatedFullText.trim()) return;
    const existing = messagesRef.current.find((m) => m.id === params.thinkingMessageId);
    if (existing && ((existing.imageUrl && existing.imageMimeType) || (existing.uploadedFileVariants && existing.uploadedFileVariants.length > 0))) {
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
      const ensuredUpdates = await ensureUrisForHistoryForSend(baseForEnsure);
      historyForAssistantImageGen = baseForEnsure.map(m => {
        const upd = ensuredUpdates[m.id];
        if (!upd) return m;
        if (upd.omitFromHistory) {
          return {
            ...m,
            uploadedFileVariants: undefined,
          } as ChatMessage;
        }
        if (upd.newVariants) {
          const nextVariants = upd.newVariants || m.uploadedFileVariants;
          if (
            (upd.newVariants && JSON.stringify(m.uploadedFileVariants || []) !== JSON.stringify(upd.newVariants || []))
          ) {
            return {
              ...m,
              uploadedFileVariants: nextVariants,
            } as ChatMessage;
          }
        }
        return m;
      });
      await new Promise(r => setTimeout(r, 0));
    } catch { /* ignore */ }

    const histForAssistantImgBase = historyForAssistantImageGen || baseForEnsure;
    let gpTextForAssistant: string | undefined;
    try {
      gpTextForAssistant = (await getGlobalProfileDB())?.text || undefined;
    } catch {
      gpTextForAssistant = undefined;
    }
    const assistantHistory = deriveHistoryForApi(histForAssistantImgBase, {
      maxMessages: computeMaxMessagesForArray(baseForEnsure.filter((message: ChatMessage) => message.role === 'user' || message.role === 'assistant')),
      maxMediaToKeep: MAX_MEDIA_TO_KEEP,
      contextSummary: resolveBookmarkContextSummary() || undefined,
      globalProfileText: gpTextForAssistant,
      placeholderLatestUserMessage: DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE,
    });
    const sanitizedAssistantHistoryForImage = await sanitizeHistoryWithVerifiedUris(assistantHistory as any);
    const assistantImgGenResult = await runMaestroImageGeneration({
      contextText: params.accumulatedFullText,
      history: sanitizedAssistantHistoryForImage,
      maestroAvatarUri: maestroAvatarUriRef.current || undefined,
      maestroAvatarMimeType: maestroAvatarMimeTypeRef.current || undefined,
    });

    if ('base64Image' in assistantImgGenResult) {
      const duration = Date.now() - assistantStartTime;
      addImageLoadDuration(duration);
      if (!hasShownCostWarning()) {
        setCostWarningShown();
        addMessage({ role: 'error', text: t('error.imageGenCostWarning'), errorAction: 'imageGenCost' });
      }
      try {
        const { optimized, upload } = await optimizeAndUploadMedia({
          dataUrl: assistantImgGenResult.base64Image,
          mimeType: assistantImgGenResult.mimeType,
          displayName: 'assistant-generated',
          setUploadPrepLabel: false,
        });
        const uploadedAttachmentState = buildUploadedAttachmentState([
          {
            id: PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID,
            uri: upload.uri,
            mimeType: upload.mimeType,
            targets: inferUploadedAttachmentTargetsForMimeType(upload.mimeType),
            source: 'original',
            order: 10,
          },
        ]);
        updateMessage(params.thinkingMessageId, {
          imageUrl: assistantImgGenResult.base64Image,
          imageMimeType: assistantImgGenResult.mimeType,
          storageOptimizedImageUrl: optimized.dataUrl,
          storageOptimizedImageMimeType: optimized.mimeType,
          ...uploadedAttachmentState,
          attachmentName: 'assistant-generated.jpg',
          isGeneratingImage: false,
          imageGenError: null,
          imageGenerationStartTime: undefined,
          maestroToolKind: 'image',
        });
      } catch {
        updateMessage(params.thinkingMessageId, {
          imageUrl: assistantImgGenResult.base64Image,
          imageMimeType: assistantImgGenResult.mimeType,
          attachmentName: 'assistant-generated.jpg',
          isGeneratingImage: false,
          imageGenError: null,
          imageGenerationStartTime: undefined,
          maestroToolKind: 'image',
        });
      }
    } else {
      updateMessage(params.thinkingMessageId, {
        isGeneratingImage: false,
        imageGenerationStartTime: undefined,
      });
    }
  }, [
    addMessage,
    computeMaxMessagesForArray,
    ensureUrisForHistoryForSend,
    getHistoryRespectingBookmark,
    maestroAvatarMimeTypeRef,
    maestroAvatarUriRef,
    messagesRef,
    optimizeAndUploadMedia,
    resolveBookmarkContextSummary,
    addImageLoadDuration,
    t,
    updateMessage,
  ]);

  // Main send message handler
  const handleSendMessageInternal = useCallback(async (
    text: string,
    passedImageBase64?: string,
    passedImageMimeType?: string,
    messageType: 'user' | 'conversational-reengagement' | 'image-reengagement' = 'user',
    options?: { triggeredByStt?: boolean }
  ): Promise<boolean> => {
    let sendStage = 'send.enter';
    const markSendStage = (stage: string, details?: Record<string, unknown>) => {
      sendStage = stage;
      logSttFlow(stage, details);
    };
    markSendStage('send.start', {
      messageType,
      textLength: text.length,
      triggeredByStt: options?.triggeredByStt === true,
      hasPassedImage: Boolean(passedImageBase64),
      isLoadingHistory: isLoadingHistoryRef.current,
      responsePending: isResponsePendingRef.current,
      speaking: speechIsSpeakingRef.current,
    });
    if (isLoadingHistoryRef.current) {
      warnSttFlow('send.skip.loadingHistory', {
        messageType,
      });
      return false;
    }
    if (!text && !passedImageBase64 && messageType === 'user') {
      warnSttFlow('send.skip.emptyUserMessage', {
        triggeredByStt: options?.triggeredByStt === true,
      });
      return false;
    }
    if (!selectedLanguagePairRef.current) {
      errorSttFlow('send.skip.noLanguagePair', {
        messageType,
      });
      console.error("No language pair selected, cannot send message.");
      addMessage({ role: 'error', text: t('error.noLanguagePair') });
      return false;
    }

    if (isResponsePendingRef.current || speechIsSpeakingRef.current) {
      warnSttFlow('send.skip.busy', {
        responsePending: isResponsePendingRef.current,
        speaking: speechIsSpeakingRef.current,
      });
      return false;
    }

    // Add sending token for unified busy state tracking (replaces setIsSending(true))
    sendingTokenRef.current = addActivityToken(TOKEN_CATEGORY.GEN, TOKEN_SUBTYPE.RESPONSE);
    const isListeningNow = selectIsListening(useMaestroStore.getState());
    const shouldResumeSttAfterSend = settingsRef.current.stt.enabled && (isListeningNow || options?.triggeredByStt === true);
    markSendStage('send.token.added', {
      isListeningNow,
      shouldResumeSttAfterSend,
    });
    if (settingsRef.current.stt.enabled && isListeningNow) {
      const claimedUtterance = typeof claimRecordedUtterance === 'function' ? claimRecordedUtterance() : null;
      if (claimedUtterance) {
        recordedUtterancePendingRef.current = claimedUtterance;
      }
      try {
        markSendStage('send.stopListening.start', {
          hadClaimedUtterance: Boolean(claimedUtterance),
        });
        await Promise.resolve(stopListening());
        markSendStage('send.stopListening.done');
      } catch {
        warnSttFlow('send.stopListening.error', {
          stage: sendStage,
        });
        /* ignore */
      }
      clearTranscript();
      markSendStage('send.clearTranscript.afterStop');
    } else if (options?.triggeredByStt) {
      clearTranscript();
      markSendStage('send.clearTranscript.sttTriggered');
    }

    if (shouldResumeSttAfterSend) {
      sttInterruptedBySendRef.current = true;
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
    let thinkingMessageId: string | null = null;
    const handleSendFailure = (error: unknown): false => {
      errorSttFlow('send.failure', {
        stage: sendStage,
        messageType,
        triggeredByStt: options?.triggeredByStt === true,
        message: error instanceof Error ? error.message : String(error),
      });
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

      if (thinkingMessageId) {
        updateMessage(thinkingMessageId, {
          thinking: false,
          thinkingTrace: undefined,
          thinkingDraftText: undefined,
          thinkingPhase: undefined,
          thinkingStatusLine: undefined,
          role: 'error',
          text: errorMessage,
          llmRawResponse: undefined,
          rawAssistantResponse: undefined,
          translations: undefined,
          isLoadingArtifact: false,
          artifactLoadStartTime: undefined,
          ...(isQuota ? { errorAction: 'quota' } : {}),
        });
      } else {
        addMessage({
          role: 'error',
          text: errorMessage,
          ...(isQuota ? { errorAction: 'quota' as const } : {}),
        });
      }

      if (sendingTokenRef.current) {
        removeActivityToken(sendingTokenRef.current);
        sendingTokenRef.current = null;
      }
      setSendPrep(null);
      try {
        sendWithFileUploadInProgressRef.current = false;
      } catch { /* ignore */ }

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
      if (suggestionsTokenRef.current) {
        removeActivityToken(suggestionsTokenRef.current);
        suggestionsTokenRef.current = null;
      }
      return false;
    };

    try {
      const currentSettingsVal = settingsRef.current;
      const shouldGenerateUserImage = currentSettingsVal.selectedCameraId === IMAGE_GEN_CAMERA_ID;
      markSendStage('send.createUserMessage.await', {
        shouldGenerateUserImage,
      });
      const userMessageContext = await createUserMessage({
        text,
        passedImageBase64,
        passedImageMimeType,
        messageType,
        shouldGenerateUserImage,
        currentSettingsVal,
        triggeredByStt: options?.triggeredByStt === true,
      });
      markSendStage('send.createUserMessage.done', {
        userMessageId: userMessageContext.userMessageId,
        hasRecordedAudio: Boolean(userMessageContext.recordedSpeechForMessage),
        hasImage: Boolean(userMessageContext.userImageToProcessBase64),
      });
      let {
        userMessageId,
        userMessageText,
        userImageToProcessBase64,
        userImageToProcessMimeType,
        userImageToProcessStorageOptimizedBase64,
        userImageToProcessStorageOptimizedMimeType,
      } = userMessageContext;

      thinkingMessageId = addMessage({
        role: 'assistant',
        thinking: true,
        thinkingTrace: [],
        thinkingDraftText: '',
        thinkingPhase: t('streaming.phasePreparingRequest') || 'Preparing request',
        thinkingStatusLine: t('streaming.preparingRequestContext') || 'Preparing request context...',
      });
      markSendStage('send.thinkingMessage.created', {
        thinkingMessageId,
      });

      cancelReengagementRef.current();

      let historyForGemini = messagesRef.current.filter(m => m.id !== thinkingMessageId);
      if (messageType === 'user' && userMessageId) {
        historyForGemini = historyForGemini.filter(m => m.id !== userMessageId);
      }

      let geminiPromptText: string;
      let systemInstructionForGemini: string = currentSystemPromptText;
      try {
        markSendStage('send.systemInstruction.globalProfile.start');
        await getGlobalProfileDB();
      } finally {
        systemInstructionForGemini = composeMaestroSystemInstruction(systemInstructionForGemini);
        markSendStage('send.systemInstruction.globalProfile.done', {
          systemInstructionLength: systemInstructionForGemini.length,
        });
      }

      // Optimize user image if needed
      if (messageType === 'user' && userImageToProcessBase64 && !userImageToProcessStorageOptimizedBase64 && userImageToProcessMimeType) {
        if (!sendWithFileUploadInProgressRef.current) {
          sendWithFileUploadInProgressRef.current = true;
        }
        try {
          markSendStage('send.currentMedia.optimize.start', {
            userMessageId,
          });
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
          markSendStage('send.currentMedia.optimize.done', {
            userMessageId,
          });
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

      let imageForGeminiContextFileUri: Array<{ fileUri: string; mimeType: string }> | undefined = undefined;

      switch (messageType) {
        case 'image-reengagement':
          geminiPromptText = REENGAGEMENT_PROMPT;
          break;
        case 'conversational-reengagement':
          geminiPromptText = REENGAGEMENT_PROMPT;
          imageForGeminiContextBase64 = undefined;
          imageForGeminiContextMimeType = undefined;
          break;
        case 'user':
        default:
          geminiPromptText = userMessageText;
          break;
      }

      if (messageType === 'image-reengagement') {
        if (typeof passedImageBase64 === 'string' && passedImageBase64 && typeof passedImageMimeType === 'string' && passedImageMimeType) {
          imageForGeminiContextBase64 = passedImageBase64;
          imageForGeminiContextMimeType = passedImageMimeType;
        }
      }

      if (messageType === 'user' && userMessageId) {
        const currentUserMessage = messagesRef.current.find(m => m.id === userMessageId);
        if (currentUserMessage && getMessageAttachmentSource(currentUserMessage)) {
          if (!sendWithFileUploadInProgressRef.current) {
            sendWithFileUploadInProgressRef.current = true;
          }
          setSendPrep({ active: true, label: t('chat.sendPrep.uploadingMedia') || 'Uploading media...' });
          try {
            markSendStage('send.currentAttachment.upload.start', {
              userMessageId,
            });
            const ensured = await ensureUploadedAttachmentVariantsForMessage(currentUserMessage);
            if (ensured.chatFileParts.length === 0) {
              throw new Error(t('streaming.failedToPrepareCurrent', { name: currentUserMessage.attachmentName || 'attachment' }) || `Failed to prepare attached media "${currentUserMessage.attachmentName || 'attachment'}" for Gemini. Try again or reattach the file.`);
            }
            imageForGeminiContextFileUri = ensured.chatFileParts;
            markSendStage('send.currentAttachment.upload.done', {
              filePartCount: ensured.chatFileParts.length,
            });
          } finally {
            setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...' } : prev));
          }
        }
      } else if (imageForGeminiContextBase64 && imageForGeminiContextMimeType) {
        if (!sendWithFileUploadInProgressRef.current) {
          sendWithFileUploadInProgressRef.current = true;
        }
        setSendPrep({ active: true, label: t('chat.sendPrep.uploadingMedia') || 'Uploading media...' });
        try {
          markSendStage('send.inlineMedia.upload.start', {
            mimeType: imageForGeminiContextMimeType,
          });
          const chatFileParts = await uploadAttachmentVariantsForSource(
            {
              dataUrl: imageForGeminiContextBase64,
              mimeType: imageForGeminiContextMimeType,
              attachmentName: attachedFileName || undefined,
            },
            attachedFileName || 'current-user-media'
          );
          if (chatFileParts.length === 0) {
            throw new Error(t('streaming.failedToPrepareCurrent', { name: attachedFileName || 'attachment' }) || `Failed to prepare attached media "${attachedFileName || 'attachment'}" for Gemini. Try again or reattach the file.`);
          }
          imageForGeminiContextFileUri = chatFileParts;
          markSendStage('send.inlineMedia.upload.done', {
            filePartCount: chatFileParts.length,
          });
        } finally {
          setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...' } : prev));
        }
      }

      setLatestGroundingChunks(undefined);

      try {
      const historySubsetForSend: ChatMessage[] = getHistoryRespectingBookmark(historyForGemini);

      let ensuredUpdates: Record<string, HistoryMediaOverride> = {};
      try {
        markSendStage('send.history.ensureUris.start', {
          historyCount: historySubsetForSend.length,
        });
        ensuredUpdates = await ensureUrisForHistoryForSend(historySubsetForSend, (done, total, etaMs) => {
          setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...', done, total, etaMs });
        });
        markSendStage('send.history.ensureUris.done', {
          updatedMessageCount: Object.keys(ensuredUpdates).length,
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
          if (upd.omitFromHistory) {
            return {
              ...m,
              uploadedFileVariants: undefined,
            } as ChatMessage;
          }
          if (upd.newVariants) {
            const nextVariants = upd.newVariants || m.uploadedFileVariants;
            if (
              (upd.newVariants && JSON.stringify(m.uploadedFileVariants || []) !== JSON.stringify(upd.newVariants || []))
            ) {
              return {
                ...m,
                uploadedFileVariants: nextVariants,
              } as ChatMessage;
            }
          }
          return m;
        });

      let globalProfileText: string | undefined = undefined;
      try {
        markSendStage('send.globalProfile.start');
        const gp2 = await getGlobalProfileDB();
        globalProfileText = gp2?.text || undefined;
        markSendStage('send.globalProfile.done', {
          hasGlobalProfile: Boolean(globalProfileText),
        });
      } catch {}

      // Ensure Maestro avatar URIs are valid before sending
      let avatarOverlayFileUri: string | undefined = undefined;
      let avatarOverlayMimeType: string | undefined = undefined;
      try {
        markSendStage('send.avatar.ensure.start');
        const avatarResult = await ensureMaestroAvatarUris();
        if (avatarResult.rawUri) {
          maestroAvatarUriRef.current = avatarResult.rawUri;
          maestroAvatarMimeTypeRef.current = avatarResult.rawMimeType;
        }
        avatarOverlayFileUri = avatarResult.overlayUri || undefined;
        avatarOverlayMimeType = avatarResult.overlayMimeType || undefined;
        markSendStage('send.avatar.ensure.done', {
          hasAvatarOverlay: Boolean(avatarOverlayFileUri),
        });
      } catch (e) {
        console.warn('Failed to ensure Maestro avatar URIs:', e);
      }

      const derivedHistory = deriveBrowserTutorHistory(historySubsetForSendFinal, {
        maxMessages: computeMaxMessagesForArray(historySubsetForSendFinal.filter((m: ChatMessage) => m.role === 'user' || m.role === 'assistant')),
        maxMediaToKeep: MAX_MEDIA_TO_KEEP,
        contextSummary: resolveBookmarkContextSummary() || undefined,
        globalProfileText,
        avatarOverlayFileUri,
        avatarOverlayMimeType,
      });
      markSendStage('send.history.sanitize.start', {
        derivedHistoryCount: derivedHistory.length,
      });
      const sanitizedDerivedHistory = await sanitizeHistoryWithVerifiedUris(derivedHistory as any);
      markSendStage('send.history.sanitize.done', {
        sanitizedHistoryCount: sanitizedDerivedHistory.length,
      });

      // User image generation for AI Camera mode
      markSendStage('send.userImageGeneration.start', {
        shouldGenerateUserImage,
      });
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
      }
      markSendStage('send.userImageGeneration.done', {
        filePartCount: imageForGeminiContextFileUri?.length || 0,
      });

      setSendPrep(null);

      markSendStage('send.gemini.await', {
        historyCount: sanitizedDerivedHistory.length,
        filePartCount: imageForGeminiContextFileUri?.length || 0,
      });
      const { finalMessageUpdates } = await handleGeminiResponse({
        thinkingMessageId,
        geminiPromptText,
        sanitizedDerivedHistory,
        systemInstructionForGemini,
        imageForGeminiContextFileUri,
        currentSettingsVal,
      });
      markSendStage('send.gemini.done', {
        thinkingMessageId,
        hasRawResponse: Boolean(finalMessageUpdates.llmRawResponse || finalMessageUpdates.rawAssistantResponse),
      });

      // Early suggestion fetch
      try {
        const textForSuggestionsEarly = finalMessageUpdates.llmRawResponse || finalMessageUpdates.rawAssistantResponse || (finalMessageUpdates.translations?.find(tr => tr.target)?.target) || "";
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
      markSendStage('send.complete', {
        thinkingMessageId,
      });

      // Resume STT if needed
      const isSpeechActive = speechIsSpeakingRef.current || (typeof hasPendingQueueItems === 'function' && hasPendingQueueItems());
      if (sttInterruptedBySendRef.current && settingsRef.current.stt.enabled && !isSpeechActive) {
        try {
          markSendStage('send.resumeStt.start');
          startListening(settingsRef.current.stt.language);
          markSendStage('send.resumeStt.done');
        } finally {
          sttInterruptedBySendRef.current = false;
        }
      }

      // Fetch suggestions if TTS not supported
      if (!isSpeechSynthesisSupported) {
        const finalAssistantMessage = messagesRef.current.find(m => m.id === thinkingMessageId);
        if (finalAssistantMessage && finalAssistantMessage.role === 'assistant' &&
          (finalAssistantMessage.llmRawResponse || finalAssistantMessage.rawAssistantResponse || (finalAssistantMessage.translations && finalAssistantMessage.translations.length > 0)) &&
          !suggestionsTokenRef.current &&
          finalAssistantMessage.id !== lastFetchedSuggestionsForRef.current) {
          const textForSuggestions = finalAssistantMessage.llmRawResponse ||
            finalAssistantMessage.rawAssistantResponse ||
            (finalAssistantMessage.translations?.find(tr => tr.target)?.target) || "";
          if (textForSuggestions.trim()) {
            requestReplySuggestions(finalAssistantMessage.id, textForSuggestions, getHistoryRespectingBookmark(messagesRef.current));
          }
        }
      }

      return true;

      } catch (error) {
        return handleSendFailure(error);
      }
    } catch (error) {
      return handleSendFailure(error);
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
    ensureUploadedAttachmentVariantsForMessage,
    uploadAttachmentVariantsForSource,
    resolveBookmarkContextSummary,
    handleGeminiResponse,
    runUserImageGeneration,
    runAssistantImageGeneration,
    requestReplySuggestions,
    speakMessage,
    isSpeechSynthesisSupported,
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
