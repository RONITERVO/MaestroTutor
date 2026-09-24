// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { AppSettings, LanguagePair, ReplySuggestion } from '../../core/types';
import type { GeminiLiveSttTurnComplete } from '../../features/speech';
import type { LiveSessionState } from '../../store/slices/liveSessionSlice';

export interface CurrentValue<T> { current: T }
export type SetSettings = (settings: AppSettings | ((previous: AppSettings) => AppSettings)) => void;
export type SendMessage = (
  text: string,
  image?: string,
  mimeType?: string,
  messageType?: 'user' | 'conversational-reengagement' | 'image-reengagement',
  options?: { triggeredByStt?: boolean },
) => Promise<boolean>;
export interface SpeechRoutingState {
  settings: AppSettings;
  responsePending: boolean;
  speaking: boolean;
  listening: boolean;
  attachedImageBase64: string | null;
  attachedImageMimeType: string | null;
  liveSessionState: LiveSessionState;
}
export interface SpeechActions {
  stopListening: () => Promise<void>;
  startListening: (language: string) => void;
  clearTranscript: () => void;
}
export interface SttTurnPorts extends SpeechActions {
  readState: () => SpeechRoutingState;
  handleCreateSuggestion: (text: string) => Promise<void>;
  handleSendMessageInternal: SendMessage;
  logSttFlow: (stage: string, details?: Record<string, unknown>) => void;
  warnSttFlow: (stage: string, details?: Record<string, unknown>) => void;
  warn: (message: string, error: unknown) => void;
}
export type SttTurnHandler = (turn: GeminiLiveSttTurnComplete) => Promise<void>;

export const STT_RESTART_DELAY_MS = 250;
export interface SpeechModePorts extends SpeechActions {
  pendingEnableRef: CurrentValue<symbol | null>;
  resetSilentObserverRef: CurrentValue<() => Promise<void>>;
  settingsRef: CurrentValue<AppSettings>;
  selectedLanguagePairRef: CurrentValue<LanguagePair | undefined>;
  stopSilentObserverRef: CurrentValue<() => Promise<void>>;
  isListening: boolean;
  setSettings: SetSettings;
  setSttError: (error: string | null) => void;
  delay: (callback: () => void, milliseconds: number) => void;
  warn: (message: string, error: unknown) => void;
}
export interface ReengagementPorts {
  isLoadingHistoryRef: CurrentValue<boolean>;
  isSendingRef: CurrentValue<boolean>;
  speechIsSpeakingRef: CurrentValue<boolean>;
  isCurrentlyPerformingVisualContextCaptureRef: CurrentValue<boolean>;
  stopSilentObserverRef: CurrentValue<() => Promise<void>>;
  settingsRef: CurrentValue<AppSettings>;
  visualContextStreamRef: CurrentValue<{ active: boolean } | null>;
  captureSnapshot: (isForReengagement?: boolean) => Promise<{ base64: string; mimeType: string } | null>;
  handleSendMessageInternal: SendMessage;
  setReplySuggestions: (suggestions: ReplySuggestion[]) => void;
  setLastFetchedSuggestionsFor: (id: string | null) => void;
}
export interface LanguageChangePorts extends SpeechActions {
  settingsRef: CurrentValue<AppSettings>;
  readState: () => SpeechRoutingState;
  cancelReengagement: () => void;
  stopSpeaking: () => void;
  setSttError: (error: string | null) => void;
  resetSilentObserver: () => Promise<void>;
  handleStopLiveSession: (options: { scheduleReengagement: boolean }) => Promise<void>;
  delay: (callback: () => void, milliseconds: number) => void;
}
