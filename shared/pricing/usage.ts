// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Turning Gemini usage metadata into a dollar figure.
 *
 * Shared deliberately. The app shows the user what a request cost, and the
 * managed backend charges their credits for the same request; if the two ever
 * disagree, the user is overcharged or the service loses money. The draft this
 * grew from had them disagree badly — its backend priced three models by
 * substring match and fell back to Pro rates for everything else, so
 * flash-lite traffic was billed at roughly ten times cost, while generated
 * images were billed at a fraction of a cent because the image fallback only
 * applied when the prompt token count was zero.
 *
 * One implementation makes that class of drift impossible rather than merely
 * unlikely, which is why this is the shared core and not a copy.
 */
import {
  type BillingModality,
  type GeminiPricingRegistry,
  type ModalityRates,
  type ModelPricingRule,
  resolvePricingRule,
} from './registry';

export type ModalityTotals = Record<BillingModality, number>;

export interface ModalityTokenCountLike {
  modality?: string;
  tokenCount?: number;
  tokens?: number;
}

export interface UsageMetadataLike {
  promptTokenCount?: number;
  cachedContentTokenCount?: number;
  candidatesTokenCount?: number;
  responseTokenCount?: number;
  thoughtsTokenCount?: number;
  toolUsePromptTokenCount?: number;
  promptTokensDetails?: ModalityTokenCountLike[];
  cacheTokensDetails?: ModalityTokenCountLike[];
  candidatesTokensDetails?: ModalityTokenCountLike[];
  responseTokensDetails?: ModalityTokenCountLike[];
  toolUsePromptTokensDetails?: ModalityTokenCountLike[];
}

export interface UsageCostInput {
  configuredModel: string;
  modelVersion?: string;
  usageMetadata?: UsageMetadataLike | null;
  generatedImages?: number;
  searchQueries?: number;
  requestCount?: number;
}

export interface CalculatedUsageCost {
  model: string;
  modelDisplayName: string;
  inputTokens: number;
  outputTokens: number;
  thinkingTokens: number;
  cachedInputTokens: number;
  toolInputTokens: number;
  inputByModality: ModalityTotals;
  outputByModality: ModalityTotals;
  generatedImages: number;
  modelCostUsd: number;
  searchPrompts: number;
  searchQueries: number;
  potentialSearchCostUsd: number;
  pricingStatus: 'priced' | 'unpriced';
  pricingNote?: string;
}

export const createModalityTotals = (): ModalityTotals => ({
  text: 0,
  audio: 0,
  image: 0,
  video: 0,
  document: 0,
});

export const asCount = (value: unknown): number => (
  typeof value === 'number' && Number.isFinite(value) && value > 0 ? value : 0
);

export const normalizeModality = (value: unknown): BillingModality => {
  const normalized = String(value || '').toLowerCase();
  if (normalized.includes('audio')) return 'audio';
  if (normalized.includes('image')) return 'image';
  if (normalized.includes('video')) return 'video';
  if (normalized.includes('document')) return 'document';
  return 'text';
};

export const toModalityTotals = (
  details: ModalityTokenCountLike[] | undefined,
  fallbackCount: number,
  fallbackModality: BillingModality = 'text'
): ModalityTotals => {
  const totals = createModalityTotals();
  if (Array.isArray(details) && details.length > 0) {
    for (const detail of details) {
      totals[normalizeModality(detail?.modality)] += asCount(detail?.tokenCount ?? detail?.tokens);
    }
    const detailedCount = Object.values(totals).reduce((sum, count) => sum + count, 0);
    if (fallbackCount > detailedCount) {
      totals[fallbackModality] += fallbackCount - detailedCount;
    }
    return totals;
  }
  totals[fallbackModality] = fallbackCount;
  return totals;
};

export const addModalityTotals = (target: ModalityTotals, addition: ModalityTotals): void => {
  for (const modality of Object.keys(target) as BillingModality[]) {
    target[modality] += addition[modality];
  }
};

export const subtractModalityTotals = (total: ModalityTotals, subtraction: ModalityTotals): ModalityTotals => {
  const result = createModalityTotals();
  for (const modality of Object.keys(result) as BillingModality[]) {
    result[modality] = Math.max(0, total[modality] - subtraction[modality]);
  }
  return result;
};

export const normalizePersistedModalityTotals = (value: unknown): ModalityTotals => {
  const source = value && typeof value === 'object' && !Array.isArray(value)
    ? value as Partial<Record<BillingModality, unknown>>
    : {};
  const result = createModalityTotals();
  for (const modality of Object.keys(result) as BillingModality[]) {
    result[modality] = asCount(source[modality]);
  }
  return result;
};

export const distributeAcrossModalities = (shape: ModalityTotals, amount: number): ModalityTotals => {
  const result = createModalityTotals();
  const shapeTotal = Object.values(shape).reduce((sum, count) => sum + count, 0);
  if (amount <= 0) return result;
  if (shapeTotal <= 0) {
    result.text = amount;
    return result;
  }
  for (const modality of Object.keys(result) as BillingModality[]) {
    result[modality] = amount * (shape[modality] / shapeTotal);
  }
  return result;
};

