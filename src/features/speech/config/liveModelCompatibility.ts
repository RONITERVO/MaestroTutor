// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import {
  ThinkingLevel,
  type LiveSendRealtimeInputParameters,
  type ThinkingConfig,
} from '@google/genai';

interface LiveMediaBlob {
  data: string;
  mimeType: string;
}

const normalizeModel = (model: string): string => (
  (model || '').trim().toLowerCase().replace(/^models\//, '')
);

/**
 * Gemini 2.5 Live uses the legacy thinking-budget and generic media contracts.
 * Unknown/future models intentionally use the current dedicated Live contract.
 */
export const usesLegacyGemini25LiveApi = (model: string): boolean => (
  normalizeModel(model).startsWith('gemini-2.5-')
);

export const getLiveMinimalThinkingConfig = (model: string): ThinkingConfig => (
  usesLegacyGemini25LiveApi(model)
    ? { thinkingBudget: 0, includeThoughts: false }
    : { thinkingLevel: ThinkingLevel.MINIMAL, includeThoughts: false }
);

export const getLiveMaxThinkingConfig = (model: string): ThinkingConfig => (
  usesLegacyGemini25LiveApi(model)
    ? { thinkingBudget: -1, includeThoughts: true }
    : { thinkingLevel: ThinkingLevel.HIGH, includeThoughts: true }
);

export const createLiveAudioInput = (
  model: string,
  audio: LiveMediaBlob
): LiveSendRealtimeInputParameters => (
  usesLegacyGemini25LiveApi(model) ? { media: audio } : { audio }
);

export const createLiveVideoInput = (
  model: string,
  video: LiveMediaBlob
): LiveSendRealtimeInputParameters => (
  usesLegacyGemini25LiveApi(model) ? { media: video } : { video }
);
