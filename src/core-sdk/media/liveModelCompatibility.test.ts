// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import {
  ActivityHandling,
  EndSensitivity,
  StartSensitivity,
  ThinkingLevel,
  TurnCoverage,
} from '@google/genai';
import {
  getLiveConversationThinkingConfig,
  getLiveMinimalThinkingConfig,
  getLiveRealtimeInputConfig,
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

  it('protects model speech from Android echo unless barge-in is explicitly enabled', () => {
    expect(getLiveRealtimeInputConfig()).toEqual({
      activityHandling: ActivityHandling.NO_INTERRUPTION,
      automaticActivityDetection: {
        disabled: false,
        startOfSpeechSensitivity: StartSensitivity.START_SENSITIVITY_LOW,
        endOfSpeechSensitivity: EndSensitivity.END_SENSITIVITY_LOW,
        prefixPaddingMs: 120,
        silenceDurationMs: 1_000,
      },
    });
    expect(getLiveRealtimeInputConfig(true).activityHandling).toBe(
      ActivityHandling.START_OF_ACTIVITY_INTERRUPTS,
    );
  });

  it('uses one manual all-input boundary for locally detected continuous turns', () => {
    expect(getLiveRealtimeInputConfig(false, true)).toEqual({
      activityHandling: ActivityHandling.NO_INTERRUPTION,
      automaticActivityDetection: { disabled: true },
      turnCoverage: TurnCoverage.TURN_INCLUDES_ALL_INPUT,
    });
  });
});
