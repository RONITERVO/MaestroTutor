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
import {
  decodeTextFromDataUrl,
  isGoogleWorkspaceShortcutMimeType,
  isMicrosoftOfficeMimeType,
  normalizeAttachmentMimeType,
} from '../utils/fileAttachments';
import { sanitizeSvgAnimationStructure } from '../utils/sanitizeSvgAnimationStructure';
import {
  buildCompactAssistantHistoryText,
  buildCompactAssistantRawText,
  getVisibleAssistantMessageText,
} from '../utils/assistantMessageContext';
import {
  buildUploadedAttachmentState,
  inferUploadedAttachmentTargetsForMimeType,
  normalizeUploadedAttachmentVariants,
  OFFICE_TEXT_UPLOADED_ATTACHMENT_VARIANT_ID,
  PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID,
  selectUploadedAttachmentParts,
  SVG_RASTER_UPLOADED_ATTACHMENT_VARIANT_ID,
  SVG_SOURCE_UPLOADED_ATTACHMENT_VARIANT_ID,
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
import { groupAdjacentRoleItems } from '../../../shared/utils/conversationTurns';
import { trackTokenUsage, trackImageGeneration, hasShownCostWarning, setCostWarningShown } from '../../../shared/utils/costTracker';
import { createSmartRef } from '../../../shared/utils/smartRef';
import { getPrimarySubtag, getShortLangCodeForPrompt } from '../../../shared/utils/languageUtils';
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

type HistoryMediaOverride = {
  newVariants?: UploadedAttachmentVariant[];
  transient?: boolean;
  omitFromHistory?: boolean;
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
        id: SVG_SOURCE_UPLOADED_ATTACHMENT_VARIANT_ID,
        source: 'svg-source',
        targets: ['chat'],
        order: 0,
        build: async () => {
          const extracted = decodeTextFromDataUrl(source.dataUrl)?.trim();
          if (!extracted) {
            throw new Error('SVG source could not be decoded for Gemini upload conversion.');
          }
          return {
            dataUrl: toUtf8Base64DataUrl('text/plain', extracted),
            mimeType: 'text/plain',
            displayName: `${displayName}.txt`,
          };
        },
      },
      {
        id: SVG_RASTER_UPLOADED_ATTACHMENT_VARIANT_ID,
        source: 'svg-rasterized',
        targets: ['chat', 'image-generation'],
        order: 5,
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

const TUTOR_FENCE_OPEN_REGEX = /^(\s{0,3})(`{3,}|~{3,})([^\n]*)$/;

const isMatchingTutorFenceClose = (
  rawLine: string,
  activeFence: { char: '`' | '~'; length: number }
): boolean => {
  const trimmed = rawLine.trim();
  if (!trimmed || trimmed[0] !== activeFence.char) return false;

  let count = 0;
  while (count < trimmed.length && trimmed[count] === activeFence.char) {
    count++;
  }

  return count >= activeFence.length && trimmed.slice(count).trim().length === 0;
};

const stripTutorVisibleLines = (responseText: string): {
  lines: string[];
  hasSkippedNonLanguageContent: boolean;
} => {
  const normalizedLines = responseText
    .replace(/\r\n/g, '\n')
    .replace(/\r/g, '\n')
    .split('\n');

  const lines: string[] = [];
  let hasSkippedNonLanguageContent = false;
  let activeFence: { char: '`' | '~'; length: number } | null = null;

  for (const rawLine of normalizedLines) {
    if (activeFence) {
      hasSkippedNonLanguageContent = true;
      if (isMatchingTutorFenceClose(rawLine, activeFence)) {
        activeFence = null;
      }
      continue;
    }

    const openMatch = TUTOR_FENCE_OPEN_REGEX.exec(rawLine);
    if (openMatch) {
      activeFence = {
        char: openMatch[2][0] as '`' | '~',
        length: openMatch[2].length,
      };
      hasSkippedNonLanguageContent = true;
      continue;
    }

    const trimmed = rawLine.trim();
    if (trimmed) {
      lines.push(trimmed);
    }
  }

  if (activeFence) {
    hasSkippedNonLanguageContent = true;
  }

  return { lines, hasSkippedNonLanguageContent };
};

const extractNativeTutorText = (line: string, nativeLangPrefix: string): string | null => {
  const trimmed = line.trim();
  if (!trimmed) return null;
  if (!trimmed.toLowerCase().startsWith(nativeLangPrefix.toLowerCase())) return null;
  return trimmed.slice(nativeLangPrefix.length).trim();
};

const parseStrictTutorResponseText = (
  responseText: string | undefined,
  nativeLanguageCode: string | undefined
): StrictParsedTutorResponse => {
  if (typeof responseText !== 'string' || !responseText.trim() || !nativeLanguageCode) {
    return { translations: [], visibleText: '', hasSkippedNonLanguageContent: false };
  }

  const nativeLangPrefix = `[${getShortLangCodeForPrompt(nativeLanguageCode)}]`;
  const stripped = stripTutorVisibleLines(responseText);
  const translations: Array<{ target: string; native: string }> = [];
  const visibleLines: string[] = [];
  let hasSkippedNonLanguageContent = stripped.hasSkippedNonLanguageContent;

  for (let i = 0; i < stripped.lines.length; i++) {
    const currentLine = stripped.lines[i];
    if (!currentLine) continue;

    const orphanNative = extractNativeTutorText(currentLine, nativeLangPrefix);
    if (orphanNative !== null) {
      hasSkippedNonLanguageContent = true;
      continue;
    }

    const nextLine = stripped.lines[i + 1] || '';
    if (!nextLine) {
      translations.push({ target: currentLine, native: '' });
      visibleLines.push(currentLine);
      continue;
    }

    const taggedNative = extractNativeTutorText(nextLine, nativeLangPrefix);
    const nativeText = taggedNative !== null ? taggedNative : nextLine;

    translations.push({
      target: currentLine,
      native: nativeText,
    });
    visibleLines.push(currentLine);
    if (nativeText) {
      visibleLines.push(`${nativeLangPrefix} ${nativeText}`);
    }
    i++;
  }

  return {
    translations,
    visibleText: visibleLines.join('\n').trim(),
    hasSkippedNonLanguageContent,
  };
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

export interface UseTutorConversationConfig {
  // Translation function
  t: TranslationFunction;
  
  // Settings
  setSettings: (settings: AppSettings | ((prev: AppSettings) => AppSettings)) => void;
  
  // Chat store
  addMessage: (message: Omit<ChatMessage, 'id' | 'timestamp'> & Partial<Pick<ChatMessage, 'id' | 'timestamp'>>) => string;
  updateMessage: (messageId: string, updates: Partial<ChatMessage>) => void;
  setMessages: (messages: ChatMessage[] | ((prev: ChatMessage[]) => ChatMessage[])) => void;
  getHistoryRespectingBookmark: (arr: ChatMessage[]) => ChatMessage[];
  computeMaxMessagesForArray: (arr: ChatMessage[]) => number | undefined;
  
  // Hardware
  captureSnapshot: (options?: boolean | {
    isForReengagement?: boolean;
    requireReadyFrame?: boolean;
  }) => Promise<{ base64: string; mimeType: string; storageOptimizedBase64: string; storageOptimizedMimeType: string } | null>;
  
  // Speech
  speakMessage: (message: ChatMessage) => void;
  isSpeechSynthesisSupported: boolean;
  stopListening: () => Promise<void>;
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
    messageType?: 'user' | 'conversational-reengagement' | 'image-reengagement',
    options?: { triggeredByStt?: boolean }
  ) => Promise<boolean>;
  handleSendMessageInternalRef: React.MutableRefObject<any>;
  
  // Suggestion handlers
  fetchAndSetReplySuggestions: (
    assistantMessageId: string,
    lastTutorMessage: string,
    history: ChatMessage[],
    options?: { responseSource?: 'chat' | 'live' }
  ) => Promise<void>;
  handleCreateSuggestion: (textToTranslate: string) => Promise<void>;
  handleSuggestionInteraction: (suggestion: ReplySuggestion, langType: 'target' | 'native') => void;
  
  // Activity stage
  setMaestroActivityStage: (stage: MaestroActivityStage) => void;
  
  // Parsing
  parseGeminiResponse: (responseText: string | undefined) => Array<{ target: string; native: string }>;
  
  // Utilities
  resolveBookmarkContextSummary: () => string | null;
  ensureUrisForHistoryForSend: (arr: ChatMessage[], onProgress?: (done: number, total: number, etaMs?: number) => void) => Promise<Record<string, HistoryMediaOverride>>;
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
      if (mimeType === 'image/svg+xml') {
        const decodedSvg = decodeTextFromDataUrl(dataUrl);
        if (decodedSvg) {
          dataUrl = toUtf8Base64DataUrl(mimeType, sanitizeSvgAnimationStructure(decodedSvg));
        }
      }
    } else {
      mimeType = mimeType || normalizeAttachmentMimeType({ name: candidate.fileName || 'artifact.txt', type: 'text/plain' });
      let normalizedContent = rawContent.replace(/\r\n/g, '\n').replace(/\r/g, '\n').trim();
      if (!normalizedContent) return null;
      if (mimeType.startsWith('image/') && mimeType !== 'image/svg+xml') {
        return null;
      }
      if (mimeType === 'image/svg+xml') {
        normalizedContent = sanitizeSvgAnimationStructure(normalizedContent);
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

  const finalizeAssistantArtifact = useCallback((
    assistantMessageId: string,
    normalizedArtifact: ReturnType<typeof normalizeSuggestionCreatorArtifact>
  ) => {
    const updates: Partial<ChatMessage> = {
      isLoadingArtifact: false,
      artifactLoadStartTime: undefined,
    };

    if (normalizedArtifact) {
      updates.imageUrl = normalizedArtifact.dataUrl;
      updates.imageMimeType = normalizedArtifact.mimeType;
      updates.attachmentName = normalizedArtifact.fileName;
      updates.storageOptimizedImageUrl = undefined;
      updates.storageOptimizedImageMimeType = undefined;
      updates.uploadedFileVariants = undefined;
    }

    updateMessage(assistantMessageId, updates);
  }, [updateMessage]);

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
  ): Promise<{ variants: UploadedAttachmentVariant[]; chatFileParts: Array<{ fileUri: string; mimeType: string }> }> => {
    let nextVariants = normalizeUploadedAttachmentVariants(message.uploadedFileVariants);
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

      const urisNeedingStatusCheck = !knownStatuses
        ? Array.from(new Set(existingUris))
        : Array.from(new Set(existingUris.filter(uri => !cachedStatuses[uri])));

      if (urisNeedingStatusCheck.length > 0) {
        try {
          const refreshedStatuses = await checkFileStatuses(urisNeedingStatusCheck);
          cachedStatuses = {
            ...cachedStatuses,
            ...refreshedStatuses,
          };
        } catch {
          if (!knownStatuses) {
            cachedStatuses = {};
          }
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
    const normalizedCurrentVariants = normalizeUploadedAttachmentVariants(message.uploadedFileVariants);
    const variantsChanged = JSON.stringify(normalizedCurrentVariants) !== JSON.stringify(normalizedState.uploadedFileVariants || []);

    if (variantsChanged) {
      updateMessage(message.id, normalizedState);
      message.uploadedFileVariants = normalizedState.uploadedFileVariants;
    }

    return {
      variants: normalizedState.uploadedFileVariants || [],
      chatFileParts: selectUploadedAttachmentParts(normalizedState, 'chat'),
    };
  }, [t, updateMessage]);

  const uploadAttachmentVariantsForSource = useCallback(async (
    source: { dataUrl: string; mimeType: string; attachmentName?: string },
    fallbackDisplayName: string
  ): Promise<Array<{ fileUri: string; mimeType: string }>> => {
    const plans = buildAttachmentUploadPlans(source, t);
    const uploadedVariants: UploadedAttachmentVariant[] = [];

    for (const plan of plans) {
      try {
        const uploadSource = await plan.build();
        const upload = await uploadMediaToFiles(
          uploadSource.dataUrl,
          uploadSource.mimeType,
          uploadSource.displayName || source.attachmentName || fallbackDisplayName
        );
        uploadedVariants.push({
          id: plan.id,
          uri: upload.uri,
          mimeType: upload.mimeType,
          targets: plan.targets,
          source: plan.source,
          order: plan.order,
        });
      } catch (error) {
        console.warn(`[uploadAttachmentVariantsForSource] Failed to upload ${plan.id} for ${fallbackDisplayName}.`, error);
      }
    }

    return selectUploadedAttachmentParts(buildUploadedAttachmentState(uploadedVariants), 'chat');
  }, [t]);

  const ensureUrisForHistoryForSend = useCallback(async (
    arr: ChatMessage[], 
    onProgress?: (done: number, total: number, etaMs?: number) => void
  ): Promise<Record<string, HistoryMediaOverride>> => {
    const candidates = computeHistorySubsetForMedia(arr);

    const mediaIndices: number[] = [];
    for (let i = 0; i < candidates.length; i++) {
      const m = candidates[i];
      const hasMedia = !!getMessageAttachmentSource(m) || normalizeUploadedAttachmentVariants(m.uploadedFileVariants).length > 0;
      if (hasMedia) mediaIndices.push(i);
    }
    const maxMedia = MAX_MEDIA_TO_KEEP;
    const keepMediaIdx = new Set<number>(mediaIndices.slice(-maxMedia));

    const cachedUrisToCheck: string[] = [];
    for (let i = 0; i < candidates.length; i++) {
      if (!keepMediaIdx.has(i)) continue;
      const m0 = candidates[i];
      normalizeUploadedAttachmentVariants(m0.uploadedFileVariants).forEach((variant) => {
        if (variant.uri) cachedUrisToCheck.push(variant.uri);
      });
    }
    let cachedStatuses: Record<string, { deleted: boolean; active: boolean }> = {};
    try {
      const uniqUris = Array.from(new Set(cachedUrisToCheck));
      if (uniqUris.length) cachedStatuses = await checkFileStatuses(uniqUris);
    } catch { cachedStatuses = {}; }

    let totalToEnsure = 0;
    const indicesNeedingUpload = new Set<number>();
    for (let i = 0; i < candidates.length; i++) {
      if (!keepMediaIdx.has(i)) continue;
      const message = candidates[i];
      const source = getMessageAttachmentSource(message);
      if (!source) continue;

      const plans = buildAttachmentUploadPlans(source, t);
      const existingVariants = normalizeUploadedAttachmentVariants(message.uploadedFileVariants);
      const needsUpload = plans.some((plan) => {
        const existingVariant = existingVariants.find(variant => variant.id === plan.id);
        if (!existingVariant) return true;
        return !!cachedStatuses[existingVariant.uri]?.deleted;
      });

      if (needsUpload) {
        totalToEnsure++;
        indicesNeedingUpload.add(i);
      }
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
    if (totalToEnsure > 0) tick();

    const updatedUriMap: Record<string, HistoryMediaOverride> = {};
    for (let idx = 0; idx < candidates.length; idx++) {
      if (!keepMediaIdx.has(idx)) continue;
      const m = candidates[idx];
      const previousVariants = normalizeUploadedAttachmentVariants(m.uploadedFileVariants);
      const localSource = getMessageAttachmentSource(m);
      const ensured = await ensureUploadedAttachmentVariantsForMessage(m, cachedStatuses);
      const nextState = buildUploadedAttachmentState(ensured.variants);
      const chatFileParts = ensured.chatFileParts;

      if (chatFileParts.length === 0 && localSource) {
        throw new Error(`Failed to prepare recent attachment "${m.attachmentName || 'attachment'}" for send. Try again or reattach the file.`);
      }

      if (chatFileParts.length === 0 && previousVariants.length > 0) {
        console.warn(`[ensureUris] Message ${m.id} has no valid uploaded file variants and will be omitted from request history.`);
        updatedUriMap[m.id] = {
          transient: true,
          omitFromHistory: true,
        };
      } else if (JSON.stringify(previousVariants) !== JSON.stringify(nextState.uploadedFileVariants || [])) {
        updatedUriMap[m.id] = {
          newVariants: nextState.uploadedFileVariants,
        };
      }
      if (indicesNeedingUpload.has(idx)) {
        try { doneCount++; tick(); } catch {}
      }
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
    history: ChatMessage[],
    options?: { responseSource?: 'chat' | 'live' }
  ) => {
    let resolvedArtifact: unknown = null;
    let resolvedToolRequest: ReturnType<typeof normalizeSuggestionCreatorToolRequest> = null;

    const finishReplySuggestionsRequest = async () => {
      setSuggestionsLoadingStreamText('');
      if (suggestionsTokenRef.current) {
        removeActivityToken(suggestionsTokenRef.current);
        suggestionsTokenRef.current = null;
      }
      const normalizedArtifact = normalizeSuggestionCreatorArtifact(resolvedArtifact);
      const hasRenderableArtifact = Boolean(normalizedArtifact);
      const isLiveSuggestionSource = options?.responseSource === 'live';
      const assistantMessage = messagesRef.current.find(message => message.id === assistantMessageId);
      const visibleAssistantText = getVisibleAssistantMessageText(assistantMessage) || lastTutorMessage;
      const buildLiveToolRawText = (
        baseText: string,
        toolRequest: NonNullable<ReturnType<typeof normalizeSuggestionCreatorToolRequest>>
      ) => {
        const normalizedBaseText = baseText.trim();
        const promptText = toolRequest.tool === 'image'
          ? (toolRequest.prompt || '').trim()
          : '';
        const rawSegments = [normalizedBaseText];
        if (promptText && !rawSegments.includes(promptText)) {
          rawSegments.push(promptText);
        }
        return buildCompactAssistantRawText(rawSegments.filter(Boolean).join('\n\n'), {
          toolRequest: {
            ...toolRequest,
            source: 'live-suggestion-creator',
          },
        });
      };

      if (isLiveSuggestionSource && (hasRenderableArtifact || resolvedToolRequest)) {
        const compactLiveRawText = hasRenderableArtifact
          ? buildCompactAssistantRawText(visibleAssistantText, {
              artifact: normalizedArtifact
                ? {
                    mimeType: normalizedArtifact.mimeType,
                    fileName: normalizedArtifact.fileName,
                    dataUrl: normalizedArtifact.dataUrl,
                    source: 'live-suggestion-creator',
                  }
                : null,
            })
          : (resolvedToolRequest
              ? buildLiveToolRawText(visibleAssistantText, resolvedToolRequest)
              : '');

        if (compactLiveRawText) {
          updateMessage(assistantMessageId, { llmRawResponse: compactLiveRawText });
        }
      }

      if (hasRenderableArtifact || !resolvedToolRequest) {
        finalizeAssistantArtifact(assistantMessageId, normalizedArtifact);
      }
      if (resolvedToolRequest) {
        const toolMessageId = hasRenderableArtifact
          ? addMessage({
              role: 'assistant',
              llmRawResponse: isLiveSuggestionSource
                ? buildLiveToolRawText(visibleAssistantText, resolvedToolRequest)
                : undefined,
            })
          : assistantMessageId;
        await executeAssistantToolRequest(toolMessageId, resolvedToolRequest);
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

    const hasStoredReplySuggestions = (message?: ChatMessage | null): boolean => (
      Boolean(message && Array.isArray(message.replySuggestions) && message.replySuggestions.length > 0)
    );

    // Check if suggestions already exist on message
    {
      const allMsgs = messagesRef.current;
      const targetIdx = allMsgs.findIndex(m => m.id === assistantMessageId);
      if (targetIdx !== -1) {
        const target = allMsgs[targetIdx];
        if (target && Array.isArray((target as any).replySuggestions) && (target as any).replySuggestions.length > 0) {
          const hasExistingAttachment = !!(
            (target.imageUrl && target.imageMimeType) ||
            (Array.isArray(target.uploadedFileVariants) && target.uploadedFileVariants.length > 0)
          );
          const visibleText = getVisibleAssistantMessageText(target).trim();
          const hasStructuredTail = !!(
            target.llmRawResponse &&
            target.llmRawResponse.trim() &&
            target.llmRawResponse.trim() !== visibleText
          );
          if (hasExistingAttachment || !hasStructuredTail) {
            lastFetchedSuggestionsForRef.current = target.id;
            setReplySuggestions((target as any).replySuggestions as ReplySuggestion[]);
            await finishReplySuggestionsRequest();
            return;
          }
        }

        let previousUserIdx = -1;
        for (let i = targetIdx - 1; i >= 0; i--) {
          if (allMsgs[i].role === 'user') {
            previousUserIdx = i;
            break;
          }
        }
        let nextUserIdx = allMsgs.length;
        for (let i = targetIdx + 1; i < allMsgs.length; i++) {
          if (allMsgs[i].role === 'user') {
            nextUserIdx = i;
            break;
          }
        }

        let blockSuggestionOwner: ChatMessage | null = null;
        for (let i = nextUserIdx - 1; i > previousUserIdx; i--) {
          const candidate = allMsgs[i];
          if (
            i !== targetIdx
            && candidate.role === 'assistant'
            && hasStoredReplySuggestions(candidate)
          ) {
            blockSuggestionOwner = candidate;
            break;
          }
        }

        if (blockSuggestionOwner?.replySuggestions) {
          lastFetchedSuggestionsForRef.current = blockSuggestionOwner.id;
          setReplySuggestions(blockSuggestionOwner.replySuggestions);
          await finishReplySuggestionsRequest();
          return;
        }
      }
    }

    // Add token for loading suggestions
    suggestionsTokenRef.current = addActivityToken(TOKEN_CATEGORY.GEN, TOKEN_SUBTYPE.SUGGESTIONS);
    setReplySuggestions([]);
    setSuggestionsLoadingStreamText('');

    const historyForPrompt = groupAdjacentRoleItems(
      getHistoryRespectingBookmark(history)
        .filter(msg => msg.role === 'user' || msg.role === 'assistant')
    )
      .slice(-6)
      .map(group => {
        if (group.role === 'user') {
          const userText = group.items
            .map(msg => msg.text?.trim() || '(sent an image)')
            .filter(Boolean)
            .join('\n\n')
            .trim();
          return userText ? `User: ${userText}` : '';
        }

        const tutorText = group.items
          .map(msg => (
            buildCompactAssistantHistoryText(msg)
            || msg.translations?.[0]?.target
            || msg.rawAssistantResponse
            || msg.text
            || '(sent an image)'
          ))
          .filter(Boolean)
          .join('\n\n')
          .trim();

        return tutorText ? `Tutor: ${tutorText}` : '';
      })
      .filter(Boolean)
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

    // Live sessions mostly produce transcript text
    const isLiveSuggestionSource = options?.responseSource === 'live';
    if (isLiveSuggestionSource) {
      suggestionPrompt +=
        `\n\nIMPORTANT: This latest tutor message came from the live audio model. Its transcript will not contain fenced artifact blocks or maestro-tool JSON even when an artifact or tool would improve the turn. For this live turn, decide yourself whether to synthesize an "artifact" object and/or a "toolRequest" object from the tutor transcript using the same quality bar as the main chat path. Artifacts, an image tool request, an audio-note tool request, a music tool request, or null are all allowed. Do not default to images or audio-note. Do consider creating different artifact, not repeating same that is already in the ui, if this is likely a followup to already created artifact on previous message. If artifact or tool does not materially improve the response, return null for them.`;
    }

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
          {
            configOverrides: { responseMimeType: "application/json" },
            lifecycleHooks: {
              onProgress: (event) => {
                const progressLine = formatGeminiStatusLine(event);
                if (progressLine && !thoughtText.trim() && !outputText.trim()) {
                  setSuggestionsLoadingStreamText(progressLine);
                }
              },
              onThoughtDelta: (_, fullThought) => {
                thoughtText = fullThought || thoughtText;
                flushSuggestionsLoadingText();
              },
              onTextDelta: (_, fullText) => {
                outputText = fullText || outputText;
                flushSuggestionsLoadingText();
              },
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
    normalizeSuggestionCreatorArtifact,
    selectedLanguagePairRef,
    settingsRef,
    isLoadingSuggestions,
    addMessage,
    addActivityToken,
    removeActivityToken,
    setReplySuggestions,
    setSuggestionsLoadingStreamText,
    formatGeminiStatusLine,
    lastFetchedSuggestionsForRef,
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

    if (existing && ((existing.imageUrl && existing.imageMimeType) || (existing.uploadedFileVariants && existing.uploadedFileVariants.length > 0))) {
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
    triggeredByStt?: boolean;
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
    logSttFlow('send.createUserMessage.start', {
      messageType: params.messageType,
      textLength: params.text.length,
      triggeredByStt: params.triggeredByStt === true,
      hasPassedImage: Boolean(userImageToProcessBase64),
      sendWithSnapshotEnabled: params.currentSettingsVal.sendWithSnapshotEnabled,
      shouldGenerateUserImage: params.shouldGenerateUserImage,
    });

    if (params.messageType !== 'user') {
      logSttFlow('send.createUserMessage.skip.nonUser', {
        messageType: params.messageType,
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
      logSttFlow('send.createUserMessage.snapshot.start', {
        triggeredByStt: params.triggeredByStt === true,
      });
      const snapshotResult = await captureSnapshot({
        isForReengagement: false,
        requireReadyFrame: params.triggeredByStt === true,
      });
      logSttFlow('send.createUserMessage.snapshot.done', {
        triggeredByStt: params.triggeredByStt === true,
        capturedImage: Boolean(snapshotResult),
      });
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
    logSttFlow('send.createUserMessage.done', {
      userMessageId,
      hasRecordedAudio: Boolean(recordedSpeechForMessage),
      hasImage: Boolean(userImageToProcessBase64),
      hasStorageOptimizedImage: Boolean(userImageToProcessStorageOptimizedBase64),
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
    imageForGeminiContextFileUri?: Array<{ fileUri: string; mimeType: string }>;
    currentSettingsVal: AppSettings;
  }) => {
    let geminiStage = 'gemini.prepare.start';
    const markGeminiStage = (stage: string, details?: Record<string, unknown>) => {
      geminiStage = stage;
      logSttFlow(stage, details);
    };
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
      const parsedDraft = parseStrictTutorResponse(streamingDraftText);
      const formattedDraft = parsedDraft.visibleText.trim();
      const fallbackDraft = parsedDraft.hasSkippedNonLanguageContent ? '' : streamingDraftText.trim();
      const draftSource = formattedDraft || fallbackDraft;
      const draftLines = draftSource
        .split('\n')
        .map(line => line.trim())
        .filter(Boolean);
      const draftToShow = draftLines.length > 6
        ? draftLines.slice(-6).join('\n')
        : draftSource.slice(-MAX_THINKING_DRAFT_CHARS);
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

    try {
      markGeminiStage('gemini.request.start', {
        thinkingMessageId: params.thinkingMessageId,
        promptLength: params.geminiPromptText.length,
        historyCount: params.sanitizedDerivedHistory.length,
        filePartCount: params.imageForGeminiContextFileUri?.length || 0,
        useGoogleSearch: params.currentSettingsVal.enableGoogleSearch,
      });
      const response = await generateGeminiResponse(
        getGeminiModels().text.default,
        params.geminiPromptText,
        params.sanitizedDerivedHistory,
        {
          systemInstruction: params.systemInstructionForGemini,
          currentFileParts: params.imageForGeminiContextFileUri,
          useGoogleSearch: params.currentSettingsVal.enableGoogleSearch,
          lifecycleHooks: {
            onProgress: (event) => {
              if (event.phase === 'attempt-processing') {
                const bucket = Math.floor((event.elapsedMs || 0) / 12000);
                if (bucket > 0 && bucket !== lastProcessingBucket) {
                  lastProcessingBucket = bucket;
                  markGeminiStage('gemini.request.processing', {
                    thinkingMessageId: params.thinkingMessageId,
                    elapsedMs: event.elapsedMs || 0,
                    bucket,
                  });
                }
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
            onTextDelta: (_, fullText) => {
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
          },
        }
      );

      markGeminiStage('gemini.request.done', {
        thinkingMessageId: params.thinkingMessageId,
        responseTextLength: response.text?.length || 0,
      });
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
      markGeminiStage('gemini.response.applied', {
        thinkingMessageId: params.thinkingMessageId,
        hasAttachmentCandidate: strictParsedResponse.hasSkippedNonLanguageContent,
        translationCount: parsedTranslationsOnComplete.length,
      });

      return {
        accumulatedFullText: responseTextForConversation,
        finalMessageUpdates,
        hasAttachmentCandidate: strictParsedResponse.hasSkippedNonLanguageContent,
      };
    } catch (error) {
      errorSttFlow('gemini.request.error', {
        stage: geminiStage,
        thinkingMessageId: params.thinkingMessageId,
        message: error instanceof Error ? error.message : String(error),
      });
      throw error;
    }
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
        thinkingPhase: 'Preparing request',
        thinkingStatusLine: 'Preparing request context...',
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
              throw new Error(`Failed to prepare attached media "${currentUserMessage.attachmentName || 'attachment'}" for Gemini. Try again or reattach the file.`);
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
            throw new Error(`Failed to prepare attached media "${attachedFileName || 'attachment'}" for Gemini. Try again or reattach the file.`);
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

      const derivedHistory = deriveHistoryForApi(historySubsetForSendFinal, {
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
