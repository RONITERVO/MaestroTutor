// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

const STORAGE_KEY = 'maestro_costTracking';
const STORAGE_KEY_WARNING_SHOWN = 'maestro_costWarningShown';
const STORAGE_VERSION = 2;
const MAX_USAGE_ENTRIES = 200;

export const GOOGLE_BILLING_URL = 'https://console.cloud.google.com/billing';
export const USAGE_TRACKING_CHANGED_EVENT = 'maestro-usage-tracking-changed';

export type UsageAccessMode = 'byok' | 'managed';

export interface CostData {
  requestCount: number;
  inputTokens: number;
  outputTokens: number;
  thoughtTokens: number;
  cachedContentTokens: number;
  toolUsePromptTokens: number;
  imageGenCount: number;
  totalCostUsd: number;
}

export interface UsageEntry extends CostData {
  id: string;
  createdAt: number;
  accessMode: UsageAccessMode;
  api: string;
  surface: string;
  model: string;
}

export interface UsageTrackingOptions {
  accessMode?: UsageAccessMode;
  api?: string;
  surface?: string;
  imageCount?: number;
}

interface CostTrackerStateV2 {
  version: number;
  entries: UsageEntry[];
  legacyTotals: CostData;
}

interface UsageMetadataCounts {
  promptTokenCount: number;
  candidatesTokenCount: number;
  thoughtsTokenCount: number;
  cachedContentTokenCount: number;
  toolUsePromptTokenCount: number;
}

interface TokenPricing {
  kind: 'tokens';
  inputPerMillion: number | ((promptTokens: number) => number);
  outputPerMillion: number | ((promptTokens: number) => number);
}

interface ImagePricing {
  kind: 'image';
  inputPerMillion: number | ((promptTokens: number) => number);
  imageOutputUsd: number;
}

type ModelPricing = TokenPricing | ImagePricing;

const EMPTY_COST_DATA: CostData = {
  requestCount: 0,
  inputTokens: 0,
  outputTokens: 0,
  thoughtTokens: 0,
  cachedContentTokens: 0,
  toolUsePromptTokens: 0,
  imageGenCount: 0,
  totalCostUsd: 0,
};

const MODEL_PRICING: Array<{ pattern: string; pricing: ModelPricing }> = [
  {
    pattern: 'gemini-3.1-pro-preview',
    pricing: {
      kind: 'tokens',
      inputPerMillion: promptTokens => (promptTokens > 200_000 ? 4 : 2),
      outputPerMillion: promptTokens => (promptTokens > 200_000 ? 18 : 12),
    },
  },
  {
    pattern: 'gemini-3-pro-preview',
    pricing: {
      kind: 'tokens',
      inputPerMillion: promptTokens => (promptTokens > 200_000 ? 4 : 2),
      outputPerMillion: promptTokens => (promptTokens > 200_000 ? 18 : 12),
    },
  },
  {
    pattern: 'gemini-3-flash-preview',
    pricing: {
      kind: 'tokens',
      inputPerMillion: 0.5,
      outputPerMillion: 3,
    },
  },
  {
    pattern: 'gemini-3-flash',
    pricing: {
      kind: 'tokens',
      inputPerMillion: 0.5,
      outputPerMillion: 3,
    },
  },
  {
    pattern: 'gemini-2.5-flash-image',
    pricing: {
      kind: 'image',
      inputPerMillion: 0.3,
      imageOutputUsd: 0.039,
    },
  },
  {
    pattern: 'gemini-2.5-flash-lite',
    pricing: {
      kind: 'tokens',
      inputPerMillion: 0.1,
      outputPerMillion: 0.4,
    },
  },
  {
    pattern: 'gemini-2.5-flash',
    pricing: {
      kind: 'tokens',
      inputPerMillion: 0.3,
      outputPerMillion: 2.5,
    },
  },
  {
    pattern: 'gemini-2.0-flash-lite',
    pricing: {
      kind: 'tokens',
      inputPerMillion: 0.075,
      outputPerMillion: 0.3,
    },
  },
];

const FALLBACK_PRICING: TokenPricing = {
  kind: 'tokens',
  inputPerMillion: 0.5,
  outputPerMillion: 3,
};

const isRecord = (value: unknown): value is Record<string, unknown> => (
  Boolean(value) && typeof value === 'object'
);

const toFiniteNumber = (value: unknown): number => {
  const numeric = Number(value);
  return Number.isFinite(numeric) ? numeric : 0;
};

const cloneEmptyCostData = (): CostData => ({ ...EMPTY_COST_DATA });

