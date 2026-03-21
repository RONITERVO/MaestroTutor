// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { registerPlugin, Capacitor } from '@capacitor/core';

// ------------------------------------------------------------------ //
//  Plugin interface types                                              //
// ------------------------------------------------------------------ //

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
}

export interface ProductDetailsAvailableEvent {
  products: ProductDetailsResult[];
}

export interface BillingErrorEvent {
  responseCode: number;
  debugMessage: string;
}

export interface OwnedThemesResult {
  ownedProductIds: string[];
}

export interface IsThemeOwnedResult {
  owned: boolean;
}

interface ThemeBillingPluginInterface {
  startConnection(): Promise<void>;
  getProductDetails(): Promise<void>;
  purchaseTheme(options: { productId: string }): Promise<void>;
  restorePurchases(): Promise<void>;
  isThemeOwned(options: { productId: string }): Promise<IsThemeOwnedResult>;
  getOwnedThemes(): Promise<OwnedThemesResult>;
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

// Register the native plugin (only active on Android; no-ops on web/iOS).
const ThemeBillingNative = registerPlugin<ThemeBillingPluginInterface>('ThemeBilling');

// ------------------------------------------------------------------ //
//  Web stub — used when running in a browser / non-Android platform   //
// ------------------------------------------------------------------ //

const isNativeAndroid = Capacitor.isNativePlatform() && Capacitor.getPlatform() === 'android';

/** Returns a stable no-op stub for non-Android platforms. */
function createWebStub(): ThemeBillingPluginInterface {
  const listeners: Array<{ remove: () => void }> = [];
  return {
    startConnection: async () => {},
    getProductDetails: async () => {},
    purchaseTheme: async () => {
      console.warn('[ThemeBilling] Purchases are only available on Android.');
    },
    restorePurchases: async () => {},
    isThemeOwned: async () => ({ owned: false }),
    getOwnedThemes: async () => ({ ownedProductIds: [] }),
    addListener: async (_event, _cb) => {
      const handle = { remove: () => {} };
      listeners.push(handle);
      return handle;
    },
    removeAllListeners: async () => {},
  };
}

const plugin: ThemeBillingPluginInterface = isNativeAndroid
  ? ThemeBillingNative
  : createWebStub();

// ------------------------------------------------------------------ //
//  Public service API                                                  //
// ------------------------------------------------------------------ //

/**
 * ThemeBillingService — thin wrapper around the native {@code ThemeBillingPlugin}.
 *
 * Use this service from React hooks / components instead of calling the
 * plugin directly so that:
 *  - Web / browser builds receive harmless no-ops.
 *  - Future changes to the plugin API only require updating this file.
 */
export const ThemeBillingService = {
  /** Initialise the billing client. Called automatically by the plugin on load. */
  startConnection: () => plugin.startConnection(),

  /** Trigger an async query for product details. Results arrive via the listener. */
  getProductDetails: () => plugin.getProductDetails(),

  /** Launch the Google Play purchase sheet for the given product ID. */
  purchaseTheme: (productId: string) => plugin.purchaseTheme({ productId }),

  /** Re-query Google Play and refresh the owned themes cache. */
  restorePurchases: () => plugin.restorePurchases(),

  /** Check whether a specific theme is owned (local cache). */
  isThemeOwned: (productId: string) => plugin.isThemeOwned({ productId }),

  /** Return all locally cached owned theme IDs. */
  getOwnedThemes: () => plugin.getOwnedThemes(),

  /** Subscribe to owned-theme changes (fires after purchase or restore). */
  onPurchasesUpdated: (cb: (event: PurchasesUpdatedEvent) => void) =>
    plugin.addListener('purchasesUpdated', cb),

  /** Subscribe to product details being ready. */
  onProductDetailsAvailable: (cb: (event: ProductDetailsAvailableEvent) => void) =>
    plugin.addListener('productDetailsAvailable', cb),

  /** Subscribe to billing errors. */
  onBillingError: (cb: (event: BillingErrorEvent) => void) =>
    plugin.addListener('billingError', cb),

  /** Remove all active listeners (call in cleanup / unmount). */
  removeAllListeners: () => plugin.removeAllListeners(),

  /** Whether billing is available on the current platform. */
  isAvailable: isNativeAndroid,
} as const;
