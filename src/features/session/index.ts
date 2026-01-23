// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * Session Feature - Public API
 * 
 * This is the single entry point for session/settings functionality.
 * External code should only import from this file.
 * 
 * Owned Store Slices: settingsSlice, reengagementSlice
 */

// Components
export { default as Header } from './components/Header';
export { default as LanguageSelectorGlobe } from './components/LanguageSelectorGlobe';
export { default as LanguageScrollWheel } from './components/LanguageScrollWheel';
export { default as GlobalProfileSummary } from './components/GlobalProfileSummary';
export { default as CollapsedMaestroStatus } from './components/CollapsedMaestroStatus';
export { default as SessionControls } from './components/SessionControls';

// Hooks
export { useSmartReengagement } from './hooks/useSmartReengagement';
export { useLanguageSelectionController as useLanguageSelection } from './hooks/useLanguageSelection';
export { useDataBackup } from './hooks/useDataBackup';

// Services
export {
  getAppSettingsDB,
  setAppSettingsDB,
} from './services/settings';

export {
  getGlobalProfileDB,
  setGlobalProfileDB,
} from './services/globalProfile';
