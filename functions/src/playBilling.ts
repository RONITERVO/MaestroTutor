// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Google Play purchase verification for managed credit packs.
 *
 * Order of operations is the whole point of this file:
 *
 *   verify with Play  ->  grant credits (idempotent)  ->  consume the token
 *
 * The previous version required the client to consume the token *before* the
 * server would grant anything, and rejected any purchase that was not already
 * consumed. That inverts the safety property. Consumption is irreversible and
 * removes the purchase from the user's Play inventory; if anything failed
 * between consuming and granting — a dropped connection, a cold start timing
 * out, a deploy — the money was taken, the token was gone, and there was
 * nothing left to retry against. The user had paid for nothing.
 *
 * Granting first is safe in a way consuming first is not: the grant is keyed on
 * the purchase token, so repeating it is a no-op, and a token that is granted
 * but not yet consumed can always be retried. The worst case is a purchase the
 * user cannot re-buy until the next reconciliation, rather than credits they
 * paid for and never received.
 */

import { google } from 'googleapis';
import type { AppUser } from './auth';
import { appConfig, getCreditsForManagedProduct } from './config';
import { getManagedAccountState, grantPurchasedCredits } from './managedBilling';
import { createHttpError } from './http';

export interface GooglePlayPurchaseRecord {
  productId: string;
  purchaseToken: string;
  packageName: string;
  orderId?: string | null;
  purchaseTime?: number | null;
  purchaseState: 'purchased' | 'pending' | 'unspecified';
  acknowledged?: boolean;
}

/** Play's numeric purchaseState: 0 purchased, 1 cancelled, 2 pending. */
const PURCHASE_STATE_PURCHASED = 0;
/** Play's numeric consumptionState: 0 yet to be consumed, 1 consumed. */
const CONSUMPTION_STATE_CONSUMED = 1;

const googleAuth = new google.auth.GoogleAuth({
  scopes: ['https://www.googleapis.com/auth/androidpublisher'],
});

const androidPublisher = google.androidpublisher({ version: 'v3', auth: googleAuth });

const fetchPurchase = async (productId: string, purchaseToken: string) => {
  const response = await androidPublisher.purchases.products.get({
    packageName: appConfig.googlePlayPackageName,
    productId,
    token: purchaseToken,
  });
  return (response.data || {}) as Record<string, unknown>;
};

/**
 * Consume server-side so the token is released even if the client never comes
 * back. Failure here is logged and swallowed: the credits are already granted,
 * and an unconsumed token is a recoverable annoyance rather than lost money.
 */
const consumePurchase = async (productId: string, purchaseToken: string): Promise<boolean> => {
  try {
    await androidPublisher.purchases.products.consume({
      packageName: appConfig.googlePlayPackageName,
      productId,
      token: purchaseToken,
    });
    return true;
  } catch (error) {
    console.error(
      `[playBilling] Credits were granted but consuming the token failed for ${productId}. `
      + 'The purchase stands; the token will be retried on the next verification.',
      error,
    );
    return false;
  }
};

export const verifyManagedGooglePlayPurchase = async (params: {
  uid: string;
  user: AppUser;
  purchase: GooglePlayPurchaseRecord;
}) => {
  const { productId, purchaseToken, packageName } = params.purchase;

  const creditsGranted = getCreditsForManagedProduct(productId);
  if (creditsGranted <= 0) {
    throw createHttpError(400, `Product ${productId} is not configured for managed credits.`);
  }
  if (!purchaseToken?.trim()) {
    throw createHttpError(400, 'Missing Google Play purchase token.');
  }
  if (packageName && packageName !== appConfig.googlePlayPackageName) {
    throw createHttpError(400, 'Google Play package name does not match this app.');
  }

  // 1. Ask Play what it thinks. The client is never trusted for purchase state.
  const verification = await fetchPurchase(productId, purchaseToken);
  const purchaseState = Number(verification.purchaseState);

  if (purchaseState !== PURCHASE_STATE_PURCHASED) {
    // Pending purchases are a normal state for slow payment methods, and the
    // client is expected to come back once Play settles them.
    throw createHttpError(409, 'Google Play purchase is not in a completed purchased state.');
  }

  // 2. Grant before consuming. Idempotent on the purchase token, so a retry
  //    after any failure below is harmless.
  const grant = await grantPurchasedCredits({
    uid: params.uid,
    user: params.user,
    purchaseToken,
    productId,
    orderId: typeof verification.orderId === 'string'
      ? verification.orderId
      : (params.purchase.orderId || null),
    creditsGranted,
    rawPurchase: params.purchase as unknown as Record<string, unknown>,
    rawVerification: verification,
  });

  // 3. Release the token so the pack can be bought again. Already-consumed
  //    tokens are left alone; this is the path a retry takes.
  const alreadyConsumed = Number(verification.consumptionState) === CONSUMPTION_STATE_CONSUMED;
  const consumed = alreadyConsumed || await consumePurchase(productId, purchaseToken);

  const accountState = await getManagedAccountState(params.uid, params.user);
  return {
    ok: true,
    alreadyProcessed: grant.alreadyProcessed,
    grantedCredits: grant.alreadyProcessed ? 0 : creditsGranted,
    consumed,
    entitlements: accountState.entitlements,
    billingSummary: accountState.billingSummary,
  };
};
