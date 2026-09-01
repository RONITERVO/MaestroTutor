// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { MAESTRO_INTEGRATION_CONFIG, isManagedCreditPack } from '../../core/config/integrations';
import { maestroManagedAccountController } from '../account/maestroManagedAccountController';

export const maestroPaymentsService = {
  isManagedCreditPack,

  getManagedCreditPackIds: (): string[] => [...MAESTRO_INTEGRATION_CONFIG.managedCreditPackIds],

  isAndroidExternalCheckoutEnabled: (): boolean => (
    MAESTRO_INTEGRATION_CONFIG.androidExternalStripeCheckoutEnabled
  ),

  /**
   * Create the one hosted Stripe checkout used by every enabled client.
   */
  startStripeCheckout: async (packId: string): Promise<void> => {
    await maestroManagedAccountController.startStripeCheckout(packId);
  },
} as const;
