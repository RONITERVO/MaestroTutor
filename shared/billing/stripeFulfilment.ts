// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Deciding what a completed Stripe Checkout session is owed.
 *
 * Pure, and separate from the Firestore write, because this is where the
 * security rules of the web payment path live: what counts as paid, whose
 * account is credited, and — most importantly — where the credit quantity comes
 * from. Those are exactly the rules that need exhausting by tests rather than
 * being read carefully once inside a webhook handler.
 */

/** The parts of a Stripe Checkout session fulfilment actually depends on. */
export interface CheckoutSessionLike {
  id: string;
  mode?: string | null;
  payment_status?: string | null;
  payment_intent?: string | { id?: string } | null;
  amount_subtotal?: number | null;
  amount_total?: number | null;
  currency?: string | null;
  total_details?: { amount_discount?: number | null } | null;
  metadata?: Record<string, string> | null;
  customer_details?: { email?: string | null } | null;
}

/** Immutable server record written when the Checkout session is created. */
export interface CheckoutGrantSnapshotLike {
  uid: string;
  packId: string;
  credits: number;
  priceCents?: number;
  currency?: string;
}

export type FulfilmentDecision =
  | {
    action: 'grant';
    uid: string;
    packId: string;
    /** Always the Checkout snapshot's number, never mutable metadata/catalogue state. */
    credits: number;
    /** Stable per purchase, so replayed deliveries are no-ops. */
    idempotencyKey: string;
    orderId: string | null;
    email: string | null;
  }
  | { action: 'skip'; reason: FulfilmentSkipReason };

export type FulfilmentSkipReason =
  | 'not-paid'
  | 'missing-snapshot'
  | 'invalid-snapshot'
  | 'invalid-discount';

export const isCheckoutFulfilmentEventType = (eventType: string): boolean => (
  eventType === 'checkout.session.completed'
  || eventType === 'checkout.session.async_payment_succeeded'
);

/** Stable provider-scoped key used by the backend purchase-claim transaction. */
export const makeStripeCheckoutIdempotencyKey = (sessionId: string): string => (
  `stripe:${sessionId}`
);

/**
 * What, if anything, this session should be granted.
 *
 * `snapshot` is written by the server under the Stripe session id before the
 * Checkout URL is returned. The session metadata and current catalogue are
 * deliberately ignored: fulfilment must keep the exact user, pack and credit
 * quantity sold when this particular Checkout session was created.
 */
export const resolveCheckoutGrant = (
  session: CheckoutSessionLike,
  snapshot: CheckoutGrantSnapshotLike | null | undefined,
): FulfilmentDecision => {
  const paymentIntent = typeof session.payment_intent === 'string'
    ? session.payment_intent
    : session.payment_intent?.id ?? null;
  const requiresDiscountProof = (
    session.payment_status === 'no_payment_required'
    || (session.payment_status === 'paid' && !paymentIntent)
  );

  // A completed session is not necessarily settled: delayed payment methods
  // complete first and settle later. A fully discounted payment-mode Checkout
  // is the one exception, and must match every price invariant from the
  // immutable server snapshot before it can grant without a PaymentIntent.
  if (session.payment_status !== 'paid' && !requiresDiscountProof) {
    return { action: 'skip', reason: 'not-paid' };
  }

  if (!snapshot) {
    return { action: 'skip', reason: 'missing-snapshot' };
  }

  const uid = typeof snapshot.uid === 'string' ? snapshot.uid.trim() : '';
  const packId = typeof snapshot.packId === 'string' ? snapshot.packId.trim() : '';
  if (
    !uid
    || !packId
    || !Number.isSafeInteger(snapshot.credits)
    || snapshot.credits <= 0
  ) {
    return { action: 'skip', reason: 'invalid-snapshot' };
  }

  if (requiresDiscountProof) {
    const snapshotCurrency = typeof snapshot.currency === 'string'
      ? snapshot.currency.trim().toLowerCase()
      : '';
    const sessionCurrency = typeof session.currency === 'string'
      ? session.currency.trim().toLowerCase()
      : '';
    const hasExactFullDiscount = (
      session.mode === 'payment'
      && Number.isSafeInteger(snapshot.priceCents)
      && Number(snapshot.priceCents) > 0
      && session.amount_subtotal === snapshot.priceCents
      && session.amount_total === 0
      && session.total_details?.amount_discount === snapshot.priceCents
      && Boolean(snapshotCurrency)
      && sessionCurrency === snapshotCurrency
      && !paymentIntent
    );
    if (!hasExactFullDiscount) {
      return { action: 'skip', reason: 'invalid-discount' };
    }
  }

  return {
    action: 'grant',
    uid,
    packId,
    credits: snapshot.credits,
    // Keyed on the session rather than the webhook event: the session is the
    // purchase, so this stays stable across event retries and would still hold
    // if another event type for the same session were ever handled.
    idempotencyKey: makeStripeCheckoutIdempotencyKey(session.id),
    orderId: paymentIntent,
    email: session.customer_details?.email ?? null,
  };
};
