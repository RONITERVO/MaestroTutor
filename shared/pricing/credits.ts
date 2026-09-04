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
import { LIVE_GATEWAY_MAX_TURNS } from '../liveGatewayProtocol';

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
/** Low media resolution is enforced for managed Live camera frames. */
export const LIVE_VIDEO_TOKENS_PER_FRAME_LOW = 70;
export const LIVE_GATEWAY_MAX_BILLABLE_TURNS = LIVE_GATEWAY_MAX_TURNS;

export interface LiveWindowRates {
  inputAudioPerMillion: number;
  outputAudioPerMillion: number;
  /** Billing reservation bound only; never sent to the model provider. */
  reservationInputTokenCeiling: number;
  /** Billing reservation bound only; never sent to the model provider. */
  reservationOutputTokenCeiling: number;
}

export const DEFAULT_LIVE_WINDOW_RATES: LiveWindowRates = {
  inputAudioPerMillion: 3,
  outputAudioPerMillion: 12,
  reservationInputTokenCeiling: 131_072,
  reservationOutputTokenCeiling: 8_192,
};

export const calculateLiveWindowUsd = (
  durationSeconds: number,
  rates: LiveWindowRates = DEFAULT_LIVE_WINDOW_RATES,
): number => {
  const seconds = Math.max(1, Math.floor(durationSeconds));
  const budget = seconds * LIVE_AUDIO_TOKENS_PER_SECOND;
  const input = Math.min(rates.reservationInputTokenCeiling, budget);
  const output = Math.min(rates.reservationOutputTokenCeiling, budget);
  return roundUsd(
    (input / 1_000_000) * rates.inputAudioPerMillion
    + (output / 1_000_000) * rates.outputAudioPerMillion,
  );
};

export interface LiveGatewayWindowRates extends LiveWindowRates {
  inputTextPerMillion: number;
  inputVideoPerMillion: number;
  outputTextPerMillion: number;
  maxBillableTurns: number;
  videoTokensPerFrame: number;
  inputTextTokensPerTurn: number;
  outputTextTokenHeadroom: number;
  reservationHeadroomMultiplier: number;
}

export const DEFAULT_LIVE_GATEWAY_WINDOW_RATES: LiveGatewayWindowRates = {
  ...DEFAULT_LIVE_WINDOW_RATES,
  // Use the most expensive enabled Live model's rates for reservation. The
  // checked-in model registry still prices settlement from actual modalities.
  inputTextPerMillion: 0.75,
  inputVideoPerMillion: 3,
  outputTextPerMillion: 4.5,
  maxBillableTurns: LIVE_GATEWAY_MAX_BILLABLE_TURNS,
  videoTokensPerFrame: LIVE_VIDEO_TOKENS_PER_FRAME_LOW,
  // Covers the system instruction and transcription/thinking text that the
  // transport byte counters cannot predict.
  inputTextTokensPerTurn: 8_192,
  outputTextTokenHeadroom: 8_192,
  reservationHeadroomMultiplier: 1.1,
};

export const getLiveGatewayWindowTokenBudget = (
  durationSeconds: number,
  rates: LiveGatewayWindowRates = DEFAULT_LIVE_GATEWAY_WINDOW_RATES,
) => {
  const seconds = Math.max(1, Math.floor(durationSeconds));
  const turns = Math.max(1, Math.floor(rates.maxBillableTurns));
  const oneDirectionAudioTokens = seconds * LIVE_AUDIO_TOKENS_PER_SECOND;
  return {
    // Earlier input and model audio can both re-enter the prompt on each turn.
    audioInputTokens: Math.min(
      rates.reservationInputTokenCeiling * turns,
      oneDirectionAudioTokens * 2 * turns,
    ),
    videoInputTokens: seconds * rates.videoTokensPerFrame * turns,
    textInputTokens: rates.inputTextTokensPerTurn * turns,
    audioOutputTokens: Math.min(rates.reservationOutputTokenCeiling, oneDirectionAudioTokens),
    textOutputTokens: rates.outputTextTokenHeadroom + (seconds * 8),
    maxBillableTurns: turns,
  };
};

/**
 * Reservation for the metered gateway, where Google re-bills retained context
 * on every turn. The gateway enforces the same turn, duration, video cadence,
 * media-resolution, and message-shape bounds used by this estimate.
 */
export const calculateLiveGatewayWindowUsd = (
  durationSeconds: number,
  rates: LiveGatewayWindowRates = DEFAULT_LIVE_GATEWAY_WINDOW_RATES,
): number => {
  const budget = getLiveGatewayWindowTokenBudget(durationSeconds, rates);
  const subtotal =
    (budget.audioInputTokens / 1_000_000) * rates.inputAudioPerMillion
    + (budget.videoInputTokens / 1_000_000) * rates.inputVideoPerMillion
    + (budget.textInputTokens / 1_000_000) * rates.inputTextPerMillion
    + (budget.audioOutputTokens / 1_000_000) * rates.outputAudioPerMillion
    + (budget.textOutputTokens / 1_000_000) * rates.outputTextPerMillion;
  return roundUsd(subtotal * Math.max(1, rates.reservationHeadroomMultiplier));
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
  expectedOutputTokens?: number;
}): number => {
  const rule = resolvePricingRule(params.model, params.pricing);
  const promptTokens = Math.max(0, params.promptTokens);

  // An image is charged as a flat amount per image, not per output token, so
  // its floor has to be that amount or the reservation cannot cover settlement.
  if (params.operation === 'generateImage') {
    const perImage = rule?.generatedImageUsdFallback ?? 0.039;
    const inputRate = rule?.inputPerMillion?.text ?? 0;
    const outputRate = rule?.outputPerMillion?.text ?? 0;
    const outputTokens = Math.max(0, Number(params.expectedOutputTokens || 0));
    return roundUsd(
      perImage
      + (promptTokens / 1_000_000) * inputRate
      + (outputTokens / 1_000_000) * outputRate
    );
  }

  const inputRate = rule?.inputPerMillion?.text ?? 0;
  const outputRate = rule?.outputPerMillion?.text ?? 0;

  const configuredOutputTokens = Number(params.expectedOutputTokens);
  const expectedOutputTokens = Number.isFinite(configuredOutputTokens) && configuredOutputTokens >= 0
    ? configuredOutputTokens
    : params.operation === 'translateText'
      ? Math.max(promptTokens, 256)
      : Math.max(promptTokens * 2, 1024);

  const estimated =
    (promptTokens / 1_000_000) * inputRate
    + (expectedOutputTokens / 1_000_000) * outputRate;

  const floor = params.operation === 'translateText' ? 0.002 : 0.005;
  return roundUsd(Math.max(estimated, floor));
};
