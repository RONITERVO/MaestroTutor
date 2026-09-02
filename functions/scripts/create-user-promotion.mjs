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

STRIPE_SECRET must contain the matching live or test restricted key.
The user must have started Checkout once so the canonical managed account
already contains its mode-specific Stripe customer id.`;

const readArgs = (argv) => {
  const values = new Map();
  for (let index = 0; index < argv.length; index += 1) {
    const key = argv[index];
    if (key === '--help' || key === '-h') return new Map([['help', 'true']]);
    if (!key.startsWith('--') || index + 1 >= argv.length) {
      throw new Error(`Invalid argument ${key}.`);
    }
    values.set(key.slice(2), argv[index + 1]);
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
const secret = process.env.STRIPE_SECRET?.trim() || '';
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
if (!secret) errors.push('STRIPE_SECRET is required in the process environment.');
if (mode && secret && !secret.startsWith(`rk_${mode}_`) && !secret.startsWith(`sk_${mode}_`)) {
  errors.push(`STRIPE_SECRET does not match ${mode} mode.`);
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
  const expiresAt = Math.floor(Date.now() / 1000) + (expiresHours * 60 * 60);
  const metadata = { purpose: 'maintainer-user-credit', firebaseUidHash: uidHash };
  const coupon = await stripe.coupons.create({
    percent_off: 100,
    duration: 'once',
    max_redemptions: 1,
    redeem_by: expiresAt,
    name: 'Maestro one-time 100% credit-pack promotion',
    metadata,
  }, { idempotencyKey: `maestro-user-coupon-${operationHash}` });
  const promotion = await stripe.promotionCodes.create({
    promotion: { type: 'coupon', coupon: coupon.id },
    customer: customer.id,
    code,
    expires_at: expiresAt,
    max_redemptions: 1,
    metadata,
  }, { idempotencyKey: `maestro-user-promotion-${operationHash}` });

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
