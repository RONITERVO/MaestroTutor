// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { logSttFlow, warnSttFlow, errorSttFlow } from '../../../shared/utils/sttFlowDebug';
// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { createAssistantTools } from '../coordinators/assistantTools';
import { createGeneratedImages } from '../coordinators/generatedImages';
import { createMediaPersistence } from '../coordinators/mediaPersistence';
import { createSendCoordinator } from '../coordinators/send';
import { createTextResponseCoordinator } from '../coordinators/textResponse';
import { truncateForToolPrompt } from '../coordinators/toolPromptContext';
import { createUserMessageCoordinator } from '../coordinators/userMessage';

import { createAttachmentUploads } from '../coordinators/attachmentUploads';
import type { UseTutorConversationConfig, UseTutorConversationReturn } from '../coordinators/conversationContracts';
import { createSuggestionCoordinator } from '../coordinators/suggestions';
export type { UseTutorConversationConfig, UseTutorConversationReturn } from '../coordinators/conversationContracts';
/** React/store composition for chat. Coordinators own send, suggestion, media
 * and response workflows; this adapter supplies live state and browser ports. */

import { useCallback, useEffect, useMemo, useRef } from 'react';
import { useShallow } from 'zustand/shallow';
import { deriveHistoryForApi, INLINE_CAP_AUDIO, safeSaveChatHistoryDB } from '..';
import { checkFileStatuses, sanitizeHistoryWithVerifiedUris, uploadMediaToFiles } from '../../../api/gemini/files';
import { translateText, type GeminiProgressEvent } from '../../../api/gemini/generative';
import { runMaestroImageGeneration, runReplySuggestions, runTutorTextTurn } from '../../../api/gemini/journeys';
import { ensureMaestroAvatarUris, invalidateMaestroAvatarCache } from '../../../api/gemini/maestroAvatarEnsure';
import { generateMusic } from '../../../api/gemini/music';
import type { TranslationFunction } from '../../../app/hooks/useTranslations';
import {
  getVisibleAssistantMessageText,
} from '../../../core-sdk/chat/assistantMessageContext';
import { buildAttachmentUploadPlans as buildCoreAttachmentUploadPlans } from '../../../core-sdk/chat/attachmentUploadPlans';
import { extractOfficeTextForUpload } from '../../../core-sdk/chat/officeTextExtraction';
import {
  normalizeSuggestionCreatorToolRequest as normalizeCoreSuggestionCreatorToolRequest,
} from '../../../core-sdk/chat/suggestionAftersteps';
import {
  parseStrictTutorResponseText,
  type StrictParsedTutorResponse,
} from '../../../core-sdk/chat/tutorResponse';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../core/config/activityTokens';
import { getGeminiModels } from '../../../core/config/models';
import {
  ChatMessage,
  RecordedUtterance,
  ReplySuggestion,
} from '../../../core/types';
import { normalizeSuggestionCreatorArtifact as normalizeCoreSuggestionCreatorArtifact } from '../../../platform/browser/assistantArtifacts';
import { isRealChatMessage } from '../../../shared/utils/common';
import { hasShownCostWarning, setCostWarningShown, trackGeminiUsage } from '../../../shared/utils/costTracker';
import { getPrimarySubtag } from '../../../shared/utils/languageUtils';
import { createSmartRef } from '../../../shared/utils/smartRef';
import { useMaestroStore } from '../../../store';
import { selectSelectedLanguagePair } from '../../../store/slices/settingsSlice';
import { selectIsCreatingSuggestion, selectIsListening, selectIsLoadingSuggestions, selectIsResponsePending, selectIsSpeaking } from '../../../store/slices/uiSlice';
import { getGlobalProfileDB, setAppSettingsDB, setGlobalProfileDB } from '../../session';
import { synthesizeGeminiAudioNote } from '../../speech/services/geminiLiveAudioNote';
import { createKeyframeFromVideoDataUrl, processMediaForUpload } from '../../vision';

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
    t, updateMessage, computeHistorySubsetForMedia,
    checkFileStatuses, uploadMediaToFiles, buildAttachmentUploadPlans,
  }), [
    t, updateMessage, computeHistorySubsetForMedia,
  ]);

  const handleReengagementThresholdChange = useCallback((newThreshold: number) => {
    setSettings(prev => {
      const next = {
        ...prev,
        smartReengagement: {
          ...prev.smartReengagement,
          thresholdSeconds: newThreshold,
        }
      };
      setAppSettingsDB(next).catch(() => { });
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

  const { optimizeAndUploadMedia, attachGeneratedToolMedia } = useMemo(() => createMediaPersistence({
    t, updateMessage, sendWithFileUploadInProgressRef,
    setSendPrep, processMediaForUpload, uploadMediaToFiles,
  }), [
    t, updateMessage, sendWithFileUploadInProgressRef,
    setSendPrep, processMediaForUpload, uploadMediaToFiles,
  ]);

  const { runUserImageGeneration, runAssistantImageGeneration } = useMemo(() => createGeneratedImages({
    t, updateMessage, addMessage,
    getHistoryRespectingBookmark, computeMaxMessagesForArray, maestroAvatarUriRef,
    maestroAvatarMimeTypeRef, messagesRef, sendWithFileUploadInProgressRef,
    setSendPrep, optimizeAndUploadMedia, ensureUrisForHistoryForSend,
    resolveBookmarkContextSummary, addImageLoadDuration, runMaestroImageGeneration,
    sanitizeHistoryWithVerifiedUris, deriveHistoryForApi, getGlobalProfileDB,
    hasShownCostWarning, setCostWarningShown,
  }), [
    t, updateMessage, addMessage,
    getHistoryRespectingBookmark, computeMaxMessagesForArray, maestroAvatarUriRef,
    maestroAvatarMimeTypeRef, messagesRef, sendWithFileUploadInProgressRef,
    setSendPrep, optimizeAndUploadMedia, ensureUrisForHistoryForSend,
    resolveBookmarkContextSummary, addImageLoadDuration, runMaestroImageGeneration,
    sanitizeHistoryWithVerifiedUris, deriveHistoryForApi, getGlobalProfileDB,
    hasShownCostWarning, setCostWarningShown,
  ]);

  const { executeAssistantToolRequest } = useMemo(() => createAssistantTools({
    updateMessage, messagesRef, selectedLanguagePairRef,
    settingsRef, runAssistantImageGeneration, attachGeneratedToolMedia,
    synthesizeGeminiAudioNote, generateMusic,
  }), [
    updateMessage, messagesRef, selectedLanguagePairRef,
    settingsRef, runAssistantImageGeneration, attachGeneratedToolMedia,
    synthesizeGeminiAudioNote, generateMusic,
  ]);

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
    runReplySuggestions,
    normalizeSuggestionCreatorArtifact,
    normalizeSuggestionCreatorToolRequest,
    executeAssistantToolRequest,
    addMessage,
    updateMessage,
    setReplySuggestions,
    setSuggestionsLoadingStreamText,
    getHistoryRespectingBookmark,
    handleReengagementThresholdChange,
    formatGeminiStatusLine,
    trackGeminiUsage,
  }), [
    executeAssistantToolRequest, handleReengagementThresholdChange, getHistoryRespectingBookmark,
    messagesRef, normalizeSuggestionCreatorToolRequest, normalizeSuggestionCreatorArtifact,
    selectedLanguagePairRef, settingsRef, isLoadingSuggestions,
    addMessage, addActivityToken, removeActivityToken,
    setReplySuggestions, setSuggestionsLoadingStreamText, formatGeminiStatusLine,
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

  const createUserMessage = useMemo(() => createUserMessageCoordinator({
    diagnostics: { logSttFlow },
    t,
    addMessage,
    captureSnapshot,
    claimRecordedUtterance,
    attachedImageBase64,
    attachedImageMimeType,
    attachedFileName,
    recordedUtterancePendingRef,
    sendWithFileUploadInProgressRef,
    setSendPrep,
    processMediaForUpload,
    INLINE_CAP_AUDIO,
  }), [
    t, addMessage, captureSnapshot,
    claimRecordedUtterance, attachedImageBase64, attachedImageMimeType,
    attachedFileName, recordedUtterancePendingRef, sendWithFileUploadInProgressRef,
    setSendPrep, processMediaForUpload, INLINE_CAP_AUDIO,
  ]);

  const handleGeminiResponse = useMemo(() => createTextResponseCoordinator({
    diagnostics: { logSttFlow, errorSttFlow },
    t,
    setSettings,
    updateMessage,
    messagesRef,
    selectedLanguagePairRef,
    runTutorTextTurn,
    trackGeminiUsage,
    setLatestGroundingChunks,
    formatGeminiPhaseLabel,
    formatGeminiStatusLine,
  }), [
    t, setSettings, updateMessage,
    messagesRef, selectedLanguagePairRef, runTutorTextTurn,
    trackGeminiUsage, setLatestGroundingChunks, formatGeminiPhaseLabel,
    formatGeminiStatusLine,
  ]);

  // Main send message handler
  const handleSendMessageInternal = useMemo(() => createSendCoordinator({
    diagnostics: { logSttFlow, warnSttFlow, errorSttFlow },
    state: { settingsRef, selectedLanguagePairRef, messagesRef, isLoadingHistoryRef, isResponsePendingRef, speechIsSpeakingRef, sendingTokenRef, suggestionsTokenRef, recordedUtterancePendingRef, sttInterruptedBySendRef, sendWithFileUploadInProgressRef, lastFetchedSuggestionsForRef, pendingRecordedAudioMessageRef, maestroAvatarUriRef, maestroAvatarMimeTypeRef },
    view: { t, setReplySuggestions, setSuggestionsLoadingStreamText, setSnapshotUserError, setSendPrep, setAttachedImage, setLatestGroundingChunks, onApiKeyGateOpen, currentSystemPromptText, attachedFileName, transcript },
    speech: { claimRecordedUtterance, stopListening, clearTranscript, startListening, speakMessage, hasPendingQueueItems, isSpeechSynthesisSupported, isListening: () => selectIsListening(useMaestroStore.getState()) },
    activity: { addActivityToken, removeActivityToken },
    reengagement: { scheduleReengagementRef, cancelReengagementRef },
    messages: { addMessage, updateMessage },
    context: { getHistoryRespectingBookmark, computeMaxMessagesForArray, resolveBookmarkContextSummary, getGlobalProfileDB, ensureMaestroAvatarUris, sanitizeHistoryWithVerifiedUris },
    attachments: { ensureUploadedAttachmentVariantsForMessage, uploadAttachmentVariantsForSource, ensureUrisForHistoryForSend, processMediaForUpload },
    journeys: { createUserMessage, handleGeminiResponse, runUserImageGeneration, requestReplySuggestions },
  }), [
    settingsRef, selectedLanguagePairRef, messagesRef,
    isLoadingHistoryRef, isResponsePendingRef, speechIsSpeakingRef,
    sendingTokenRef, suggestionsTokenRef, recordedUtterancePendingRef,
    sttInterruptedBySendRef, sendWithFileUploadInProgressRef, lastFetchedSuggestionsForRef,
    pendingRecordedAudioMessageRef, maestroAvatarUriRef, maestroAvatarMimeTypeRef,
    t, setReplySuggestions, setSuggestionsLoadingStreamText,
    setSnapshotUserError, setSendPrep, setAttachedImage,
    setLatestGroundingChunks, onApiKeyGateOpen, currentSystemPromptText,
    attachedFileName, transcript, claimRecordedUtterance,
    stopListening, clearTranscript, startListening,
    speakMessage, hasPendingQueueItems, isSpeechSynthesisSupported,
    addActivityToken, removeActivityToken, scheduleReengagementRef,
    cancelReengagementRef, addMessage, updateMessage,
    getHistoryRespectingBookmark, computeMaxMessagesForArray, resolveBookmarkContextSummary,
    getGlobalProfileDB, ensureMaestroAvatarUris, sanitizeHistoryWithVerifiedUris,
    ensureUploadedAttachmentVariantsForMessage, uploadAttachmentVariantsForSource, ensureUrisForHistoryForSend,
    processMediaForUpload, createUserMessage, handleGeminiResponse,
    runUserImageGeneration, requestReplySuggestions,
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

