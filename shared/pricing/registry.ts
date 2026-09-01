// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

export type BillingModality = 'text' | 'audio' | 'image' | 'video' | 'document';

export type ModalityRates = Partial<Record<BillingModality, number>>;

export interface ModelPricingRule {
  id: string;
  displayName: string;
  matches: string[];
  inputPerMillion?: ModalityRates;
  outputPerMillion?: ModalityRates;
  cachedInputPerMillion?: ModalityRates;
  longContext?: {
    abovePromptTokens: number;
    inputPerMillion: ModalityRates;
    outputPerMillion: ModalityRates;
    cachedInputPerMillion?: ModalityRates;
  };
  generatedImageUsdFallback?: number;
  generatedAudioPerMinuteUsd?: number;
  unpricedReason?: string;
}

export interface GeminiPricingRegistry {
  schemaVersion: 1;
  currency: 'USD';
  estimateBasis: 'paid-standard-list';
  effectiveAt: string;
  sourceUrl: string;
  googleSearch: {
    pricePerThousandQueriesUsd: number;
    monthlyFreePrompts: number;
  };
  models: ModelPricingRule[];
}

const allModalities = (rate: number): ModalityRates => ({
  text: rate,
  audio: rate,
  image: rate,
  video: rate,
  document: rate,
});

export const DEFAULT_GEMINI_PRICING: GeminiPricingRegistry = {
  schemaVersion: 1,
  currency: 'USD',
  estimateBasis: 'paid-standard-list',
  effectiveAt: '2026-09-01',
  sourceUrl: 'https://ai.google.dev/gemini-api/docs/pricing',
  googleSearch: {
    pricePerThousandQueriesUsd: 14,
    monthlyFreePrompts: 5000,
  },
  models: [
    {
      id: 'gemini-3.6-flash',
      displayName: 'Gemini 3.6 Flash',
      matches: ['gemini-flash-latest', 'gemini-3.6-flash'],
      // Promotional Standard rates published through 2026-12-31. Re-verify
      // before 2027-01-01, when Google currently says these rates double.
      inputPerMillion: allModalities(0.75),
      outputPerMillion: allModalities(3.75),
      cachedInputPerMillion: allModalities(0.075),
    },
    {
      id: 'gemini-3.5-flash-lite',
      displayName: 'Gemini 3.5 Flash-Lite',
      matches: ['gemini-flash-lite-latest', 'gemini-3.5-flash-lite'],
      inputPerMillion: allModalities(0.3),
      outputPerMillion: allModalities(2.5),
      cachedInputPerMillion: allModalities(0.03),
    },
    {
      id: 'gemini-3.1-pro-preview',
      displayName: 'Gemini 3.1 Pro Preview',
      matches: ['gemini-pro-latest', 'gemini-3.1-pro-preview'],
      inputPerMillion: allModalities(2),
      outputPerMillion: allModalities(12),
      cachedInputPerMillion: allModalities(0.2),
      longContext: {
        abovePromptTokens: 200000,
        inputPerMillion: allModalities(4),
        outputPerMillion: allModalities(18),
        cachedInputPerMillion: allModalities(0.4),
      },
    },
    {
      id: 'gemini-3.1-flash-live-preview',
      displayName: 'Gemini 3.1 Flash Live Preview',
      matches: ['gemini-3.1-flash-live-preview'],
      inputPerMillion: {
        text: 0.75,
        audio: 3,
        image: 1,
        video: 1,
        document: 1,
      },
      outputPerMillion: {
        text: 4.5,
        audio: 12,
        image: 4.5,
        video: 4.5,
        document: 4.5,
      },
    },
    {
      id: 'gemini-2.5-flash-image',
      displayName: 'Gemini 2.5 Flash Image',
      matches: ['gemini-2.5-flash-image'],
      inputPerMillion: { text: 0.3, image: 0.3, document: 0.3 },
      outputPerMillion: { text: 2.5, image: 30 },
      generatedImageUsdFallback: 0.039,
    },
    {
      id: 'gemini-2.5-flash-native-audio-preview-12-2025',
      displayName: 'Gemini 2.5 Flash Native Audio Preview',
      matches: ['gemini-2.5-flash-native-audio-preview-12-2025'],
      inputPerMillion: { text: 0.5, audio: 3, video: 3 },
      outputPerMillion: { text: 2, audio: 12 },
    },
    {
      id: 'gemini-2.5-flash',
      displayName: 'Gemini 2.5 Flash',
      matches: ['gemini-2.5-flash'],
      inputPerMillion: { text: 0.3, image: 0.3, video: 0.3, document: 0.3, audio: 1 },
      outputPerMillion: allModalities(2.5),
      cachedInputPerMillion: { text: 0.03, image: 0.03, video: 0.03, document: 0.03, audio: 0.1 },
    },
    {
      id: 'gemini-2.0-flash',
      displayName: 'Gemini 2.0 Flash (retired)',
      matches: ['gemini-2.0-flash'],
      inputPerMillion: { text: 0.1, image: 0.1, video: 0.1, document: 0.1, audio: 0.7 },
      outputPerMillion: allModalities(0.4),
      cachedInputPerMillion: { text: 0.025, image: 0.025, video: 0.025, document: 0.025, audio: 0.175 },
    },
    {
      id: 'lyria-realtime-exp',
      displayName: 'Lyria RealTime Experimental',
      matches: ['lyria-realtime-exp'],
      unpricedReason: 'Google does not currently publish a Lyria RealTime list price.',
    },
  ],
};

