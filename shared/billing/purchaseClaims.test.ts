// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { makePurchaseClaimId } from './purchaseClaims';

describe('purchase claim ids', () => {
  it('are deterministic without exposing the external purchase id', () => {
    const token = 'sensitive-store-token-123';
    const first = makePurchaseClaimId('google-play', token);
    const second = makePurchaseClaimId('google-play', token);

    expect(first).toBe(second);
    expect(first).toMatch(/^google-play_[a-f0-9]{64}$/);
    expect(first).not.toContain(token);
  });

  it('namespace identical external ids by provider', () => {
    const externalId = 'same-opaque-id';

    expect(makePurchaseClaimId('google-play', externalId))
      .not.toBe(makePurchaseClaimId('stripe', externalId));
  });
});
