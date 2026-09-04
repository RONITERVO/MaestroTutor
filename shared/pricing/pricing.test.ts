// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { DEFAULT_GEMINI_PRICING, resolvePricingRule } from './registry';
import { calculateGeminiUsageCost } from './usage';
import {
  calculateLiveGatewayWindowUsd,
  calculateLiveWindowUsd,
  creditsToUsd,
  estimateOperationUsd,
  uploadBytesToCredits,
  usdToCredits,
} from './credits';

/**
 * These are the money tests. Every case below is a defect the draft backend
 * actually had, so each one is a regression guard rather than a hypothetical.
 */

const CREDITS_PER_USD = 1000;
const pricing = DEFAULT_GEMINI_PRICING;

describe('generated images are billed as images', () => {
  it('charges the per-image amount even though the prompt has tokens', () => {
    // The draft only applied the image fallback when prompt AND candidate
    // tokens were both zero. A real image request reports prompt tokens for the
    // text prompt, so the fallback never fired and an image was billed as a
    // handful of input tokens — fractions of a cent instead of cents.
    const cost = calculateGeminiUsageCost({
      configuredModel: 'gemini-2.5-flash-image',
      usageMetadata: { promptTokenCount: 12, candidatesTokenCount: 0 },
      generatedImages: 1,
    }, pricing);

    const rule = resolvePricingRule('gemini-2.5-flash-image', pricing);
    // Asserted unconditionally: a skip here would hide the very regression the
    // test exists to catch.
    expect(rule?.generatedImageUsdFallback).toBe(0.039);
    expect(cost.modelCostUsd).toBeGreaterThanOrEqual(0.039);
  });

  it('scales with the number of images produced', () => {
    const one = calculateGeminiUsageCost({
      configuredModel: 'gemini-2.5-flash-image',
      usageMetadata: { promptTokenCount: 12 },
      generatedImages: 1,
    }, pricing);
    const three = calculateGeminiUsageCost({
      configuredModel: 'gemini-2.5-flash-image',
      usageMetadata: { promptTokenCount: 12 },
      generatedImages: 3,
    }, pricing);
    expect(three.modelCostUsd).toBeGreaterThan(one.modelCostUsd);
  });
});

describe('model rates come from the registry, not a fallback', () => {
  it('uses the current Gemini 3.7 Flash promotional Standard rate', () => {
    const flash = resolvePricingRule('gemini-flash-latest', pricing);
    expect(pricing.effectiveAt).toBe('2026-09-01');
    expect(flash?.inputPerMillion?.text).toBe(0.75);
    expect(flash?.outputPerMillion?.text).toBe(3.75);
    expect(flash?.cachedInputPerMillion?.text).toBe(0.075);
    expect(flash?.id).toBe('gemini-3.7-flash');
  });

  it('prices a lite model well below the pro rate', () => {
    // The draft mapped three model substrings and fell back to Pro rates for
    // anything else, so flash-lite traffic was billed at roughly ten times its
    // real cost. Both must resolve to their own rule here.
    const lite = calculateGeminiUsageCost({
      configuredModel: 'gemini-flash-lite-latest',
      usageMetadata: { promptTokenCount: 1_000_000, candidatesTokenCount: 0 },
    }, pricing);
    const pro = calculateGeminiUsageCost({
      configuredModel: 'gemini-pro-latest',
      usageMetadata: { promptTokenCount: 1_000_000, candidatesTokenCount: 0 },
    }, pricing);

    expect(lite.pricingStatus).toBe('priced');
    expect(pro.pricingStatus).toBe('priced');
    expect(lite.modelCostUsd).toBeLessThan(pro.modelCostUsd);
  });

  it('reports unpriced rather than guessing at an unknown model', () => {
    // Silently applying someone else's rate is how the draft overcharged. An
    // unknown model must be visibly unpriced so it can be caught.
    const cost = calculateGeminiUsageCost({
      configuredModel: 'some-model-that-does-not-exist',
      usageMetadata: { promptTokenCount: 1000, candidatesTokenCount: 1000 },
    }, pricing);
    expect(cost.pricingStatus).toBe('unpriced');
    expect(cost.modelCostUsd).toBe(0);
  });

  it('bills cached input below fresh input', () => {
    const fresh = calculateGeminiUsageCost({
      configuredModel: 'gemini-flash-latest',
      usageMetadata: { promptTokenCount: 100_000 },
    }, pricing);
    const cached = calculateGeminiUsageCost({
      configuredModel: 'gemini-flash-latest',
      usageMetadata: { promptTokenCount: 100_000, cachedContentTokenCount: 100_000 },
    }, pricing);
    expect(cached.modelCostUsd).toBeLessThan(fresh.modelCostUsd);
  });
});