export const getRate = (rates: ModalityRates | undefined, modality: BillingModality): number | undefined => (
  rates?.[modality] ?? rates?.text
);

export const priceModalities = (totals: ModalityTotals, rates: ModalityRates | undefined): number => {
  let cost = 0;
  for (const modality of Object.keys(totals) as BillingModality[]) {
    const rate = getRate(rates, modality);
    if (rate !== undefined) {
      cost += (totals[modality] / 1_000_000) * rate;
    }
  }
  return cost;
};

export const pickRates = (rule: ModelPricingRule, promptTokenCount: number) => {
  if (rule.longContext && promptTokenCount > rule.longContext.abovePromptTokens) {
    return {
      input: rule.longContext.inputPerMillion,
      output: rule.longContext.outputPerMillion,
      cached: rule.longContext.cachedInputPerMillion,
    };
  }
  return {
    input: rule.inputPerMillion,
    output: rule.outputPerMillion,
    cached: rule.cachedInputPerMillion,
  };
};

export const calculateGeminiUsageCost = (
  options: UsageCostInput,
  pricing: GeminiPricingRegistry,
): CalculatedUsageCost => {
  const metadata = options.usageMetadata || {};
  const promptTokenCount = asCount(metadata.promptTokenCount);
  const cachedInputTokens = Math.min(promptTokenCount, asCount(metadata.cachedContentTokenCount));
  const candidateTokens = asCount(metadata.candidatesTokenCount ?? metadata.responseTokenCount);
  const thinkingTokens = asCount(metadata.thoughtsTokenCount);
  const toolInputTokens = asCount(metadata.toolUsePromptTokenCount);
  const generatedImages = asCount(options.generatedImages);
  const searchQueries = asCount(options.searchQueries);
  const searchPrompts = searchQueries > 0 ? 1 : 0;

  const promptByModality = toModalityTotals(metadata.promptTokensDetails, promptTokenCount);
  const cachedByModality = Array.isArray(metadata.cacheTokensDetails) && metadata.cacheTokensDetails.length > 0
    ? toModalityTotals(metadata.cacheTokensDetails, cachedInputTokens)
    : distributeAcrossModalities(promptByModality, cachedInputTokens);
  const uncachedPromptByModality = subtractModalityTotals(promptByModality, cachedByModality);
  const toolByModality = toModalityTotals(metadata.toolUsePromptTokensDetails, toolInputTokens);
  const inputByModality = { ...promptByModality };
  addModalityTotals(inputByModality, toolByModality);

  const outputDetails = metadata.candidatesTokensDetails ?? metadata.responseTokensDetails;
  const hasOutputDetails = Array.isArray(outputDetails) && outputDetails.length > 0;
  const outputByModality = toModalityTotals(
    outputDetails,
    generatedImages > 0 && !hasOutputDetails ? 0 : candidateTokens
  );

  const requestedModel = (options.modelVersion || options.configuredModel || '').replace(/^models\//, '');
  const rule = resolvePricingRule(requestedModel, pricing);
  const resolvedModel = rule?.id || requestedModel || options.configuredModel || 'unknown-model';
  const modelDisplayName = rule?.displayName || requestedModel || options.configuredModel || 'Unknown model';

  let modelCostUsd = 0;
  let pricingStatus: 'priced' | 'unpriced' = 'unpriced';
  let pricingNote: string | undefined = rule?.unpricedReason || 'No pricing rule is available for this model.';
  if (rule && (rule.inputPerMillion || rule.outputPerMillion || rule.generatedImageUsdFallback !== undefined)) {
    const rates = pickRates(rule, promptTokenCount);
    modelCostUsd += priceModalities(uncachedPromptByModality, rates.input);
    modelCostUsd += priceModalities(toolByModality, rates.input);
    modelCostUsd += priceModalities(cachedByModality, rates.cached ?? rates.input);
    modelCostUsd += priceModalities(outputByModality, rates.output);
    modelCostUsd += (thinkingTokens / 1_000_000) * (rates.output?.text ?? 0);
    if (generatedImages > 0 && !hasOutputDetails && rule.generatedImageUsdFallback !== undefined) {
      modelCostUsd += generatedImages * rule.generatedImageUsdFallback;
    }
    pricingStatus = 'priced';
    pricingNote = undefined;
  }

  const potentialSearchCostUsd = (
    searchQueries / 1000
  ) * pricing.googleSearch.pricePerThousandQueriesUsd;

  return {
    model: resolvedModel,
    modelDisplayName,
    inputTokens: promptTokenCount + toolInputTokens,
    outputTokens: candidateTokens + thinkingTokens,
    thinkingTokens,
    cachedInputTokens,
    toolInputTokens,
    inputByModality,
    outputByModality,
    generatedImages,
    modelCostUsd,
    searchPrompts,
    searchQueries,
    potentialSearchCostUsd,
    pricingStatus,
    ...(pricingNote ? { pricingNote } : {}),
  };
};
