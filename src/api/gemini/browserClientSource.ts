// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { CoreGeminiClient } from '../../core-sdk/managedGeminiClient';
import type { GeminiClientSource } from '../../core-sdk/gemini/clientSource';

/** Browser access policy stays in client.ts; do not resolve it before a journey starts. */
export function browserClientSource(aiClient?: CoreGeminiClient): GeminiClientSource {
  return aiClient ? { aiClient } : { resolveAiClient: async () => (await import('./client')).getAi() };
}
