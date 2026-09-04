// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { applyLiveCostControls, getLiveCostControlConfig } from './liveCostControls';

describe('Live context cost controls', () => {
  it('uses low-resolution camera tokens and a bounded sliding context', () => {
    expect(getLiveCostControlConfig()).toEqual({
      contextWindowCompression: {
        triggerTokens: '25000',
        slidingWindow: { targetTokens: '8000' },
      },
      mediaResolution: 'MEDIA_RESOLUTION_LOW',
    });
  });

  it('overrides caller attempts to enlarge the managed context', () => {
    expect(applyLiveCostControls({
      temperature: 0.5,
      contextWindowCompression: { triggerTokens: '100000' },
      mediaResolution: 'MEDIA_RESOLUTION_HIGH',
    })).toMatchObject({
      temperature: 0.5,
      contextWindowCompression: {
        triggerTokens: '25000',
        slidingWindow: { targetTokens: '8000' },
      },
      mediaResolution: 'MEDIA_RESOLUTION_LOW',
    });
  });
});
