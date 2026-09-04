// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Backend pricing.
 *
 * Deliberately thin. Every rate, every model match and the whole usage-to-USD
 * computation come from `shared/pricing`, which the app bills the user's own
 * API key against. This file only binds those to the server's configuration
 * (credits per dollar, upload rate) and keeps the call signatures the rest of
 * the backend already uses.
 *
 * It exists as an adapter rather than a second implementation on purpose: the
 * previous version of this file was an independent table of three model
 * substrings with a Pro-rate fallback, which silently overcharged every model
 * it had not heard of and undercharged image generation by three orders of
 * magnitude. There is no version of that bug that a shared table can have.
 */

import { DEFAULT_GEMINI_PRICING } from '../../shared/pricing/registry';
import {
  type ManagedOperation,
  calculateLiveWindowUsd,
  calculateLiveGatewayWindowUsd,
  creditsToUsd as sharedCreditsToUsd,
  estimateOperationUsd,
  roundUsd,
  uploadBytesToCredits as sharedUploadBytesToCredits,
  usdToCredits as sharedUsdToCredits,
  LIVE_AUDIO_TOKENS_PER_SECOND,
  DEFAULT_LIVE_WINDOW_RATES,
  getLiveGatewayWindowTokenBudget,
} from '../../shared/pricing/credits';
import { calculateGeminiUsageCost } from '../../shared/pricing/usage';
import { appConfig } from './config';

/**
 * The rate card the server bills against.
 *
 * Bundled rather than fetched: a request must never be billed at rates that
 * moved underneath it mid-flight, and a network failure must never leave the
 * server unable to price. Updating rates is a deploy, which is also what makes
 * the change auditable.
 */
const pricing = DEFAULT_GEMINI_PRICING;

export const pricingEffectiveAt = pricing.effectiveAt;

export const usdToCredits = (usd: number): number => (
  sharedUsdToCredits(usd, appConfig.managedCreditsPerUsd)
);

export const creditsToUsd = (credits: number): number => (
  sharedCreditsToUsd(credits, appConfig.managedCreditsPerUsd)
);

export const uploadBytesToCredits = (bytes: number): number => (
  sharedUploadBytesToCredits(bytes, appConfig.managedUploadCreditsPerMb)
);

export const uploadBytesToUsd = (bytes: number): number => (
  creditsToUsd(uploadBytesToCredits(bytes))
);

export const getManagedLiveWindowTokenBudget = (durationSeconds: number) => {
  const seconds = Math.max(1, Math.floor(durationSeconds));
  const budget = seconds * LIVE_AUDIO_TOKENS_PER_SECOND;
  return {
    audioInputTokens: Math.min(DEFAULT_LIVE_WINDOW_RATES.reservationInputTokenCeiling, budget),
    audioOutputTokens: Math.min(DEFAULT_LIVE_WINDOW_RATES.reservationOutputTokenCeiling, budget),
  };
};

export const calculateManagedLiveWindowUsd = (durationSeconds: number): number => (
  calculateLiveWindowUsd(durationSeconds)
);

export const calculateManagedLiveWindowCredits = (durationSeconds: number): number => (
  usdToCredits(calculateManagedLiveWindowUsd(durationSeconds))
);

export const getManagedLiveGatewayTokenBudget = (durationSeconds: number) => (
  getLiveGatewayWindowTokenBudget(durationSeconds)
);

export const calculateManagedLiveGatewayWindowUsd = (durationSeconds: number): number => (
  calculateLiveGatewayWindowUsd(durationSeconds)
);

export const calculateManagedLiveGatewayWindowCredits = (durationSeconds: number): number => (
  usdToCredits(calculateManagedLiveGatewayWindowUsd(durationSeconds))
);

export const estimateReservationUsd = (params: {
  model: string;
  promptTokens: number;
  operation: string;
  searchQueries?: number;
  expectedOutputTokens?: number;
}): number => roundUsd(
  estimateOperationUsd({
    model: params.model,
    promptTokens: params.promptTokens,
    operation: params.operation as ManagedOperation,
    pricing,
    expectedOutputTokens: params.expectedOutputTokens,
  }) + googleSearchQueriesToUsd(params.searchQueries || 0)
);

export const googleSearchQueriesToUsd = (searchQueries: number): number => roundUsd(
  (Math.max(0, Math.floor(searchQueries)) / 1_000)
  * pricing.googleSearch.pricePerThousandQueriesUsd
);

/**
 * What a completed request actually cost.
 *
 * `generatedImages` matters: an image is charged as a flat amount per image
 * rather than per output token, and passing it is the difference between
 * billing cents and billing a rounding error.
 */
export const usageMetadataToUsd = (
  model: string,
  usageMetadata: Record<string, unknown> | undefined,
  fallbackOperation?: string,
  generatedImages?: number,
  searchQueries?: number,
  modelVersion?: string,
): number => {
  const images = Number.isFinite(generatedImages)
    ? Math.max(0, generatedImages as number)
    : (fallbackOperation === 'generateImage' ? 1 : 0);

  const cost = calculateGeminiUsageCost({
    configuredModel: model,
    modelVersion,
    usageMetadata: (usageMetadata || {}) as Record<string, never>,
    generatedImages: images,
    searchQueries,
  }, pricing);

  // An unpriced model must not silently bill as free — that is revenue lost with
  // no signal. Charge the floor for the operation and make it visible in logs.
  if (cost.pricingStatus === 'unpriced') {
    console.error(
      `[pricing] No rate for model "${model}" (operation ${fallbackOperation ?? 'unknown'}). `
      + 'Billing the operation floor. Add it to shared/pricing/registry.ts.'
    );
    return estimateReservationUsd({
      model,
      promptTokens: Number(usageMetadata?.promptTokenCount || 0),
      operation: fallbackOperation || 'generateContent',
      searchQueries,
    });
  }

  return roundUsd(cost.modelCostUsd + cost.potentialSearchCostUsd);
};
