// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { getGeminiModels } from '../../core/config/models';
import {
  BillingModality,
  GeminiPricingRegistry,
  ModelPricingRule,
  ModalityRates,
  resolvePricingRule,
} from '../../core/config/pricing';

const STORAGE_KEY = 'maestro_costTracking';
const STORAGE_KEY_WARNING_SHOWN = 'maestro_costWarningShown';
const COST_SCHEMA_VERSION = 2;

export const GOOGLE_BILLING_URL = 'https://console.cloud.google.com/billing';

export type CostFeature =
  | 'tutor'
  | 'suggestions'
  | 'translation'
  | 'image'
  | 'liveConversation'
  | 'reengagement'
  | 'stt'
  | 'tts'
  | 'audioNote'
  | 'music';

type ModalityTotals = Record<BillingModality, number>;

export interface CostBreakdownEntry {
  feature: CostFeature;
  model: string;
  modelDisplayName: string;
  pricingEffectiveAt: string;
  requests: number;
  inputTokens: number;
  outputTokens: number;
  thinkingTokens: number;
  cachedInputTokens: number;
  toolInputTokens: number;
  inputByModality: ModalityTotals;
  outputByModality: ModalityTotals;
  generatedImages: number;
  generatedAudioSeconds: number;
  searchPrompts: number;
  searchQueries: number;
  modelCostUsd: number;
  potentialSearchCostUsd: number;
  pricingStatus: 'priced' | 'unpriced';
  pricingNote?: string;
}

interface CostDataV2 {
  schemaVersion: 2;
  startedAt: string;
  updatedAt: string;
  pricingEffectiveAt: string;
  entries: CostBreakdownEntry[];
  legacyEstimateUsd: number;
  legacyInputTokens: number;
  legacyOutputTokens: number;
  legacyImageGenCount: number;
}

export interface CostSummary {
  schemaVersion: 2;
  startedAt: string;
  updatedAt: string;
  pricingEffectiveAt: string;
  pricingSourceUrl: string;
  entries: CostBreakdownEntry[];
  inputTokens: number;
  outputTokens: number;
  thinkingTokens: number;
  cachedInputTokens: number;
  imageGenCount: number;
  searchPrompts: number;
  searchQueries: number;
  knownModelCostUsd: number;
  potentialSearchCostUsd: number;
  totalCostUsd: number;
  legacyEstimateUsd: number;
  hasUnpricedUsage: boolean;
}

