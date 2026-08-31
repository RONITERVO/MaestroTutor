// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import {
  type CheckoutSessionLike,
  type CheckoutGrantSnapshotLike,
  isCheckoutFulfilmentEventType,
  resolveCheckoutGrant,
} from './stripeFulfilment';

/**
 * These cover the ways a web payment path gives money away: fulfilling an
 * unpaid session, trusting a quantity that travelled through the browser, or
 * granting twice because the key was not stable across retries.
 */

const snapshot = (overrides: Partial<CheckoutGrantSnapshotLike> = {}): CheckoutGrantSnapshotLike => ({
  uid: 'user-1',
  packId: 'pack_small',
  credits: 1000,
  ...overrides,
});

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
    const decision = resolveCheckoutGrant(session(), snapshot());
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
      snapshot(),
    );
    expect(decision).toMatchObject({ action: 'grant', orderId: 'pi_expanded' });
  });
});

describe('credits come from the immutable Checkout snapshot', () => {
  it('ignores an inflated credits value in metadata', () => {
    // The metadata round-trips through the browser and back from Stripe. A
    // fulfilment that trusted it would hand out whatever the session claimed.
    const decision = resolveCheckoutGrant(
      session({ metadata: { firebaseUid: 'user-1', packId: 'pack_small', credits: '999999999' } }),
      snapshot(),
    );
    expect(decision).toMatchObject({ action: 'grant', credits: 1000 });
  });

  it('preserves the sold quantity if the current catalogue later changes', () => {
    const decision = resolveCheckoutGrant(
      session({ metadata: { firebaseUid: 'user-1', packId: 'replacement_pack', credits: '6000' } }),
      snapshot({ packId: 'pack_small', credits: 1000 }),
    );
    expect(decision).toMatchObject({
      action: 'grant',
      packId: 'pack_small',
      credits: 1000,
    });
  });

  it('refuses a missing snapshot', () => {
    expect(resolveCheckoutGrant(session(), null)).toEqual({
      action: 'skip',
      reason: 'missing-snapshot',
    });
  });

  it.each([0, 1.5, Number.MAX_SAFE_INTEGER + 1])('refuses invalid snapshot credits %s', (credits) => {
    expect(resolveCheckoutGrant(session(), snapshot({ credits }))).toEqual({
      action: 'skip',
      reason: 'invalid-snapshot',
    });
  });
});

describe('only settled payments are fulfilled', () => {
  for (const status of ['unpaid', 'no_payment_required', null, undefined]) {
    it(`does not grant when payment_status is ${String(status)}`, () => {
      // Completion is not payment: delayed methods complete the session first
      // and settle later, and some never settle at all.
      const decision = resolveCheckoutGrant(
        session({ payment_status: status as string | null | undefined }),
        snapshot(),
      );
      expect(decision).toEqual({ action: 'skip', reason: 'not-paid' });
    });
  }

  it('grants credits when a delayed payment later succeeds', () => {
    expect(isCheckoutFulfilmentEventType('checkout.session.completed')).toBe(true);
    expect(resolveCheckoutGrant(session({ payment_status: 'unpaid' }), snapshot())).toEqual({
      action: 'skip',
      reason: 'not-paid',
    });

    expect(isCheckoutFulfilmentEventType('checkout.session.async_payment_succeeded')).toBe(true);
    expect(resolveCheckoutGrant(session({ payment_status: 'paid' }), snapshot())).toMatchObject({
      action: 'grant',
      uid: 'user-1',
      credits: 1000,
      idempotencyKey: 'stripe:cs_test_123',
    });
  });

  it('does not route non-Checkout payment events through fulfilment', () => {
    expect(isCheckoutFulfilmentEventType('payment_intent.succeeded')).toBe(false);
  });
});

describe('snapshots that cannot be attributed', () => {
  it('skips when there is no account to credit', () => {
    const decision = resolveCheckoutGrant(session(), snapshot({ uid: '   ' }));
    expect(decision).toEqual({ action: 'skip', reason: 'invalid-snapshot' });
  });

  it('skips when there is no pack to grant', () => {
    const decision = resolveCheckoutGrant(session(), snapshot({ packId: ' ' }));
    expect(decision).toEqual({ action: 'skip', reason: 'invalid-snapshot' });
  });
});

describe('idempotency', () => {
  it('keys on the session, so retried deliveries collapse to one grant', () => {
    // Stripe retries webhooks until they are acknowledged; retries are normal.
    const first = resolveCheckoutGrant(session(), snapshot());
    const retry = resolveCheckoutGrant(session(), snapshot());
    expect(first).toMatchObject({ action: 'grant', idempotencyKey: 'stripe:cs_test_123' });
    expect(retry).toMatchObject({ idempotencyKey: 'stripe:cs_test_123' });
  });

  it('gives different purchases different keys', () => {
    const a = resolveCheckoutGrant(session({ id: 'cs_a' }), snapshot());
    const b = resolveCheckoutGrant(session({ id: 'cs_b' }), snapshot());
    if (a.action !== 'grant' || b.action !== 'grant') throw new Error('both should grant');
    expect(a.idempotencyKey).not.toBe(b.idempotencyKey);
  });

  it('namespaces the key so it cannot collide with a Play purchase token', () => {
    const decision = resolveCheckoutGrant(session(), snapshot());
    if (decision.action !== 'grant') throw new Error('should grant');
    expect(decision.idempotencyKey.startsWith('stripe:')).toBe(true);
  });
});
