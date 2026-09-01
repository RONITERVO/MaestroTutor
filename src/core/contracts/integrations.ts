// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/** Google Play is retained only so historical entitlements remain readable. */
export type PurchasePlatform = 'stripe' | 'google-play';

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
