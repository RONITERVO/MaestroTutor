// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { ThinkingLevel, type ThinkingConfig } from '@google/genai';

// Gemini 3.1 Flash Live does not support a strict thinking-off mode. Minimal is
// the closest supported level and keeps latency as low as the model allows.
export const LIVE_MINIMAL_THINKING_CONFIG: ThinkingConfig = {
  thinkingLevel: ThinkingLevel.MINIMAL,
  includeThoughts: false,
};

export const LIVE_MAX_THINKING_CONFIG: ThinkingConfig = {
  thinkingLevel: ThinkingLevel.HIGH,
  includeThoughts: true,
};
