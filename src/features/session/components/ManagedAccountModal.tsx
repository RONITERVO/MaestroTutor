// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Everything managed access needs to say, kept off the API-key card.
 *
 * The card is deliberately two controls tall: a button that fetches a Gemini
 * key and the field to paste it into. Managed access arrived with a balance
 * table, five buttons and four paragraphs of prose, which pushed the card past
 * a phone screen and buried the field it was built around. The compact row on
 * the card is now the whole surface; the detail, the rarely used actions and
 * the copy explaining them live here, one tap away.
 */
import React, { useEffect, useRef } from 'react';
import type { ManagedAccessSession } from '../../../core/contracts/backend';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { IconCreditCard, IconXMark } from '../../../shared/ui/Icons';

export const formatCredits = (value: number): string => (
  value.toLocaleString(undefined, {
    minimumFractionDigits: value % 1 === 0 ? 0 : 2,
    maximumFractionDigits: 2,
  })
);

export const formatUsd = (value: number): string => (
  value.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
);

interface ManagedAccountModalProps {
  isOpen: boolean;
  session: ManagedAccessSession | null;
  statusMessage: string | null;
  errorMessage: string | null;
  isSigningIn: boolean;
  isRefreshing: boolean;
  isPurchasing: boolean;
  isDeletingAccount: boolean;
  isDeleteConfirmOpen: boolean;
  purchasingAvailable: boolean;
  deleteConfirmationText: string;
  onClose: () => void;
  onSignIn: () => void;
  onSignOut: () => void;
  onRefresh: () => void;
  onPurchase: () => void;
  onOpenActivity: () => void;
  onOpenDeleteConfirm: () => void;
  onCancelDelete: () => void;
  onDeleteConfirmationTextChange: (value: string) => void;
  onDeleteAccount: () => void;
}

const GHOST_BUTTON_CLASS = 'px-3 py-2 text-gate-text hover:bg-gate-bg disabled:opacity-60 focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin';
const PRIMARY_BUTTON_CLASS = 'bg-gate-btn-bg px-3 py-2 text-gate-btn-text hover:bg-gate-btn-bg/80 disabled:opacity-60 focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin';

