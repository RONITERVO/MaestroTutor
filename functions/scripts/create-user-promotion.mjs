// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/**
 * Create one short-lived, customer-restricted 100% Stripe promotion code.
 *
 * This is deliberately a maintainer command rather than a product endpoint.
 * It verifies the Firebase user, stored mode-specific Stripe customer and
 * Stripe customer metadata before creating any provider objects.
 */

import { createHash, randomBytes } from 'node:crypto';
import process from 'node:process';
import { applicationDefault, initializeApp } from 'firebase-admin/app';
import { getAuth } from 'firebase-admin/auth';
import { getFirestore } from 'firebase-admin/firestore';
import Stripe from 'stripe';

const usage = `Usage:
  npm --prefix functions run promotion:create-user -- \\
    --project <firebase-project> --mode <live|test> \\
    (--uid <firebase-uid> | --email <firebase-email>) \\
    [--code <customer-code>] [--expires-hours <1-168>]

STRIPE_PROMOTION_SECRET must contain the matching live or test restricted key.
The user must have started Checkout once so the canonical managed account
already contains its mode-specific Stripe customer id.`;

const supportedOptions = new Set([
  'project',
  'mode',
  'uid',
  'email',
  'code',
  'expires-hours',
]);

const readArgs = (argv) => {
  const values = new Map();
  for (let index = 0; index < argv.length; index += 1) {
    const key = argv[index];
    if (key === '--help' || key === '-h') return new Map([['help', 'true']]);
    const option = key.startsWith('--') ? key.slice(2) : '';
    if (!supportedOptions.has(option) || index + 1 >= argv.length) {
      throw new Error(`Invalid argument ${key}.`);
    }
    values.set(option, argv[index + 1]);
    index += 1;
  }
  return values;
};

const fail = (message) => {
  process.stderr.write(`${message}\n\n${usage}\n`);
  process.exitCode = 1;
};

let args;
try {
  args = readArgs(process.argv.slice(2));
} catch (error) {
  fail(error instanceof Error ? error.message : String(error));
}

if (!args || args.has('help')) {
  if (args?.has('help')) process.stdout.write(`${usage}\n`);
  process.exit(args?.has('help') ? 0 : 1);
}

const projectId = args.get('project')?.trim() || '';
const mode = args.get('mode')?.trim() || '';
const requestedUid = args.get('uid')?.trim() || '';
const email = args.get('email')?.trim() || '';
const secret = process.env.STRIPE_PROMOTION_SECRET?.trim() || '';
const expiresHours = Number(args.get('expires-hours') || '24');
const code = (args.get('code')?.trim() || `MAESTRO-${randomBytes(8).toString('hex')}`).toUpperCase();

