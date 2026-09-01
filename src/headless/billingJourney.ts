// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { HeadlessClient } from './client';
import { completeStripeTestCheckout } from './hostedBrowser';
import { makePurchaseClaimId } from '../../shared/billing/purchaseClaims';

export interface StripeTestJourneyInput {
  packId: string;
  expectedCredits: number;
  email?: string;
  headless?: boolean;
  timeoutMs?: number;
  attempts?: number;
  intervalMs?: number;
  operationId?: string;
}

export const runStripeTestCheckoutJourney = async (
  client: HeadlessClient,
  input: StripeTestJourneyInput,
) => {
  const operationId = input.operationId || client.runtime.ids.create('stripe-test-checkout');
  const expectedCredits = Math.floor(input.expectedCredits);
  if (!Number.isSafeInteger(expectedCredits) || expectedCredits <= 0) {
    throw new Error('expectedCredits must be a positive safe integer.');
  }
  const email = input.email?.trim() || process.env.MAESTRO_FIREBASE_EMAIL?.trim() || '';
  if (!email) throw new Error('email or MAESTRO_FIREBASE_EMAIL is required for the hosted Checkout form.');

  const before = await client.account.refreshAccount(operationId);
  const creditsBefore = before.account.billingSummary.availableCredits;
  const checkout = await client.account.startStripeCheckout(input.packId, operationId);
  if (!checkout.sessionId.startsWith('cs_test_')) {
    throw new Error('Refusing to continue: Stripe returned a non-test Checkout session.');
  }
  client.runtime.events.emit({
    operationId,
    journey: 'billing',
    phase: 'checkout.hostedStarted',
    data: { sessionId: checkout.sessionId, provider: 'stripe-test' },
  });
  const hosted = await completeStripeTestCheckout({
    checkoutUrl: checkout.url,
    sessionId: checkout.sessionId,
    profileDirectory: client.profile.directory,
    email,
    headless: input.headless,
    timeoutMs: input.timeoutMs,
  });
  client.runtime.events.emit({
    operationId,
    journey: 'billing',
    phase: 'checkout.hostedCompleted',
    data: { sessionId: checkout.sessionId, returnOrigin: hosted.returnOrigin },
  });

  const targetCredits = creditsBefore + expectedCredits;
  const poll = client.account.startStripeReturnPolling({
    operationId,
    attempts: input.attempts ?? 15,
    intervalMs: input.intervalMs ?? 2_000,
    isComplete: result => result.account.billingSummary.availableCredits >= targetCredits,
  });
  const after = await poll.completion;
  if (!after) throw new Error('Stripe reconciliation completed without an account response.');
  const creditsAfter = after.account.billingSummary.availableCredits;
  if (creditsAfter - creditsBefore !== expectedCredits) {
    throw new Error(`Expected one ${expectedCredits}-credit grant, observed a ${creditsAfter - creditsBefore}-credit delta.`);
  }
  const ledgersAfter = await client.account.listLedgers(200, operationId);
  const expectedPurchaseClaimId = makePurchaseClaimId('stripe', checkout.sessionId);
  const matchingPurchaseEntries = ledgersAfter.billing.entries.filter(entry => (
    entry.kind === 'purchase'
    && entry.metadata?.purchaseClaimId === expectedPurchaseClaimId
  ));
  if (matchingPurchaseEntries.length !== 1) {
    throw new Error(`Expected one ledger entry for Checkout session ${checkout.sessionId}, observed ${matchingPurchaseEntries.length}.`);
  }

  client.runtime.events.emit({
    operationId,
    journey: 'billing',
    phase: 'checkout.invariantsPassed',
    data: { sessionId: checkout.sessionId, creditsBefore, creditsAfter, expectedCredits },
  });
  return {
    operationId,
    sessionId: checkout.sessionId,
    creditsBefore,
    creditsAfter,
    grantedCredits: creditsAfter - creditsBefore,
    purchaseClaimId: expectedPurchaseClaimId,
    newPurchaseLedgerEntries: matchingPurchaseEntries.length,
    returnOrigin: hosted.returnOrigin,
  };
};
