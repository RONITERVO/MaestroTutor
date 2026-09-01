// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Capacitor } from '@capacitor/core';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { maestroPaymentsService } from '../../../services/payments/maestroPaymentsService';
import { googleAuthService } from '../../../services/auth/googleAuthService';
import { maestroAccountService } from '../../../services/account/maestroAccountService';
import { maestroBackendService } from '../../../services/backend/maestroBackendService';
import type { ManagedAccessSession } from '../../../core/contracts/backend';
import type { GooglePlayPurchaseRecord } from '../../../core/contracts/integrations';
import {
  loadPendingManagedPurchases,
  removePendingManagedPurchase,
  upsertPendingManagedPurchase,
} from '../../../core/security/pendingManagedPurchasesStorage';

interface ManagedAccessPanelProps {
  session: ManagedAccessSession | null;
}

const formatCredits = (value: number): string => (
  value.toLocaleString(undefined, {
    minimumFractionDigits: value % 1 === 0 ? 0 : 2,
    maximumFractionDigits: 2,
  })
);

const formatUsd = (value: number): string => (
  value.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
);

const sha256Hex = async (value: string): Promise<string> => {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(value));
  return [...new Uint8Array(digest)]
    .map(byte => byte.toString(16).padStart(2, '0'))
    .join('');
};

const dedupePurchaseRecords = (purchaseRecords: GooglePlayPurchaseRecord[]): GooglePlayPurchaseRecord[] => {
  const recordsByToken = new Map<string, GooglePlayPurchaseRecord>();
  for (const purchase of purchaseRecords) {
    if (!purchase.purchaseToken) continue;
    recordsByToken.set(purchase.purchaseToken, purchase);
  }
  return [...recordsByToken.values()];
};