const errors = [];
if (!projectId) errors.push('--project is required.');
if (mode !== 'live' && mode !== 'test') errors.push('--mode must be live or test.');
if (Boolean(requestedUid) === Boolean(email)) errors.push('Provide exactly one of --uid or --email.');
if (!Number.isInteger(expiresHours) || expiresHours < 1 || expiresHours > 168) {
  errors.push('--expires-hours must be a whole number from 1 through 168.');
}
if (!/^[A-Z0-9-]{4,64}$/.test(code)) errors.push('--code must be 4-64 letters, numbers or dashes.');
if (!secret) errors.push('STRIPE_PROMOTION_SECRET is required in the process environment.');
if (mode && secret && !secret.startsWith(`rk_${mode}_`)) {
  errors.push(`STRIPE_PROMOTION_SECRET must be a restricted key for ${mode} mode.`);
}
if (errors.length > 0) {
  fail(errors.join('\n'));
} else {
  const app = initializeApp({ credential: applicationDefault(), projectId });
  const auth = getAuth(app);
  const db = getFirestore(app);
  const user = requestedUid
    ? await auth.getUser(requestedUid)
    : await auth.getUserByEmail(email);
  const uid = user.uid;
  const account = await db.doc(`users/${uid}/managedAccounts/default`).get();
  const customerField = mode === 'live' ? 'stripeCustomerIdLive' : 'stripeCustomerIdTest';
  const customerId = account.data()?.[customerField];
  if (typeof customerId !== 'string' || !customerId) {
    throw new Error(`No ${mode} Stripe customer is stored for this user. Start Checkout once, cancel it, then retry.`);
  }

  const stripe = new Stripe(secret);
  const customer = await stripe.customers.retrieve(customerId);
  if (customer.deleted) throw new Error('The stored Stripe customer has been deleted. Start Checkout once, then retry.');
  if (customer.metadata?.firebaseUid !== uid) {
    throw new Error('The stored Stripe customer does not match the Firebase user; no promotion was created.');
  }

  const uidHash = createHash('sha256').update(uid).digest('hex');
  const operationHash = createHash('sha256').update(`${mode}\0${uid}\0${code}`).digest('hex');
  const couponId = `maestro-user-${operationHash.slice(0, 40)}`;
  const requestedExpiresAt = Math.floor(Date.now() / 1000) + (expiresHours * 60 * 60);
  const metadata = {
    purpose: 'maintainer-user-credit',
    firebaseUidHash: uidHash,
    operationHash,
  };
  const isMissingResource = (error) => (
    error?.code === 'resource_missing' || Number(error?.statusCode) === 404
  );

  // A deterministic coupon id makes a retry recover the first run's original
  // expiry instead of changing parameters under a reused idempotency key.
  let coupon;
  try {
    coupon = await stripe.coupons.retrieve(couponId);
  } catch (error) {
    if (!isMissingResource(error)) throw error;
    coupon = await stripe.coupons.create({
      id: couponId,
      percent_off: 100,
      duration: 'once',
      max_redemptions: 1,
      redeem_by: requestedExpiresAt,
      name: 'Maestro one-time 100% credit-pack promotion',
      metadata,
    }, { idempotencyKey: `maestro-user-coupon-${operationHash}` });
  }

  if (
    coupon.percent_off !== 100
    || coupon.duration !== 'once'
    || coupon.max_redemptions !== 1
    || coupon.metadata?.operationHash !== operationHash
    || coupon.metadata?.firebaseUidHash !== uidHash
    || typeof coupon.redeem_by !== 'number'
    || !coupon.valid
  ) {
    throw new Error('The recoverable Stripe coupon does not match this operation. Use a new code; nothing was created.');
  }
  const expiresAt = coupon.redeem_by;

  const existingPromotions = await stripe.promotionCodes.list({
    code,
    customer: customer.id,
    limit: 10,
  });
  let promotion = existingPromotions.data.find((candidate) => (
    candidate.metadata?.operationHash === operationHash
    && candidate.metadata?.firebaseUidHash === uidHash
  ));
  if (!promotion) {
    promotion = await stripe.promotionCodes.create({
      promotion: { type: 'coupon', coupon: coupon.id },
      customer: customer.id,
      code,
      expires_at: expiresAt,
      max_redemptions: 1,
      metadata,
    }, { idempotencyKey: `maestro-user-promotion-${operationHash}` });
  }

  const promotionCustomerId = typeof promotion.customer === 'string'
    ? promotion.customer
    : promotion.customer?.id ?? null;
  const promotionCouponId = typeof promotion.promotion.coupon === 'string'
    ? promotion.promotion.coupon
    : promotion.promotion.coupon?.id ?? null;
  if (
    promotionCustomerId !== customer.id
    || promotionCouponId !== coupon.id
    || promotion.max_redemptions !== 1
    || promotion.expires_at !== expiresAt
    || !promotion.active
    || promotion.times_redeemed !== 0
  ) {
    throw new Error('The recoverable Stripe promotion does not match this operation. Use a new code; nothing was created.');
  }

  process.stdout.write(`${JSON.stringify({
    ok: true,
    mode,
    code: promotion.code,
    promotionId: promotion.id,
    couponId: coupon.id,
    expiresAt: new Date(expiresAt * 1000).toISOString(),
    firebaseUidHash: uidHash,
  }, null, 2)}\n`);
}
