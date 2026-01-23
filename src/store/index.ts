// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * Store Public API
 * 
 * This is the single entry point for all store access.
 * Import from here, not from individual slices.
 */

export { 
  useMaestroStore, 
  getStoreState, 
  subscribeToStore,
  initialSettings,
  MAX_VISIBLE_MESSAGES_DEFAULT,
  allGeneratedLanguagePairs,
  DEFAULT_LANGUAGE_PAIR_ID
} from './maestroStore';

export {
  selectSettings,
  selectSelectedLanguagePair,
  selectCurrentSystemPromptText,
  selectCurrentReplySuggestionsPromptText,
  selectTargetLanguageDef,
  selectNativeLanguageDef,
} from './slices/settingsSlice';

export {
  selectMessages,
  selectReplySuggestions,
  selectSendPrep,
  selectLatestGroundingChunks,
  selectAttachedImageBase64,
  selectAttachedImageMimeType,
  // DEPRECATED - causes infinite loops in React 18+ strict mode
  // selectAttachedImage,
} from './slices/chatSlice';

export {
  selectTranscript,
  selectSttError,
  selectSpeakingUtteranceText,
} from './slices/speechSlice';

export type {
  MaestroStore,
  SettingsSlice,
  ChatSlice,
  SpeechSlice,
  HardwareSlice,
  ReengagementSlice,
  ReengagementPhase,
  LiveSessionSlice,
  LiveSessionState,
  UiSlice,
  DiagnosticsSlice,
} from './maestroStore';
