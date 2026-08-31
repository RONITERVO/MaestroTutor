// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Stripe checkout and fulfilment for managed credit packs.
 *
 * This is the web half of the same balance Google Play funds on Android. Both
 * end in `grantPurchasedCredits`, keyed on whatever identifies the purchase in
 * its storefront, so a user who buys on the web and a user who buys in the app
 * hold the same kind of credits.
 *
 * Two rules govern everything here, and both are the difference between a
 * billing integration that works and one that gives money away:
 *
 * **Credits are granted from the webhook, never from the browser.** The
 * redirect back from Checkout is a UI convenience: it can be forged, replayed,
 * closed before it fires, or simply never arrive on a flaky connection. The
 * webhook is the only statement from Stripe that a payment actually settled.
 *
 * **Every grant is idempotent on the purchase.** Stripe retries webhooks until
 * they are acknowledged, and retries are normal rather than exceptional. The
 * key is the Checkout session id rather than the event id, because the session
 * is the purchase: if a second event type for the same session were ever
 * handled, an event-id key would grant twice while a session key still would
 * not.
 */

import Stripe from 'stripe';
import type { Request, Response } from 'express';
import type { AppUser } from './auth';
import { appConfig, getCreditPackForCheckout, isStripeConfigured } from './config';
import { getManagedAccountState, grantPurchasedCredits } from './managedBilling';
import { createHttpError } from './http';
import { adminAuth, adminDb } from './firebase';
import {
  resolveCheckoutGrant,
  type CheckoutGrantSnapshotLike,
} from '../../shared/billing/stripeFulfilment';

let cachedStripe: Stripe | null = null;

interface StripeCheckoutGrantSnapshot extends CheckoutGrantSnapshotLike {
  schemaVersion: 1;
  priceCents: number;
  currency: string;
  createdAt: number;
}

const checkoutGrantRef = (sessionId: string) => (
  adminDb.collection('stripeCheckoutGrants').doc(sessionId)
);

const requireStripe = (): Stripe => {
  if (!isStripeConfigured()) {
    throw createHttpError(500, 'Stripe is not configured on this backend.');
  }
  if (!cachedStripe) {
    cachedStripe = new Stripe(appConfig.stripeSecretKey);
  }
  return cachedStripe;
};

/**
 * Live and test mode keep separate customer records.
 *
 * A customer id created with a test key does not exist to a live key, so
 * storing one id and reusing it across a mode switch fails at checkout with an
 * error that reads like a Stripe outage rather than a configuration mistake.
 */
const stripeMode = (): 'live' | 'test' => (
  /^(sk|rk)_live_/.test(appConfig.stripeSecretKey) ? 'live' : 'test'
);

const customerFieldForMode = (): 'stripeCustomerIdLive' | 'stripeCustomerIdTest' => (
  stripeMode() === 'live' ? 'stripeCustomerIdLive' : 'stripeCustomerIdTest'
);

const getOrCreateCustomer = async (uid: string, user: AppUser): Promise<string> => {
  const stripe = requireStripe();
  const ref = adminDb.collection('users').doc(uid).collection('account').doc('summary');
  const field = customerFieldForMode();

  const snapshot = await ref.get();
  const existingId = snapshot.exists ? (snapshot.data() || {})[field] : null;

  if (typeof existingId === 'string' && existingId) {
    try {
      const existing = await stripe.customers.retrieve(existingId);
      if (!(existing as { deleted?: boolean }).deleted) return existingId;
    } catch {
      // Falls through and creates a new one. A stored id that Stripe no longer
      // recognises — a deleted customer, or one from the other mode — must not
      // be allowed to block the purchase.
    }
  }

  const customer = await stripe.customers.create({
    email: user.email || undefined,
    metadata: { firebaseUid: uid },
  });
  await ref.set({ [field]: customer.id }, { merge: true });
  return customer.id;
};

