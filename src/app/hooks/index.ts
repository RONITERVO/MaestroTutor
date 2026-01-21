// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * App Hooks - Barrel exports for all application-level hooks.
 * 
 * These hooks extract and organize the logic previously contained in App.tsx,
 * following the "Lift, Split, and Colocate" pattern for better maintainability.
 * 
 * NEW: Zustand store is now the single source of truth for shared state.
 * Hooks here serve as bridges during transition. Eventually, components
 * should import directly from the store.
 */

// Zustand Store (single source of truth)
export { 
  useMaestroStore, 
  getStoreState, 
  subscribeToStore,
  type MaestroStore 
} from '../../store';

// Translation
export { useTranslations, type TranslationFunction } from './useTranslations';

// App Lifecycle
export { useAppLifecycle } from './useAppLifecycle';
export { useAppAssets } from './useAppAssets';
export { useMaestroActivityStage } from './useMaestroActivityStage';
export { useAutoSendOnSilence } from './useAutoSendOnSilence';
export { useAutoFetchSuggestions } from './useAutoFetchSuggestions';
export { useSuggestionModeAutoRestart } from './useSuggestionModeAutoRestart';
export { useIdleReengagement } from './useIdleReengagement';
export { useLanguageSelectionController } from './useLanguageSelectionController';

// Settings Management
export { 
  useAppSettings, 
  initialSettings,
  MAX_VISIBLE_MESSAGES_DEFAULT,
  allGeneratedLanguagePairs,
  DEFAULT_LANGUAGE_PAIR_ID,
  type UseAppSettingsReturn,
  type UseAppSettingsConfig 
} from './useAppSettings';

// Chat/Message State
export { 
  useChatStore,
  type UseChatStoreConfig,
  type UseChatStoreReturn 
} from './useChatStore';

// Hardware (Camera/Microphone)
export { 
  useHardware,
  type UseHardwareConfig,
  type UseHardwareReturn 
} from './useHardware';

// Speech (TTS/STT)
export { 
  useSpeechController,
  type UseSpeechControllerConfig,
  type UseSpeechControllerReturn 
} from './useSpeechController';

// Main Orchestrator
export { 
  useMaestroController,
  type UseMaestroControllerConfig,
  type UseMaestroControllerReturn 
} from './useMaestroController';

// Live Session (Gemini Live Conversation)
export { 
  useLiveSession,
  type UseLiveSessionConfig,
  type UseLiveSessionReturn 
} from './useLiveSession';

// Data Backup (Save/Load All Chats)
export { 
  useDataBackup,
  type UseDataBackupConfig,
  type UseDataBackupReturn 
} from './useDataBackup';
