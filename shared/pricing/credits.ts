// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Credits are the unit users buy and spend in managed mode.
 *
 * The conversion and the pre-flight estimate live here, beside the usage
 * pricing, so a request is estimated and settled against the same table. The
 * backend reserves credits before calling Gemini and settles against real usage
 * afterwards; if the estimate came from different numbers than the settlement,
 * reservations would routinely be too small to cover the work and users would
 * be cut off mid-request.
 */

import type { GeminiPricingRegistry } from './registry';
import { resolvePricingRule } from './registry';

/** A managed operation, as named by both the client and the backend. */
export type ManagedOperation =
  | 'generateContent'
  | 'streamContent'
  | 'translateText'
  | 'generateImage'
  | 'live'
  | 'music'
  | 'upload';

export const roundUsd = (value: number): number => (
  Number.isFinite(value) ? Math.max(0, Math.round(value * 1_000_000) / 1_000_000) : 0
);

/**
 * Credits are always rounded up. A request that costs anything at all costs at
 * least one credit, so no operation can ever be free through rounding.
 */
export const usdToCredits = (usd: number, creditsPerUsd: number): number => {
  if (!Number.isFinite(usd) || usd <= 0) return 0;
  return Math.max(1, Math.ceil(usd * creditsPerUsd));
};

export const creditsToUsd = (credits: number, creditsPerUsd: number): number => (
  creditsPerUsd > 0 ? roundUsd(credits / creditsPerUsd) : 0
);

export const uploadBytesToCredits = (bytes: number, creditsPerMb: number): number => {
  if (!Number.isFinite(bytes) || bytes <= 0) return 0;
  return Math.max(1, Math.ceil((bytes / (1024 * 1024)) * creditsPerMb));
};

/** Gemini Live bills audio per token; this is the published token rate per second. */
export const LIVE_AUDIO_TOKENS_PER_SECOND = 32;

export interface LiveWindowRates {
  inputAudioPerMillion: number;
  outputAudioPerMillion: number;
  maxInputTokens: number;
  maxOutputTokens: number;
}

export const DEFAULT_LIVE_WINDOW_RATES: LiveWindowRates = {
  inputAudioPerMillion: 3,
  outputAudioPerMillion: 12,
  maxInputTokens: 131_072,
  maxOutputTokens: 8_192,
};

export const calculateLiveWindowUsd = (
  durationSeconds: number,
  rates: LiveWindowRates = DEFAULT_LIVE_WINDOW_RATES,
): number => {
  const seconds = Math.max(1, Math.floor(durationSeconds));
  const budget = seconds * LIVE_AUDIO_TOKENS_PER_SECOND;
  const input = Math.min(rates.maxInputTokens, budget);
  const output = Math.min(rates.maxOutputTokens, budget);
  return roundUsd(
    (input / 1_000_000) * rates.inputAudioPerMillion
    + (output / 1_000_000) * rates.outputAudioPerMillion,
  );
};

/**
 * What to hold before a request runs.
 *
 * Deliberately generous. An estimate that lands under the real cost leaves the
 * settlement unable to cover itself, and the alternative — letting the balance
 * go negative — is how a prepaid system ends up giving work away. Anything held
 * beyond the true cost is returned at settlement, so erring high costs the user
 * nothing except briefly reserved headroom.
 */
export const estimateOperationUsd = (params: {
  model: string;
  promptTokens: number;
  operation: ManagedOperation;
  pricing: GeminiPricingRegistry;
}): number => {
  const rule = resolvePricingRule(params.model, params.pricing);
  const promptTokens = Math.max(0, params.promptTokens);

  // An image is charged as a flat amount per image, not per output token, so
  // its floor has to be that amount or the reservation cannot cover settlement.
  if (params.operation === 'generateImage') {
    const perImage = rule?.generatedImageUsdFallback ?? 0.039;
    const inputRate = rule?.inputPerMillion?.text ?? 0;
    return roundUsd(perImage + (promptTokens / 1_000_000) * inputRate);
  }

  const inputRate = rule?.inputPerMillion?.text ?? 0;
  const outputRate = rule?.outputPerMillion?.text ?? 0;

  const expectedOutputTokens = params.operation === 'translateText'
    ? Math.max(promptTokens, 256)
    : Math.max(promptTokens * 2, 1024);

  const estimated =
    (promptTokens / 1_000_000) * inputRate
    + (expectedOutputTokens / 1_000_000) * outputRate;

  const floor = params.operation === 'translateText' ? 0.002 : 0.005;
  return roundUsd(Math.max(estimated, floor));
};
