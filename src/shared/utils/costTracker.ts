// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

const STORAGE_KEY = 'maestro_costTracking';
const STORAGE_KEY_WARNING_SHOWN = 'maestro_costWarningShown';

export const GOOGLE_BILLING_URL = 'https://console.cloud.google.com/billing';

export interface CostData {
  inputTokens: number;
  outputTokens: number;
  imageGenCount: number;
  totalCostUsd: number;
}

interface ModelPricing {
  inputPerMillion: number;
  outputPerMillion: number;
}

const MODEL_PRICING: { pattern: string; pricing: ModelPricing }[] = [
  { pattern: 'gemini-3-flash', pricing: { inputPerMillion: 0.50, outputPerMillion: 3.00 } },
  { pattern: 'gemini-2.5-flash', pricing: { inputPerMillion: 0.30, outputPerMillion: 2.50 } },
  { pattern: 'gemini-2.0-flash', pricing: { inputPerMillion: 0.10, outputPerMillion: 0.40 } },
];

const FALLBACK_PRICING: ModelPricing = { inputPerMillion: 0.50, outputPerMillion: 3.00 };

const IMAGE_GEN_COST_USD = 0.039;

const getPricing = (model: string): ModelPricing => {
  const lower = model.toLowerCase();
  for (const entry of MODEL_PRICING) {
    if (lower.includes(entry.pattern)) return entry.pricing;
  }
  return FALLBACK_PRICING;
};

const readData = (): CostData => {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return { inputTokens: 0, outputTokens: 0, imageGenCount: 0, totalCostUsd: 0 };
    const parsed = JSON.parse(raw);
    return {
      inputTokens: parsed.inputTokens || 0,
      outputTokens: parsed.outputTokens || 0,
      imageGenCount: parsed.imageGenCount || 0,
      totalCostUsd: parsed.totalCostUsd || 0,
    };
  } catch {
    return { inputTokens: 0, outputTokens: 0, imageGenCount: 0, totalCostUsd: 0 };
  }
};

const writeData = (data: CostData): void => {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
  } catch { /* ignore */ }
};

export const trackTokenUsage = (model: string, usageMetadata: any): void => {
  if (!usageMetadata) return;
  const inputCount = usageMetadata.promptTokenCount || 0;
  const outputCount = usageMetadata.candidatesTokenCount || 0;
  if (inputCount === 0 && outputCount === 0) return;

  const pricing = getPricing(model);
  const cost = (inputCount / 1_000_000) * pricing.inputPerMillion
             + (outputCount / 1_000_000) * pricing.outputPerMillion;

  const data = readData();
  data.inputTokens += inputCount;
  data.outputTokens += outputCount;
  data.totalCostUsd += cost;
  writeData(data);
};

export const trackImageGeneration = (): void => {
  const data = readData();
  data.imageGenCount += 1;
  data.totalCostUsd += IMAGE_GEN_COST_USD;
  writeData(data);
};

export const getCostSummary = (): CostData => readData();

export const resetCostTracking = (): void => {
  try {
    localStorage.removeItem(STORAGE_KEY);
    localStorage.removeItem(STORAGE_KEY_WARNING_SHOWN);
  } catch { /* ignore */ }
};

export const hasShownCostWarning = (): boolean => {
  try {
    return localStorage.getItem(STORAGE_KEY_WARNING_SHOWN) === '1';
  } catch {
    return false;
  }
};

export const setCostWarningShown = (): void => {
  try {
    localStorage.setItem(STORAGE_KEY_WARNING_SHOWN, '1');
  } catch { /* ignore */ }
};
