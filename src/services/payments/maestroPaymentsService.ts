// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { MAESTRO_INTEGRATION_CONFIG, isManagedBillingProduct } from '../../core/config/integrations';
import type {
  GooglePlayPurchaseRecord,
  VerifyGooglePlayPurchaseRequest,
  VerifyGooglePlayPurchaseResult,
} from '../../core/contracts/integrations';
import { maestroManagedAccountController } from '../account/maestroManagedAccountController';
import { maestroBackendService } from '../backend/maestroBackendService';
import { googlePlayBillingService } from './googlePlayBillingService';

const normalizePurchaseRecord = (purchase: GooglePlayPurchaseRecord): GooglePlayPurchaseRecord => ({
  ...purchase,
  packageName: purchase.packageName || MAESTRO_INTEGRATION_CONFIG.googlePlayPackageName,
});

export const maestroPaymentsService = {
  themeBilling: googlePlayBillingService,

  isManagedBillingProduct,

  getManagedBillingProductIds: (): string[] => [...MAESTRO_INTEGRATION_CONFIG.managedBillingProductIds],

  /**
   * Buy credits on the web.
   *
   * Google Play's payments policy requires Play Billing for purchases made
   * inside the Android app, so this is the web path only; Android continues to
   * go through `verifyGooglePlayPurchase`. Both fund the same balance.
   */
  startStripeCheckout: async (packId: string): Promise<void> => {
    await maestroManagedAccountController.startStripeCheckout(packId);
  },

  verifyGooglePlayPurchase: async (
    payload: VerifyGooglePlayPurchaseRequest
  ): Promise<VerifyGooglePlayPurchaseResult> => (
    maestroBackendService.verifyGooglePlayPurchase({
      ...payload,
      purchase: normalizePurchaseRecord(payload.purchase),
    })
  ),
} as const;
