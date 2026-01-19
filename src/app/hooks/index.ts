// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * App Hooks - Barrel exports for all application-level hooks.
 * 
 * These hooks extract and organize the logic previously contained in App.tsx,
 * following the "Lift, Split, and Colocate" pattern for better maintainability.
 */

// Translation
export { useTranslations, type TranslationFunction } from './useTranslations';

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

// UI State
export { 
  useUiBusyState,
  type UseUiBusyStateReturn 
} from './useUiBusyState';

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
