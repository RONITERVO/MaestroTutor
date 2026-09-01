// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import type { GooglePlayPurchaseRecord } from '../../core/contracts/integrations';
import { getManagedBillingNativePlugin, isNativeManagedBilling } from './managedBillingNativePlugin';

export interface ProductDetailsResult {
  productId: string;
  title: string;
  description: string;
  formattedPrice?: string;
  priceAmountMicros?: number;
  priceCurrencyCode?: string;
}

export interface PurchasesUpdatedEvent {
  purchases: GooglePlayPurchaseRecord[];
}

export interface ProductDetailsAvailableEvent {
  products: ProductDetailsResult[];
}

export interface BillingErrorEvent {
  responseCode: number;
  debugMessage: string;
}

export interface UnconsumedPurchasesResult {
  purchases: GooglePlayPurchaseRecord[];
}

interface ManagedBillingPluginInterface {
  startConnection(): Promise<void>;
  getProductDetails(options: { productIds: string[] }): Promise<void>;
  purchaseProduct(options: { productId: string; obfuscatedAccountId: string }): Promise<void>;
  restorePurchases(): Promise<void>;
  getUnconsumedPurchases(): Promise<UnconsumedPurchasesResult>;
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

const ManagedBillingNative = getManagedBillingNativePlugin<ManagedBillingPluginInterface>();

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

const createWebStub = (): ManagedBillingPluginInterface => ({
  // Read-only calls resolve empty off Android; web purchases use Stripe.
  startConnection: async () => {},
  getProductDetails: async () => {},
  // Anything that would need Play to complete must reject, not resolve.
  purchaseProduct: unavailable,
  restorePurchases: async () => {},
  getUnconsumedPurchases: async () => ({ purchases: [] }),
  addListener: async () => ({ remove: () => {} }),
  removeAllListeners: async () => {},
});

const plugin = isNativeManagedBilling && ManagedBillingNative ? ManagedBillingNative : createWebStub();

export const googlePlayBillingService = {
  startConnection: () => plugin.startConnection(),
  getProductDetails: (productIds: string[]) => plugin.getProductDetails({ productIds }),
  purchaseProduct: (productId: string, obfuscatedAccountId: string) => (
    plugin.purchaseProduct({ productId, obfuscatedAccountId })
  ),
  restorePurchases: () => plugin.restorePurchases(),
  getUnconsumedPurchases: (): Promise<UnconsumedPurchasesResult> => (
    plugin.getUnconsumedPurchases()
  ),
  onPurchasesUpdated: (cb: (event: PurchasesUpdatedEvent) => void) => plugin.addListener('purchasesUpdated', cb),
  onProductDetailsAvailable: (cb: (event: ProductDetailsAvailableEvent) => void) => plugin.addListener('productDetailsAvailable', cb),
  onBillingError: (cb: (event: BillingErrorEvent) => void) => plugin.addListener('billingError', cb),
  removeAllListeners: () => plugin.removeAllListeners(),
  isAvailable: isNativeManagedBilling,
} as const;
