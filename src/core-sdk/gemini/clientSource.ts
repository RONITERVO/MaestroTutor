// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { CoreGeminiClient } from '../managedGeminiClient';

/** Resolve at request time so access changes and journey event ordering survive retries. */
export type GeminiClientSource =
  | { aiClient: CoreGeminiClient; resolveAiClient?: () => Promise<CoreGeminiClient> }
  | { aiClient?: undefined; resolveAiClient: () => Promise<CoreGeminiClient> };

/** Forward only the transport capability, never unrelated journey options into request configuration. */
export const pickGeminiClientSource = (source: GeminiClientSource): GeminiClientSource => source.aiClient
  ? { aiClient: source.aiClient }
  : { resolveAiClient: source.resolveAiClient };