const normalizeCostData = (value: unknown): CostData => {
  if (!isRecord(value)) return cloneEmptyCostData();
  return {
    requestCount: Math.max(0, Math.floor(toFiniteNumber(value.requestCount))),
    inputTokens: Math.max(0, Math.floor(toFiniteNumber(value.inputTokens))),
    outputTokens: Math.max(0, Math.floor(toFiniteNumber(value.outputTokens))),
    thoughtTokens: Math.max(0, Math.floor(toFiniteNumber(value.thoughtTokens))),
    cachedContentTokens: Math.max(0, Math.floor(toFiniteNumber(value.cachedContentTokens))),
    toolUsePromptTokens: Math.max(0, Math.floor(toFiniteNumber(value.toolUsePromptTokens))),
    imageGenCount: Math.max(0, Math.floor(toFiniteNumber(value.imageGenCount))),
    totalCostUsd: Math.max(0, toFiniteNumber(value.totalCostUsd)),
  };
};

const mergeCostData = (left: CostData, right: CostData): CostData => ({
  requestCount: left.requestCount + right.requestCount,
  inputTokens: left.inputTokens + right.inputTokens,
  outputTokens: left.outputTokens + right.outputTokens,
  thoughtTokens: left.thoughtTokens + right.thoughtTokens,
  cachedContentTokens: left.cachedContentTokens + right.cachedContentTokens,
  toolUsePromptTokens: left.toolUsePromptTokens + right.toolUsePromptTokens,
  imageGenCount: left.imageGenCount + right.imageGenCount,
  totalCostUsd: left.totalCostUsd + right.totalCostUsd,
});

const summarizeEntries = (entries: UsageEntry[]): CostData => entries.reduce(
  (summary, entry) => mergeCostData(summary, entry),
  cloneEmptyCostData(),
);

const normalizeUsageEntry = (value: unknown): UsageEntry | null => {
  if (!isRecord(value)) return null;
  const id = typeof value.id === 'string' && value.id.trim() ? value.id.trim() : '';
  if (!id) return null;

  const accessMode = value.accessMode === 'managed' ? 'managed' : 'byok';
  const createdAt = Math.max(0, Math.floor(toFiniteNumber(value.createdAt)));
  const normalized = normalizeCostData(value);

  return {
    id,
    createdAt,
    accessMode,
    api: typeof value.api === 'string' && value.api.trim() ? value.api.trim() : 'generate-content',
    surface: typeof value.surface === 'string' && value.surface.trim() ? value.surface.trim() : 'chat-response',
    model: typeof value.model === 'string' ? value.model.trim() : '',
    ...normalized,
  };
};

const normalizeState = (value: unknown): CostTrackerStateV2 => {
  if (!isRecord(value) || value.version !== STORAGE_VERSION) {
    return {
      version: STORAGE_VERSION,
      entries: [],
      legacyTotals: cloneEmptyCostData(),
    };
  }

  const rawEntries = Array.isArray(value.entries) ? value.entries : [];
  const entries = rawEntries
    .map(normalizeUsageEntry)
    .filter((entry): entry is UsageEntry => Boolean(entry))
    .slice(0, MAX_USAGE_ENTRIES);

  return {
    version: STORAGE_VERSION,
    entries,
    legacyTotals: normalizeCostData(value.legacyTotals),
  };
};

const dispatchUsageTrackingChanged = (): void => {
  try {
    if (typeof window !== 'undefined') {
      window.dispatchEvent(new CustomEvent(USAGE_TRACKING_CHANGED_EVENT));
    }
  } catch {
    // Ignore browser event issues.
  }
};

const readState = (): CostTrackerStateV2 => {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return {
        version: STORAGE_VERSION,
        entries: [],
        legacyTotals: cloneEmptyCostData(),
      };
    }

    const parsed = JSON.parse(raw);
    if (isRecord(parsed) && parsed.version === STORAGE_VERSION) {
      return normalizeState(parsed);
    }

    return {
      version: STORAGE_VERSION,
      entries: [],
      legacyTotals: normalizeCostData(parsed),
    };
  } catch {
    return {
      version: STORAGE_VERSION,
      entries: [],
      legacyTotals: cloneEmptyCostData(),
    };
  }
};

const writeState = (state: CostTrackerStateV2): void => {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  } catch {
    // Ignore storage write failures.
  }
  dispatchUsageTrackingChanged();
};

const getPricing = (model: string): ModelPricing => {
  const lower = model.toLowerCase();
  for (const entry of MODEL_PRICING) {
    if (lower.includes(entry.pattern)) return entry.pricing;
  }
  return FALLBACK_PRICING;
};

const resolveRate = (
  rate: number | ((promptTokens: number) => number),
  promptTokens: number
): number => (
  typeof rate === 'function'
    ? rate(promptTokens)
    : rate
);

