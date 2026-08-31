// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { getGeminiModels } from '../../core/config/models';
import { type BillingModality, resolvePricingRule } from '../../core/config/pricing';
// The cost maths lives in shared/ because the managed backend bills with the
// very same code. See shared/pricing/usage.ts.
import {
  type CalculatedUsageCost,
  type ModalityTokenCountLike,
  type ModalityTotals,
  type UsageCostInput,
  type UsageMetadataLike,
  addModalityTotals,
  asCount,
  calculateGeminiUsageCost as calculateUsageCostShared,
  createModalityTotals,
  normalizePersistedModalityTotals,
  toModalityTotals,
} from '../../../shared/pricing/usage';

export type { CalculatedUsageCost, ModalityTokenCountLike, UsageMetadataLike };

export interface TrackGeminiUsageOptions extends UsageCostInput {
  feature: CostFeature;
}

/** Prices a request against the registry the app is currently running with. */
export const calculateGeminiUsageCost = (
  options: TrackGeminiUsageOptions,
  pricing = getGeminiModels().pricing,
): CalculatedUsageCost => calculateUsageCostShared(options, pricing);

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
        requests: asCount(entry.requests),
        inputTokens: asCount(entry.inputTokens),
        outputTokens: asCount(entry.outputTokens),
        thinkingTokens: asCount(entry.thinkingTokens),
        cachedInputTokens: asCount(entry.cachedInputTokens),
        toolInputTokens: asCount(entry.toolInputTokens),
        inputByModality: normalizePersistedModalityTotals(entry.inputByModality),
        outputByModality: normalizePersistedModalityTotals(entry.outputByModality),
        generatedImages: asCount(entry.generatedImages),
        generatedAudioSeconds: asCount(entry.generatedAudioSeconds),
        searchPrompts: asCount(entry.searchPrompts),
        searchQueries: asCount(entry.searchQueries),
        modelCostUsd: asCount(entry.modelCostUsd),
        potentialSearchCostUsd: asCount(entry.potentialSearchCostUsd),
        pricingEffectiveAt: entry.pricingEffectiveAt || parsed.pricingEffectiveAt || getGeminiModels().pricing.effectiveAt,
      })),
      legacyEstimateUsd: asCount(parsed.legacyEstimateUsd),
      legacyInputTokens: asCount(parsed.legacyInputTokens),
      legacyOutputTokens: asCount(parsed.legacyOutputTokens),
      legacyImageGenCount: asCount(parsed.legacyImageGenCount),
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
  entry.requests += options.requestCount === undefined ? 1 : asCount(options.requestCount);
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

type LiveUsageTrackingOptions = Pick<TrackGeminiUsageOptions, 'feature' | 'configuredModel' | 'modelVersion'>;

const normalizeUsageDetails = (
  details: ModalityTokenCountLike[] | undefined
): ModalityTokenCountLike[] | undefined => {
  if (!Array.isArray(details) || details.length === 0) return undefined;
  const totals = toModalityTotals(details, 0);
  const normalized = (Object.keys(totals) as BillingModality[])
    .filter(modality => totals[modality] > 0)
    .map(modality => ({ modality, tokenCount: totals[modality] }));
  return normalized.length > 0 ? normalized : undefined;
};

const normalizeUsageSnapshot = (metadata: UsageMetadataLike): UsageMetadataLike => ({
  promptTokenCount: asCount(metadata.promptTokenCount),
  cachedContentTokenCount: asCount(metadata.cachedContentTokenCount),
  candidatesTokenCount: asCount(metadata.candidatesTokenCount ?? metadata.responseTokenCount),
  thoughtsTokenCount: asCount(metadata.thoughtsTokenCount),
  toolUsePromptTokenCount: asCount(metadata.toolUsePromptTokenCount),
  promptTokensDetails: normalizeUsageDetails(metadata.promptTokensDetails),
  cacheTokensDetails: normalizeUsageDetails(metadata.cacheTokensDetails),
  candidatesTokensDetails: normalizeUsageDetails(
    metadata.candidatesTokensDetails ?? metadata.responseTokensDetails
  ),
  toolUsePromptTokensDetails: normalizeUsageDetails(metadata.toolUsePromptTokensDetails),
});

