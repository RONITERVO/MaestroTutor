// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import {
  type CheckoutSessionLike,
  type CreditPackLike,
  resolveCheckoutGrant,
} from './stripeFulfilment';

/**
 * These cover the ways a web payment path gives money away: fulfilling an
 * unpaid session, trusting a quantity that travelled through the browser, or
 * granting twice because the key was not stable across retries.
 */

const CATALOGUE: Record<string, CreditPackLike> = {
  pack_small: { id: 'pack_small', credits: 1000 },
  pack_large: { id: 'pack_large', credits: 6000 },
};

const lookupPack = (packId: string): CreditPackLike | undefined => CATALOGUE[packId];

const session = (overrides: Partial<CheckoutSessionLike> = {}): CheckoutSessionLike => ({
  id: 'cs_test_123',
  payment_status: 'paid',
  payment_intent: 'pi_test_123',
  metadata: { firebaseUid: 'user-1', packId: 'pack_small', credits: '1000' },
  customer_details: { email: 'buyer@example.com' },
  ...overrides,
});

describe('a paid session', () => {
  it('grants the pack to the buyer', () => {
    const decision = resolveCheckoutGrant(session(), lookupPack);
    expect(decision).toMatchObject({
      action: 'grant',
      uid: 'user-1',
      packId: 'pack_small',
      credits: 1000,
      orderId: 'pi_test_123',
    });
  });

  it('reads the payment intent when Stripe expands it into an object', () => {
    const decision = resolveCheckoutGrant(
      session({ payment_intent: { id: 'pi_expanded' } }),
      lookupPack,
    );
    expect(decision).toMatchObject({ action: 'grant', orderId: 'pi_expanded' });
  });
});

describe('credits come from the catalogue, never the session', () => {
  it('ignores an inflated credits value in metadata', () => {
    // The metadata round-trips through the browser and back from Stripe. A
    // fulfilment that trusted it would hand out whatever the session claimed.
    const decision = resolveCheckoutGrant(
      session({ metadata: { firebaseUid: 'user-1', packId: 'pack_small', credits: '999999999' } }),
      lookupPack,
    );
    expect(decision).toMatchObject({ action: 'grant', credits: 1000 });
  });

  it('refuses a pack the server does not sell', () => {
    const decision = resolveCheckoutGrant(
      session({ metadata: { firebaseUid: 'user-1', packId: 'pack_invented' } }),
      lookupPack,
    );
    expect(decision).toEqual({ action: 'skip', reason: 'unknown-pack' });
  });

  it('refuses a pack that resolves to no credits', () => {
    const decision = resolveCheckoutGrant(
      session({ metadata: { firebaseUid: 'user-1', packId: 'pack_zero' } }),
      () => ({ id: 'pack_zero', credits: 0 }),
    );
    expect(decision).toEqual({ action: 'skip', reason: 'unknown-pack' });
  });
});

describe('only settled payments are fulfilled', () => {
  for (const status of ['unpaid', 'no_payment_required', null, undefined]) {
    it(`does not grant when payment_status is ${String(status)}`, () => {
      // Completion is not payment: delayed methods complete the session first
      // and settle later, and some never settle at all.
      const decision = resolveCheckoutGrant(
        session({ payment_status: status as string | null | undefined }),
        lookupPack,
      );
      expect(decision).toEqual({ action: 'skip', reason: 'not-paid' });
    });
  }
});

describe('sessions that cannot be attributed', () => {
  it('skips when there is no account to credit', () => {
    const decision = resolveCheckoutGrant(
      session({ metadata: { packId: 'pack_small' } }),
      lookupPack,
    );
    expect(decision).toEqual({ action: 'skip', reason: 'missing-metadata' });
  });

  it('skips when there is no pack to grant', () => {
    const decision = resolveCheckoutGrant(
      session({ metadata: { firebaseUid: 'user-1' } }),
      lookupPack,
    );
    expect(decision).toEqual({ action: 'skip', reason: 'missing-metadata' });
  });

  it('skips when metadata is absent entirely', () => {
    const decision = resolveCheckoutGrant(session({ metadata: null }), lookupPack);
    expect(decision).toEqual({ action: 'skip', reason: 'missing-metadata' });
  });

  it('treats blank metadata as absent', () => {
    const decision = resolveCheckoutGrant(
      session({ metadata: { firebaseUid: '   ', packId: 'pack_small' } }),
      lookupPack,
    );
    expect(decision).toEqual({ action: 'skip', reason: 'missing-metadata' });
  });
});

describe('idempotency', () => {
  it('keys on the session, so retried deliveries collapse to one grant', () => {
    // Stripe retries webhooks until they are acknowledged; retries are normal.
    const first = resolveCheckoutGrant(session(), lookupPack);
    const retry = resolveCheckoutGrant(session(), lookupPack);
    expect(first).toMatchObject({ action: 'grant', idempotencyKey: 'stripe:cs_test_123' });
    expect(retry).toMatchObject({ idempotencyKey: 'stripe:cs_test_123' });
  });

  it('gives different purchases different keys', () => {
    const a = resolveCheckoutGrant(session({ id: 'cs_a' }), lookupPack);
    const b = resolveCheckoutGrant(session({ id: 'cs_b' }), lookupPack);
    if (a.action !== 'grant' || b.action !== 'grant') throw new Error('both should grant');
    expect(a.idempotencyKey).not.toBe(b.idempotencyKey);
  });

  it('namespaces the key so it cannot collide with a Play purchase token', () => {
    const decision = resolveCheckoutGrant(session(), lookupPack);
    if (decision.action !== 'grant') throw new Error('should grant');
    expect(decision.idempotencyKey.startsWith('stripe:')).toBe(true);
  });
});