const readUsageMetadataCounts = (usageMetadata: unknown): UsageMetadataCounts => {
  if (!isRecord(usageMetadata)) {
    return {
      promptTokenCount: 0,
      candidatesTokenCount: 0,
      thoughtsTokenCount: 0,
      cachedContentTokenCount: 0,
      toolUsePromptTokenCount: 0,
    };
  }

  return {
    promptTokenCount: Math.max(0, Math.floor(toFiniteNumber(usageMetadata.promptTokenCount))),
    candidatesTokenCount: Math.max(0, Math.floor(toFiniteNumber(usageMetadata.candidatesTokenCount))),
    thoughtsTokenCount: Math.max(0, Math.floor(toFiniteNumber(usageMetadata.thoughtsTokenCount))),
    cachedContentTokenCount: Math.max(0, Math.floor(toFiniteNumber(usageMetadata.cachedContentTokenCount))),
    toolUsePromptTokenCount: Math.max(0, Math.floor(toFiniteNumber(usageMetadata.toolUsePromptTokenCount))),
  };
};

const calculateCostUsd = (
  model: string,
  usage: UsageMetadataCounts,
  imageCount: number
): number => {
  const pricing = getPricing(model);
  const promptTokens = usage.promptTokenCount;

  if (pricing.kind === 'image') {
    return (
      (promptTokens / 1_000_000) * resolveRate(pricing.inputPerMillion, promptTokens) +
      (imageCount * pricing.imageOutputUsd)
    );
  }

  return (
    (promptTokens / 1_000_000) * resolveRate(pricing.inputPerMillion, promptTokens) +
    ((usage.candidatesTokenCount + usage.thoughtsTokenCount) / 1_000_000) *
      resolveRate(pricing.outputPerMillion, promptTokens)
  );
};

const createUsageEntry = (
  model: string,
  usageMetadata: unknown,
  options: UsageTrackingOptions = {}
): UsageEntry | null => {
  const usage = readUsageMetadataCounts(usageMetadata);
  const imageGenCount = Math.max(0, Math.floor(toFiniteNumber(options.imageCount ?? 0)));
  const hasUsage =
    usage.promptTokenCount > 0 ||
    usage.candidatesTokenCount > 0 ||
    usage.thoughtsTokenCount > 0 ||
    usage.cachedContentTokenCount > 0 ||
    usage.toolUsePromptTokenCount > 0 ||
    imageGenCount > 0;

  if (!hasUsage) return null;

  return {
    id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    createdAt: Date.now(),
    accessMode: options.accessMode === 'managed' ? 'managed' : 'byok',
    api: options.api || 'generate-content',
    surface: options.surface || 'chat-response',
    model,
    requestCount: 1,
    inputTokens: usage.promptTokenCount,
    outputTokens: usage.candidatesTokenCount,
    thoughtTokens: usage.thoughtsTokenCount,
    cachedContentTokens: usage.cachedContentTokenCount,
    toolUsePromptTokens: usage.toolUsePromptTokenCount,
    imageGenCount,
    totalCostUsd: calculateCostUsd(model, usage, imageGenCount),
  };
};

const appendUsageEntry = (entry: UsageEntry): void => {
  const state = readState();
  writeState({
    ...state,
    entries: [entry, ...state.entries].slice(0, MAX_USAGE_ENTRIES),
  });
};

export const trackTokenUsage = (
  model: string,
  usageMetadata: unknown,
  options: UsageTrackingOptions = {}
): void => {
  const entry = createUsageEntry(model, usageMetadata, options);
  if (!entry) return;
  appendUsageEntry(entry);
};

export const trackImageGeneration = (options: {
  model: string;
  usageMetadata?: unknown;
  accessMode?: UsageAccessMode;
  api?: string;
  surface?: string;
  imageCount?: number;
}): void => {
  const entry = createUsageEntry(options.model, options.usageMetadata, {
    accessMode: options.accessMode,
    api: options.api || 'generate-image',
    surface: options.surface || 'image-generation',
    imageCount: options.imageCount ?? 1,
  });
  if (!entry) return;
  appendUsageEntry(entry);
};

export const getUsageEntries = (): UsageEntry[] => readState().entries;

export const getLegacyCostSummary = (): CostData => readState().legacyTotals;

export const hasLegacyUsageTotals = (): boolean => {
  const legacyTotals = getLegacyCostSummary();
  return (
    legacyTotals.totalCostUsd > 0 ||
    legacyTotals.inputTokens > 0 ||
    legacyTotals.outputTokens > 0 ||
    legacyTotals.imageGenCount > 0
  );
};

export const getCostSummary = (): CostData => {
  const state = readState();
  return mergeCostData(state.legacyTotals, summarizeEntries(state.entries));
};

export const clearUsageHistory = (): void => {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Ignore storage removal issues.
  }
  dispatchUsageTrackingChanged();
};

export const resetCostTracking = (): void => {
  try {
    localStorage.removeItem(STORAGE_KEY);
    localStorage.removeItem(STORAGE_KEY_WARNING_SHOWN);
  } catch {
    // Ignore storage removal issues.
  }
  dispatchUsageTrackingChanged();
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
    // Ignore storage write failures.
  }
};