const ManagedAccountModal: React.FC<ManagedAccountModalProps> = ({
  isOpen,
  session,
  statusMessage,
  errorMessage,
  isSigningIn,
  isRefreshing,
  isPurchasing,
  isDeletingAccount,
  isDeleteConfirmOpen,
  purchasingAvailable,
  deleteConfirmationText,
  onClose,
  onSignIn,
  onSignOut,
  onRefresh,
  onPurchase,
  onOpenActivity,
  onOpenDeleteConfirm,
  onCancelDelete,
  onDeleteConfirmationTextChange,
  onDeleteAccount,
}) => {
  const { t } = useAppTranslations();
  const dialogRef = useRef<HTMLDivElement>(null);
  const isSignedIn = Boolean(session?.firebaseIdToken);

  useEffect(() => {
    if (!isOpen) return undefined;
    const previouslyFocused = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
    const focusFrame = requestAnimationFrame(() => dialogRef.current?.focus());

    return () => {
      cancelAnimationFrame(focusFrame);
      requestAnimationFrame(() => {
        if (previouslyFocused?.isConnected) previouslyFocused.focus();
      });
    };
  }, [isOpen]);

  const handleDialogKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      onClose();
      return;
    }
    if (event.key !== 'Tab') return;

    const dialog = dialogRef.current;
    if (!dialog) return;
    const focusable = Array.from(dialog.querySelectorAll<HTMLElement>(
      'button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), summary, [tabindex]:not([tabindex="-1"])'
    )).filter(element => element.getAttribute('aria-hidden') !== 'true');
    if (focusable.length === 0) {
      event.preventDefault();
      dialog.focus();
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && (document.activeElement === first || document.activeElement === dialog)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[110] flex items-center justify-center bg-scrim-modal p-4" onClick={onClose}>
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="managed-account-title"
        tabIndex={-1}
        className="flex max-h-[calc(100dvh-2rem)] w-full max-w-sm flex-col bg-gate-bg text-sm text-gate-text shadow-2xl outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin"
        onClick={event => event.stopPropagation()}
        onKeyDown={handleDialogKeyDown}
      >
        <header className="flex items-center justify-between border-b border-gate-muted-text/20 px-4 py-3">
          <div className="flex items-center gap-2">
            <IconCreditCard className="h-5 w-5 text-gate-accent" />
            <h2 id="managed-account-title" className="font-sketch text-lg font-semibold">
              {t('managedAccess.title')}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('managedAccess.closeDetails')}
            className="p-1 text-gate-muted-text hover:text-gate-text focus:outline-none focus:ring-2 focus:ring-gate-accent"
          >
            <IconXMark className="h-5 w-5" />
          </button>
        </header>

        <div className="space-y-3 overflow-y-auto p-4">
          <p className="text-gate-muted-text">{t('managedAccess.description')}</p>

          <div className="grid gap-2 text-xs sm:text-sm">
            <div className="flex items-center justify-between gap-3">
              <span className="text-gate-muted-text">{t('managedAccess.statusLabel')}</span>
              <span className="min-w-0 truncate font-medium">
                {session?.user.email || t('managedAccess.notSignedIn')}
              </span>
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
            <div className="border border-notice-ok-border bg-notice-ok-bg px-3 py-2 text-xs text-notice-ok-text">
              {statusMessage}
            </div>
          )}

          {errorMessage && (
            <div role="alert" className="border border-notice-error-border bg-notice-error-bg px-3 py-2 text-xs text-notice-error-text">
              {errorMessage}
            </div>
          )}

          <div className="flex flex-wrap gap-2">
            {!isSignedIn ? (
              <button type="button" onClick={onSignIn} disabled={isSigningIn} className={PRIMARY_BUTTON_CLASS}>
                {isSigningIn ? t('managedAccess.signingIn') : t('managedAccess.signIn')}
              </button>
            ) : (
              <>
                <button type="button" onClick={onRefresh} disabled={isRefreshing} className={GHOST_BUTTON_CLASS}>
                  {isRefreshing ? t('managedAccess.refreshing') : t('managedAccess.refresh')}
                </button>
                <button type="button" onClick={onOpenActivity} className={GHOST_BUTTON_CLASS}>
                  {t('managedAccess.activityAction')}
                </button>
                <button type="button" onClick={onSignOut} className={GHOST_BUTTON_CLASS}>
                  {t('managedAccess.signOut')}
                </button>
              </>
            )}

            {purchasingAvailable && (
              <button
                type="button"
                onClick={onPurchase}
                disabled={!isSignedIn || isPurchasing}
                className={PRIMARY_BUTTON_CLASS}
              >
                {isPurchasing ? t('managedAccess.purchasing') : t('managedAccess.buyCredits')}
              </button>
            )}
          </div>

          <div className="space-y-1 text-xs text-gate-muted-text">
            {!purchasingAvailable && <p>{t('managedAccess.androidOnly')}</p>}
            <p>{t('managedAccess.keepByok')}</p>
            <p>{t('managedAccess.billingNote')}</p>
          </div>

          {isSignedIn && (
            <div className="space-y-3 border border-danger-zone-border bg-danger-zone-bg px-3 py-3">
              <div className="space-y-1">
                <div className="font-medium text-danger-zone-text">{t('managedAccess.deleteTitle')}</div>
                <p className="text-xs text-danger-zone-text/80">{t('managedAccess.deleteDescription')}</p>
              </div>

              {!isDeleteConfirmOpen ? (
                <button
                  type="button"
                  onClick={onOpenDeleteConfirm}
                  className="px-3 py-2 text-danger-zone-text hover:bg-danger-ghost-hover focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin"
                >
                  {t('managedAccess.deleteAction')}
                </button>
              ) : (
                <div className="space-y-2">
                  <p className="text-xs text-danger-zone-text/80">{t('managedAccess.deleteConfirmHint')}</p>
                  <input
                    type="text"
                    value={deleteConfirmationText}
                    onChange={event => onDeleteConfirmationTextChange(event.target.value)}
                    placeholder="DELETE"
                    aria-label={t('managedAccess.deleteConfirmHint')}
                    className="w-full rounded-none border border-danger-input-border bg-danger-input-bg px-3 py-2 text-base text-gate-text focus:outline-none focus:ring-1 focus:ring-danger-input-ring sm:text-sm"
                  />
                  <div className="flex flex-wrap gap-2">
                    <button type="button" onClick={onCancelDelete} className={GHOST_BUTTON_CLASS}>
                      {t('managedAccess.deleteCancel')}
                    </button>
                    <button
                      type="button"
                      onClick={onDeleteAccount}
                      disabled={isDeletingAccount || deleteConfirmationText.trim().toUpperCase() !== 'DELETE'}
                      className="bg-danger-btn-bg px-3 py-2 text-danger-btn-text hover:bg-danger-btn-hover disabled:opacity-60 focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin"
                    >
                      {isDeletingAccount ? t('managedAccess.deleting') : t('managedAccess.deleteConfirm')}
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default ManagedAccountModal;
