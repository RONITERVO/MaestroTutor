// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Capacitor } from '@capacitor/core';
import { Browser } from '@capacitor/browser';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { maestroPaymentsService } from '../../../services/payments/maestroPaymentsService';
import { maestroManagedAccountController } from '../../../services/account/maestroManagedAccountController';
import { maestroBackendService } from '../../../services/backend/maestroBackendService';
import type { ManagedAccessSession } from '../../../core/contracts/backend';
import ManagedAccountActivityModal from './ManagedAccountActivityModal';

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
  const primaryPackId = maestroPaymentsService.getManagedCreditPackIds()[0] || '';
  const isNative = Capacitor.isNativePlatform();
  const nativeExternalCheckoutEnabled = isNative
    && maestroPaymentsService.isAndroidExternalCheckoutEnabled();
  const purchasingAvailable = (
    maestroBackendService.isConfigured()
    && Boolean(primaryPackId)
    && (!isNative || nativeExternalCheckoutEnabled)
  );
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isPurchasing, setIsPurchasing] = useState(false);
  const [isDeletingAccount, setIsDeletingAccount] = useState(false);
  const [isDeleteConfirmOpen, setIsDeleteConfirmOpen] = useState(false);
  const [isActivityOpen, setIsActivityOpen] = useState(false);
  const [deleteConfirmationText, setDeleteConfirmationText] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const refreshAccount = useCallback(async () => {
    if (!session?.firebaseIdToken) return;
    setIsRefreshing(true);
    setErrorMessage(null);
    setStatusMessage(null);
    try {
      await maestroManagedAccountController.refreshAccount();
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : t('managedAccess.refreshFailed'));
    } finally {
      setIsRefreshing(false);
    }
  }, [session?.firebaseIdToken, t]);
  const refreshAccountRef = useRef(refreshAccount);
  const translationRef = useRef(t);
  const activeNativeCheckoutRef = useRef(false);
  const stripeReturnPollRef = useRef<ReturnType<
    typeof maestroManagedAccountController.startStripeReturnPolling
  > | null>(null);

  useEffect(() => {
    refreshAccountRef.current = refreshAccount;
  }, [refreshAccount]);

  useEffect(() => {
    translationRef.current = t;
  }, [t]);

  useEffect(() => {
    if (!nativeExternalCheckoutEnabled) return undefined;
    let disposed = false;
    let handle: { remove: () => Promise<void> } | null = null;
    const onBrowserFinished = () => {
      if (!activeNativeCheckoutRef.current || disposed) return;
      activeNativeCheckoutRef.current = false;
      setIsPurchasing(false);
      setStatusMessage(translationRef.current('managedAccess.checkoutReturn'));
      stripeReturnPollRef.current?.cancel();
      stripeReturnPollRef.current = maestroManagedAccountController.startStripeReturnPolling({
        attempts: 15,
        intervalMs: 2000,
        refresh: async () => {
          await refreshAccountRef.current();
          return null;
        },
      });
    };
    void Browser.addListener('browserFinished', onBrowserFinished)
      .then(async listener => {
        if (disposed) {
          await listener.remove();
          return;
        }
        handle = listener;
      })
      .catch(() => {
        if (!disposed) setErrorMessage(translationRef.current('managedAccess.purchaseFailed'));
      });

    return () => {
      disposed = true;
      activeNativeCheckoutRef.current = false;
      stripeReturnPollRef.current?.cancel();
      stripeReturnPollRef.current = null;
      void handle?.remove();
    };
  }, [nativeExternalCheckoutEnabled]);

  const handleSignIn = useCallback(async () => {
    setIsSigningIn(true);
    setErrorMessage(null);
    setStatusMessage(null);
    try {
      await maestroManagedAccountController.signIn();
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
      await maestroManagedAccountController.signOut();
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : t('managedAccess.signOutFailed'));
    }
  }, [t]);

  /**
   * Every enabled platform creates the same Stripe Checkout session. Android
   * opens it in a Custom Tab only when the release is explicitly enrolled for
   * external checkout; Stripe's webhook remains the sole grant authority.
   */
  const handlePurchase = useCallback(async () => {
    if (!session?.firebaseIdToken) {
      setErrorMessage(t('managedAccess.signInRequired'));
      return;
    }
    if (!primaryPackId) {
      setErrorMessage(t('managedAccess.productMissing'));
      return;
    }

    setErrorMessage(null);
    setStatusMessage(null);
    setIsPurchasing(true);
    if (nativeExternalCheckoutEnabled) activeNativeCheckoutRef.current = true;
    try {
      // Web navigation leaves the page. Native navigation returns after the
      // Custom Tab opens and browserFinished performs reconciliation.
      await maestroManagedAccountController.startStripeCheckout(primaryPackId);
    } catch (error) {
      activeNativeCheckoutRef.current = false;
      setIsPurchasing(false);
      setErrorMessage(error instanceof Error ? error.message : t('managedAccess.purchaseFailed'));
    }
  }, [nativeExternalCheckoutEnabled, primaryPackId, session, t]);

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
    const poll = maestroManagedAccountController.startStripeReturnPolling({
      attempts: 5,
      intervalMs: 2000,
      // Keep the callback current without making token refreshes restart the
      // redirect reconciliation effect.
      refresh: async () => {
        await refreshAccountRef.current();
        return null;
      },
    });
    return poll.cancel;
    // Deliberately runs once on mount: it reacts to the redirect that brought
    // the user here, not to anything that changes afterwards.
  }, []);

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
      await maestroManagedAccountController.deleteAccount({
        confirmation: deleteConfirmationText,
        actualUserId: session.user.id,
      });
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
              onClick={() => setIsActivityOpen(true)}
              className="px-3 py-2 text-gate-text hover:bg-gate-bg focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin"
            >
              {t('managedAccess.activityAction')}
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
              disabled={!session?.firebaseIdToken || isPurchasing || !primaryPackId}
              className="bg-gate-btn-bg px-3 py-2 text-gate-btn-text hover:bg-gate-btn-bg/80 disabled:opacity-60 sketchy-border-thin"
            >
              {isPurchasing ? t('managedAccess.purchasing') : t('managedAccess.buyCredits')}
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
      <ManagedAccountActivityModal
        isOpen={isActivityOpen}
        onClose={() => setIsActivityOpen(false)}
      />
    </section>
  );
};

export default ManagedAccessPanel;