const usageCountDelta = (current: unknown, previous: unknown): number => {
  const currentCount = asCount(current);
  const previousCount = asCount(previous);
  return currentCount >= previousCount ? currentCount - previousCount : currentCount;
};

const usageDetailsDelta = (
  current: ModalityTokenCountLike[] | undefined,
  previous: ModalityTokenCountLike[] | undefined
): ModalityTokenCountLike[] | undefined => {
  if (!current) return undefined;
  const currentTotals = toModalityTotals(current, 0);
  const previousTotals = toModalityTotals(previous, 0);
  const delta = (Object.keys(currentTotals) as BillingModality[])
    .map(modality => ({
      modality,
      tokenCount: usageCountDelta(currentTotals[modality], previousTotals[modality]),
    }))
    .filter(detail => detail.tokenCount > 0);
  return delta.length > 0 ? delta : undefined;
};

const usageSnapshotDelta = (
  current: UsageMetadataLike,
  previous?: UsageMetadataLike
): UsageMetadataLike => ({
  promptTokenCount: usageCountDelta(current.promptTokenCount, previous?.promptTokenCount),
  cachedContentTokenCount: usageCountDelta(
    current.cachedContentTokenCount,
    previous?.cachedContentTokenCount
  ),
  candidatesTokenCount: usageCountDelta(current.candidatesTokenCount, previous?.candidatesTokenCount),
  thoughtsTokenCount: usageCountDelta(current.thoughtsTokenCount, previous?.thoughtsTokenCount),
  toolUsePromptTokenCount: usageCountDelta(
    current.toolUsePromptTokenCount,
    previous?.toolUsePromptTokenCount
  ),
  promptTokensDetails: usageDetailsDelta(current.promptTokensDetails, previous?.promptTokensDetails),
  cacheTokensDetails: usageDetailsDelta(current.cacheTokensDetails, previous?.cacheTokensDetails),
  candidatesTokensDetails: usageDetailsDelta(
    current.candidatesTokensDetails,
    previous?.candidatesTokensDetails
  ),
  toolUsePromptTokensDetails: usageDetailsDelta(
    current.toolUsePromptTokensDetails,
    previous?.toolUsePromptTokensDetails
  ),
});

const usageDetailsTotal = (details: ModalityTokenCountLike[] | undefined): number => (
  Object.values(toModalityTotals(details, 0)).reduce((sum, count) => sum + count, 0)
);

const hasUsage = (metadata: UsageMetadataLike): boolean => (
  asCount(metadata.promptTokenCount)
  + asCount(metadata.cachedContentTokenCount)
  + asCount(metadata.candidatesTokenCount)
  + asCount(metadata.thoughtsTokenCount)
  + asCount(metadata.toolUsePromptTokenCount)
  + [
    metadata.promptTokensDetails,
    metadata.cacheTokensDetails,
    metadata.candidatesTokensDetails,
    metadata.toolUsePromptTokensDetails,
  ].reduce((sum, details) => sum + usageDetailsTotal(details), 0)
  > 0
);

export const createLiveUsageTracker = (options: LiveUsageTrackingOptions) => {
  let previous: UsageMetadataLike | undefined;
  let hasRecordedRequest = false;
  return {
    trackSnapshot(metadata: UsageMetadataLike): void {
      const normalized = normalizeUsageSnapshot(metadata);
      const delta = usageSnapshotDelta(normalized, previous);
      previous = normalized;
      if (!hasUsage(delta)) return;
      trackGeminiUsage({
        ...options,
        usageMetadata: delta,
        requestCount: hasRecordedRequest ? 0 : 1,
      });
      hasRecordedRequest = true;
    },
  };
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
