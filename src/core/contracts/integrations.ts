// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import type { ManagedBillingSummary } from './backend';

export type PurchasePlatform = 'google-play';

export type GooglePlayPurchaseState = 'purchased' | 'pending' | 'unspecified';

export interface AppUser {
  id: string;
  email: string | null;
  displayName: string | null;
  photoUrl: string | null;
}

export interface EntitlementRecord {
  id: string;
  platform: PurchasePlatform;
  productId: string;
  creditsGranted: number;
  purchaseToken: string | null;
  orderId: string | null;
  createdAt: number;
}

export interface GooglePlayPurchaseRecord {
  productId: string;
  purchaseToken: string;
  packageName: string;
  orderId?: string | null;
  purchaseTime?: number | null;
  purchaseState: GooglePlayPurchaseState;
  acknowledged?: boolean;
}

export interface VerifyGooglePlayPurchaseRequest {
  purchase: GooglePlayPurchaseRecord;
}

export interface VerifyGooglePlayPurchaseResult {
  ok: boolean;
  alreadyProcessed: boolean;
  grantedCredits: number;
  entitlements: EntitlementRecord[];
  billingSummary: ManagedBillingSummary;
}
