// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it, vi } from 'vitest';
import type { ManagedAccountSummaryResponse } from '../core/contracts/backend';
import { createCoreEventJournal } from './events';
import { buildAiContentReportRequest, createManagedAccountController } from './managedAccount';
import type { CoreClock } from './runtime';

const summary = (credits = 10): ManagedAccountSummaryResponse => ({
  account: {
    user: { id: 'test-user', email: 'test@example.com', displayName: 'Test', photoUrl: null },
    entitlements: [],
    billingSummary: {
      availableCredits: credits,
      reservedCredits: 0,
      lifetimePurchasedCredits: credits,
      lifetimeSpentCredits: 0,
      lifetimeSpentUsd: 0,
      updatedAt: 1,
      lastPurchaseAt: 1,
      lastChargeAt: null,
      lastProductId: 'pack',
    },
  },
});

const createManualClock = () => {
  const callbacks = new Map<number, () => void>();
  let nextHandle = 0;
  const clock: CoreClock & { tick: () => void } = {
    now: () => 100,
    sleep: async () => undefined,
    setInterval: callback => {
      const handle = ++nextHandle;
      callbacks.set(handle, callback);
      return handle;
    },
    clearInterval: handle => {
      callbacks.delete(handle as number);
    },
    tick: () => {
      for (const callback of [...callbacks.values()]) callback();
    },
  };
  return clock;
};

const createFixture = () => {
  const journal = createCoreEventJournal({ now: () => 100 });
  const clock = createManualClock();
  const backend = {
    getAccountSummary: vi.fn().mockResolvedValue(summary()),
    listUsageLedger: vi.fn().mockResolvedValue({ entries: [] }),
    listBillingLedger: vi.fn().mockResolvedValue({ entries: [] }),
    createStripeCheckoutSession: vi.fn().mockResolvedValue({
      url: 'https://checkout.stripe.test/session',
      sessionId: 'cs_test_1',
    }),
    submitAiContentReport: vi.fn().mockResolvedValue({ ok: true as const, reportId: 'report-1', createdAt: 1 }),
    deleteManagedAccount: vi.fn().mockResolvedValue({
      ok: true as const,
      deletedAt: 2,
      releasedReservationCount: 0,
      deletedReservationCount: 0,
      deletedManagedFileCount: 0,
      anonymizedPurchaseCount: 0,
      anonymizedReportCount: 0,
      remoteManagedFileFailures: 0,
      queuedRemoteCleanupCount: 0,
      deletedStripeCustomerCount: 0,
    }),
  };
  const identity = { beginSignIn: vi.fn().mockResolvedValue({}), signOut: vi.fn().mockResolvedValue(undefined) };
  const navigation = { navigate: vi.fn().mockResolvedValue(undefined) };
  const controller = createManagedAccountController({
    backend,
    identity,
    navigation,
    runtime: {
      clock,
      ids: { create: prefix => `${prefix}-1` },
      events: journal,
    },
  });
  return { controller, backend, identity, navigation, journal, clock };
};

describe('managed account controller', () => {
  it('uses the shared sign-in, account-refresh and event path', async () => {
    const fixture = createFixture();
    await expect(fixture.controller.signIn('sign-in-op')).resolves.toEqual(summary());
    expect(fixture.identity.beginSignIn).toHaveBeenCalledOnce();
    expect(fixture.backend.getAccountSummary).toHaveBeenCalledOnce();
    expect(fixture.journal.snapshot().map(event => event.phase)).toEqual([
      'signIn.started',
      'signIn.identityReady',
      'refresh.started',
      'refresh.succeeded',
      'signIn.succeeded',
    ]);
  });

  it('creates a checkout before handing its URL to the navigation adapter', async () => {
    const fixture = createFixture();
    await expect(fixture.controller.startStripeCheckout('pack')).resolves.toEqual({
      url: 'https://checkout.stripe.test/session',
      sessionId: 'cs_test_1',
    });
    expect(fixture.navigation.navigate).toHaveBeenCalledWith('https://checkout.stripe.test/session');
    expect(fixture.journal.snapshot().map(event => event.phase)).toEqual([
      'checkout.started',
      'checkout.sessionCreated',
      'checkout.navigationRequested',
    ]);
  });

  it('runs the five-attempt post-checkout account refresh through the controller', async () => {
    const fixture = createFixture();
    const poll = fixture.controller.startStripeReturnPolling({ attempts: 5, intervalMs: 2000 });
    for (let attempt = 0; attempt < 5; attempt += 1) {
      fixture.clock.tick();
      await vi.waitFor(() => expect(fixture.backend.getAccountSummary).toHaveBeenCalledTimes(attempt + 1));
    }
    await expect(poll.completion).resolves.toEqual(summary());
    expect(fixture.journal.snapshot().filter(event => event.phase === 'reconcile.attempted')).toHaveLength(5);
  });

  it('can stop reconciliation as soon as a checkout invariant is observed', async () => {
    const fixture = createFixture();
    fixture.backend.getAccountSummary.mockResolvedValue(summary(1_010));
    const poll = fixture.controller.startStripeReturnPolling({
      attempts: 15,
      intervalMs: 2000,
      isComplete: result => result.account.billingSummary.availableCredits >= 1_010,
    });
    fixture.clock.tick();
    await expect(poll.completion).resolves.toEqual(summary(1_010));
    expect(fixture.backend.getAccountSummary).toHaveBeenCalledOnce();
    expect(fixture.journal.snapshot().filter(event => event.phase === 'reconcile.attempted')).toHaveLength(1);
  });

  it('refuses a mismatched disposable user before destructive work', async () => {
    const fixture = createFixture();
    await expect(fixture.controller.deleteAccount({
      confirmation: 'DELETE',
      expectedUserId: 'disposable-user',
      actualUserId: 'real-user',
    })).rejects.toThrow('disposable test user');
    expect(fixture.backend.deleteManagedAccount).not.toHaveBeenCalled();
  });

  it('builds one report payload from the same assistant message model', () => {
    expect(buildAiContentReportRequest({
      message: {
        id: 'message-1',
        role: 'assistant',
        timestamp: 12,
        translations: [{ target: 'Hola', native: 'Hello' }],
        llmRawResponse: 'raw',
      },
      accessMode: 'managed',
      reason: 'deceptive',
      notes: '  details  ',
    })).toEqual({
      accessMode: 'managed',
      messageId: 'message-1',
      reason: 'deceptive',
      assistantText: 'Hola / Hello',
      rawAssistantResponse: 'raw',
      notes: 'details',
      surface: 'chat',
      model: undefined,
      createdAtClient: 12,
    });
  });
});
