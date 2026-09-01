// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it, vi } from 'vitest';
import { createCoreEventJournal } from '../core-sdk/events';
import { createCoreRuntime } from '../core-sdk/runtime';
import { makePurchaseClaimId } from '../../shared/billing/purchaseClaims';
import type { HeadlessClient } from './client';

vi.mock('./hostedBrowser', () => ({
  completeStripeTestCheckout: vi.fn(async () => ({ returnOrigin: 'https://staging.example' })),
}));

import { runStripeTestCheckoutJourney } from './billingJourney';

const accountResult = (credits: number) => ({
  account: { billingSummary: { availableCredits: credits } },
});

const createClient = (billingEntries: unknown[]): HeadlessClient => {
  const events = createCoreEventJournal();
  return {
    runtime: createCoreRuntime({ events }),
    events,
    profile: { directory: 'D:\\tmp\\headless-profile' },
    account: {
      refreshAccount: vi.fn(async () => accountResult(10)),
      startStripeCheckout: vi.fn(async () => ({
        sessionId: 'cs_test_exact_claim',
        url: 'https://checkout.stripe.com/c/pay/cs_test_exact_claim',
      })),
      startStripeReturnPolling: vi.fn(() => ({
        completion: Promise.resolve(accountResult(1_010)),
        cancel: vi.fn(),
      })),
      listLedgers: vi.fn(async () => ({ billing: { entries: billingEntries }, usage: { entries: [] } })),
    },
  } as unknown as HeadlessClient;
};

describe('Stripe headless checkout proof', () => {
  it('proves the new Checkout by its exact hashed claim instead of a bounded count delta', async () => {
    const purchaseClaimId = makePurchaseClaimId('stripe', 'cs_test_exact_claim');
    const olderEntries = Array.from({ length: 199 }, (_, index) => ({
      kind: index % 2 ? 'purchase' : 'usage',
      metadata: { purchaseClaimId: `older-${index}` },
    }));
    const client = createClient([
      { kind: 'purchase', metadata: { purchaseClaimId } },
      ...olderEntries,
    ]);

    await expect(runStripeTestCheckoutJourney(client, {
      packId: 'pack_1000',
      expectedCredits: 1_000,
      email: 'ci@example.test',
      headless: true,
    })).resolves.toMatchObject({
      grantedCredits: 1_000,
      purchaseClaimId,
      newPurchaseLedgerEntries: 1,
    });
  });

  it('rejects duplicate ledger evidence for the same purchase claim', async () => {
    const purchaseClaimId = makePurchaseClaimId('stripe', 'cs_test_exact_claim');
    const entry = { kind: 'purchase', metadata: { purchaseClaimId } };
    const client = createClient([entry, entry]);

    await expect(runStripeTestCheckoutJourney(client, {
      packId: 'pack_1000',
      expectedCredits: 1_000,
      email: 'ci@example.test',
    })).rejects.toThrow('observed 2');
  });
});
