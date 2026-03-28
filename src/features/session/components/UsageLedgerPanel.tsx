// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useMemo } from 'react';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import type { CostData, UsageEntry } from '../../../shared/utils/costTracker';
import { IconCreditCard, IconSparkles, IconTrash } from '../../../shared/ui/Icons';

interface UsageLedgerPanelProps {
  summary: CostData;
  entries: UsageEntry[];
  hasLegacyTotals: boolean;
  hasSavedApiKey: boolean;
  onOpenGoogleBilling: () => void;
  onClearHistory: () => void;
}

const formatCurrency = (value: number): string => value.toLocaleString(undefined, {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const formatWholeNumber = (value: number): string => Math.round(value).toLocaleString();

const formatTimestamp = (value: number): string => {
  try {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(value);
  } catch {
    return new Date(value).toLocaleString();
  }
};

const UsageLedgerPanel: React.FC<UsageLedgerPanelProps> = ({
  summary,
  entries,
  hasLegacyTotals,
  hasSavedApiKey,
  onOpenGoogleBilling,
  onClearHistory,
}) => {
  const { t } = useAppTranslations();

  const usageByMode = useMemo(() => entries.reduce(
    (acc, entry) => {
      acc[entry.accessMode] += entry.totalCostUsd;
      return acc;
    },
    { byok: 0, managed: 0 },
  ), [entries]);

  const legacySpend = Math.max(0, summary.totalCostUsd - usageByMode.byok - usageByMode.managed);
  const byokSpend = usageByMode.byok + legacySpend;
  const canOpenGoogleBilling = hasSavedApiKey || hasLegacyTotals || byokSpend > 0;

  const getSurfaceLabel = (surface: string): string => {
    switch (surface) {
      case 'chat-response':
        return t('usageLedger.surface.chatResponse');
      case 'reply-suggestions':
        return t('usageLedger.surface.replySuggestions');
      case 'suggestion-translation':
        return t('usageLedger.surface.suggestionTranslation');
      case 'user-image-generation':
        return t('usageLedger.surface.userImageGeneration');
      case 'assistant-image-generation':
        return t('usageLedger.surface.assistantImageGeneration');
      case 'image-generation':
        return t('usageLedger.surface.imageGeneration');
      default:
        return surface;
    }
  };

  const getApiLabel = (api: string): string => {
    switch (api) {
      case 'generate-content':
        return t('usageLedger.api.generateContent');
      case 'translate-text':
        return t('usageLedger.api.translateText');
      case 'generate-image':
        return t('usageLedger.api.generateImage');
      default:
        return api;
    }
  };

  return (
    <section className="space-y-4">
      <div className="bg-gate-input-bg/70 p-4 text-sm text-gate-text space-y-2 sketchy-border-thin">
        <div className="font-medium text-gate-text font-sketch">{t('usageLedger.summaryTitle')}</div>
        <p className="text-gate-muted-text">{t('usageLedger.subtitle')}</p>
      </div>

      <div className="grid gap-2 sm:grid-cols-2 text-sm">
        <div className="bg-gate-input-bg/70 p-3 sketchy-border-thin">
          <div className="text-xs uppercase tracking-[0.2em] text-gate-muted-text/80">
            {t('usageLedger.totalSpend')}
          </div>
          <div className="mt-1 text-xl font-semibold text-gate-text">
            ~${formatCurrency(summary.totalCostUsd)}
          </div>
        </div>

        <div className="bg-gate-input-bg/70 p-3 sketchy-border-thin">
          <div className="text-xs uppercase tracking-[0.2em] text-gate-muted-text/80">
            {t('usageLedger.requests')}
          </div>
          <div className="mt-1 text-xl font-semibold text-gate-text">
            {formatWholeNumber(summary.requestCount)}
          </div>
        </div>

        <div className="bg-gate-input-bg/70 p-3 sketchy-border-thin">
          <div className="text-xs uppercase tracking-[0.2em] text-gate-muted-text/80">
            {t('usageLedger.byokSpend')}
          </div>
          <div className="mt-1 text-lg font-semibold text-gate-text">
            ~${formatCurrency(byokSpend)}
          </div>
        </div>

        <div className="bg-gate-input-bg/70 p-3 sketchy-border-thin">
          <div className="text-xs uppercase tracking-[0.2em] text-gate-muted-text/80">
            {t('usageLedger.managedSpend')}
          </div>
          <div className="mt-1 text-lg font-semibold text-gate-text">
            ~${formatCurrency(usageByMode.managed)}
          </div>
        </div>
      </div>

      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3 text-sm">
        <div className="bg-gate-bg/70 p-3 sketchy-border-thin">
          <div className="text-xs text-gate-muted-text">{t('usageLedger.inputTokens')}</div>
          <div className="mt-1 font-medium text-gate-text">{formatWholeNumber(summary.inputTokens)}</div>
        </div>

        <div className="bg-gate-bg/70 p-3 sketchy-border-thin">
          <div className="text-xs text-gate-muted-text">{t('usageLedger.outputTokens')}</div>
          <div className="mt-1 font-medium text-gate-text">{formatWholeNumber(summary.outputTokens)}</div>
        </div>

        <div className="bg-gate-bg/70 p-3 sketchy-border-thin">
          <div className="text-xs text-gate-muted-text">{t('usageLedger.thoughtTokens')}</div>
          <div className="mt-1 font-medium text-gate-text">{formatWholeNumber(summary.thoughtTokens)}</div>
        </div>

        <div className="bg-gate-bg/70 p-3 sketchy-border-thin">
          <div className="text-xs text-gate-muted-text">{t('usageLedger.cachedTokens')}</div>
          <div className="mt-1 font-medium text-gate-text">{formatWholeNumber(summary.cachedContentTokens)}</div>
        </div>

        <div className="bg-gate-bg/70 p-3 sketchy-border-thin">
          <div className="text-xs text-gate-muted-text">{t('usageLedger.toolTokens')}</div>
          <div className="mt-1 font-medium text-gate-text">{formatWholeNumber(summary.toolUsePromptTokens)}</div>
        </div>

        <div className="bg-gate-bg/70 p-3 sketchy-border-thin">
          <div className="text-xs text-gate-muted-text">{t('usageLedger.images')}</div>
          <div className="mt-1 font-medium text-gate-text">{formatWholeNumber(summary.imageGenCount)}</div>
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        {canOpenGoogleBilling && (
          <button
            type="button"
            onClick={onOpenGoogleBilling}
            className="inline-flex items-center gap-2 bg-gate-btn-bg px-3 py-2 text-sm text-gate-btn-text hover:bg-gate-btn-bg/80 sketchy-border-thin"
          >
            <IconCreditCard className="h-4 w-4" />
            <span>{t('usageLedger.openGoogleBilling')}</span>
          </button>
        )}

        <button
          type="button"
          onClick={onClearHistory}
          className="inline-flex items-center gap-2 px-3 py-2 text-sm text-gate-text hover:bg-gate-input-bg sketchy-border-thin"
        >
          <IconTrash className="h-4 w-4" />
          <span>{t('usageLedger.clearHistory')}</span>
        </button>
      </div>

      <div className="text-xs text-gate-muted-text space-y-1">
        <p>{t('usageLedger.localOnlyNote')}</p>
        {hasLegacyTotals && (
          <p>{t('usageLedger.legacyNote')}</p>
        )}
        {canOpenGoogleBilling && (
          <p>{t('usageLedger.byokNote')}</p>
        )}
      </div>

      <div className="space-y-2 max-h-[360px] overflow-y-auto pr-1">
        {entries.length === 0 ? (
          <div className="bg-gate-input-bg/70 p-4 text-sm text-gate-muted-text sketchy-border-thin">
            {hasLegacyTotals ? t('usageLedger.legacyOnlyEmpty') : t('usageLedger.noEntries')}
          </div>
        ) : (
          entries.map((entry) => (
            <article
              key={entry.id}
              className="bg-gate-input-bg/70 p-3 text-sm text-gate-text space-y-2 sketchy-border-thin"
            >
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="font-medium text-gate-text">{getSurfaceLabel(entry.surface)}</div>
                  <div className="text-xs text-gate-muted-text">{getApiLabel(entry.api)}</div>
                </div>

                <div className="shrink-0 text-right space-y-1">
                  <div className="inline-flex items-center gap-1 rounded-full bg-gate-bg px-2 py-1 text-[11px] uppercase tracking-[0.2em] text-gate-muted-text sketchy-border-thin">
                    <IconSparkles className="h-3 w-3" />
                    <span>{entry.accessMode === 'managed' ? t('usageLedger.access.managed') : t('usageLedger.access.byok')}</span>
                  </div>
                  <div className="text-[11px] text-gate-muted-text">{formatTimestamp(entry.createdAt)}</div>
                </div>
              </div>

              {entry.model && (
                <div className="font-mono text-[11px] text-gate-muted-text break-all">
                  {entry.model}
                </div>
              )}

              <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4 text-xs">
                <div>
                  <div className="text-gate-muted-text">{t('usageLedger.totalSpend')}</div>
                  <div className="mt-1 font-medium text-gate-text">~${formatCurrency(entry.totalCostUsd)}</div>
                </div>

                <div>
                  <div className="text-gate-muted-text">{t('usageLedger.inputTokens')}</div>
                  <div className="mt-1 font-medium text-gate-text">{formatWholeNumber(entry.inputTokens)}</div>
                </div>

                <div>
                  <div className="text-gate-muted-text">{t('usageLedger.outputTokens')}</div>
                  <div className="mt-1 font-medium text-gate-text">{formatWholeNumber(entry.outputTokens)}</div>
                </div>

                <div>
                  <div className="text-gate-muted-text">{t('usageLedger.thoughtTokens')}</div>
                  <div className="mt-1 font-medium text-gate-text">{formatWholeNumber(entry.thoughtTokens)}</div>
                </div>
              </div>

              {(entry.cachedContentTokens > 0 || entry.toolUsePromptTokens > 0 || entry.imageGenCount > 0) && (
                <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-gate-muted-text">
                  {entry.cachedContentTokens > 0 && (
                    <span>{t('usageLedger.cachedInline', { count: formatWholeNumber(entry.cachedContentTokens) })}</span>
                  )}
                  {entry.toolUsePromptTokens > 0 && (
                    <span>{t('usageLedger.toolInline', { count: formatWholeNumber(entry.toolUsePromptTokens) })}</span>
                  )}
                  {entry.imageGenCount > 0 && (
                    <span>{t('usageLedger.imagesInline', { count: formatWholeNumber(entry.imageGenCount) })}</span>
                  )}
                </div>
              )}
            </article>
          ))
        )}
      </div>
    </section>
  );
};

export default UsageLedgerPanel;
