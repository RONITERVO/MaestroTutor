// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ThemeBillingService,
  type BillingErrorEvent,
  type ProductDetailsResult,
} from '../ThemeBillingService';
import { THEME_PRODUCTS, type ThemeProduct } from '../config/themeProducts';

export interface ThemeBillingState {
  /** Product details fetched from Google Play (may be empty if unavailable). */
  products: ProductDetailsResult[];
  /** Set of owned product IDs according to the latest Google Play query. */
  ownedProductIds: Set<string>;
  /** True while a purchase or restore is in progress. */
  isPurchasing: boolean;
  /** Last billing error, or null if none. */
  billingError: BillingErrorEvent | null;
  /** True on Android where billing is available. */
  isAvailable: boolean;
}

export interface ThemeBillingActions {
  /** Launch the Google Play purchase sheet for the given theme. */
  purchaseTheme: (productId: string) => Promise<void>;
  /** Re-query Google Play to restore purchases. */
  restorePurchases: () => Promise<void>;
  /** Clear the current billing error. */
  clearError: () => void;
  /** Merge static product catalogue with live pricing from Google Play. */
  getEnrichedProducts: () => Array<ThemeProduct & { formattedPrice?: string; owned: boolean }>;
}

/**
 * React hook that manages Google Play Billing state for theme purchases.
 *
 * Features:
 * - Subscribes to native billing events (purchases, errors, product details).
 * - Exposes enriched product list with live pricing from Google Play.
 * - Provides typed actions for purchase and restore.
 * - Cleans up all listeners on unmount.
 */
export function useThemeBilling(): ThemeBillingState & ThemeBillingActions {
  const [products, setProducts] = useState<ProductDetailsResult[]>([]);
  const [ownedProductIds, setOwnedProductIds] = useState<Set<string>>(new Set());
  const [isPurchasing, setIsPurchasing] = useState(false);
  const [billingError, setBillingError] = useState<BillingErrorEvent | null>(null);

  // Track mounted state to avoid setting state after unmount.
  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;

    const listenerHandles: Array<Promise<{ remove: () => void }>> = [];

    // --- subscriptions ------------------------------------------------ //

    const purchasesHandle = ThemeBillingService.onPurchasesUpdated(event => {
      if (!mountedRef.current) return;
      setOwnedProductIds(new Set(event.ownedProductIds));
      setIsPurchasing(false);
    });

    const productDetailsHandle = ThemeBillingService.onProductDetailsAvailable(event => {
      if (!mountedRef.current) return;
      setProducts(event.products);
    });

    const errorHandle = ThemeBillingService.onBillingError(event => {
      if (!mountedRef.current) return;
      setBillingError(event);
      setIsPurchasing(false);
    });

    listenerHandles.push(purchasesHandle, productDetailsHandle, errorHandle);

    // --- initial data -------------------------------------------------- //
    // Fetch owned themes from the local cache immediately.
    ThemeBillingService.getOwnedThemes().then(result => {
      if (mountedRef.current) {
        setOwnedProductIds(new Set(result.ownedProductIds));
      }
    });

    // Request product details (results arrive via event).
    ThemeBillingService.getProductDetails();

    // Restore purchases from Google Play on mount (authoritative source).
    ThemeBillingService.restorePurchases();

    return () => {
      mountedRef.current = false;
      // Remove all listeners.
      Promise.all(listenerHandles).then(handles => {
        handles.forEach(h => h.remove());
      });
    };
  }, []);

  // ------------------------------------------------------------------ //
  //  Actions                                                             //
  // ------------------------------------------------------------------ //

  const purchaseTheme = useCallback(async (productId: string) => {
    setBillingError(null);
    setIsPurchasing(true);
    try {
      await ThemeBillingService.purchaseTheme(productId);
      // Purchase result arrives via the purchasesUpdated event.
    } catch (err) {
      if (mountedRef.current) {
        setBillingError({ responseCode: -1, debugMessage: String(err) });
        setIsPurchasing(false);
      }
    }
  }, []);

  const restorePurchases = useCallback(async () => {
    setBillingError(null);
    await ThemeBillingService.restorePurchases();
    // Results arrive via the purchasesUpdated event.
  }, []);

  const clearError = useCallback(() => setBillingError(null), []);

  const getEnrichedProducts = useCallback(() => {
    return THEME_PRODUCTS.map(themeProduct => {
      const liveDetails = products.find(p => p.productId === themeProduct.productId);
      return {
        ...themeProduct,
        formattedPrice: liveDetails?.formattedPrice,
        owned: ownedProductIds.has(themeProduct.productId),
      };
    });
  }, [products, ownedProductIds]);

  return {
    products,
    ownedProductIds,
    isPurchasing,
    billingError,
    isAvailable: ThemeBillingService.isAvailable,
    purchaseTheme,
    restorePurchases,
    clearError,
    getEnrichedProducts,
  };
}
