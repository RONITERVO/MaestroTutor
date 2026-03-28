// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { Capacitor, registerPlugin } from '@capacitor/core';
import type { GooglePlayPurchaseRecord } from '../../core/contracts/integrations';

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
  purchaseTheme(options: { productId: string }): Promise<void>;
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

const ThemeBillingNative = registerPlugin<ThemeBillingPluginInterface>('ThemeBilling');
const isNativeAndroid = Capacitor.isNativePlatform() && Capacitor.getPlatform() === 'android';

const createWebStub = (): ThemeBillingPluginInterface => ({
  startConnection: async () => {},
  getProductDetails: async () => {},
  purchaseTheme: async () => {
    console.warn('[GooglePlayBilling] Purchases are only available on Android.');
  },
  restorePurchases: async () => {},
  consumePurchase: async () => {},
  isThemeOwned: async () => ({ owned: false }),
  getOwnedThemes: async () => ({ ownedProductIds: [], purchases: [] }),
  getOwnedPurchases: async () => ({ ownedProductIds: [], purchases: [] }),
  addListener: async () => ({ remove: () => {} }),
  removeAllListeners: async () => {},
});

const plugin = isNativeAndroid ? ThemeBillingNative : createWebStub();

export const googlePlayBillingService = {
  startConnection: () => plugin.startConnection(),
  getProductDetails: (productIds?: string[]) => (
    plugin.getProductDetails(productIds?.length ? { productIds } : {})
  ),
  purchaseProduct: (productId: string) => plugin.purchaseTheme({ productId }),
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
  isAvailable: isNativeAndroid,
} as const;
