// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Managed access, compressed to one row of the API-key card.
 *
 * The card's shape is its argument: fetch a key, paste a key. Managed access is
 * the alternative to that, so it gets the same weight — one control, matching
 * the row below it, with the balance carried as a preview on the button that
 * opens the rest. Everything wordy (the account table, purchases, sign-out,
 * deletion, and the notices explaining them) lives in ManagedAccountModal.
 *
 * The Stripe listeners stay here rather than in the modal: a checkout that
 * returns while the modal is closed must still reconcile.
 */
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Capacitor } from '@capacitor/core';
import { Browser } from '@capacitor/browser';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { maestroPaymentsService } from '../../../services/payments/maestroPaymentsService';
import { maestroManagedAccountController } from '../../../services/account/maestroManagedAccountController';
import { maestroBackendService } from '../../../services/backend/maestroBackendService';
import type { ManagedAccessSession } from '../../../core/contracts/backend';
import { IconArrowPath, IconCreditCard, IconExclamationTriangle } from '../../../shared/ui/Icons';
import { describeManagedAccessError } from '../../../shared/utils/managedAccessErrors';
import ManagedAccountActivityModal from './ManagedAccountActivityModal';
import ManagedAccountModal, { formatCredits } from './ManagedAccountModal';

interface ManagedAccessPanelProps {
  session: ManagedAccessSession | null;
}

