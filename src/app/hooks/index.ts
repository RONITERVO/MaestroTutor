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
export { useAppInitialization } from './useAppInitialization';
export { useMaestroActivityStage } from './useMaestroActivityStage';
export { useIdleReengagement } from './useIdleReengagement';
