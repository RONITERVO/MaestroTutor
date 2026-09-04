// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import {
  ActivityHandling,
  EndSensitivity,
  StartSensitivity,
  ThinkingLevel,
  TurnCoverage,
  type RealtimeInputConfig,
  type ThinkingConfig,
} from '@google/genai';

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

/**
 * Android/WebView microphone capture can classify the app's own speaker output
 * as user activity. Keep reliability as the default and require a deliberate
 * opt-in before activity is allowed to cut off a model response.
 */
export const getLiveRealtimeInputConfig = (
  allowModelInterruptions = false,
  manualActivityBoundaries = false,
): RealtimeInputConfig => ({
  activityHandling: allowModelInterruptions
    ? ActivityHandling.START_OF_ACTIVITY_INTERRUPTS
    : ActivityHandling.NO_INTERRUPTION,
  ...(manualActivityBoundaries
    ? {
        automaticActivityDetection: { disabled: true },
        turnCoverage: TurnCoverage.TURN_INCLUDES_ALL_INPUT,
      }
    : {
        automaticActivityDetection: {
          disabled: false,
          startOfSpeechSensitivity: StartSensitivity.START_SENSITIVITY_LOW,
          endOfSpeechSensitivity: EndSensitivity.END_SENSITIVITY_LOW,
          prefixPaddingMs: 120,
          // Leave enough room for an ordinary hesitation or short stutter. A
          // 600 ms cutoff can end a turn on roughly half a second of low-energy
          // speech plus the natural quiet around it.
          silenceDurationMs: 1_000,
        },
      }),
});
