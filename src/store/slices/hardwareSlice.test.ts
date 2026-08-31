// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { DEVICE_BUDGETS, detectDevicePerformanceTier } from './hardwareSlice';

describe('detectDevicePerformanceTier', () => {
  it('classifies a low-memory device as low', () => {
    expect(detectDevicePerformanceTier({ deviceMemory: 2, hardwareConcurrency: 8 })).toBe('low');
  });

  it('classifies a few-core device as low even with plenty of RAM', () => {
    expect(detectDevicePerformanceTier({ deviceMemory: 8, hardwareConcurrency: 4 })).toBe('low');
  });

  it('classifies a mid-range device as mid', () => {
    expect(detectDevicePerformanceTier({ deviceMemory: 4, hardwareConcurrency: 8 })).toBe('mid');
  });

  it('classifies a strong device as high', () => {
    expect(detectDevicePerformanceTier({ deviceMemory: 8, hardwareConcurrency: 8 })).toBe('high');
  });

  it('lands on mid when the browser does not report memory', () => {
    // deviceMemory is missing on many Android WebViews; guessing high there is
    // what gets an app terminated, so the unknown case must not be optimistic.
    expect(detectDevicePerformanceTier({ hardwareConcurrency: 8 })).toBe('mid');
  });

  it('still trusts a weak core count when memory is unreported', () => {
    expect(detectDevicePerformanceTier({ hardwareConcurrency: 2 })).toBe('low');
  });
});

describe('DEVICE_BUDGETS', () => {
  it('never lets a tier go below one live embed', () => {
    for (const budgets of Object.values(DEVICE_BUDGETS)) {
      expect(budgets.maxLiveEmbeds).toBeGreaterThanOrEqual(1);
      expect(budgets.pdfWindowPages).toBeGreaterThanOrEqual(1);
    }
  });

  it('is monotonic: a stronger tier is never stingier', () => {
    const { low, mid, high } = DEVICE_BUDGETS;
    for (const key of ['maxLiveEmbeds', 'posterBudget', 'pdfWindowPages', 'pdfScaleCap', 'maxVisibleMessagesCap'] as const) {
      expect(mid[key]).toBeGreaterThanOrEqual(low[key]);
      expect(high[key]).toBeGreaterThanOrEqual(mid[key]);
    }
  });

  it('disables posters entirely on the low tier', () => {
    expect(DEVICE_BUDGETS.low.posterBudget).toBe(0);
  });

  it('never lets any tier run more than one live embed', () => {
    // The product requirement is one running artifact at a time; the arbiter
    // supports more, so the cap has to be asserted rather than assumed.
    for (const budgets of Object.values(DEVICE_BUDGETS)) {
      expect(budgets.maxLiveEmbeds).toBe(1);
    }
  });
});
