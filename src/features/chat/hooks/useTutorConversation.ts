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
  RecordedUtterance,
  UploadedAttachmentVariant,
} from '../../../core/types';
import { ApiError } from '../../../api/gemini/client';
import { generateGeminiResponse, translateText, type GeminiProgressEvent } from '../../../api/gemini/generative';
import { sanitizeHistoryWithVerifiedUris, uploadMediaToFiles, checkFileStatuses } from '../../../api/gemini/files';
import { generateMusic } from '../../../api/gemini/music';
import { generateImage } from '../../../api/gemini/vision';
import { ensureMaestroAvatarUris, invalidateMaestroAvatarCache } from '../../../api/gemini/maestroAvatarEnsure';
import { getGlobalProfileDB, setGlobalProfileDB, setAppSettingsDB } from '../../session';
import { safeSaveChatHistoryDB, deriveHistoryForApi, INLINE_CAP_AUDIO } from '..';
import { processMediaForUpload, createKeyframeFromVideoDataUrl } from '../../vision';
import { getOfficePreview } from '../utils/officePreview';
import { isGoogleWorkspaceShortcutMimeType, isMicrosoftOfficeMimeType, normalizeAttachmentMimeType } from '../utils/fileAttachments';
import {
  buildUploadedAttachmentState,
  inferUploadedAttachmentTargetsForMimeType,
  normalizeUploadedAttachmentVariants,
  OFFICE_TEXT_UPLOADED_ATTACHMENT_VARIANT_ID,
  PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID,
  selectUploadedAttachmentParts,
  SVG_RASTER_UPLOADED_ATTACHMENT_VARIANT_ID,
  upsertUploadedAttachmentVariant,
  VIDEO_KEYFRAME_UPLOADED_ATTACHMENT_VARIANT_ID,
} from '../utils/uploadedAttachmentVariants';
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
import { synthesizeGeminiAudioNote } from '../../speech/services/geminiLiveAudioNote';
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

const DEFAULT_ARTIFACT_FILE_NAMES: Record<string, string> = {
  'image/svg+xml': 'artifact.svg',
  'text/html': 'artifact.html',
  'text/markdown': 'artifact.md',
  'text/csv': 'artifact.csv',
  'text/tab-separated-values': 'artifact.tsv',
  'application/json': 'artifact.json',
  'application/xml': 'artifact.xml',
  'text/xml': 'artifact.xml',
  'text/css': 'artifact.css',
  'text/javascript': 'artifact.js',
  'application/javascript': 'artifact.js',
  'text/typescript': 'artifact.ts',
  'text/plain': 'artifact.txt',
};

