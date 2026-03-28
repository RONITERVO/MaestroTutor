// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

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

const googleAuth = new google.auth.GoogleAuth({
  scopes: ['https://www.googleapis.com/auth/androidpublisher'],
});

const androidPublisher = google.androidpublisher({
  version: 'v3',
  auth: googleAuth,
});

const sleep = (ms: number): Promise<void> => new Promise(resolve => setTimeout(resolve, ms));

const fetchPurchaseVerification = async (params: {
  productId: string;
  purchaseToken: string;
}) => {
  const verificationResponse = await androidPublisher.purchases.products.get({
    packageName: appConfig.googlePlayPackageName,
    productId: params.productId,
    token: params.purchaseToken,
  });

  return verificationResponse.data as Record<string, unknown>;
};

export const verifyManagedGooglePlayPurchase = async (params: {
  uid: string;
  user: AppUser;
  purchase: GooglePlayPurchaseRecord;
}) => {
  const creditsGranted = getCreditsForManagedProduct(params.purchase.productId);
  if (creditsGranted <= 0) {
    throw createHttpError(400, `Product ${params.purchase.productId} is not configured for managed credits.`);
  }

  if (!params.purchase.purchaseToken?.trim()) {
    throw createHttpError(400, 'Missing Google Play purchase token.');
  }

  if (
    params.purchase.packageName &&
    params.purchase.packageName !== appConfig.googlePlayPackageName
  ) {
    throw createHttpError(400, 'Google Play package name does not match this app.');
  }

  let verification: Record<string, unknown> | null = null;
  for (let attempt = 0; attempt < 6; attempt += 1) {
    verification = await fetchPurchaseVerification({
      productId: params.purchase.productId,
      purchaseToken: params.purchase.purchaseToken,
    });
    const purchaseState = Number(verification.purchaseState);
    if (purchaseState !== 0) {
      throw createHttpError(409, 'Google Play purchase is not in a completed purchased state.');
    }

    const consumptionState = Number(verification.consumptionState);
    if (consumptionState === 1) {
      break;
    }

    if (attempt < 5) {
      await sleep(500);
    }
  }

  if (!verification) {
    throw createHttpError(500, 'Google Play purchase verification returned no data.');
  }

  const purchaseState = Number(verification.purchaseState);
  if (purchaseState !== 0) {
    throw createHttpError(409, 'Google Play purchase is not in a completed purchased state.');
  }

  const consumptionState = Number(verification.consumptionState);
  if (consumptionState !== 1) {
    throw createHttpError(409, 'Purchase must be consumed by the client before credits are granted.');
  }

  const transactionResult = await grantPurchasedCredits({
    uid: params.uid,
    user: params.user,
    purchaseToken: params.purchase.purchaseToken,
    productId: params.purchase.productId,
    orderId: typeof verification.orderId === 'string'
      ? verification.orderId
      : (params.purchase.orderId || null),
    creditsGranted,
    rawPurchase: params.purchase as unknown as Record<string, unknown>,
    rawVerification: verification,
  });

  const accountState = await getManagedAccountState(params.uid, params.user);
  return {
    ok: true,
    alreadyProcessed: transactionResult.alreadyProcessed,
    grantedCredits: transactionResult.alreadyProcessed ? 0 : creditsGranted,
    entitlements: accountState.entitlements,
    billingSummary: accountState.billingSummary,
  };
};