const ManagedAccessPanel: React.FC<ManagedAccessPanelProps> = ({ session }) => {
  const { t } = useAppTranslations();
  const billingService = maestroPaymentsService.themeBilling;
  const managedProductIds = useMemo(() => maestroPaymentsService.getManagedBillingProductIds(), []);
  const primaryProductId = managedProductIds[0] || '';
  const webBillingAvailable = (
    !Capacitor.isNativePlatform()
    && maestroBackendService.isConfigured()
    && Boolean(primaryProductId)
  );
  const purchasingAvailable = billingService.isAvailable || webBillingAvailable;
  const processingTokensRef = useRef<Set<string>>(new Set());
  const completedTokensRef = useRef<Set<string>>(new Set());

  const [products, setProducts] = useState<Array<{
    productId: string;
    title: string;
    description: string;
    formattedPrice?: string;
  }>>([]);
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isPurchasing, setIsPurchasing] = useState(false);
  const [isDeletingAccount, setIsDeletingAccount] = useState(false);
  const [isDeleteConfirmOpen, setIsDeleteConfirmOpen] = useState(false);
  const [deleteConfirmationText, setDeleteConfirmationText] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const activeProduct = products.find(product => product.productId === primaryProductId) || null;

  const refreshAccount = useCallback(async () => {
    if (!session?.firebaseIdToken) return;
    setIsRefreshing(true);
    setErrorMessage(null);
    setStatusMessage(null);
    try {
      await maestroAccountService.getManagedAccountSummary();
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : t('managedAccess.refreshFailed'));
    } finally {
      setIsRefreshing(false);
    }
  }, [session?.firebaseIdToken, t]);
  const refreshAccountRef = useRef(refreshAccount);

  useEffect(() => {
    refreshAccountRef.current = refreshAccount;
  }, [refreshAccount]);

  const processPendingPurchases = useCallback(async (purchaseRecords: GooglePlayPurchaseRecord[]) => {
    if (!session?.firebaseIdToken) return;
    const pendingPurchases = await loadPendingManagedPurchases();
    const relevantPurchases = dedupePurchaseRecords([
      ...pendingPurchases.map(record => record.purchase),
      ...purchaseRecords,
    ]).filter(record => (
      record.purchaseState === 'purchased'
      && managedProductIds.includes(record.productId)
      && !completedTokensRef.current.has(record.purchaseToken)
    ));

    for (const record of relevantPurchases) {
      if (processingTokensRef.current.has(record.purchaseToken)) {
        continue;
      }
      processingTokensRef.current.add(record.purchaseToken);
      try {
        // Recorded locally first, so a purchase survives the app being killed
        // between Play confirming it and the backend hearing about it.
        await upsertPendingManagedPurchase(record);

        // The token is handed straight to the backend, which verifies it with
        // Play, grants the credits and only then consumes it. The client
        // deliberately does not consume: consumption is irreversible, so
        // consuming before the grant meant any failure in between took the
        // money and destroyed the token with nothing left to retry against.
        // Granting is keyed on the token, so retrying this is a no-op.
        await maestroPaymentsService.verifyGooglePlayPurchase({ purchase: record });
        completedTokensRef.current.add(record.purchaseToken);
        await removePendingManagedPurchase(record.purchaseToken);
      } catch (error) {
        throw error;
      } finally {
        processingTokensRef.current.delete(record.purchaseToken);
      }
    }
  }, [billingService, managedProductIds, session?.firebaseIdToken]);

  useEffect(() => {
    if (!billingService.isAvailable || managedProductIds.length === 0) return undefined;

    let mounted = true;
    const listenerPromises: Array<Promise<{ remove: () => void }>> = [];

    listenerPromises.push(
      billingService.onProductDetailsAvailable(event => {
        if (!mounted) return;
        setProducts(event.products.filter(product => managedProductIds.includes(product.productId)));
      }),
      billingService.onPurchasesUpdated(event => {
        if (!mounted) return;
        setIsPurchasing(false);
        void processPendingPurchases(event.purchases || [])
          .then(() => refreshAccount())
          .catch(error => {
            setErrorMessage(error instanceof Error ? error.message : t('managedAccess.purchaseSyncFailed'));
          });
      }),
      billingService.onBillingError(event => {
        if (!mounted) return;
        setIsPurchasing(false);
        setErrorMessage(event.debugMessage || t('managedAccess.genericError'));
      }),
    );

    void billingService.startConnection()
      .then(() => billingService.getProductDetails(managedProductIds))
      .then(() => processPendingPurchases([]))
      .then(() => billingService.getOwnedPurchases())
      .then(result => processPendingPurchases(result.purchases || []))
      .catch(error => {
        if (!mounted) return;
        setErrorMessage(error instanceof Error ? error.message : t('managedAccess.billingUnavailable'));
      });

    return () => {
      mounted = false;
      Promise.all(listenerPromises).then(handles => {
        handles.forEach(handle => handle.remove());
      });
    };
  }, [billingService, managedProductIds, processPendingPurchases, refreshAccount, t]);

  const handleSignIn = useCallback(async () => {
    setIsSigningIn(true);
    setErrorMessage(null);
    setStatusMessage(null);
    try {
      await googleAuthService.beginSignIn();
      await refreshAccount();
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : t('managedAccess.signInFailed'));
    } finally {
      setIsSigningIn(false);
    }
  }, [refreshAccount, t]);

  const handleSignOut = useCallback(async () => {
    setErrorMessage(null);
    setStatusMessage(null);
    try {
      await googleAuthService.signOutManagedSession();
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : t('managedAccess.signOutFailed'));
    }
  }, [t]);

  /**
   * Buy credits, by whichever route this platform allows.
   *
   * Android must use Play Billing — Google's payments policy requires it for
   * purchases made inside the app — while the web goes to Stripe Checkout.
   * Both fund the same credit balance, so which one ran is invisible
   * afterwards.
   */
  const handlePurchase = useCallback(async () => {
    if (!session?.firebaseIdToken) {
      setErrorMessage(t('managedAccess.signInRequired'));
      return;
    }
    if (!primaryProductId) {
      setErrorMessage(t('managedAccess.productMissing'));
      return;
    }

    setErrorMessage(null);
    setStatusMessage(null);
    setIsPurchasing(true);
    try {
      if (billingService.isAvailable) {
        const obfuscatedAccountId = await sha256Hex(session.user.id);
        await billingService.purchaseProduct(primaryProductId, obfuscatedAccountId);
        // Play drives the rest through the purchase listener, which keeps the
        // spinner up until the purchase is reconciled.
        return;
      }

      // Navigates away to Stripe, so the spinner is never cleared on success.
      await maestroPaymentsService.startStripeCheckout(primaryProductId);
    } catch (error) {
      setIsPurchasing(false);
      setErrorMessage(error instanceof Error ? error.message : t('managedAccess.purchaseFailed'));
    }
  }, [billingService, primaryProductId, session, t]);

  /*
   * Coming back from Stripe, the credits may not have landed yet: the webhook
   * is a separate call from Stripe to the backend and can arrive either side of
   * the redirect. Refreshing a few times covers the usual gap without making
   * the user wonder whether their money went somewhere.
   */
  useEffect(() => {
    if (typeof window === 'undefined') return undefined;
    const params = new URLSearchParams(window.location.search);
    if (params.get('billing') !== 'success') return undefined;

    // Drop the marker so a later reload does not re-trigger this.
    params.delete('billing');
    const query = params.toString();
    window.history.replaceState({}, '', `${window.location.pathname}${query ? `?${query}` : ''}`);

    setStatusMessage(t('managedAccess.checkoutComplete') || 'Payment received. Adding your credits…');
    let attempt = 0;
    const timer = window.setInterval(() => {
      attempt += 1;
      void refreshAccountRef.current();
      if (attempt >= 5) window.clearInterval(timer);
    }, 2000);
    return () => window.clearInterval(timer);
    // Deliberately runs once on mount: it reacts to the redirect that brought
    // the user here, not to anything that changes afterwards.
  }, []);

  const handleRestore = useCallback(async () => {
    setErrorMessage(null);
    setStatusMessage(null);
    try {
      await billingService.restorePurchases();
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : t('managedAccess.restoreFailed'));
    }
  }, [billingService, t]);

  const handleDeleteManagedAccount = useCallback(async () => {
    if (!session?.firebaseIdToken) {
      setErrorMessage(t('managedAccess.signInRequired'));
      return;
    }
    if (deleteConfirmationText.trim().toUpperCase() !== 'DELETE') {
      setErrorMessage(t('managedAccess.deleteNeedsConfirmation'));
      return;
    }

    setIsDeletingAccount(true);
    setErrorMessage(null);
    setStatusMessage(null);
    try {
      await maestroBackendService.deleteManagedAccount();
      await googleAuthService.signOutManagedSession();
      setDeleteConfirmationText('');
      setIsDeleteConfirmOpen(false);
      setStatusMessage(t('managedAccess.deleteSuccess'));
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : t('managedAccess.deleteFailed'));
    } finally {
      setIsDeletingAccount(false);
    }
  }, [deleteConfirmationText, session?.firebaseIdToken, t]);

  return (
    <section className="bg-gate-input-bg/70 p-4 text-sm text-gate-text space-y-3 sketchy-border-thin">
      <div className="space-y-1">
        <div className="font-medium text-gate-text font-sketch">{t('managedAccess.title')}</div>
        <p className="text-gate-muted-text">{t('managedAccess.description')}</p>
      </div>

      <div className="grid gap-2 text-xs sm:text-sm">
        <div className="flex items-center justify-between gap-3">
          <span className="text-gate-muted-text">{t('managedAccess.statusLabel')}</span>
          <span className="font-medium">{session?.user.email || t('managedAccess.notSignedIn')}</span>
        </div>
        <div className="flex items-center justify-between gap-3">
          <span className="text-gate-muted-text">{t('managedAccess.balanceLabel')}</span>
          <span className="font-medium">
            {formatCredits(session?.billingSummary.availableCredits || 0)} {t('managedAccess.creditsUnit')}
          </span>
        </div>
        <div className="flex items-center justify-between gap-3">
          <span className="text-gate-muted-text">{t('managedAccess.spentLabel')}</span>
          <span className="font-medium">
            {formatCredits(session?.billingSummary.lifetimeSpentCredits || 0)} {t('managedAccess.creditsUnit')}
            {' / $'}
            {formatUsd(session?.billingSummary.lifetimeSpentUsd || 0)}
          </span>
        </div>
      </div>

      {activeProduct && (
        <div className="rounded-md border border-line-border/50 bg-gate-bg/80 px-3 py-2 text-xs sm:text-sm">
          <div className="font-medium text-gate-text">{activeProduct.title || t('managedAccess.packTitle')}</div>
          <div className="text-gate-muted-text">
            {t('managedAccess.packDescription')}
            {activeProduct.formattedPrice ? ` - ${activeProduct.formattedPrice}` : ''}
          </div>
        </div>
      )}

      {statusMessage && (
        <div className="rounded-md border border-emerald-300 bg-emerald-50 px-3 py-2 text-xs text-emerald-900">
          {statusMessage}
        </div>
      )}

      {errorMessage && (
        <div className="rounded-md border border-red-300 bg-red-50 px-3 py-2 text-xs text-red-800">
          {errorMessage}
        </div>
      )}

      <div className="flex flex-wrap gap-2">
        {!session?.firebaseIdToken ? (
          <button
            type="button"
            onClick={() => void handleSignIn()}
            disabled={isSigningIn}
            className="bg-gate-btn-bg px-3 py-2 text-gate-btn-text hover:bg-gate-btn-bg/80 disabled:opacity-60 sketchy-border-thin"
          >
            {isSigningIn ? t('managedAccess.signingIn') : t('managedAccess.signIn')}
          </button>
        ) : (
          <>
            <button
              type="button"
              onClick={() => void refreshAccount()}
              disabled={isRefreshing}
              className="px-3 py-2 text-gate-text hover:bg-gate-bg disabled:opacity-60 sketchy-border-thin"
            >
              {isRefreshing ? t('managedAccess.refreshing') : t('managedAccess.refresh')}
            </button>
            <button
              type="button"
              onClick={() => void handleSignOut()}
              className="px-3 py-2 text-gate-text hover:bg-gate-bg sketchy-border-thin"
            >
              {t('managedAccess.signOut')}
            </button>
          </>
        )}

        {purchasingAvailable ? (
          <>
            <button
              type="button"
              onClick={() => void handlePurchase()}
              disabled={!session?.firebaseIdToken || isPurchasing || !primaryProductId}
              className="bg-gate-btn-bg px-3 py-2 text-gate-btn-text hover:bg-gate-btn-bg/80 disabled:opacity-60 sketchy-border-thin"
            >
              {isPurchasing ? t('managedAccess.purchasing') : t('managedAccess.buyCredits')}
            </button>
            {billingService.isAvailable && (
              <button
                type="button"
                onClick={() => void handleRestore()}
                disabled={!session?.firebaseIdToken}
                className="px-3 py-2 text-gate-text hover:bg-gate-bg disabled:opacity-60 sketchy-border-thin"
              >
                {t('managedAccess.restorePurchases')}
              </button>
            )}
          </>
        ) : (
          <div className="text-xs text-gate-muted-text">{t('managedAccess.androidOnly')}</div>
        )}
      </div>

      <div className="text-xs text-gate-muted-text space-y-1">
        <p>{t('managedAccess.keepByok')}</p>
        <p>{t('managedAccess.billingNote')}</p>
      </div>

      {session?.firebaseIdToken && (
        <div className="rounded-md border border-red-300/80 bg-red-50/70 px-3 py-3 space-y-3">
          <div className="space-y-1">
            <div className="font-medium text-red-900">{t('managedAccess.deleteTitle')}</div>
            <p className="text-xs text-red-900/80">{t('managedAccess.deleteDescription')}</p>
          </div>

          {!isDeleteConfirmOpen ? (
            <button
              type="button"
              onClick={() => {
                setStatusMessage(null);
                setErrorMessage(null);
                setIsDeleteConfirmOpen(true);
              }}
              className="px-3 py-2 text-red-900 hover:bg-red-100 sketchy-border-thin"
            >
              {t('managedAccess.deleteAction')}
            </button>
          ) : (
            <div className="space-y-2">
              <p className="text-xs text-red-900/80">{t('managedAccess.deleteConfirmHint')}</p>
              <input
                type="text"
                value={deleteConfirmationText}
                onChange={(event) => setDeleteConfirmationText(event.target.value)}
                placeholder="DELETE"
                className="w-full px-3 py-2 bg-white text-base text-gate-text border border-red-300 rounded-none focus:outline-none focus:ring-1 focus:ring-red-400 sm:text-sm"
              />
              <div className="flex flex-wrap gap-2">
                <button
                  type="button"
                  onClick={() => {
                    setDeleteConfirmationText('');
                    setIsDeleteConfirmOpen(false);
                    setErrorMessage(null);
                  }}
                  className="px-3 py-2 text-gate-text hover:bg-gate-bg sketchy-border-thin"
                >
                  {t('managedAccess.deleteCancel')}
                </button>
                <button
                  type="button"
                  onClick={() => void handleDeleteManagedAccount()}
                  disabled={isDeletingAccount || deleteConfirmationText.trim().toUpperCase() !== 'DELETE'}
                  className="px-3 py-2 bg-red-700 text-white hover:bg-red-800 disabled:opacity-60 sketchy-border-thin"
                >
                  {isDeletingAccount ? t('managedAccess.deleting') : t('managedAccess.deleteConfirm')}
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </section>
  );
};

export default ManagedAccessPanel;
