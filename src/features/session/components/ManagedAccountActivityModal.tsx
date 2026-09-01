// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import React, { useEffect, useRef, useState } from 'react';
import type {
  ManagedBillingLedgerEntry,
  ManagedUsageLedgerEntry,
} from '../../../core/contracts/backend';
import { maestroManagedAccountController } from '../../../services/account/maestroManagedAccountController';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { IconClock, IconXMark } from '../../../shared/ui/Icons';
import { SmallSpinner } from '../../../shared/ui/SmallSpinner';

interface ManagedAccountActivityModalProps {
  isOpen: boolean;
  onClose: () => void;
}

const formatDate = (timestamp: number): string => {
  const date = new Date(timestamp);
  return Number.isNaN(date.getTime()) ? String(timestamp) : date.toLocaleString();
};

const formatCredits = (credits: number): string => credits.toLocaleString(undefined, {
  maximumFractionDigits: 4,
});

const ManagedAccountActivityModal: React.FC<ManagedAccountActivityModalProps> = ({ isOpen, onClose }) => {
  const { t } = useAppTranslations();
  const dialogRef = useRef<HTMLDivElement>(null);
  const [usageEntries, setUsageEntries] = useState<ManagedUsageLedgerEntry[]>([]);
  const [billingEntries, setBillingEntries] = useState<ManagedBillingLedgerEntry[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen) return undefined;
    let mounted = true;
    setIsLoading(true);
    setErrorMessage(null);
    void maestroManagedAccountController.listLedgers(50)
      .then(({ usage, billing }) => {
        if (!mounted) return;
        setUsageEntries(usage.entries);
        setBillingEntries(billing.entries);
      })
      .catch(error => {
        if (!mounted) return;
        setErrorMessage(error instanceof Error ? error.message : t('managedAccess.activityLoadFailed'));
      })
      .finally(() => {
        if (mounted) setIsLoading(false);
      });
    requestAnimationFrame(() => dialogRef.current?.focus());
    return () => {
      mounted = false;
    };
  }, [isOpen, t]);

  if (!isOpen) return null;

  const renderEmpty = () => (
    <p className="px-3 py-4 text-center text-xs text-gate-muted-text">
      {t('managedAccess.activityEmpty')}
    </p>
  );

  return (
    <div className="fixed inset-0 z-[120] flex items-center justify-center bg-black/45 p-4" onClick={onClose}>
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="managed-account-activity-title"
        tabIndex={-1}
        className="flex max-h-[calc(100dvh-2rem)] w-full max-w-2xl flex-col bg-gate-bg text-gate-text shadow-2xl outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin"
        onClick={event => event.stopPropagation()}
        onKeyDown={event => {
          if (event.key === 'Escape') onClose();
        }}
      >
        <header className="flex items-center justify-between border-b border-gate-muted-text/20 px-4 py-3">
          <div className="flex items-center gap-2">
            <IconClock className="h-5 w-5 text-gate-accent" />
            <h2 id="managed-account-activity-title" className="font-sketch text-lg font-semibold">
              {t('managedAccess.activityTitle')}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="p-1 text-gate-muted-text hover:text-gate-text focus:outline-none focus:ring-2 focus:ring-gate-accent"
            aria-label={t('managedAccess.activityClose')}
          >
            <IconXMark className="h-5 w-5" />
          </button>
        </header>

        <div className="overflow-y-auto p-4">
          {isLoading ? (
            <div className="flex min-h-32 items-center justify-center gap-2 text-sm text-gate-muted-text">
              <SmallSpinner className="h-4 w-4" />
              {t('managedAccess.activityLoading')}
            </div>
          ) : errorMessage ? (
            <div className="border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-800">
              {errorMessage}
            </div>
          ) : (
            <div className="grid gap-4 md:grid-cols-2">
              <section aria-labelledby="managed-usage-title">
                <h3 id="managed-usage-title" className="mb-2 font-sketch font-semibold">
                  {t('managedAccess.usageLedgerTitle')}
                </h3>
                <div className="divide-y divide-gate-muted-text/15 bg-gate-input-bg/45 sketchy-border-thin">
                  {usageEntries.length === 0 ? renderEmpty() : usageEntries.map(entry => (
                    <div key={entry.id} className="space-y-1 px-3 py-2 text-xs">
                      <div className="flex justify-between gap-3">
                        <strong className="break-all">{entry.operation}</strong>
                        <span className="shrink-0 tabular-nums">−{formatCredits(entry.billedCredits)}</span>
                      </div>
                      <div className="flex justify-between gap-3 text-gate-muted-text">
                        <span className="truncate">{entry.model}</span>
                        <time className="shrink-0" dateTime={new Date(entry.createdAt).toISOString()}>{formatDate(entry.createdAt)}</time>
                      </div>
                    </div>
                  ))}
                </div>
              </section>

              <section aria-labelledby="managed-billing-title">
                <h3 id="managed-billing-title" className="mb-2 font-sketch font-semibold">
                  {t('managedAccess.billingLedgerTitle')}
                </h3>
                <div className="divide-y divide-gate-muted-text/15 bg-gate-input-bg/45 sketchy-border-thin">
                  {billingEntries.length === 0 ? renderEmpty() : billingEntries.map(entry => (
                    <div key={entry.id} className="space-y-1 px-3 py-2 text-xs">
                      <div className="flex justify-between gap-3">
                        <strong>{entry.kind}</strong>
                        <span className="shrink-0 tabular-nums">{formatCredits(entry.credits)}</span>
                      </div>
                      <div className="flex justify-between gap-3 text-gate-muted-text">
                        <span className="truncate">{entry.productId || '—'}</span>
                        <time className="shrink-0" dateTime={new Date(entry.createdAt).toISOString()}>{formatDate(entry.createdAt)}</time>
                      </div>
                    </div>
                  ))}
                </div>
              </section>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default ManagedAccountActivityModal;
