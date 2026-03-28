// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { maestroPaymentsService } from '../../../services/payments/maestroPaymentsService';
import { googleAuthService } from '../../../services/auth/googleAuthService';
import { maestroAccountService } from '../../../services/account/maestroAccountService';
import type { ManagedAccessSession } from '../../../core/contracts/backend';
import type { GooglePlayPurchaseRecord } from '../../../core/contracts/integrations';

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

const ManagedAccessPanel: React.FC<ManagedAccessPanelProps> = ({ session }) => {
  const { t } = useAppTranslations();
  const billingService = maestroPaymentsService.themeBilling;
  const managedProductIds = useMemo(() => maestroPaymentsService.getManagedBillingProductIds(), []);
  const primaryProductId = managedProductIds[0] || '';
  const verifiedTokensRef = useRef<Set<string>>(new Set());

  const [products, setProducts] = useState<Array<{
    productId: string;
    title: string;
    description: string;
    formattedPrice?: string;
  }>>([]);
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isPurchasing, setIsPurchasing] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const activeProduct = products.find(product => product.productId === primaryProductId) || null;

  const refreshAccount = useCallback(async () => {
    if (!session?.firebaseIdToken) return;
    setIsRefreshing(true);
    setErrorMessage(null);
    try {
      await maestroAccountService.getManagedAccountSummary();
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : t('managedAccess.refreshFailed'));
    } finally {
      setIsRefreshing(false);
    }
  }, [session?.firebaseIdToken, t]);

  const verifyAndConsumePurchases = useCallback(async (purchaseRecords: GooglePlayPurchaseRecord[]) => {
    if (!session?.firebaseIdToken) return;
    const relevantPurchases = purchaseRecords.filter(record => (
      record.purchaseState === 'purchased'
      && managedProductIds.includes(record.productId)
      && !verifiedTokensRef.current.has(record.purchaseToken)
    ));
    if (!relevantPurchases.length) return;

    for (const record of relevantPurchases) {
      verifiedTokensRef.current.add(record.purchaseToken);
      try {
        await maestroPaymentsService.verifyGooglePlayPurchase({ purchase: record });
        await billingService.consumePurchase(record.purchaseToken);
      } catch (error) {
        verifiedTokensRef.current.delete(record.purchaseToken);
        throw error;
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
        void verifyAndConsumePurchases(event.purchases || [])
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
      .then(() => billingService.getOwnedPurchases())
      .then(result => verifyAndConsumePurchases(result.purchases || []))
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
  }, [billingService, managedProductIds, refreshAccount, t, verifyAndConsumePurchases]);

  const handleSignIn = useCallback(async () => {
    setIsSigningIn(true);
    setErrorMessage(null);
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
    try {
      await googleAuthService.signOutManagedSession();
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : t('managedAccess.signOutFailed'));
    }
  }, [t]);

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
    setIsPurchasing(true);
    try {
      await billingService.purchaseProduct(primaryProductId);
    } catch (error) {
      setIsPurchasing(false);
      setErrorMessage(error instanceof Error ? error.message : t('managedAccess.purchaseFailed'));
    }
  }, [billingService, primaryProductId, session?.firebaseIdToken, t]);

  const handleRestore = useCallback(async () => {
    setErrorMessage(null);
    try {
      await billingService.restorePurchases();
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : t('managedAccess.restoreFailed'));
    }
  }, [billingService, t]);

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

        {billingService.isAvailable ? (
          <>
            <button
              type="button"
              onClick={() => void handlePurchase()}
              disabled={!session?.firebaseIdToken || isPurchasing || !primaryProductId}
              className="bg-gate-btn-bg px-3 py-2 text-gate-btn-text hover:bg-gate-btn-bg/80 disabled:opacity-60 sketchy-border-thin"
            >
              {isPurchasing ? t('managedAccess.purchasing') : t('managedAccess.buyCredits')}
            </button>
            <button
              type="button"
              onClick={() => void handleRestore()}
              disabled={!session?.firebaseIdToken}
              className="px-3 py-2 text-gate-text hover:bg-gate-bg disabled:opacity-60 sketchy-border-thin"
            >
              {t('managedAccess.restorePurchases')}
            </button>
          </>
        ) : (
          <div className="text-xs text-gate-muted-text">{t('managedAccess.androidOnly')}</div>
        )}
      </div>

      <div className="text-xs text-gate-muted-text space-y-1">
        <p>{t('managedAccess.keepByok')}</p>
        <p>{t('managedAccess.billingNote')}</p>
      </div>
    </section>
  );
};

export default ManagedAccessPanel;
