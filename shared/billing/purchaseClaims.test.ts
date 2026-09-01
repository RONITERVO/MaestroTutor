// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { makePurchaseClaimId } from './purchaseClaims';

describe('purchase claim ids', () => {
  it('are deterministic without exposing the external purchase id', () => {
    const token = 'sensitive-store-token-123';
    const first = makePurchaseClaimId('stripe', token);
    const second = makePurchaseClaimId('stripe', token);

    expect(first).toBe(second);
    expect(first).toMatch(/^stripe_[a-f0-9]{64}$/);
    expect(first).not.toContain(token);
  });

  it('keeps the provider namespace for legacy migration safety', () => {
    expect(makePurchaseClaimId('google-play', 'legacy-token'))
      .toMatch(/^google-play_[a-f0-9]{64}$/);
  });
});
