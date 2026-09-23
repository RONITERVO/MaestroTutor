// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {
  generateGeminiResponse as generateResponse,
  translateText as translate,
  type GenerateGeminiResponseOptions as CoreResponseOptions,
  type GeminiRequestLifecycleHooks,
} from '../../core-sdk/gemini/generative';
import type { CoreGeminiClient } from '../../core-sdk/managedGeminiClient';
import { browserClientSource } from './browserClientSource';

export type { GeminiRetryReason, GeminiProgressPhase, GeminiProgressEvent, GeminiRequestLifecycleHooks } from '../../core-sdk/gemini/generative';
export type GenerateGeminiResponseOptions = Omit<CoreResponseOptions, 'aiClient' | 'resolveAiClient'> & { aiClient?: CoreGeminiClient };

export const generateGeminiResponse = (modelName: string, userPrompt: string, history: any[], options: GenerateGeminiResponseOptions = {}) =>
  generateResponse(modelName, userPrompt, history, { ...options, ...browserClientSource(options.aiClient) });

export const translateText = (text: string, from: string, to: string,
  options: { aiClient?: CoreGeminiClient; lifecycleHooks?: GeminiRequestLifecycleHooks } = {}) =>
  translate(text, from, to, { ...options, ...browserClientSource(options.aiClient) });
