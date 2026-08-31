// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { MAESTRO_INTEGRATION_CONFIG, isManagedBillingProduct } from '../../core/config/integrations';
import type {
  GooglePlayPurchaseRecord,
  VerifyGooglePlayPurchaseRequest,
  VerifyGooglePlayPurchaseResult,
} from '../../core/contracts/integrations';
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

  verifyGooglePlayPurchase: async (
    payload: VerifyGooglePlayPurchaseRequest
  ): Promise<VerifyGooglePlayPurchaseResult> => (
    maestroBackendService.verifyGooglePlayPurchase({
      ...payload,
      purchase: normalizePurchaseRecord(payload.purchase),
    })
  ),
} as const;