interface ModalityTokenCountLike {
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

export interface TrackGeminiUsageOptions {
  feature: CostFeature;
  configuredModel: string;
  modelVersion?: string;
  usageMetadata?: UsageMetadataLike | null;
  generatedImages?: number;
  searchQueries?: number;
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

const createModalityTotals = (): ModalityTotals => ({
  text: 0,
  audio: 0,
  image: 0,
  video: 0,
  document: 0,
});

const asCount = (value: unknown): number => (
  typeof value === 'number' && Number.isFinite(value) && value > 0 ? value : 0
);

const normalizeModality = (value: unknown): BillingModality => {
  const normalized = String(value || '').toLowerCase();
  if (normalized.includes('audio')) return 'audio';
  if (normalized.includes('image')) return 'image';
  if (normalized.includes('video')) return 'video';
  if (normalized.includes('document')) return 'document';
  return 'text';
};

const toModalityTotals = (
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

const addModalityTotals = (target: ModalityTotals, addition: ModalityTotals): void => {
  for (const modality of Object.keys(target) as BillingModality[]) {
    target[modality] += addition[modality];
  }
};

const subtractModalityTotals = (total: ModalityTotals, subtraction: ModalityTotals): ModalityTotals => {
  const result = createModalityTotals();
  for (const modality of Object.keys(result) as BillingModality[]) {
    result[modality] = Math.max(0, total[modality] - subtraction[modality]);
  }
  return result;
};

const getRate = (rates: ModalityRates | undefined, modality: BillingModality): number | undefined => (
  rates?.[modality] ?? rates?.text
);

const priceModalities = (totals: ModalityTotals, rates: ModalityRates | undefined): number => {
  let cost = 0;
  for (const modality of Object.keys(totals) as BillingModality[]) {
    const rate = getRate(rates, modality);
    if (rate !== undefined) {
      cost += (totals[modality] / 1_000_000) * rate;
    }
  }
  return cost;
};

const pickRates = (rule: ModelPricingRule, promptTokenCount: number) => {
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
  options: TrackGeminiUsageOptions,
  pricing: GeminiPricingRegistry = getGeminiModels().pricing
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
  const cachedByModality = toModalityTotals(metadata.cacheTokensDetails, cachedInputTokens);
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
  outputByModality.text += thinkingTokens;

  const requestedModel = (options.modelVersion || options.configuredModel || '').replace(/^models\//, '');
  const rule = resolvePricingRule(requestedModel, pricing)
    ?? resolvePricingRule(options.configuredModel, pricing);
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

const createEmptyData = (): CostDataV2 => {
  const now = new Date().toISOString();
  return {
    schemaVersion: COST_SCHEMA_VERSION,
    startedAt: now,
    updatedAt: now,
    pricingEffectiveAt: getGeminiModels().pricing.effectiveAt,
    entries: [],
    legacyEstimateUsd: 0,
    legacyInputTokens: 0,
    legacyOutputTokens: 0,
    legacyImageGenCount: 0,
  };
};

const migrateLegacyData = (parsed: Record<string, unknown>): CostDataV2 => ({
  ...createEmptyData(),
  legacyEstimateUsd: asCount(parsed.totalCostUsd),
  legacyInputTokens: asCount(parsed.inputTokens),
  legacyOutputTokens: asCount(parsed.outputTokens),
  legacyImageGenCount: asCount(parsed.imageGenCount),
});

const readData = (): CostDataV2 => {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return createEmptyData();
    const parsed = JSON.parse(raw) as Partial<CostDataV2> & Record<string, unknown>;
    if (parsed.schemaVersion !== COST_SCHEMA_VERSION || !Array.isArray(parsed.entries)) {
      return migrateLegacyData(parsed);
    }
    return {
      ...createEmptyData(),
      ...parsed,
      schemaVersion: COST_SCHEMA_VERSION,
      entries: parsed.entries.map(entry => ({
        ...entry,
        searchPrompts: entry.searchPrompts || 0,
        pricingEffectiveAt: entry.pricingEffectiveAt || parsed.pricingEffectiveAt || getGeminiModels().pricing.effectiveAt,
      })),
    } as CostDataV2;
  } catch {
    return createEmptyData();
  }
};

const writeData = (data: CostDataV2): void => {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
  } catch {
    // Ignore storage write errors.
  }
};

const getOrCreateEntry = (
  data: CostDataV2,
  feature: CostFeature,
  model: string,
  modelDisplayName: string,
  pricingEffectiveAt: string
): CostBreakdownEntry => {
  const existing = data.entries.find(entry => (
    entry.feature === feature
    && entry.model === model
    && entry.pricingEffectiveAt === pricingEffectiveAt
  ));
  if (existing) return existing;
  const created: CostBreakdownEntry = {
    feature,
    model,
    modelDisplayName,
    pricingEffectiveAt,
    requests: 0,
    inputTokens: 0,
    outputTokens: 0,
    thinkingTokens: 0,
    cachedInputTokens: 0,
    toolInputTokens: 0,
    inputByModality: createModalityTotals(),
    outputByModality: createModalityTotals(),
    generatedImages: 0,
    generatedAudioSeconds: 0,
    searchPrompts: 0,
    searchQueries: 0,
    modelCostUsd: 0,
    potentialSearchCostUsd: 0,
    pricingStatus: 'priced',
  };
  data.entries.push(created);
  return created;
};

export const trackGeminiUsage = (options: TrackGeminiUsageOptions): void => {
  if (!options.usageMetadata && !options.generatedImages && !options.searchQueries) return;
  const calculated = calculateGeminiUsageCost(options);
  const data = readData();
  const pricingEffectiveAt = getGeminiModels().pricing.effectiveAt;
  const entry = getOrCreateEntry(
    data,
    options.feature,
    calculated.model,
    calculated.modelDisplayName,
    pricingEffectiveAt
  );
  entry.requests += 1;
  entry.inputTokens += calculated.inputTokens;
  entry.outputTokens += calculated.outputTokens;
  entry.thinkingTokens += calculated.thinkingTokens;
  entry.cachedInputTokens += calculated.cachedInputTokens;
  entry.toolInputTokens += calculated.toolInputTokens;
  addModalityTotals(entry.inputByModality, calculated.inputByModality);
  addModalityTotals(entry.outputByModality, calculated.outputByModality);
  entry.generatedImages += calculated.generatedImages;
  entry.searchPrompts += calculated.searchPrompts;
  entry.searchQueries += calculated.searchQueries;
  entry.modelCostUsd += calculated.modelCostUsd;
  entry.potentialSearchCostUsd += calculated.potentialSearchCostUsd;
  if (calculated.pricingStatus === 'unpriced') {
    entry.pricingStatus = 'unpriced';
    entry.pricingNote = calculated.pricingNote;
  }
  data.updatedAt = new Date().toISOString();
  data.pricingEffectiveAt = pricingEffectiveAt;
  writeData(data);
};

export const trackMusicGeneration = (model: string, durationSeconds: number): void => {
  const pricing = getGeminiModels().pricing;
  const rule = resolvePricingRule(model, pricing);
  const normalizedDuration = asCount(durationSeconds);
  if (normalizedDuration === 0) return;
  const resolvedModel = rule?.id || model.replace(/^models\//, '') || 'unknown-music-model';
  const data = readData();
  const entry = getOrCreateEntry(
    data,
    'music',
    resolvedModel,
    rule?.displayName || resolvedModel,
    pricing.effectiveAt
  );
  entry.requests += 1;
  entry.generatedAudioSeconds += normalizedDuration;
  if (rule?.generatedAudioPerMinuteUsd !== undefined) {
    entry.modelCostUsd += (normalizedDuration / 60) * rule.generatedAudioPerMinuteUsd;
  } else {
    entry.pricingStatus = 'unpriced';
    entry.pricingNote = rule?.unpricedReason || 'No published price is available for this music model.';
  }
  data.updatedAt = new Date().toISOString();
  data.pricingEffectiveAt = pricing.effectiveAt;
  writeData(data);
};

export const getCostSummary = (): CostSummary => {
  const data = readData();
  const summary = data.entries.reduce((acc, entry) => {
    acc.inputTokens += entry.inputTokens;
    acc.outputTokens += entry.outputTokens;
    acc.thinkingTokens += entry.thinkingTokens;
    acc.cachedInputTokens += entry.cachedInputTokens;
    acc.imageGenCount += entry.generatedImages;
    acc.searchPrompts += entry.searchPrompts;
    acc.searchQueries += entry.searchQueries;
    acc.knownModelCostUsd += entry.modelCostUsd;
    acc.potentialSearchCostUsd += entry.potentialSearchCostUsd;
    acc.hasUnpricedUsage ||= entry.pricingStatus === 'unpriced';
    return acc;
  }, {
    inputTokens: 0,
    outputTokens: 0,
    thinkingTokens: 0,
    cachedInputTokens: 0,
    imageGenCount: 0,
    searchPrompts: 0,
    searchQueries: 0,
    knownModelCostUsd: 0,
    potentialSearchCostUsd: 0,
    hasUnpricedUsage: false,
  });
  return {
    schemaVersion: COST_SCHEMA_VERSION,
    startedAt: data.startedAt,
    updatedAt: data.updatedAt,
    pricingEffectiveAt: data.pricingEffectiveAt,
    pricingSourceUrl: getGeminiModels().pricing.sourceUrl,
    entries: data.entries.slice().sort((a, b) => (b.modelCostUsd + b.potentialSearchCostUsd) - (a.modelCostUsd + a.potentialSearchCostUsd)),
    ...summary,
    totalCostUsd: summary.knownModelCostUsd + summary.potentialSearchCostUsd,
    legacyEstimateUsd: data.legacyEstimateUsd,
  };
};

export const resetCostTracking = (): void => {
  try {
    localStorage.removeItem(STORAGE_KEY);
    localStorage.removeItem(STORAGE_KEY_WARNING_SHOWN);
  } catch {
    // Ignore storage errors.
  }
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
  } catch {
    // Ignore storage errors.
  }
};
