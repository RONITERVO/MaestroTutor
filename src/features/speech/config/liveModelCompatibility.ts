// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { ThinkingLevel, type ThinkingConfig } from '@google/genai';

const normalizeModel = (model: string): string => (
  (model || '').trim().toLowerCase().replace(/^models\//, '')
);

/**
 * Gemini 2.5 Live uses token budgets; newer Live models use thinking levels.
 */
export const usesGemini25ThinkingBudget = (model: string): boolean => (
  normalizeModel(model).startsWith('gemini-2.5-')
);

export const getLiveMinimalThinkingConfig = (model: string): ThinkingConfig => (
  usesGemini25ThinkingBudget(model)
    ? { thinkingBudget: 0, includeThoughts: false }
    : { thinkingLevel: ThinkingLevel.MINIMAL, includeThoughts: false }
);

export const getLiveConversationThinkingConfig = (model: string): ThinkingConfig => (
  usesGemini25ThinkingBudget(model)
    ? { thinkingBudget: -1, includeThoughts: true }
    : { thinkingLevel: ThinkingLevel.HIGH, includeThoughts: true }
);
