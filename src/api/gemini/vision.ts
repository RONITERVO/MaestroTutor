// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { generateImage as generate, type GenerateImageParams } from '../../core-sdk/gemini/vision';
import type { CoreGeminiClient } from '../../core-sdk/managedGeminiClient';
import { trackGeminiUsage } from '../../shared/utils/costTracker';
import { browserClientSource } from './browserClientSource';

export type { ImageGenerationResult } from '../../core-sdk/gemini/vision';

export const generateImage = (params: Omit<GenerateImageParams, 'aiClient' | 'resolveAiClient' | 'onUsage'> & { aiClient?: CoreGeminiClient }) =>
  generate({ ...params, ...browserClientSource(params.aiClient), onUsage: trackGeminiUsage });