const isFiniteNonNegativeNumber = (value: unknown): value is number => (
  typeof value === 'number' && Number.isFinite(value) && value >= 0
);

const isModalityRates = (value: unknown): value is ModalityRates => {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
  const entries = Object.entries(value);
  if (entries.length === 0 || !isFiniteNonNegativeNumber((value as ModalityRates).text)) return false;
  return entries.every(([key, rate]) => (
    ['text', 'audio', 'image', 'video', 'document'].includes(key)
    && isFiniteNonNegativeNumber(rate)
  ));
};

const isModelPricingRule = (value: unknown): value is ModelPricingRule => {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
  const candidate = value as ModelPricingRule;
  if (!candidate.id || !candidate.displayName || !Array.isArray(candidate.matches) || candidate.matches.length === 0) return false;
  if (!candidate.matches.every(match => typeof match === 'string' && match.trim().length > 0)) return false;
  if (candidate.inputPerMillion !== undefined && !isModalityRates(candidate.inputPerMillion)) return false;
  if (candidate.outputPerMillion !== undefined && !isModalityRates(candidate.outputPerMillion)) return false;
  if (candidate.cachedInputPerMillion !== undefined && !isModalityRates(candidate.cachedInputPerMillion)) return false;
  if (candidate.generatedImageUsdFallback !== undefined && !isFiniteNonNegativeNumber(candidate.generatedImageUsdFallback)) return false;
  if (candidate.generatedAudioPerMinuteUsd !== undefined && !isFiniteNonNegativeNumber(candidate.generatedAudioPerMinuteUsd)) return false;
  if (candidate.longContext) {
    if (!isFiniteNonNegativeNumber(candidate.longContext.abovePromptTokens)) return false;
    if (!isModalityRates(candidate.longContext.inputPerMillion)) return false;
    if (!isModalityRates(candidate.longContext.outputPerMillion)) return false;
    if (candidate.longContext.cachedInputPerMillion !== undefined && !isModalityRates(candidate.longContext.cachedInputPerMillion)) return false;
  }
  return true;
};

export const isGeminiPricingRegistry = (value: unknown): value is GeminiPricingRegistry => {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
  const candidate = value as GeminiPricingRegistry;
  return (
    candidate.schemaVersion === 1
    && candidate.currency === 'USD'
    && candidate.estimateBasis === 'paid-standard-list'
    && typeof candidate.effectiveAt === 'string'
    && candidate.effectiveAt.trim().length > 0
    && typeof candidate.sourceUrl === 'string'
    && candidate.sourceUrl.startsWith('https://')
    && isFiniteNonNegativeNumber(candidate.googleSearch?.pricePerThousandQueriesUsd)
    && isFiniteNonNegativeNumber(candidate.googleSearch?.monthlyFreePrompts)
    && Array.isArray(candidate.models)
    && candidate.models.length > 0
    && candidate.models.every(isModelPricingRule)
  );
};

export const clonePricingRegistry = (pricing: GeminiPricingRegistry): GeminiPricingRegistry => (
  JSON.parse(JSON.stringify(pricing)) as GeminiPricingRegistry
);

export const resolvePricingRule = (
  model: string,
  pricing: GeminiPricingRegistry = DEFAULT_GEMINI_PRICING
): ModelPricingRule | undefined => {
  const normalized = (model || '').trim().toLowerCase().replace(/^models\//, '');
  const dateSuffix = '(?:\\d{4}-\\d{2}-\\d{2}|\\d{2}-\\d{2,4}|\\d{8})';
  const versionSuffixPattern = new RegExp(`^-(?:(?:preview|exp)(?:-${dateSuffix})?|${dateSuffix}|\\d{3})$`);
  let best: { rule: ModelPricingRule; matchLength: number } | undefined;

  for (const rule of pricing.models) {
    if (rule.matches.some(rawMatch => normalized === rawMatch.trim().toLowerCase().replace(/^models\//, ''))) {
      return rule;
    }
  }

  for (const rule of pricing.models) {
    for (const rawMatch of rule.matches) {
      const match = rawMatch.trim().toLowerCase().replace(/^models\//, '');
      if (!normalized.startsWith(match)) continue;
      const suffix = normalized.slice(match.length);
      if (!versionSuffixPattern.test(suffix)) continue;
      if (!best || match.length > best.matchLength) {
        best = { rule, matchLength: match.length };
      }
    }
  }
  return best?.rule;
};
