// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const APP_TITLE_KEY = "app.title";
export const LOCAL_STORAGE_SETTINGS_KEY = "maestro_settings_local_v2";
export const IMAGE_GEN_CAMERA_ID = "image-gen-camera";
export const MAX_MEDIA_TO_KEEP = 10;

/**
 * Available Gemini Live TTS voices with their characteristics, updated, these support all live languages.
 */
export const GEMINI_VOICES = [
  { id: 'Zephyr', description: 'Bright' },
  { id: 'Puck', description: 'Upbeat' },
  { id: 'Charon', description: 'Informative' },
  { id: 'Kore', description: 'Firm' },
  { id: 'Fenrir', description: 'Excitable' },
] as const;

export type GeminiVoiceId = typeof GEMINI_VOICES[number]['id'];