const sanitizeArtifactFileName = (value?: string | null, mimeType?: string | null): string => {
  const raw = (value || '').trim();
  const fallback = DEFAULT_ARTIFACT_FILE_NAMES[(mimeType || '').trim().toLowerCase()] || 'artifact.txt';
  const normalized = (raw || fallback).replace(/[\\/:*?"<>|]/g, '-').replace(/\s+/g, '_');
  return normalized || fallback;
};

const inferMimeTypeFromDataUrl = (value?: string | null): string | null => {
  const match = typeof value === 'string' ? value.match(/^data:([^;,]+)(?:;[^,]*)?,/i) : null;
  return match?.[1]?.trim().toLowerCase() || null;
};

type MediaPayloadOverride = {
  oldUri?: string;
  newUri?: string;
  newMimeType?: string;
  newVariants?: UploadedAttachmentVariant[];
  transient?: boolean;
  omitFromPayload?: boolean;
};

type AttachmentUploadPlan = {
  id: string;
  source: UploadedAttachmentVariant['source'];
  targets: UploadedAttachmentVariant['targets'];
  order: number;
  build: () => Promise<{ dataUrl: string; mimeType: string; displayName?: string }>;
};

const getMessageAttachmentSource = (
  message: Pick<ChatMessage, 'imageUrl' | 'imageMimeType' | 'storageOptimizedImageUrl' | 'storageOptimizedImageMimeType' | 'attachmentName'>
): { dataUrl: string; mimeType: string; attachmentName?: string } | null => {
  if (typeof message.imageUrl === 'string' && message.imageUrl && typeof message.imageMimeType === 'string' && message.imageMimeType) {
    return {
      dataUrl: message.imageUrl,
      mimeType: message.imageMimeType,
      attachmentName: message.attachmentName,
    };
  }

  if (
    typeof message.storageOptimizedImageUrl === 'string' &&
    message.storageOptimizedImageUrl &&
    typeof message.storageOptimizedImageMimeType === 'string' &&
    message.storageOptimizedImageMimeType
  ) {
    return {
      dataUrl: message.storageOptimizedImageUrl,
      mimeType: message.storageOptimizedImageMimeType,
      attachmentName: message.attachmentName,
    };
  }

  return null;
};

const buildAttachmentUploadPlans = (
  source: { dataUrl: string; mimeType: string; attachmentName?: string },
  t: TranslationFunction
): AttachmentUploadPlan[] => {
  const mimeType = (source.mimeType || '').trim().toLowerCase();
  const displayName = source.attachmentName || 'attachment';

  if (mimeType.startsWith('video/')) {
    return [
      {
        id: VIDEO_KEYFRAME_UPLOADED_ATTACHMENT_VARIANT_ID,
        source: 'video-keyframe',
        targets: ['chat', 'image-generation'],
        order: 0,
        build: async () => {
          const keyframe = await createKeyframeFromVideoDataUrl(source.dataUrl, {
            at: 'start',
            maxDim: 768,
            quality: 0.75,
            outputMime: 'image/jpeg',
          });
          return {
            dataUrl: keyframe.dataUrl,
            mimeType: keyframe.mimeType,
            displayName: `${displayName}-keyframe.jpg`,
          };
        },
      },
      {
        id: PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID,
        source: 'original',
        targets: ['chat'],
        order: 10,
        build: async () => ({
          dataUrl: source.dataUrl,
          mimeType: source.mimeType,
          displayName,
        }),
      },
    ];
  }

  if (isOfficeMimeUnsupportedByGemini(mimeType)) {
    return [
      {
        id: OFFICE_TEXT_UPLOADED_ATTACHMENT_VARIANT_ID,
        source: 'office-text',
        targets: ['chat'],
        order: 0,
        build: async () => {
          const preview = await getOfficePreview(source.dataUrl, source.mimeType, source.attachmentName);
          const extracted = (preview.text || '').trim();
          if (!extracted) {
            throw new Error(preview.note || 'No extracted Office text available for Gemini upload conversion.');
          }
          return {
            dataUrl: toUtf8Base64DataUrl('text/plain', extracted),
            mimeType: 'text/plain',
            displayName: `${displayName}.txt`,
          };
        },
      },
    ];
  }

  if (isSvgMimeType(mimeType)) {
    return [
      {
        id: SVG_RASTER_UPLOADED_ATTACHMENT_VARIANT_ID,
        source: 'svg-rasterized',
        targets: ['chat', 'image-generation'],
        order: 0,
        build: async () => {
          const rasterized = await processMediaForUpload(source.dataUrl, source.mimeType, { t });
          if (
            !rasterized.dataUrl ||
            !rasterized.mimeType ||
            !rasterized.mimeType.startsWith('image/') ||
            isSvgMimeType(rasterized.mimeType)
          ) {
            throw new Error(`SVG rasterization did not produce a supported image MIME: ${rasterized.mimeType}`);
          }
          return {
            dataUrl: rasterized.dataUrl,
            mimeType: rasterized.mimeType,
            displayName: `${displayName}.jpg`,
          };
        },
      },
    ];
  }

  return [
    {
      id: PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID,
      source: 'original',
      targets: inferUploadedAttachmentTargetsForMimeType(mimeType),
      order: 10,
      build: async () => ({
        dataUrl: source.dataUrl,
        mimeType: source.mimeType,
        displayName,
      }),
    },
  ];
};

type StrictParsedTutorResponse = {
  translations: Array<{ target: string; native: string }>;
  visibleText: string;
  hasSkippedNonLanguageContent: boolean;
};

type SuggestionCreatorArtifact = {
  mimeType?: string;
  fileName?: string;
  encoding?: string;
  content?: string;
};

type MaestroToolKind = NonNullable<ChatMessage['maestroToolKind']>;
type ToolAttachmentPhase = NonNullable<ChatMessage['toolAttachmentPhase']>;

type SuggestionCreatorToolRequest = {
  tool?: string;
  prompt?: string;
  text?: string;
  durationSeconds?: number;
  musicDurationSeconds?: number;
};

const SUPPORTED_MAESTRO_TOOLS: MaestroToolKind[] = ['image', 'audio-note', 'music'];

const isSupportedMaestroTool = (value: unknown): value is MaestroToolKind => (
  typeof value === 'string' &&
  SUPPORTED_MAESTRO_TOOLS.includes(value as MaestroToolKind)
);

const truncateForToolPrompt = (value: string, maxChars: number = 420): string => {
  const normalized = (value || '').replace(/\s+/g, ' ').trim();
  if (!normalized) return '';
  return normalized.length > maxChars ? `${normalized.slice(0, maxChars - 1)}…` : normalized;
};

const getVisibleAssistantMessageText = (message?: ChatMessage | null): string => {
  if (!message) return '';
  if (message.translations?.length) {
    return message.translations
      .map(pair => [pair?.target?.trim(), pair?.native?.trim()].filter(Boolean).join('\n'))
      .filter(Boolean)
      .join('\n');
  }
  return message.rawAssistantResponse || message.text || '';
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

  const parseStrictTutorResponse = useCallback((responseText: string | undefined): StrictParsedTutorResponse => {
    if (typeof responseText !== 'string' || !responseText.trim() || !selectedLanguagePairRef.current) {
      return { translations: [], visibleText: '', hasSkippedNonLanguageContent: false };
    }

    const lines = responseText
      .replace(/\r\n/g, '\n')
      .replace(/\r/g, '\n')
      .split('\n')
      .map(line => line.trim());
    const nativeLangPrefix = `[${getShortLangCodeForPrompt(selectedLanguagePairRef.current.nativeLanguageCode)}]`;
    const translations: Array<{ target: string; native: string }> = [];
    let hasSkippedNonLanguageContent = false;

    for (let i = 0; i < lines.length; i++) {
      const currentLine = lines[i];
      if (!currentLine) continue;

      if (currentLine.startsWith(nativeLangPrefix)) {
        hasSkippedNonLanguageContent = true;
        continue;
      }

      let nextIndex = i + 1;
      while (nextIndex < lines.length && !lines[nextIndex].trim()) {
        nextIndex++;
      }

      const nextLine = nextIndex < lines.length ? lines[nextIndex].trim() : '';
      if (nextLine && nextLine.startsWith(nativeLangPrefix)) {
        translations.push({
          target: currentLine,
          native: nextLine.substring(nativeLangPrefix.length).trim(),
        });
        i = nextIndex;
        continue;
      }

      hasSkippedNonLanguageContent = true;
    }

    const visibleLines = translations.flatMap(pair => [
      pair.target,
      pair.native ? `${nativeLangPrefix} ${pair.native}` : '',
    ].filter(Boolean));

    return {
      translations,
      visibleText: visibleLines.join('\n').trim(),
      hasSkippedNonLanguageContent,
    };
  }, [selectedLanguagePairRef]);

  const parseGeminiResponse = useCallback((responseText: string | undefined): Array<{ target: string; native: string }> => {
    return parseStrictTutorResponse(responseText).translations;
  }, [parseStrictTutorResponse]);

  const normalizeSuggestionCreatorArtifact = useCallback((artifact: unknown) => {
    if (!artifact || typeof artifact !== 'object') return null;

    const candidate = artifact as SuggestionCreatorArtifact;
    const rawContent = typeof candidate.content === 'string' ? candidate.content : '';
    const rawEncoding = typeof candidate.encoding === 'string' ? candidate.encoding.trim().toLowerCase() : 'text';
    const isDataUrlEncoding = rawEncoding === 'data-url' || rawEncoding === 'dataurl' || rawEncoding === 'data_url';
    if (!rawContent.trim()) return null;

    let mimeType = typeof candidate.mimeType === 'string' ? candidate.mimeType.trim().toLowerCase() : '';
    let dataUrl: string;

    if (isDataUrlEncoding) {
      dataUrl = rawContent.trim();
      if (!/^data:[^,]+,/i.test(dataUrl)) return null;
      mimeType = mimeType || inferMimeTypeFromDataUrl(dataUrl) || '';
    } else {
      mimeType = mimeType || normalizeAttachmentMimeType({ name: candidate.fileName || 'artifact.txt', type: 'text/plain' });
      const normalizedContent = rawContent.replace(/\r\n/g, '\n').replace(/\r/g, '\n').trim();
      if (!normalizedContent) return null;
      if (mimeType.startsWith('image/') && mimeType !== 'image/svg+xml') {
        return null;
      }
      dataUrl = toUtf8Base64DataUrl(mimeType || 'text/plain', normalizedContent);
    }

    const fileName = sanitizeArtifactFileName(candidate.fileName, mimeType);
    return {
      dataUrl,
      mimeType: mimeType || 'text/plain',
      fileName,
    };
  }, []);

  const normalizeSuggestionCreatorToolRequest = useCallback((toolRequest: unknown, assistantMessageId: string) => {
    if (!toolRequest || typeof toolRequest !== 'object') return null;

    const candidate = toolRequest as SuggestionCreatorToolRequest;
    const toolValue = typeof candidate.tool === 'string' ? candidate.tool.trim().toLowerCase() : '';
    if (!isSupportedMaestroTool(toolValue)) return null;

    if (toolValue === 'image' && !settingsRef.current.imageGenerationModeEnabled) {
      return null;
    }

    const assistantMessage = messagesRef.current.find(message => message.id === assistantMessageId);
    const fallbackText = truncateForToolPrompt(getVisibleAssistantMessageText(assistantMessage), 500);
    const prompt = typeof candidate.prompt === 'string' ? candidate.prompt.trim() : '';
    const text = typeof candidate.text === 'string' ? candidate.text.trim() : '';
    const rawDuration = Number(candidate.durationSeconds ?? candidate.musicDurationSeconds);
    const durationSeconds = Number.isFinite(rawDuration)
      ? Math.max(8, Math.min(20, Math.round(rawDuration)))
      : undefined;

    if (toolValue === 'audio-note') {
      return {
        tool: toolValue,
        text: text || prompt || fallbackText,
      } as const;
    }

    if (toolValue === 'music') {
      return {
        tool: toolValue,
        prompt: prompt || text || truncateForToolPrompt(fallbackText, 280),
        durationSeconds,
      } as const;
    }

    return {
      tool: toolValue,
      prompt: prompt || text || truncateForToolPrompt(fallbackText, 280),
    } as const;
  }, [messagesRef, settingsRef]);

  const finalizeAssistantArtifact = useCallback((assistantMessageId: string, artifact: unknown) => {
    const normalized = normalizeSuggestionCreatorArtifact(artifact);
    const updates: Partial<ChatMessage> = {
      isLoadingArtifact: false,
      artifactLoadStartTime: undefined,
    };

    if (normalized) {
      updates.imageUrl = normalized.dataUrl;
      updates.imageMimeType = normalized.mimeType;
      updates.attachmentName = normalized.fileName;
      updates.storageOptimizedImageUrl = undefined;
      updates.storageOptimizedImageMimeType = undefined;
      updates.uploadedFileVariants = undefined;
      updates.uploadedFileUri = undefined;
      updates.uploadedFileMimeType = undefined;
    }

    updateMessage(assistantMessageId, updates);
  }, [normalizeSuggestionCreatorArtifact, updateMessage]);

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

  const setThinkingStatusLine = useCallback((messageId: string, line?: string) => {
    const cleanedLine = typeof line === 'string' ? line.trim() : '';
    const current = messagesRef.current.find(m => m.id === messageId);
    if (!current || !current.thinking) return;

    const nextValue = cleanedLine || undefined;
    if ((current.thinkingStatusLine || undefined) === nextValue) return;
    updateMessage(messageId, { thinkingStatusLine: nextValue });
  }, [messagesRef, updateMessage]);

  const formatGeminiStatusLine = useCallback((event: GeminiProgressEvent): string | undefined => {
    const elapsedSeconds = typeof event.elapsedMs === 'number'
      ? Math.max(0, Math.floor(event.elapsedMs / 1000))
      : undefined;

    switch (event.phase) {
      case 'attempt-start':
        return `Connecting to ${event.model} (attempt ${event.attempt}/${event.totalAttempts})...`;
      case 'attempt-processing':
        if (typeof elapsedSeconds !== 'number') return undefined;
        return `Waiting for model output... ${elapsedSeconds}s elapsed.`;
      case 'high-demand':
        if (event.reason === 'no-output-timeout' && typeof elapsedSeconds === 'number' && elapsedSeconds > 0) {
          return `No model output after ${elapsedSeconds}s. Request is likely queued on Google servers.`;
        }
        return 'High demand detected. Request is queued on Google servers.';
      case 'fallback-switch':
        return `Switching to fallback model ${event.model}.`;
      case 'retry-scheduled':
        return `Retrying in ${Math.ceil((event.retryInMs || 0) / 1000)}s...`;
      case 'success':
        return undefined;
      default:
        return undefined;
    }
  }, []);

  const formatGeminiPhaseLabel = useCallback((event: GeminiProgressEvent): string | undefined => {
    switch (event.phase) {
      case 'attempt-start':
        return 'Connecting';
      case 'attempt-processing':
        return 'Processing';
      case 'high-demand':
        return 'High demand';
      case 'fallback-switch':
        return 'Switching model';
      case 'retry-scheduled':
        return 'Retrying';
      case 'success':
        return 'Finalizing';
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

  const ensureUploadedAttachmentVariantsForMessage = useCallback(async (
    message: ChatMessage,
    knownStatuses?: Record<string, { deleted: boolean; active: boolean }>
  ): Promise<{ variants: UploadedAttachmentVariant[]; payloadFiles: Array<{ fileUri: string; mimeType: string }> }> => {
    let nextVariants = normalizeUploadedAttachmentVariants(message);
    if (knownStatuses) {
      nextVariants = nextVariants.filter(variant => !knownStatuses[variant.uri]?.deleted);
    }
    const source = getMessageAttachmentSource(message);
    const plans = source ? buildAttachmentUploadPlans(source, t) : [];

    if (plans.length > 0) {
      let cachedStatuses: Record<string, { deleted: boolean; active: boolean }> = knownStatuses || {};
      const plannedVariantIds = new Set(plans.map(plan => plan.id));
      const existingUris = nextVariants
        .filter(variant => plannedVariantIds.has(variant.id))
        .map(variant => variant.uri);

      if (!knownStatuses && existingUris.length > 0) {
        try {
          cachedStatuses = await checkFileStatuses(Array.from(new Set(existingUris)));
        } catch {
          cachedStatuses = {};
        }
      }

      for (const plan of plans) {
        const existingVariant = nextVariants.find(variant => variant.id === plan.id);
        const existingDeleted = !!(existingVariant && cachedStatuses[existingVariant.uri]?.deleted);
        if (existingVariant && !existingDeleted) {
          nextVariants = upsertUploadedAttachmentVariant(nextVariants, {
            ...existingVariant,
            id: plan.id,
            source: plan.source,
            targets: plan.targets,
            order: plan.order,
          });
          continue;
        }

        if (!source) continue;

        try {
          const uploadSource = await plan.build();
          const upload = await uploadMediaToFiles(
            uploadSource.dataUrl,
            uploadSource.mimeType,
            uploadSource.displayName || message.attachmentName || 'send-history'
          );
          nextVariants = upsertUploadedAttachmentVariant(nextVariants, {
            id: plan.id,
            uri: upload.uri,
            mimeType: upload.mimeType,
            targets: plan.targets,
            source: plan.source,
            order: plan.order,
          });
        } catch (error) {
          console.warn(`[ensureUploads] Failed to upload ${plan.id} variant for message ${message.id}.`, error);
          nextVariants = nextVariants.filter(variant => variant.id !== plan.id);
        }
      }
    }

    const normalizedState = buildUploadedAttachmentState(nextVariants);
    const normalizedCurrentVariants = normalizeUploadedAttachmentVariants(message);
    const variantsChanged = JSON.stringify(normalizedCurrentVariants) !== JSON.stringify(nextVariants);
    const primaryChanged =
      message.uploadedFileUri !== normalizedState.uploadedFileUri ||
      message.uploadedFileMimeType !== normalizedState.uploadedFileMimeType;

    if (variantsChanged || primaryChanged) {
      updateMessage(message.id, normalizedState);
      message.uploadedFileVariants = normalizedState.uploadedFileVariants;
      message.uploadedFileUri = normalizedState.uploadedFileUri;
      message.uploadedFileMimeType = normalizedState.uploadedFileMimeType;
    }

    return {
      variants: normalizedState.uploadedFileVariants || [],
      payloadFiles: selectUploadedAttachmentParts(
        {
          uploadedFileUri: normalizedState.uploadedFileUri,
          uploadedFileMimeType: normalizedState.uploadedFileMimeType,
          uploadedFileVariants: normalizedState.uploadedFileVariants,
        },
        'chat'
      ),
    };
  }, [t, updateMessage]);

  const ensureUrisForHistoryForSend = useCallback(async (
    arr: ChatMessage[], 
    onProgress?: (done: number, total: number, etaMs?: number) => void
  ): Promise<Record<string, MediaPayloadOverride>> => {
    const candidates = computeHistorySubsetForMedia(arr);

    const mediaIndices: number[] = [];
    for (let i = 0; i < candidates.length; i++) {
      const m = candidates[i];
      const hasMedia = !!getMessageAttachmentSource(m) || normalizeUploadedAttachmentVariants(m).length > 0;
      if (hasMedia) mediaIndices.push(i);
    }
    const maxMedia = MAX_MEDIA_TO_KEEP;
    const keepMediaIdx = new Set<number>(mediaIndices.slice(-maxMedia));

    const cachedUrisToCheck: string[] = [];
    for (let i = 0; i < candidates.length; i++) {
      if (!keepMediaIdx.has(i)) continue;
      const m0 = candidates[i];
      normalizeUploadedAttachmentVariants(m0).forEach((variant) => {
        if (variant.uri) cachedUrisToCheck.push(variant.uri);
      });
    }
    let cachedStatuses: Record<string, { deleted: boolean; active: boolean }> = {};
    try {
      const uniqUris = Array.from(new Set(cachedUrisToCheck));
      if (uniqUris.length) cachedStatuses = await checkFileStatuses(uniqUris);
    } catch { cachedStatuses = {}; }

    let totalToEnsure = 0;
    for (let i = 0; i < candidates.length; i++) {
      if (!keepMediaIdx.has(i)) continue;
      const message = candidates[i];
      const source = getMessageAttachmentSource(message);
      if (!source) continue;

      const plans = buildAttachmentUploadPlans(source, t);
      const existingVariants = normalizeUploadedAttachmentVariants(message);
      const needsUpload = plans.some((plan) => {
        const existingVariant = existingVariants.find(variant => variant.id === plan.id);
        if (!existingVariant) return true;
        return !!cachedStatuses[existingVariant.uri]?.deleted;
      });

      if (needsUpload) totalToEnsure++;
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
      const previousPrimary = buildUploadedAttachmentState(normalizeUploadedAttachmentVariants(m));
      try {
        const ensured = await ensureUploadedAttachmentVariantsForMessage(m, cachedStatuses);
        const nextState = buildUploadedAttachmentState(ensured.variants);
        const payloadFiles = ensured.payloadFiles;

        if (
          payloadFiles.length === 0 &&
          (getMessageAttachmentSource(m) || previousPrimary.uploadedFileUri || (previousPrimary.uploadedFileVariants && previousPrimary.uploadedFileVariants.length > 0))
        ) {
          console.warn(`[ensureUris] Message ${m.id} has no valid uploaded payload variants and will be omitted from this payload.`);
          updatedUriMap[m.id] = {
            oldUri: previousPrimary.uploadedFileUri,
            transient: true,
            omitFromPayload: true,
          };
        } else if (
          previousPrimary.uploadedFileUri !== nextState.uploadedFileUri ||
          previousPrimary.uploadedFileMimeType !== nextState.uploadedFileMimeType ||
          JSON.stringify(previousPrimary.uploadedFileVariants || []) !== JSON.stringify(nextState.uploadedFileVariants || [])
        ) {
          updatedUriMap[m.id] = {
            oldUri: previousPrimary.uploadedFileUri,
            newUri: nextState.uploadedFileUri,
            newMimeType: nextState.uploadedFileMimeType,
            newVariants: nextState.uploadedFileVariants,
          };
        }
      } catch (e) {
        console.warn('Pre-send URI ensure failed for message', m.id, e);
      }
      try { doneCount++; tick(); } catch {}
      await new Promise(r => setTimeout(r, 0));
    }
    return updatedUriMap;
  }, [computeHistorySubsetForMedia, ensureUploadedAttachmentVariantsForMessage, t]);

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
    let resolvedArtifact: unknown = null;
    let resolvedToolRequest: ReturnType<typeof normalizeSuggestionCreatorToolRequest> = null;

    const finishReplySuggestionsRequest = async () => {
      setSuggestionsLoadingStreamText('');
      if (suggestionsTokenRef.current) {
        removeActivityToken(suggestionsTokenRef.current);
        suggestionsTokenRef.current = null;
      }
      if (resolvedToolRequest) {
        await executeAssistantToolRequest(assistantMessageId, resolvedToolRequest);
      } else {
        finalizeAssistantArtifact(assistantMessageId, resolvedArtifact);
      }
    };

    // Check if already loading using token state
    if (isLoadingSuggestions) {
      return;
    }
    if (!lastTutorMessage.trim() || !selectedLanguagePairRef.current) {
      setReplySuggestions([]);
      await finishReplySuggestionsRequest();
      return;
    }

    // Check if suggestions already exist on message
    {
      const allMsgs = messagesRef.current;
      const targetIdx = allMsgs.findIndex(m => m.id === assistantMessageId);
      if (targetIdx !== -1) {
        const target = allMsgs[targetIdx];
        if (target && Array.isArray((target as any).replySuggestions) && (target as any).replySuggestions.length > 0) {
          const hasExistingAttachment = !!(
            (target.imageUrl && target.imageMimeType) ||
            (target.uploadedFileUri && target.uploadedFileMimeType)
          );
          const visibleText = getVisibleAssistantMessageText(target).trim();
          const hasStructuredTail = !!(
            target.llmRawResponse &&
            target.llmRawResponse.trim() &&
            target.llmRawResponse.trim() !== visibleText
          );
          if (hasExistingAttachment || !hasStructuredTail) {
            setReplySuggestions((target as any).replySuggestions as ReplySuggestion[]);
            await finishReplySuggestionsRequest();
            return;
          }
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
              const progressLine = formatGeminiStatusLine(event);
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
        resolvedArtifact = parsedResponse?.artifact ?? null;
        resolvedToolRequest = normalizeSuggestionCreatorToolRequest(parsedResponse?.toolRequest ?? null, assistantMessageId);
        if (resolvedToolRequest) {
          resolvedArtifact = null;
        }

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
    await finishReplySuggestionsRequest();
  }, [
    currentReplySuggestionsPromptText, 
    executeAssistantToolRequest,
    finalizeAssistantArtifact,
    handleReengagementThresholdChange, 
    getHistoryRespectingBookmark,
    messagesRef,
    normalizeSuggestionCreatorToolRequest,
    selectedLanguagePairRef,
    settingsRef,
    isLoadingSuggestions,
    addActivityToken,
    removeActivityToken,
    setReplySuggestions,
    setSuggestionsLoadingStreamText,
    formatGeminiStatusLine,
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

    if (existing && ((existing.imageUrl && existing.imageMimeType) || (existing.uploadedFileUri && existing.uploadedFileMimeType))) {
      return;
    }

    if (!toolRequest) {
      return;
    }

    if (toolRequest.tool === 'image') {
      const assistantMessage = messagesRef.current.find(m => m.id === assistantMessageId);

      // Prefer to use the full raw LLM response (or fallback to prompt only and then visible text. Why?: More context is useful for image generator.) 
      const fullRawText = assistantMessage?.llmRawResponse
        || toolRequest.prompt
        || assistantMessage?.rawAssistantResponse
        || getVisibleAssistantMessageText(assistantMessage);

      await runAssistantImageGeneration({
        thinkingMessageId: assistantMessageId,
        accumulatedFullText: fullRawText,
        currentSettingsVal: settingsRef.current,
      });
      return;
    }

    updateMessage(assistantMessageId, {
      isGeneratingToolAttachment: true,
      toolAttachmentStartTime: Date.now(),
      toolAttachmentPhase: 'pending' as ToolAttachmentPhase,
      maestroToolKind: toolRequest.tool,
    });

    try {
      if (toolRequest.tool === 'audio-note') {
        const selectedLanguagePair = selectedLanguagePairRef.current;
        const langCode = getPrimarySubtag(selectedLanguagePair?.targetLanguageCode || settingsRef.current.stt.language || 'en');
        const audioNote = await synthesizeGeminiAudioNote({
          text: truncateForToolPrompt(toolRequest.text || getVisibleAssistantMessageText(messagesRef.current.find(m => m.id === assistantMessageId)), 500),
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
        return;
      }

      if (toolRequest.tool === 'music') {
        const music = await generateMusic({
          prompt: (toolRequest.prompt || getVisibleAssistantMessageText(messagesRef.current.find(m => m.id === assistantMessageId))).trim(),
          durationSeconds: toolRequest.durationSeconds,
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
      }
    } catch (error) {
      console.warn(`[MaestroTool] ${toolRequest.tool} generation failed.`, error);
      updateMessage(assistantMessageId, {
        isGeneratingToolAttachment: false,
        toolAttachmentStartTime: undefined,
        toolAttachmentPhase: undefined,
      });
    }
  }

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
    imageForGeminiContextFileUri?: string | Array<{ fileUri: string; mimeType: string }>;
    currentSettingsVal: AppSettings;
  }) => {
    let lastProcessingBucket = -1;
    let streamingDraftText = '';
    let lastDraftFlushAt = 0;
    let thoughtBuffer = '';
    let lastThoughtFlushAt = 0;
    let currentPhaseLabel = '';
    let hasVisibleModelOutput = false;

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
      appendThinkingTrace(params.thinkingMessageId, condensed.slice(0, 220));
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
          const phaseLabel = formatGeminiPhaseLabel(event);
          if (phaseLabel && phaseLabel !== currentPhaseLabel) {
            currentPhaseLabel = phaseLabel;
            updateMessage(params.thinkingMessageId, { thinkingPhase: phaseLabel });
          }
          if (!hasVisibleModelOutput) {
            const line = formatGeminiStatusLine(event);
            if (line) {
              setThinkingStatusLine(params.thinkingMessageId, line);
            }
          }
        },
        onTextDelta: (_deltaText, fullText) => {
          hasVisibleModelOutput = true;
          streamingDraftText = fullText || streamingDraftText;
          setThinkingStatusLine(params.thinkingMessageId, undefined);
          if (currentPhaseLabel !== 'Final response') {
            currentPhaseLabel = 'Final response';
            updateMessage(params.thinkingMessageId, { thinkingPhase: 'Final response' });
          }
          flushThinkingDraft(false);
        },
        onThoughtDelta: (deltaThought) => {
          hasVisibleModelOutput = true;
          thoughtBuffer += deltaThought;
          setThinkingStatusLine(params.thinkingMessageId, undefined);
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
    const strictParsedResponse = parseStrictTutorResponse(accumulatedFullText);
    const responseTextForConversation = strictParsedResponse.visibleText;
    const parsedTranslationsOnComplete = strictParsedResponse.translations;
    const groundingChunks = response.candidates?.[0]?.groundingMetadata?.groundingChunks as GroundingChunk[] | undefined;
    if (groundingChunks?.length) {
      setLatestGroundingChunks(groundingChunks);
    }

    const finalMessageUpdates: Partial<ChatMessage> = {
      thinking: false,
      thinkingTrace: undefined,
      thinkingDraftText: undefined,
      thinkingPhase: undefined,
      thinkingStatusLine: undefined,
      translations: parsedTranslationsOnComplete.length > 0 ? parsedTranslationsOnComplete : undefined,
      llmRawResponse: accumulatedFullText,
      rawAssistantResponse: responseTextForConversation || undefined,
      text: parsedTranslationsOnComplete.length === 0 ? (responseTextForConversation || undefined) : undefined,
      isLoadingArtifact: strictParsedResponse.hasSkippedNonLanguageContent,
      artifactLoadStartTime: strictParsedResponse.hasSkippedNonLanguageContent ? Date.now() : undefined,
    };
    updateMessage(params.thinkingMessageId, finalMessageUpdates);

    return {
      accumulatedFullText: responseTextForConversation,
      finalMessageUpdates,
      hasAttachmentCandidate: strictParsedResponse.hasSkippedNonLanguageContent,
    };
  }, [appendThinkingTrace, formatGeminiPhaseLabel, formatGeminiStatusLine, parseStrictTutorResponse, setLatestGroundingChunks, setThinkingStatusLine, updateMessage]);

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
          imageForGeminiContextMimeType: upload.mimeType,
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
            uploadedFileVariants: undefined,
            imageFileUri: undefined,
            imageMimeType: undefined,
          } as ChatMessage;
        }
        if (upd.newUri || upd.newVariants) {
          const nextMime = upd.newMimeType || m.uploadedFileMimeType;
          const nextVariants = upd.newVariants || m.uploadedFileVariants;
          if (
            m.uploadedFileUri !== upd.newUri ||
            (upd.newMimeType && m.uploadedFileMimeType !== upd.newMimeType) ||
            (upd.newVariants && JSON.stringify(m.uploadedFileVariants || []) !== JSON.stringify(upd.newVariants || []))
          ) {
            return {
              ...m,
              uploadedFileUri: upd.newUri,
              uploadedFileMimeType: nextMime,
              uploadedFileVariants: nextVariants,
            } as ChatMessage;
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
        } catch (e) {
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
      thinkingTrace: [],
      thinkingDraftText: '',
      thinkingPhase: 'Preparing request',
      thinkingStatusLine: 'Preparing request context...',
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

    let imageForGeminiContextFileUri: string | Array<{ fileUri: string; mimeType: string }> | undefined = undefined;

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

    if (messageType === 'user' && userMessageId) {
      const currentUserMessage = messagesRef.current.find(m => m.id === userMessageId);
      if (currentUserMessage && getMessageAttachmentSource(currentUserMessage)) {
        try {
          if (!sendWithFileUploadInProgressRef.current) {
            sendWithFileUploadInProgressRef.current = true;
          }
          setSendPrep({ active: true, label: t('chat.sendPrep.uploadingMedia') || 'Uploading media...' });
          const ensured = await ensureUploadedAttachmentVariantsForMessage(currentUserMessage);
          if (ensured.payloadFiles.length > 0) {
            imageForGeminiContextFileUri = ensured.payloadFiles;
            imageForGeminiContextMimeType = ensured.payloadFiles[0]?.mimeType;
          }
        } catch (e) {
          console.warn('Failed to upload current media to Files API; will send without media', e);
          imageForGeminiContextFileUri = undefined;
        } finally {
          setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...' } : prev));
        }
      }
    } else if (imageForGeminiContextBase64 && imageForGeminiContextMimeType) {
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
        imageForGeminiContextMimeType = up.mimeType;
        imageForGeminiContextFileUri = [{ fileUri: up.uri, mimeType: up.mimeType }];
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
              uploadedFileVariants: undefined,
              imageFileUri: undefined,
              imageMimeType: undefined,
            } as ChatMessage;
          }
          if (upd.newUri || upd.newVariants) {
            const nextMime = upd.newMimeType || m.uploadedFileMimeType;
            const nextVariants = upd.newVariants || m.uploadedFileVariants;
            if (
              m.uploadedFileUri !== upd.newUri ||
              (upd.newMimeType && m.uploadedFileMimeType !== upd.newMimeType) ||
              (upd.newVariants && JSON.stringify(m.uploadedFileVariants || []) !== JSON.stringify(upd.newVariants || []))
            ) {
              return {
                ...m,
                uploadedFileUri: upd.newUri,
                uploadedFileMimeType: nextMime,
                uploadedFileVariants: nextVariants,
              } as ChatMessage;
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

      const { finalMessageUpdates } = await handleGeminiResponse({
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
    ensureUploadedAttachmentVariantsForMessage,
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