export const createManagedCheckoutSession = async (params: {
  uid: string;
  user: AppUser;
  packId: string;
}): Promise<{ url: string; sessionId: string }> => {
  const stripe = requireStripe();
  if (!appConfig.appUrl) {
    throw createHttpError(500, 'APP_URL is required to build Stripe return URLs.');
  }

  const pack = getCreditPackForCheckout(params.packId);
  if (!pack) {
    throw createHttpError(400, `Unknown credit pack "${params.packId}".`);
  }

  const customer = await getOrCreateCustomer(params.uid, params.user);
  const appUrl = appConfig.appUrl.replace(/\/+$/, '');

  const session = await stripe.checkout.sessions.create({
    mode: 'payment',
    customer,
    success_url: `${appUrl}/?billing=success`,
    cancel_url: `${appUrl}/?billing=cancel`,
    allow_promotion_codes: true,
    line_items: [{
      quantity: 1,
      price_data: {
        currency: appConfig.billingCurrency,
        unit_amount: pack.priceCents,
        product_data: {
          name: `${pack.credits.toLocaleString('en-US')} Maestro credits`,
          metadata: { packId: pack.id },
        },
      },
    }],
    // Carried on the session so fulfilment never has to trust the browser for
    // who bought what: the webhook reads these back from Stripe.
    metadata: {
      firebaseUid: params.uid,
      packId: pack.id,
      credits: String(pack.credits),
    },
  });

  if (!session.url) {
    throw createHttpError(502, 'Stripe did not return a checkout URL.');
  }

  // Firestore `create` is deliberately used instead of set/merge: a Checkout
  // session's grant is immutable and keyed by the session id. Catalogue edits
  // after this point must never change what this already-created session buys.
  await checkoutGrantRef(session.id).create({
    schemaVersion: 1,
    uid: params.uid,
    packId: pack.id,
    credits: pack.credits,
    priceCents: pack.priceCents,
    currency: appConfig.billingCurrency,
    createdAt: Date.now(),
  } satisfies StripeCheckoutGrantSnapshot);

  return { url: session.url, sessionId: session.id };
};

/**
 * Fulfil a completed Checkout session.
 *
 * Deliberately handles exactly one event type. Stripe emits several for a
 * single purchase — `checkout.session.completed`, `payment_intent.succeeded`,
 * `charge.succeeded` — and acting on more than one would grant the same pack
 * more than once for anything keyed per event.
 */
const fulfilCheckoutSession = async (session: Stripe.Checkout.Session): Promise<boolean> => {
  const grantSnapshotDoc = await checkoutGrantRef(session.id).get();
  const grantSnapshot = grantSnapshotDoc.exists
    ? grantSnapshotDoc.data() as StripeCheckoutGrantSnapshot
    : null;

  // Every rule about what may be granted lives in the shared, tested decision
  // function; this only performs the write it asks for.
  const decision = resolveCheckoutGrant(session, grantSnapshot);
  if (decision.action === 'skip') {
    if (decision.reason !== 'not-paid') {
      console.error(`[stripeBilling] Session ${session.id} not fulfilled: ${decision.reason}.`);
    }
    return false;
  }

  const userRecord = await adminAuth.getUser(decision.uid).catch(() => null);
  const user: AppUser = {
    id: decision.uid,
    email: userRecord?.email || decision.email,
    displayName: userRecord?.displayName || null,
    photoUrl: userRecord?.photoURL || null,
  };

  const grant = await grantPurchasedCredits({
    uid: decision.uid,
    user,
    purchaseToken: decision.idempotencyKey,
    productId: decision.packId,
    orderId: decision.orderId,
    creditsGranted: decision.credits,
    platform: 'stripe',
    rawPurchase: {
      sessionId: session.id,
      packId: decision.packId,
      checkoutSnapshot: grantSnapshot,
    },
    rawVerification: session as unknown as Record<string, unknown>,
  });

  return !grant.alreadyProcessed;
};

/**
 * Verify and process a Stripe webhook.
 *
 * `rawBody` must be the exact bytes Stripe sent. Signature verification is over
 * the raw payload, so the route has to be registered with a raw body parser
 * ahead of any JSON middleware — parsing and re-serialising changes the bytes
 * and every signature check fails.
 */
export const handleStripeWebhook = async (req: Request, res: Response): Promise<void> => {
  const stripe = requireStripe();
  const signature = req.headers['stripe-signature'];
  if (typeof signature !== 'string') {
    throw createHttpError(400, 'Missing Stripe signature header.');
  }

  const rawBody = req.body;
  if (!Buffer.isBuffer(rawBody)) {
    throw createHttpError(400, 'Stripe webhook raw body is unavailable.');
  }

  let event: Stripe.Event;
  try {
    event = stripe.webhooks.constructEvent(rawBody, signature, appConfig.stripeWebhookSecret);
  } catch (error) {
    // An unverifiable payload is rejected outright: anyone can POST to this
    // route, and the signature is the only thing that makes it trustworthy.
    console.error('[stripeBilling] Rejected a webhook with an invalid signature.', error);
    throw createHttpError(400, 'Invalid Stripe signature.');
  }

  if (event.type === 'checkout.session.completed') {
    const granted = await fulfilCheckoutSession(event.data.object as Stripe.Checkout.Session);
    console.log(`[stripeBilling] ${event.type} ${event.id}: ${granted ? 'granted' : 'no-op'}`);
  }

  // Everything is acknowledged, including event types we ignore. Returning an
  // error would make Stripe retry a delivery that will never be actioned.
  res.json({ received: true });
};

export const getStripeAccountState = async (uid: string, user: AppUser) => (
  getManagedAccountState(uid, user)
);
