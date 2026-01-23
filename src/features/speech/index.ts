// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * Speech Feature - Public API
 * 
 * This is the single entry point for speech (TTS/STT) functionality.
 * External code should only import from this file.
 * 
 * Owned Store Slice: speechSlice
 */

// Components
export { default as SttLanguageSelector } from './components/SttLanguageSelector';

// Hooks
export { default as useBrowserSpeech } from './hooks/useBrowserSpeech';
export { useTtsEngine } from './hooks/useTtsEngine';
export { useGeminiLiveConversation, type LiveSessionState } from './hooks/useGeminiLiveConversation';
export { useGeminiLiveStt } from './hooks/useGeminiLiveStt';
export { useSpeechOrchestrator } from './hooks/useSpeechOrchestrator';
export { useAutoSendOnSilence } from './hooks/useAutoSendOnSilence';
export { useSuggestionModeAutoRestart } from './hooks/useSuggestionModeAutoRestart';

// Utils
export { pcmToWav, splitPcmBySilence } from './utils/audioProcessing';