describe('credit conversion', () => {
  it('never rounds a chargeable request down to nothing', () => {
    expect(usdToCredits(0.0000001, CREDITS_PER_USD)).toBe(1);
    expect(usdToCredits(0, CREDITS_PER_USD)).toBe(0);
    expect(usdToCredits(-5, CREDITS_PER_USD)).toBe(0);
  });

  it('round-trips to within one credit, always rounding in the service favour', () => {
    const usd = 1.2345;
    const credits = usdToCredits(usd, CREDITS_PER_USD);
    const back = creditsToUsd(credits, CREDITS_PER_USD);
    expect(back).toBeGreaterThanOrEqual(usd);
    expect(back - usd).toBeLessThanOrEqual(1 / CREDITS_PER_USD);
  });

  it('charges at least one credit for any upload', () => {
    expect(uploadBytesToCredits(1, 10)).toBe(1);
    expect(uploadBytesToCredits(0, 10)).toBe(0);
    expect(uploadBytesToCredits(10 * 1024 * 1024, 10)).toBe(100);
  });
});

describe('reservation estimates cover what settlement will charge', () => {
  it('can reserve a server-enforced output ceiling', () => {
    const reserved = estimateOperationUsd({
      model: 'gemini-flash-latest',
      promptTokens: 1_000,
      operation: 'streamContent',
      pricing,
      expectedOutputTokens: 8_192,
    });
    const rule = resolvePricingRule('gemini-flash-latest', pricing)!;
    const exactCeiling = (1_000 / 1_000_000) * rule.inputPerMillion!.text!
      + (8_192 / 1_000_000) * rule.outputPerMillion!.text!;
    expect(reserved).toBeGreaterThanOrEqual(exactCeiling);
  });

  const cases: Array<{ model: string; promptTokens: number }> = [
    { model: 'gemini-flash-latest', promptTokens: 500 },
    { model: 'gemini-flash-latest', promptTokens: 50_000 },
    { model: 'gemini-flash-lite-latest', promptTokens: 2_000 },
    { model: 'gemini-pro-latest', promptTokens: 20_000 },
  ];

  for (const { model, promptTokens } of cases) {
    it(`holds enough for ${model} at ${promptTokens} prompt tokens`, () => {
      const reserved = estimateOperationUsd({
        model, promptTokens, operation: 'generateContent', pricing,
      });
      // Settlement against a typical response: output roughly equal to prompt.
      const settled = calculateGeminiUsageCost({
        configuredModel: model,
        usageMetadata: { promptTokenCount: promptTokens, candidatesTokenCount: promptTokens },
      }, pricing).modelCostUsd;

      // Under-reserving is the failure that matters: the settlement would have
      // nothing to draw on. Over-reserving is returned to the user.
      expect(reserved).toBeGreaterThanOrEqual(settled);
    });
  }

  it('holds the per-image amount for an image request', () => {
    const reserved = estimateOperationUsd({
      model: 'gemini-2.5-flash-image', promptTokens: 20, operation: 'generateImage', pricing,
    });
    expect(reserved).toBeGreaterThanOrEqual(0.039);
  });
});

describe('live windows', () => {
  it('costs more the longer the window', () => {
    expect(calculateLiveWindowUsd(60)).toBeGreaterThan(calculateLiveWindowUsd(30));
  });

  it('is capped by the model token limits rather than growing without bound', () => {
    // Both durations exceed the input and output token ceilings, so they must
    // price identically however much longer one of them is.
    const capped = calculateLiveWindowUsd(5_000);
    const wayOverCapped = calculateLiveWindowUsd(86_400);
    expect(wayOverCapped).toBe(capped);
  });

  it('reserves for six re-billed turns, low-resolution camera input and text overhead', () => {
    const gateway = calculateLiveGatewayWindowUsd(120);
    const oldSinglePassEstimate = calculateLiveWindowUsd(120);

    expect(gateway).toBe(0.454925);
    expect(gateway).toBeGreaterThan(oldSinglePassEstimate * 4);
  });
});
