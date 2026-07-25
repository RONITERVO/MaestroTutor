// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { beforeEach, describe, expect, it } from 'vitest';
import { DEFAULT_GEMINI_PRICING } from '../../core/config/pricing';
import {
  calculateGeminiUsageCost,
  getCostSummary,
  trackGeminiUsage,
  trackMusicGeneration,
} from './costTracker';

const storageValues = new Map<string, string>();
const mockLocalStorage: Storage = {
  get length() { return storageValues.size; },
  clear: () => storageValues.clear(),
  getItem: key => storageValues.get(key) ?? null,
  key: index => Array.from(storageValues.keys())[index] ?? null,
  removeItem: key => { storageValues.delete(key); },
  setItem: (key, value) => { storageValues.set(key, value); },
};
Object.defineProperty(globalThis, 'localStorage', {
  value: mockLocalStorage,
  configurable: true,
});

beforeEach(() => {
  localStorage.clear();
});

const calculate = (overrides: Partial<Parameters<typeof calculateGeminiUsageCost>[0]> = {}) => (
  calculateGeminiUsageCost({
    feature: 'tutor',
    configuredModel: 'gemini-flash-latest',
    usageMetadata: {},
    ...overrides,
  }, DEFAULT_GEMINI_PRICING)
);

describe('calculateGeminiUsageCost', () => {
  it('prices latest aliases using their current canonical model rates', () => {
    const flash = calculate({
      usageMetadata: { promptTokenCount: 1_000_000, candidatesTokenCount: 1_000_000 },
    });
    expect(flash.model).toBe('gemini-3.6-flash');
    expect(flash.modelCostUsd).toBeCloseTo(9);

    const flashLite = calculate({
      configuredModel: 'gemini-flash-lite-latest',
      usageMetadata: { promptTokenCount: 1_000_000, candidatesTokenCount: 1_000_000 },
    });
    expect(flashLite.model).toBe('gemini-3.5-flash-lite');
    expect(flashLite.modelCostUsd).toBeCloseTo(2.8);
  });

  it('includes thinking and tool-use tokens at their billed rates', () => {
    const result = calculate({
      usageMetadata: {
        promptTokenCount: 1_000_000,
        candidatesTokenCount: 500_000,
        thoughtsTokenCount: 500_000,
        toolUsePromptTokenCount: 1_000_000,
      },
    });
    expect(result.inputTokens).toBe(2_000_000);
    expect(result.outputTokens).toBe(1_000_000);
    expect(result.modelCostUsd).toBeCloseTo(10.5);
  });

  it('uses reduced cache rates without charging cached tokens twice', () => {
    const result = calculate({
      usageMetadata: {
        promptTokenCount: 1_000_000,
        cachedContentTokenCount: 400_000,
        candidatesTokenCount: 0,
      },
    });
    expect(result.modelCostUsd).toBeCloseTo(0.96);
  });

  it('prices Live usage by input and output modality', () => {
    const result = calculate({
      feature: 'liveConversation',
      configuredModel: 'gemini-3.1-flash-live-preview',
      usageMetadata: {
        promptTokenCount: 2_000_000,
        responseTokenCount: 1_000_000,
        thoughtsTokenCount: 1_000_000,
        promptTokensDetails: [
          { modality: 'AUDIO', tokenCount: 1_000_000 },
          { modality: 'VIDEO', tokenCount: 1_000_000 },
        ],
        responseTokensDetails: [{ modality: 'AUDIO', tokenCount: 1_000_000 }],
      },
    });
    expect(result.modelCostUsd).toBeCloseTo(20.5);
    expect(result.inputByModality.audio).toBe(1_000_000);
    expect(result.outputByModality.audio).toBe(1_000_000);
  });

  it('uses Pro long-context rates above 200k prompt tokens', () => {
    const result = calculate({
      configuredModel: 'gemini-pro-latest',
      usageMetadata: { promptTokenCount: 200_001, candidatesTokenCount: 1_000_000 },
    });
    expect(result.modelCostUsd).toBeCloseTo((200_001 / 1_000_000) * 4 + 18);
  });

  it('uses the returned model version after a fallback', () => {
    const result = calculate({
      configuredModel: 'gemini-flash-latest',
      modelVersion: 'gemini-3.1-pro-preview',
      usageMetadata: { promptTokenCount: 100_000, candidatesTokenCount: 1_000_000 },
    });
    expect(result.model).toBe('gemini-3.1-pro-preview');
    expect(result.modelCostUsd).toBeCloseTo(12.2);
  });

  it('uses the documented per-image fallback if image modality details are unavailable', () => {
    const result = calculate({
      feature: 'image',
      configuredModel: 'gemini-2.5-flash-image',
      generatedImages: 1,
      usageMetadata: { promptTokenCount: 1_000_000, candidatesTokenCount: 1290 },
    });
    expect(result.modelCostUsd).toBeCloseTo(0.339);
    expect(result.generatedImages).toBe(1);
  });

  it('keeps Search cost separate because the monthly allowance is project-wide', () => {
    const result = calculate({ searchQueries: 10 });
    expect(result.modelCostUsd).toBe(0);
    expect(result.searchPrompts).toBe(1);
    expect(result.potentialSearchCostUsd).toBeCloseTo(0.14);
  });

  it('marks unknown models as unpriced instead of silently applying a fallback rate', () => {
    const result = calculate({
      configuredModel: 'future-model-without-pricing',
      usageMetadata: { promptTokenCount: 1000, candidatesTokenCount: 1000 },
    });
    expect(result.pricingStatus).toBe('unpriced');
    expect(result.modelCostUsd).toBe(0);
  });
});

describe('cost tracking storage', () => {
  it('preserves the old unverifiable estimate separately during migration', () => {
    localStorage.setItem('maestro_costTracking', JSON.stringify({
      inputTokens: 1000,
      outputTokens: 500,
      imageGenCount: 2,
      totalCostUsd: 1.23,
    }));
    const summary = getCostSummary();
    expect(summary.legacyEstimateUsd).toBe(1.23);
    expect(summary.totalCostUsd).toBe(0);
    expect(summary.entries).toEqual([]);
  });

  it('aggregates priced and explicitly unpriced activity without mixing them', () => {
    trackGeminiUsage({
      feature: 'tutor',
      configuredModel: 'gemini-flash-latest',
      usageMetadata: { promptTokenCount: 1_000_000, candidatesTokenCount: 1_000_000 },
    });
    trackMusicGeneration('lyria-realtime-exp', 90);

    const summary = getCostSummary();
    expect(summary.knownModelCostUsd).toBeCloseTo(9);
    expect(summary.totalCostUsd).toBeCloseTo(9);
    expect(summary.hasUnpricedUsage).toBe(true);
    expect(summary.entries).toHaveLength(2);
    expect(summary.entries.find(entry => entry.feature === 'music')).toMatchObject({
      pricingStatus: 'unpriced',
      generatedAudioSeconds: 90,
    });
  });
});
