// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { ThinkingLevel } from '@google/genai';
import {
  getLiveConversationThinkingConfig,
  getLiveMinimalThinkingConfig,
  usesGemini25ThinkingBudget,
} from './liveModelCompatibility';

const gemini25Model = 'gemini-2.5-flash-native-audio-preview-12-2025';
const currentModel = 'gemini-3.1-flash-live-preview';

describe('Live model compatibility', () => {
  it('recognizes Gemini 2.5 thinking-budget model names', () => {
    expect(usesGemini25ThinkingBudget(gemini25Model)).toBe(true);
    expect(usesGemini25ThinkingBudget(` models/${gemini25Model.toUpperCase()} `)).toBe(true);
    expect(usesGemini25ThinkingBudget(currentModel)).toBe(false);
    expect(usesGemini25ThinkingBudget('gemini-live-latest')).toBe(false);
  });

  it('disables 2.5 thinking and keeps current Live thinking minimal for TTS', () => {
    expect(getLiveMinimalThinkingConfig(gemini25Model)).toEqual({
      thinkingBudget: 0,
      includeThoughts: false,
    });
    expect(getLiveMinimalThinkingConfig(currentModel)).toEqual({
      thinkingLevel: ThinkingLevel.MINIMAL,
      includeThoughts: false,
    });
  });

  it('uses each model family thinking contract for full Live conversations', () => {
    expect(getLiveConversationThinkingConfig(gemini25Model)).toEqual({
      thinkingBudget: -1,
      includeThoughts: true,
    });
    expect(getLiveConversationThinkingConfig(currentModel)).toEqual({
      thinkingLevel: ThinkingLevel.HIGH,
      includeThoughts: true,
    });
  });
});
