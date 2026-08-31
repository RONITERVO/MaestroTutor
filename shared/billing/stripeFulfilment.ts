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
  payment_status?: string | null;
  payment_intent?: string | { id?: string } | null;
  metadata?: Record<string, string> | null;
  customer_details?: { email?: string | null } | null;
}

export interface CreditPackLike {
  id: string;
  credits: number;
}

export type FulfilmentDecision =
  | {
    action: 'grant';
    uid: string;
    packId: string;
    /** Always the catalogue's number, never the session's. */
    credits: number;
    /** Stable per purchase, so replayed deliveries are no-ops. */
    idempotencyKey: string;
    orderId: string | null;
    email: string | null;
  }
  | { action: 'skip'; reason: FulfilmentSkipReason };

export type FulfilmentSkipReason =
  | 'not-paid'
  | 'missing-metadata'
  | 'unknown-pack';

/**
 * What, if anything, this session should be granted.
 *
 * `lookupPack` is the server's catalogue. The session's own `credits` metadata
 * is deliberately ignored: it round-trips through the browser and through
 * Stripe, and a fulfilment that trusted it would hand out whatever quantity the
 * session claimed. Metadata is used only to identify *which* pack and *whose*
 * account — both of which are then re-checked here.
 */
export const resolveCheckoutGrant = (
  session: CheckoutSessionLike,
  lookupPack: (packId: string) => CreditPackLike | undefined,
): FulfilmentDecision => {
  // A completed session is not necessarily a paid one: delayed payment methods
  // complete first and settle later, and granting on completion alone would
  // give credits away for payments that may still fail.
  if (session.payment_status !== 'paid') {
    return { action: 'skip', reason: 'not-paid' };
  }

  const uid = session.metadata?.firebaseUid?.trim();
  const packId = session.metadata?.packId?.trim();
  if (!uid || !packId) {
    return { action: 'skip', reason: 'missing-metadata' };
  }

  const pack = lookupPack(packId);
  if (!pack || !(pack.credits > 0)) {
    return { action: 'skip', reason: 'unknown-pack' };
  }

  const paymentIntent = typeof session.payment_intent === 'string'
    ? session.payment_intent
    : session.payment_intent?.id ?? null;

  return {
    action: 'grant',
    uid,
    packId: pack.id,
    credits: pack.credits,
    // Keyed on the session rather than the webhook event: the session is the
    // purchase, so this stays stable across event retries and would still hold
    // if another event type for the same session were ever handled.
    idempotencyKey: `stripe:${session.id}`,
    orderId: paymentIntent,
    email: session.customer_details?.email ?? null,
  };
};
