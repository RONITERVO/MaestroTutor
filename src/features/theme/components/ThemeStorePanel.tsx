// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import React, { useCallback } from 'react';
import { IconXMark, IconCheck, IconUndo, IconSparkles } from '../../../shared/ui/Icons';
import { useThemeBilling } from '../hooks/useThemeBilling';

interface ThemeStorePanelProps {
  onClose: () => void;
}

/**
 * Bottom-sheet panel that displays purchasable color themes from Google Play.
 *
 * - Shows theme cards with preview colour swatches and Google Play pricing.
 * - Displays a "Buy" button for themes that have not yet been purchased.
 * - Displays an "Owned ✓" badge for themes that have already been purchased.
 * - Triggers Google Play purchase sheet via {@link useThemeBilling}.
 * - "Restore Purchases" button re-queries Google Play to sync ownership.
 * - Not available on non-Android platforms (shows an informational message).
 */
const ThemeStorePanel: React.FC<ThemeStorePanelProps> = ({ onClose }) => {
  const {
    isPurchasing,
    billingError,
    isAvailable,
    purchaseTheme,
    restorePurchases,
    clearError,
    getEnrichedProducts,
  } = useThemeBilling();

  const enrichedProducts = getEnrichedProducts();

  const handleBuy = useCallback(
    async (productId: string) => {
      await purchaseTheme(productId);
    },
    [purchaseTheme],
  );

  const handleRestore = useCallback(async () => {
    clearError();
    await restorePurchases();
  }, [restorePurchases, clearError]);

  return (
    <>
      {/* Backdrop */}
      <div className="fixed inset-0 z-[89] bg-black/20" onClick={onClose} />

      {/* Panel */}
      <div
        className="fixed inset-x-0 bottom-0 z-[90] flex flex-col bg-theme-panel-bg/10 backdrop-blur-md border-t border-line-border shadow-2xl rounded-t-2xl overflow-hidden"
        style={{ maxHeight: '70vh' }}
      >
        {/* Drag handle + header */}
        <div className="flex flex-col items-center pt-2 pb-1 px-4 shrink-0">
          <div className="w-10 h-1 bg-theme-muted-text/30 rounded-full mb-2" />
          <div className="flex w-full items-center justify-between">
            <div className="flex items-center gap-2">
              <IconSparkles className="w-5 h-5 text-theme-panel-text" />
              <h2 className="text-lg font-sketch text-theme-panel-text">Theme Store</h2>
            </div>
            <div className="flex items-center gap-2">
              {/* Restore purchases */}
              <button
                type="button"
                title="Restore Purchases"
                onClick={handleRestore}
                disabled={isPurchasing}
                className="p-1.5 rounded-lg text-theme-muted-text hover:text-theme-panel-text hover:bg-theme-input-bg transition-colors disabled:opacity-40"
              >
                <IconUndo className="w-4 h-4" />
              </button>
              {/* Close */}
              <button
                type="button"
                title="Close"
                onClick={onClose}
                className="p-1.5 rounded-lg text-theme-muted-text hover:text-theme-panel-text hover:bg-theme-input-bg transition-colors"
              >
                <IconXMark className="w-5 h-5" />
              </button>
            </div>
          </div>
        </div>

        {/* Error banner */}
        {billingError && (
          <div className="mx-4 mb-2 px-3 py-2 rounded-lg bg-input-error-bg/15 text-input-error-text text-xs flex items-start gap-2 shrink-0">
            <span className="flex-1">
              Purchase failed. Please check your connection and try again.
            </span>
            <button
              type="button"
              onClick={clearError}
              className="shrink-0 text-input-error-text/70 hover:text-input-error-text"
            >
              <IconXMark className="w-3 h-3" />
            </button>
          </div>
        )}

        {/* Non-Android notice */}
        {!isAvailable && (
          <div className="mx-4 mb-3 px-3 py-2 rounded-lg bg-theme-input-bg text-theme-muted-text text-xs text-center">
            Theme purchases are only available on Android via Google Play.
          </div>
        )}

        {/* Product list */}
        <div className="flex-1 overflow-y-auto px-4 pb-4 space-y-3">
          {enrichedProducts.map(product => (
            <div
              key={product.productId}
              className="flex items-stretch gap-3 p-3 rounded-xl bg-theme-input-bg border border-theme-input-border"
            >
              {/* Color swatches */}
              <div className="flex flex-col gap-1 shrink-0 justify-center">
                {product.previewColors.map((hsl, i) => (
                  <div
                    key={i}
                    className="w-5 h-5 rounded-sm"
                    style={{ backgroundColor: `hsl(${hsl})` }}
                  />
                ))}
              </div>

              {/* Details */}
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-1.5">
                  <span className="text-base">{product.icon}</span>
                  <span className="font-sketch text-theme-panel-text text-sm leading-tight">
                    {product.displayName}
                  </span>
                  {product.owned && (
                    <span className="ml-auto flex items-center gap-0.5 text-xs text-flag-busy-text bg-flag-busy-bg/20 px-1.5 py-0.5 rounded-full shrink-0">
                      <IconCheck className="w-3 h-3" />
                      Owned
                    </span>
                  )}
                </div>
                <p className="text-xs text-theme-muted-text mt-0.5 leading-snug">
                  {product.description}
                </p>
                {product.formattedPrice && !product.owned && (
                  <p className="text-xs text-theme-muted-text mt-1 font-medium">
                    {product.formattedPrice}
                  </p>
                )}
              </div>

              {/* Action button */}
              <div className="shrink-0 flex items-center">
                {product.owned ? (
                  <div className="p-2 rounded-lg text-flag-busy-text">
                    <IconCheck className="w-5 h-5" />
                  </div>
                ) : (
                  <button
                    type="button"
                    disabled={isPurchasing || !isAvailable}
                    onClick={() => handleBuy(product.productId)}
                    className="px-3 py-1.5 rounded-lg bg-gate-btn-bg text-gate-btn-text text-xs font-medium active:opacity-80 disabled:opacity-40 transition-opacity"
                  >
                    {isPurchasing ? '…' : 'Buy'}
                  </button>
                )}
              </div>
            </div>
          ))}

          {enrichedProducts.length === 0 && (
            <p className="text-center text-theme-muted-text text-sm py-6">
              Loading themes…
            </p>
          )}
        </div>

        {/* Footer note */}
        <div className="px-4 pb-4 pt-1 shrink-0">
          <p className="text-center text-theme-muted-text/60 text-[10px] leading-tight">
            Purchases are managed by Google Play and tied to your Google account.{' '}
            Tap the restore button (↩) to recover past purchases on reinstall.
          </p>
        </div>
      </div>
    </>
  );
};

export default ThemeStorePanel;