const ROW_BUTTON_CLASS = 'inline-flex h-8 items-center justify-center text-gate-muted-text transition-colors hover:bg-gate-bg hover:text-gate-text focus:outline-none focus:ring-2 focus:ring-gate-accent disabled:opacity-60 sketchy-border-thin';

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
  const [isDetailsOpen, setIsDetailsOpen] = useState(false);
  const [isActivityOpen, setIsActivityOpen] = useState(false);
  const [deleteConfirmationText, setDeleteConfirmationText] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const isSignedIn = Boolean(session?.firebaseIdToken);

  /**
   * The card has no room for a reason, so every failure opens the one surface
   * that can carry one. Without this an error would only tint an icon.
   */
  const reportError = useCallback((error: unknown, fallbackKey: string) => {
    setErrorMessage(describeManagedAccessError(error, t, fallbackKey));
    setIsDetailsOpen(true);
  }, [t]);

  const refreshAccount = useCallback(async () => {
    if (!session?.firebaseIdToken) return;
    setIsRefreshing(true);
    setErrorMessage(null);
    setStatusMessage(null);
    try {
      await maestroManagedAccountController.refreshAccount();
    } catch (error) {
      reportError(error, 'managedAccess.refreshFailed');
    } finally {
      setIsRefreshing(false);
    }
  }, [reportError, session?.firebaseIdToken]);
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
      // Reconciliation takes a few seconds and the user just paid: show it.
      setIsDetailsOpen(true);
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
      reportError(error, 'managedAccess.signInFailed');
    } finally {
      setIsSigningIn(false);
    }
  }, [reportError]);

  const handleSignOut = useCallback(async () => {
    setErrorMessage(null);
    setStatusMessage(null);
    try {
      await maestroManagedAccountController.signOut();
    } catch (error) {
      reportError(error, 'managedAccess.signOutFailed');
    }
  }, [reportError]);

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
      reportError(error, 'managedAccess.purchaseFailed');
    }
  }, [nativeExternalCheckoutEnabled, primaryPackId, reportError, session, t]);

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
    // The redirect reloaded the page, so nothing is open to report progress in.
    setIsDetailsOpen(true);
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
      reportError(error, 'managedAccess.deleteFailed');
    } finally {
      setIsDeletingAccount(false);
    }
  }, [deleteConfirmationText, reportError, session, t]);

  const availableCredits = session?.billingSummary.availableCredits || 0;
  const detailsLabel = isSignedIn
    ? t('managedAccess.accountAction', {
      account: session?.user.email || '',
      credits: `${formatCredits(availableCredits)} ${t('managedAccess.creditsUnit')}`,
    })
    : t('managedAccess.detailsAction');
  const DetailsIcon = errorMessage ? IconExclamationTriangle : IconCreditCard;

  return (
    <div className="space-y-3">
      {isSignedIn ? (
        <div
          className={`flex min-h-12 w-full items-center gap-1 bg-gate-input-bg/75 py-2 pl-4 pr-2 sketchy-border-thin ${errorMessage ? 'border-gate-error-border' : 'border-gate-ok-border'}`}
        >
          <span className="min-w-0 flex-1 truncate text-sm text-gate-text">{session?.user.email}</span>
          <button
            type="button"
            onClick={() => void refreshAccount()}
            disabled={isRefreshing}
            aria-label={t('managedAccess.refresh')}
            title={t('managedAccess.refresh')}
            className={`${ROW_BUTTON_CLASS} w-8`}
          >
            <IconArrowPath className={`h-4 w-4 ${isRefreshing ? 'animate-spin' : ''}`} />
          </button>
          <button
            type="button"
            onClick={() => setIsDetailsOpen(true)}
            aria-label={detailsLabel}
            title={detailsLabel}
            className={`${ROW_BUTTON_CLASS} gap-1 px-2 text-xs ${errorMessage ? 'text-notice-error-text' : ''}`}
          >
            <DetailsIcon className="h-3.5 w-3.5" />
            <span>{formatCredits(availableCredits)}</span>
          </button>
        </div>
      ) : (
        <div className="flex items-stretch gap-2">
          <button
            type="button"
            onClick={() => void handleSignIn()}
            disabled={isSigningIn}
            className="min-h-12 flex-1 bg-gate-btn-bg px-4 py-3 text-left text-sm font-medium text-gate-btn-text hover:bg-gate-btn-bg/80 disabled:opacity-60 focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin"
          >
            {isSigningIn ? t('managedAccess.signingIn') : t('managedAccess.signIn')}
          </button>
          <button
            type="button"
            onClick={() => setIsDetailsOpen(true)}
            aria-label={detailsLabel}
            title={detailsLabel}
            className={`min-h-12 w-12 shrink-0 items-center justify-center bg-gate-bg text-gate-muted-text transition-colors hover:bg-gate-input-bg hover:text-gate-text focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin inline-flex ${errorMessage ? 'text-notice-error-text' : ''}`}
          >
            {/*
              * A credit card, not a question mark: the API-key field below has
              * its own help button, and two identical icons on one card would
              * be two different explanations behind the same symbol.
              */}
            <DetailsIcon className="h-5 w-5" />
          </button>
        </div>
      )}

      <ManagedAccountModal
        isOpen={isDetailsOpen}
        session={session}
        statusMessage={statusMessage}
        errorMessage={errorMessage}
        isSigningIn={isSigningIn}
        isRefreshing={isRefreshing}
        isPurchasing={isPurchasing}
        isDeletingAccount={isDeletingAccount}
        isDeleteConfirmOpen={isDeleteConfirmOpen}
        purchasingAvailable={purchasingAvailable}
        deleteConfirmationText={deleteConfirmationText}
        onClose={() => setIsDetailsOpen(false)}
        onSignIn={() => void handleSignIn()}
        onSignOut={() => void handleSignOut()}
        onRefresh={() => void refreshAccount()}
        onPurchase={() => void handlePurchase()}
        onOpenActivity={() => {
          setIsDetailsOpen(false);
          setIsActivityOpen(true);
        }}
        onOpenDeleteConfirm={() => {
          setStatusMessage(null);
          setErrorMessage(null);
          setIsDeleteConfirmOpen(true);
        }}
        onCancelDelete={() => {
          setDeleteConfirmationText('');
          setIsDeleteConfirmOpen(false);
          setErrorMessage(null);
        }}
        onDeleteConfirmationTextChange={setDeleteConfirmationText}
        onDeleteAccount={() => void handleDeleteManagedAccount()}
      />
      <ManagedAccountActivityModal
        isOpen={isActivityOpen}
        onClose={() => setIsActivityOpen(false)}
      />
    </div>
  );
};

export default ManagedAccessPanel;
