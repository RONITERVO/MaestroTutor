// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import type { ManagedJourneyBillingSnapshot } from './managedJourneyBilling';
import {
  evaluateManagedLiveBilling,
  evaluateManagedJourneyBilling,
  evaluateManagedJourneyFailureBilling,
} from './managedJourneyBilling';

const snapshot = (
  availableCredits: number,
  lifetimeSpentCredits: number,
  lifetimeSpentUsd: number,
  usageEntries: ManagedJourneyBillingSnapshot['usageEntries'] = [],
  billingEntries: ManagedJourneyBillingSnapshot['billingEntries'] = [],
  reservedCredits = 0,
): ManagedJourneyBillingSnapshot => ({
  account: {
    account: {
      user: { id: 'user-1', email: 'test@example.com', displayName: null, photoUrl: null },
      entitlements: [],
      billingSummary: {
        availableCredits,
        reservedCredits,
        lifetimePurchasedCredits: 1_000,
        lifetimeSpentCredits,
        lifetimeSpentUsd,
        updatedAt: 1,
        lastPurchaseAt: 1,
        lastChargeAt: 1,
        lastProductId: 'pack_1000',
      },
    },
  },
  usageEntries,
  billingEntries,
});

describe('managed journey billing evidence', () => {
  it('requires account, usage ledger and charge ledger to reconcile exactly', () => {
    const before = snapshot(1_000, 0, 0);
    const after = snapshot(875, 125, 0.125, [{
      id: 'usage-1', operation: 'generateContent', model: 'model-1', billedCredits: 125, billedUsd: 0.125, createdAt: 2,
    }], [{
      id: 'charge-1', kind: 'charge', credits: 125, usd: 0.125, productId: null, createdAt: 2,
    }]);
    expect(evaluateManagedJourneyBilling(before, after)).toMatchObject({
      passed: true,
      creditsSpent: 125,
      usageCredits: 125,
      chargeCredits: 125,
      reservedCreditsAfter: 0,
    });
  });

  it('rejects missing charges and stranded reservations', () => {
    const evidence = evaluateManagedJourneyBilling(
      snapshot(1_000, 0, 0),
      snapshot(900, 100, 0.1, [], [], 50),
    );
    expect(evidence.passed).toBe(false);
    expect(evidence.mismatches).toEqual(expect.arrayContaining([
      'The managed journey left reserved credits behind.',
      'The managed journey produced no complete paid-usage evidence.',
      'Usage-ledger credits do not equal the account spend delta.',
      'Charge-ledger credits do not equal the account spend delta.',
    ]));
  });

  it('does not call a paid failed Live attempt safe merely because its ledgers reconcile', () => {
    const before = snapshot(1_000, 0, 0);
    const after = snapshot(913, 87, 0.087, [{
      id: 'usage-live-failed',
      operation: 'liveGateway',
      model: 'gemini-live',
      billedCredits: 87,
      billedUsd: 0.087,
      createdAt: 2,
      metadata: { liveOpenRequestId: 'synthetic-live-failed-1' },
    }], [{
      id: 'charge-live-failed',
      kind: 'charge',
      credits: 87,
      usd: 0.087,
      productId: null,
      createdAt: 2,
    }]);

    expect(evaluateManagedJourneyFailureBilling(
      before,
      after,
      ['synthetic-live-failed-1'],
    )).toMatchObject({
      passed: false,
      failedLiveChargeEntries: 1,
      failedLiveCredits: 87,
      mismatches: ['1 failed Live attempt(s) consumed 87 managed credits.'],
    });
  });

  it('also detects a failed charge left by a legacy token deployment', () => {
    const before = snapshot(1_000, 0, 0);
    const after = snapshot(913, 87, 0.087, [{
      id: 'usage-live-failed-legacy',
      operation: 'liveToken',
      model: 'gemini-live',
      billedCredits: 87,
      billedUsd: 0.087,
      createdAt: 2,
      metadata: { liveOpenRequestId: 'synthetic-live-failed-legacy' },
    }], [{
      id: 'charge-live-failed-legacy',
      kind: 'charge',
      credits: 87,
      usd: 0.087,
      productId: null,
      createdAt: 2,
    }]);

    expect(evaluateManagedJourneyFailureBilling(
      before,
      after,
      ['synthetic-live-failed-legacy'],
    )).toMatchObject({
      passed: false,
      failedLiveChargeEntries: 1,
      failedLiveCredits: 87,
    });
  });

  it('keeps unrelated successful Live charges out of failure evidence', () => {
    const before = snapshot(1_000, 0, 0);
    const after = snapshot(913, 87, 0.087, [{
      id: 'usage-live-success',
      operation: 'liveGateway',
      model: 'gemini-live',
      billedCredits: 87,
      billedUsd: 0.087,
      createdAt: 2,
      metadata: { liveOpenRequestId: 'synthetic-live-success-1' },
    }], [{
      id: 'charge-live-success', kind: 'charge', credits: 87, usd: 0.087, productId: null, createdAt: 2,
    }]);

    expect(evaluateManagedJourneyFailureBilling(
      before,
      after,
      ['synthetic-live-failed-1'],
    )).toMatchObject({
      passed: true,
      failedLiveChargeEntries: 0,
      failedLiveCredits: 0,
    });
  });

  it('correlates every persistent Live turn and transport byte with one exact charge', () => {
    const before = snapshot(1_000, 0, 0);
    const metadata = {
      liveOpenRequestId: 'synthetic-live-six-turns',
      operation: 'liveGateway',
      usageSource: 'provider',
      creditsPerUsd: 1_000,
      providerTurnCompleteCount: 6,
      providerUsageTurnCount: 6,
      clientTurnBoundaryCount: 6,
      inputAudioBytes: 192_000,
      inputVideoFrameCount: 6,
      outputAudioBytes: 288_000,
      promptTokenCount: 192,
      responseTokenCount: 192,
      totalTokenCount: 384,
      promptTokensDetails: [{ modality: 'AUDIO', tokenCount: 192 }],
      responseTokensDetails: [{ modality: 'AUDIO', tokenCount: 192 }],
    };
    const after = snapshot(997, 3, 0.00288, [{
      id: 'usage-live',
      operation: 'liveGateway',
      model: 'gemini-2.5-flash-native-audio-preview-12-2025',
      billedCredits: 3,
      chargedCredits: 3,
      shortfallCredits: 0,
      billedUsd: 0.00288,
      createdAt: 2,
      metadata,
    }], [{
      id: 'charge-live',
      kind: 'charge',
      credits: 3,
      billedCredits: 3,
      shortfallCredits: 0,
      usd: 0.00288,
      productId: null,
      createdAt: 2,
      metadata,
    }]);

    expect(evaluateManagedLiveBilling(before, after, 'synthetic-live-six-turns', {
      connectedTurns: 6,
      sentAudioBytes: 192_000,
      sentVideoFrames: 6,
      receivedAudioBytes: 288_000,
    })).toMatchObject({
      passed: true,
      usageRows: 1,
      chargeRows: 1,
      releaseRows: 0,
      billedCredits: 3,
      chargedCredits: 3,
      shortfallCredits: 0,
      providerTurnCompleteCount: 6,
    });
  });

  it('rejects an underfunded or incomplete persistent Live settlement', () => {
    const before = snapshot(100, 0, 0);
    const metadata = {
      liveOpenRequestId: 'synthetic-live-six-turns',
      operation: 'liveGateway',
      usageSource: 'transport',
      creditsPerUsd: 1_000,
      providerTurnCompleteCount: 5,
      providerUsageTurnCount: 5,
      clientTurnBoundaryCount: 6,
      inputAudioBytes: 190_000,
      inputVideoFrameCount: 5,
      outputAudioBytes: 288_000,
      promptTokenCount: 190,
      responseTokenCount: 192,
      totalTokenCount: 382,
      promptTokensDetails: [{ modality: 'AUDIO', tokenCount: 190 }],
      responseTokensDetails: [{ modality: 'AUDIO', tokenCount: 192 }],
    };
    const after = snapshot(98, 2, 0.002874, [{
      id: 'usage-live',
      operation: 'liveGateway',
      model: 'gemini-2.5-flash-native-audio-preview-12-2025',
      billedCredits: 3,
      chargedCredits: 2,
      shortfallCredits: 1,
      billedUsd: 0.002874,
      createdAt: 2,
      metadata,
    }], [{
      id: 'charge-live',
      kind: 'charge',
      credits: 2,
      billedCredits: 3,
      shortfallCredits: 1,
      usd: 0.002874,
      productId: null,
      createdAt: 2,
      metadata,
    }]);

    const evidence = evaluateManagedLiveBilling(before, after, 'synthetic-live-six-turns', {
      connectedTurns: 6,
      sentAudioBytes: 192_000,
      sentVideoFrames: 6,
      receivedAudioBytes: 288_000,
    });
    expect(evidence.passed).toBe(false);
    expect(evidence.mismatches).toEqual(expect.arrayContaining([
      'The Live reservation did not fully cover the provider-derived charge.',
      'The gateway accounted for 5 completed provider turn(s), expected 6.',
      'The gateway priced 5 provider turn snapshot(s), expected 6.',
      'The gateway accounted for 190000 input byte(s), expected 192000.',
      'The gateway accounted for 5 video frame(s), expected 6.',
    ]));
  });
});
