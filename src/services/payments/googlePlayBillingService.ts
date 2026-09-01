// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import type { GooglePlayPurchaseRecord } from '../../core/contracts/integrations';
import { getThemeBillingNativePlugin, isNativeAndroidBilling } from './themeBillingNativePlugin';

export interface ProductDetailsResult {
  productId: string;
  title: string;
  description: string;
  formattedPrice?: string;
  priceAmountMicros?: number;
  priceCurrencyCode?: string;
}

export interface PurchasesUpdatedEvent {
  ownedProductIds: string[];
  purchases: GooglePlayPurchaseRecord[];
}

export interface ProductDetailsAvailableEvent {
  products: ProductDetailsResult[];
}

export interface BillingErrorEvent {
  responseCode: number;
  debugMessage: string;
}

export interface OwnedPurchasesResult {
  ownedProductIds: string[];
  purchases: GooglePlayPurchaseRecord[];
}

export interface IsProductOwnedResult {
  owned: boolean;
}

interface ThemeBillingPluginInterface {
  startConnection(): Promise<void>;
  getProductDetails(options?: { productIds?: string[] }): Promise<void>;
  purchaseTheme(options: { productId: string; obfuscatedAccountId: string }): Promise<void>;
  restorePurchases(): Promise<void>;
  consumePurchase(options: { purchaseToken: string }): Promise<void>;
  isThemeOwned(options: { productId: string }): Promise<IsProductOwnedResult>;
  getOwnedThemes(): Promise<OwnedPurchasesResult>;
  getOwnedPurchases?(): Promise<OwnedPurchasesResult>;
  addListener(
    eventName: 'purchasesUpdated',
    listenerFunc: (event: PurchasesUpdatedEvent) => void,
  ): Promise<{ remove: () => void }>;
  addListener(
    eventName: 'productDetailsAvailable',
    listenerFunc: (event: ProductDetailsAvailableEvent) => void,
  ): Promise<{ remove: () => void }>;
  addListener(
    eventName: 'billingError',
    listenerFunc: (event: BillingErrorEvent) => void,
  ): Promise<{ remove: () => void }>;
  removeAllListeners(): Promise<void>;
}

const ThemeBillingNative = getThemeBillingNativePlugin<ThemeBillingPluginInterface>();

/**
 * Thrown by the web stub for anything that cannot complete off Android.
 *
 * Callers show a spinner until the purchase promise settles and rely on it
 * rejecting to stop. A stub that resolved instead left the UI spinning
 * forever with no way out, because the purchase event it was waiting for was
 * never going to arrive.
 */
export class BillingUnavailableError extends Error {
  constructor() {
    super('Purchases are only available in the Android app.');
    this.name = 'BillingUnavailableError';
  }
}

const unavailable = async (): Promise<never> => {
  throw new BillingUnavailableError();
};

const createWebStub = (): ThemeBillingPluginInterface => ({
  // Read-only calls resolve empty: nothing is owned off Android, and callers
  // treat that as a legitimate answer rather than a failure.
  startConnection: async () => {},
  getProductDetails: async () => {},
  // Anything that would need Play to complete must reject, not resolve.
  purchaseTheme: unavailable,
  restorePurchases: async () => {},
  consumePurchase: unavailable,
  isThemeOwned: async () => ({ owned: false }),
  getOwnedThemes: async () => ({ ownedProductIds: [], purchases: [] }),
  getOwnedPurchases: async () => ({ ownedProductIds: [], purchases: [] }),
  addListener: async () => ({ remove: () => {} }),
  removeAllListeners: async () => {},
});

const plugin = isNativeAndroidBilling && ThemeBillingNative ? ThemeBillingNative : createWebStub();

export const googlePlayBillingService = {
  startConnection: () => plugin.startConnection(),
  getProductDetails: (productIds?: string[]) => (
    plugin.getProductDetails(productIds?.length ? { productIds } : {})
  ),
  purchaseProduct: (productId: string, obfuscatedAccountId: string) => (
    plugin.purchaseTheme({ productId, obfuscatedAccountId })
  ),
  restorePurchases: () => plugin.restorePurchases(),
  consumePurchase: (purchaseToken: string) => plugin.consumePurchase({ purchaseToken }),
  isProductOwned: (productId: string) => plugin.isThemeOwned({ productId }),
  getOwnedPurchases: async (): Promise<OwnedPurchasesResult> => {
    if (typeof plugin.getOwnedPurchases === 'function') {
      return plugin.getOwnedPurchases();
    }
    return plugin.getOwnedThemes();
  },
  onPurchasesUpdated: (cb: (event: PurchasesUpdatedEvent) => void) => plugin.addListener('purchasesUpdated', cb),
  onProductDetailsAvailable: (cb: (event: ProductDetailsAvailableEvent) => void) => plugin.addListener('productDetailsAvailable', cb),
  onBillingError: (cb: (event: BillingErrorEvent) => void) => plugin.addListener('billingError', cb),
  removeAllListeners: () => plugin.removeAllListeners(),
  isAvailable: isNativeAndroidBilling,
} as const;
