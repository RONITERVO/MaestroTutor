// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import {
  googlePlayBillingService,
  type BillingErrorEvent,
  type IsProductOwnedResult,
  type OwnedPurchasesResult,
  type ProductDetailsAvailableEvent,
  type ProductDetailsResult,
  type PurchasesUpdatedEvent,
} from '../../services/payments/googlePlayBillingService';

export type {
  BillingErrorEvent,
  ProductDetailsAvailableEvent,
  ProductDetailsResult,
  PurchasesUpdatedEvent,
};

export type OwnedThemesResult = OwnedPurchasesResult;
export type IsThemeOwnedResult = IsProductOwnedResult;

export const ThemeBillingService = {
  startConnection: googlePlayBillingService.startConnection,
  getProductDetails: googlePlayBillingService.getProductDetails,
  purchaseTheme: googlePlayBillingService.purchaseProduct,
  restorePurchases: googlePlayBillingService.restorePurchases,
  consumePurchase: googlePlayBillingService.consumePurchase,
  isThemeOwned: googlePlayBillingService.isProductOwned,
  getOwnedThemes: googlePlayBillingService.getOwnedPurchases,
  getOwnedPurchases: googlePlayBillingService.getOwnedPurchases,
  onPurchasesUpdated: googlePlayBillingService.onPurchasesUpdated,
  onProductDetailsAvailable: googlePlayBillingService.onProductDetailsAvailable,
  onBillingError: googlePlayBillingService.onBillingError,
  removeAllListeners: googlePlayBillingService.removeAllListeners,
  isAvailable: googlePlayBillingService.isAvailable,
} as const;
