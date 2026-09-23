// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { ChatMessage, ReplySuggestion, GroundingChunk, MaestroActivityStage, AppSettings, RecordedUtterance } from '../../../core/types';
import type { TranslationFunction } from '../../../app/hooks/useTranslations';
import type { HistoryMediaOverride } from './attachmentUploads';

/** A mutable state port; React refs and the store's getter-backed cells both
 * satisfy it. Coordinators never import React or the application store. */
export interface MutableValue<T> { current: T }

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
  scheduleReengagementRef: MutableValue<(reason: string, delayOverrideMs?: number) => void>;
  cancelReengagementRef: MutableValue<() => void>;

  // UI State
  transcript: string;

  // Prompts
  currentSystemPromptText: string;
  currentReplySuggestionsPromptText: string;

  // Reply suggestions (managed by useChatStore, passed through)
  setReplySuggestions: (suggestions: ReplySuggestion[] | ((prev: ReplySuggestion[]) => ReplySuggestion[])) => void;

  // Toggle suggestion mode callback - using ref to allow late binding
  handleToggleSuggestionModeRef?: MutableValue<((forceState?: boolean) => void) | undefined>;

  // Maestro avatar refs - passed from App.tsx where the avatar is loaded
  maestroAvatarUriRef: MutableValue<string | null>;
  maestroAvatarMimeTypeRef: MutableValue<string | null>;

  // Hardware errors
  setSnapshotUserError?: React.Dispatch<React.SetStateAction<string | null>>;

  // Api key gate
  onApiKeyGateOpen?: (options?: { reason?: 'missing' | 'invalid' | 'quota'; instructionIndex?: number }) => void;
}

export interface UseTutorConversationReturn {
  // State
  isSending: boolean;
  isSendingRef: MutableValue<boolean>;
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
  handleSendMessageInternalRef: MutableValue<any>;

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


export interface ConversationDiagnostics {
  logSttFlow(stage: string, details?: Record<string, unknown>): void;
  warnSttFlow(stage: string, details?: Record<string, unknown>): void;
  errorSttFlow(stage: string, details?: Record<string, unknown>): void;
}
