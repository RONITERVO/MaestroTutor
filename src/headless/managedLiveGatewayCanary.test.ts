// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import type { ManagedJourneyBillingSnapshot } from './managedJourneyBilling';
import { evaluateManagedLiveNoOutput } from './managedLiveGatewayCanary';

const snapshot = (input: {
  available?: number;
  spent?: number;
  reserved?: number;
  usage?: ManagedJourneyBillingSnapshot['usageEntries'];
  billing?: ManagedJourneyBillingSnapshot['billingEntries'];
} = {}): ManagedJourneyBillingSnapshot => ({
  account: {
    account: {
      user: { id: 'user-1', email: null, displayName: null, photoUrl: null },
      entitlements: [],
      billingSummary: {
        availableCredits: input.available ?? 1_000,
        reservedCredits: input.reserved ?? 0,
        lifetimePurchasedCredits: 1_000,
        lifetimeSpentCredits: input.spent ?? 0,
        lifetimeSpentUsd: 0,
        updatedAt: 1,
        lastPurchaseAt: 1,
        lastChargeAt: null,
        lastProductId: 'pack_1000',
      },
    },
  },
  usageEntries: input.usage || [],
  billingEntries: input.billing || [],
});

describe('managed Live no-output evidence', () => {
  it('requires unchanged spend and one request-correlated release', () => {
    const result = evaluateManagedLiveNoOutput(snapshot(), snapshot({ billing: [{
      id: 'release-1',
      kind: 'reservation-release',
      credits: 87,
      usd: 0.087,
      productId: null,
      createdAt: 2,
      metadata: { operation: 'liveGateway', liveOpenRequestId: 'live-request-1' },
    }] }), 'live-request-1');

    expect(result).toMatchObject({
      passed: true,
      usageRows: 0,
      chargeRows: 0,
      releaseRows: 1,
      releasedCredits: 87,
    });
  });

  it('fails when setup-only traffic is charged even if reservations reconcile', () => {
    const requestId = 'live-request-2';
    const result = evaluateManagedLiveNoOutput(snapshot(), snapshot({
      available: 913,
      spent: 87,
      usage: [{
        id: 'usage-1', operation: 'liveGateway', model: 'live', billedCredits: 87,
        billedUsd: 0.087, createdAt: 2, metadata: { liveOpenRequestId: requestId },
      }],
      billing: [{
        id: 'charge-1', kind: 'charge', credits: 87, usd: 0.087, productId: null,
        createdAt: 2, metadata: { operation: 'liveGateway', liveOpenRequestId: requestId },
      }],
    }), requestId);

    expect(result.passed).toBe(false);
    expect(result.mismatches).toEqual(expect.arrayContaining([
      'A no-output session changed available credits.',
      'A no-output session changed lifetime spend.',
      'A no-output session created a usage row.',
      'A no-output session created a charge row.',
    ]));
  });
});
