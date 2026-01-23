// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * Vision Feature - Public API
 * 
 * This is the single entry point for vision/camera functionality.
 * External code should only import from this file.
 * 
 * Owned Store Slice: hardwareSlice
 */

// Services
export { processMediaForUpload } from './services/mediaOptimizationService';

// Utils
export { 
  getFacingModeFromLabel,
  createKeyframeFromVideoDataUrl,
} from './utils/mediaUtils';

// Hooks
export { useCameraManager } from './hooks/useCameraManager';

// Components
export { default as VisualContextVideo } from './components/VisualContextVideo';
