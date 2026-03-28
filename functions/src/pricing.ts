// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { appConfig } from './config';

interface ModelPricing {
  inputPerMillion: number;
  outputPerMillion: number;
}

const MODEL_PRICING: Array<{ pattern: string; pricing: ModelPricing }> = [
  { pattern: 'gemini-3-flash', pricing: { inputPerMillion: 0.50, outputPerMillion: 3.00 } },
  { pattern: 'gemini-2.5-flash', pricing: { inputPerMillion: 0.30, outputPerMillion: 2.50 } },
  { pattern: 'gemini-2.0-flash', pricing: { inputPerMillion: 0.10, outputPerMillion: 0.40 } },
];

const FALLBACK_PRICING: ModelPricing = { inputPerMillion: 0.50, outputPerMillion: 3.00 };
const IMAGE_GENERATION_FALLBACK_USD = 0.039;

const getPricing = (model: string): ModelPricing => {
  const lower = model.toLowerCase();
  for (const entry of MODEL_PRICING) {
    if (lower.includes(entry.pattern)) {
      return entry.pricing;
    }
  }
  return FALLBACK_PRICING;
};

const roundUsd = (value: number): number => Math.max(0, Math.round(value * 1_000_000) / 1_000_000);

export const usdToCredits = (usd: number): number => {
  if (usd <= 0) return 0;
  return Math.max(1, Math.ceil(usd * appConfig.managedCreditsPerUsd));
};

export const creditsToUsd = (credits: number): number => roundUsd(credits / appConfig.managedCreditsPerUsd);

export const uploadBytesToCredits = (bytes: number): number => {
  if (bytes <= 0) return 0;
  const megabytes = bytes / (1024 * 1024);
  return Math.max(1, Math.ceil(megabytes * appConfig.managedUploadCreditsPerMb));
};

export const uploadBytesToUsd = (bytes: number): number => (
  creditsToUsd(uploadBytesToCredits(bytes))
);

export const estimateReservationUsd = (params: {
  model: string;
  promptTokens: number;
  operation: string;
}): number => {
  const pricing = getPricing(params.model);
  const promptTokens = Math.max(0, params.promptTokens);

  let estimatedOutputTokens = Math.max(promptTokens, 1024);
  let minimumUsd = 0.005;

  if (params.operation === 'translateText') {
    estimatedOutputTokens = Math.max(promptTokens, 256);
    minimumUsd = 0.002;
  } else if (params.operation === 'generateImage') {
    estimatedOutputTokens = Math.max(promptTokens * 4, 4096);
    minimumUsd = IMAGE_GENERATION_FALLBACK_USD;
  } else {
    estimatedOutputTokens = Math.max(promptTokens * 2, 1024);
    minimumUsd = 0.005;
  }

  const estimatedUsd =
    (promptTokens / 1_000_000) * pricing.inputPerMillion +
    (estimatedOutputTokens / 1_000_000) * pricing.outputPerMillion;

  return roundUsd(Math.max(estimatedUsd, minimumUsd));
};

export const usageMetadataToUsd = (
  model: string,
  usageMetadata: Record<string, unknown> | undefined,
  fallbackOperation?: string
): number => {
  const pricing = getPricing(model);
  const promptTokenCount = Number(usageMetadata?.promptTokenCount || 0);
  const candidatesTokenCount = Number(usageMetadata?.candidatesTokenCount || 0);

  if (promptTokenCount <= 0 && candidatesTokenCount <= 0) {
    if (fallbackOperation === 'generateImage') {
      return IMAGE_GENERATION_FALLBACK_USD;
    }
    return 0;
  }

  const usd =
    (promptTokenCount / 1_000_000) * pricing.inputPerMillion +
    (candidatesTokenCount / 1_000_000) * pricing.outputPerMillion;

  return roundUsd(usd);
};
