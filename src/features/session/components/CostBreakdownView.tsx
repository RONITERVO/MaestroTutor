// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React from 'react';
import {
  IconBookOpen,
  IconCamera,
  IconChevronLeft,
  IconClock,
  IconCreditCard,
  IconMicrophone,
  IconQuestionMarkCircle,
  IconSparkles,
  IconSpeaker,
  IconTranslate,
  IconWaveform,
} from '../../../shared/ui/Icons';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { openExternalUrl } from '../../../shared/utils/openExternalUrl';
import {
  CostBreakdownEntry,
  CostFeature,
  CostSummary,
  GOOGLE_BILLING_URL,
} from '../../../shared/utils/costTracker';

interface CostBreakdownViewProps {
  summary: CostSummary;
  onBack: () => void;
}

const formatUsd = (value: number): string => {
  if (value === 0) return '$0.00';
  if (value < 0.01) return `$${value.toFixed(4)}`;
  return `$${value.toFixed(2)}`;
};

const formatUsdDetailed = (value: number): string => (
  value > 0 && value < 10 ? `$${value.toFixed(3)}` : formatUsd(value)
);

const formatTokens = (value: number): string => new Intl.NumberFormat(undefined, {
  notation: value >= 10000 ? 'compact' : 'standard',
  maximumFractionDigits: 1,
}).format(value);

const formatDuration = (seconds: number): string => {
  if (seconds < 60) return `${Math.round(seconds)}s`;
  return `${(seconds / 60).toFixed(seconds < 600 ? 1 : 0)}m`;
};

const formatDate = (value: string): string => {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(date);
};

const featureIcon: Record<CostFeature, React.ComponentType<any>> = {
  tutor: IconBookOpen,
  suggestions: IconSparkles,
  translation: IconTranslate,
  image: IconCamera,
  liveConversation: IconMicrophone,
  reengagement: IconWaveform,
  stt: IconMicrophone,
  tts: IconSpeaker,
  audioNote: IconSpeaker,
  music: IconWaveform,
};

const FEATURE_TRANSLATION_KEYS: Record<CostFeature, string> = {
  tutor: 'costBreakdown.feature.tutor',
  suggestions: 'costBreakdown.feature.suggestions',
  translation: 'costBreakdown.feature.translation',
  image: 'costBreakdown.feature.image',
  liveConversation: 'costBreakdown.feature.liveConversation',
  reengagement: 'costBreakdown.feature.reengagement',
  stt: 'costBreakdown.feature.stt',
  tts: 'costBreakdown.feature.tts',
  audioNote: 'costBreakdown.feature.audioNote',
  music: 'costBreakdown.feature.music',
};

const summarizeModalities = (entry: CostBreakdownEntry): string => {
  const parts: string[] = [];
  for (const modality of ['text', 'audio', 'image', 'video', 'document'] as const) {
    const input = entry.inputByModality[modality];
    const output = entry.outputByModality[modality];
    if (input + output > 0) {
      parts.push(`${modality} ${formatTokens(input)} in / ${formatTokens(output)} out`);
    }
  }
  return parts.join(' · ');
};

export const CostBreakdownView: React.FC<CostBreakdownViewProps> = ({ summary, onBack }) => {
  const { t } = useAppTranslations();
  const hasPotentialSearchCost = summary.potentialSearchCostUsd > 0;
  const totalLabel = hasPotentialSearchCost
    ? t('costBreakdown.maximumEstimate')
    : t('costBreakdown.totalEstimate');

  return (
    <div
      className="flex max-h-[calc(100dvh-2rem)] w-full max-w-2xl flex-col overflow-hidden bg-gate-bg text-gate-text shadow-2xl sketchy-border sketch-shape-7"
      role="dialog"
      aria-modal="true"
      aria-labelledby="cost-breakdown-title"
      onClick={(event) => event.stopPropagation()}
    >
      <header className="flex shrink-0 items-start gap-3 border-b border-gate-muted-text/15 px-4 py-4 sm:px-6">
        <button
          type="button"
          onClick={onBack}
          className="mt-0.5 inline-flex h-9 w-9 shrink-0 items-center justify-center bg-gate-input-bg/70 text-gate-muted-text transition-colors hover:text-gate-text focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin"
          aria-label={t('costBreakdown.back')}
        >
          <IconChevronLeft className="h-5 w-5" />
        </button>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h2 id="cost-breakdown-title" className="font-sketch text-xl font-semibold">
              {t('costBreakdown.title')}
            </h2>
            <span className="bg-gate-accent/15 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-gate-accent sketchy-border-thin">
              {t('costBreakdown.paidTierBadge')}
            </span>
          </div>
          <p className="mt-1 text-xs leading-relaxed text-gate-muted-text">
            {t('costBreakdown.subtitle')}
          </p>
        </div>
      </header>

      <div className="overflow-y-auto overscroll-contain px-4 py-4 sm:px-6 sm:py-5">
        <section className="relative overflow-hidden bg-gate-btn-bg p-4 text-gate-btn-text msg-depth sketchy-border-thin sketch-shape-2">
          <div className="absolute -right-5 -top-6 opacity-10">
            <IconSparkles className="h-24 w-24" />
          </div>
          <div className="relative">
            <p className="text-xs font-semibold uppercase tracking-[0.16em] opacity-65">{totalLabel}</p>
            <p className="mt-1 font-sketch text-4xl font-bold tabular-nums">{formatUsd(summary.totalCostUsd)}</p>
            <div className="mt-3 grid grid-cols-2 gap-2 text-xs">
              <div className="bg-gate-bg/15 px-3 py-2 sketchy-border-thin">
                <span className="block opacity-65">{t('costBreakdown.modelUsage')}</span>
                <strong className="mt-0.5 block tabular-nums">{formatUsdDetailed(summary.knownModelCostUsd)}</strong>
              </div>
              <div className="bg-gate-bg/15 px-3 py-2 sketchy-border-thin">
                <span className="block opacity-65">{t('costBreakdown.searchMaximum')}</span>
                <strong className="mt-0.5 block tabular-nums">{formatUsdDetailed(summary.potentialSearchCostUsd)}</strong>
              </div>
            </div>
          </div>
        </section>

        <section className="mt-4 grid grid-cols-2 gap-2 sm:grid-cols-4" aria-label={t('costBreakdown.usageTotals')}>
          {[
            [t('costBreakdown.inputTokens'), formatTokens(summary.inputTokens)],
            [t('costBreakdown.outputTokens'), formatTokens(summary.outputTokens)],
            [t('costBreakdown.thinkingTokens'), formatTokens(summary.thinkingTokens)],
            [t('costBreakdown.images'), String(summary.imageGenCount)],
          ].map(([label, value]) => (
            <div key={label} className="bg-gate-input-bg/55 px-3 py-2 sketchy-border-thin">
              <span className="block text-[10px] uppercase tracking-wide text-gate-muted-text">{label}</span>
              <strong className="mt-0.5 block text-sm tabular-nums">{value}</strong>
            </div>
          ))}
        </section>

        <section className="mt-5" aria-labelledby="cost-activity-title">
          <div className="mb-2 flex items-center justify-between gap-3">
            <h3 id="cost-activity-title" className="font-sketch text-base font-semibold">{t('costBreakdown.byActivity')}</h3>
            <span className="text-right text-[10px] leading-relaxed text-gate-muted-text">
              {t('costBreakdown.trackingSince', { date: formatDate(summary.startedAt) })}<br />
              {t('costBreakdown.rateDate', { date: summary.pricingEffectiveAt })}
            </span>
          </div>
          <div className="space-y-2">
            {summary.entries.length === 0 ? (
              <div className="bg-gate-input-bg/45 px-4 py-5 text-center text-sm text-gate-muted-text sketchy-border-thin">
                {t('costBreakdown.noNewUsage')}
              </div>
            ) : summary.entries.map((entry) => {
              const FeatureIcon = featureIcon[entry.feature];
              const entryTotal = entry.modelCostUsd + entry.potentialSearchCostUsd;
              const modalitySummary = summarizeModalities(entry);
              return (
                <details key={`${entry.feature}:${entry.model}:${entry.pricingEffectiveAt}`} className="group bg-gate-input-bg/45 sketchy-border-thin">
                  <summary className="flex cursor-pointer list-none items-center gap-3 px-3 py-3 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-gate-accent">
                    <div className="flex h-9 w-9 shrink-0 items-center justify-center bg-gate-bg text-gate-accent sketchy-border-thin">
                      <FeatureIcon className="h-4.5 w-4.5" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                        <strong className="text-sm">{t(FEATURE_TRANSLATION_KEYS[entry.feature])}</strong>
                        {entry.pricingStatus === 'unpriced' && (
                          <span className="bg-gate-error-text/10 px-1.5 py-0.5 text-[9px] font-semibold uppercase text-gate-error-text sketchy-border-thin">
                            {t('costBreakdown.unpriced')}
                          </span>
                        )}
                      </div>
                      <p className="truncate text-[11px] text-gate-muted-text">{entry.modelDisplayName}</p>
                    </div>
                    <div className="text-right">
                      <strong className="block text-sm tabular-nums">{formatUsd(entryTotal)}</strong>
                      <span className="block text-[10px] text-gate-muted-text">
                        {entry.requests === 1
                          ? t('costBreakdown.requestSingle')
                          : t('costBreakdown.requests', { count: entry.requests })}
                      </span>
                    </div>
                    <span className="text-gate-muted-text transition-transform group-open:rotate-90">›</span>
                  </summary>
                  <div className="border-t border-gate-muted-text/10 px-4 py-3 text-[11px] leading-relaxed text-gate-muted-text">
                    <div className="grid grid-cols-2 gap-x-4 gap-y-1">
                      <span>{t('costBreakdown.inputTokens')}</span><strong className="text-right text-gate-text">{formatTokens(entry.inputTokens)}</strong>
                      <span>{t('costBreakdown.outputTokens')}</span><strong className="text-right text-gate-text">{formatTokens(entry.outputTokens)}</strong>
                      <span>{t('costBreakdown.thinkingTokens')}</span><strong className="text-right text-gate-text">{formatTokens(entry.thinkingTokens)}</strong>
                      <span>{t('costBreakdown.cachedTokens')}</span><strong className="text-right text-gate-text">{formatTokens(entry.cachedInputTokens)}</strong>
                      {entry.generatedImages > 0 && <><span>{t('costBreakdown.images')}</span><strong className="text-right text-gate-text">{entry.generatedImages}</strong></>}
                      {entry.generatedAudioSeconds > 0 && <><span>{t('costBreakdown.generatedAudio')}</span><strong className="text-right text-gate-text">{formatDuration(entry.generatedAudioSeconds)}</strong></>}
                      {entry.searchPrompts > 0 && <><span>{t('costBreakdown.searchPrompts')}</span><strong className="text-right text-gate-text">{entry.searchPrompts}</strong></>}
                      {entry.searchQueries > 0 && <><span>{t('costBreakdown.searchQueries')}</span><strong className="text-right text-gate-text">{entry.searchQueries}</strong></>}
                    </div>
                    {modalitySummary && <p className="mt-2 border-t border-gate-muted-text/10 pt-2">{modalitySummary}</p>}
                    <p className="mt-2">{t('costBreakdown.entryRateDate', { date: entry.pricingEffectiveAt })}</p>
                    {entry.pricingNote && <p className="mt-2 text-gate-error-text">{entry.pricingNote}</p>}
                  </div>
                </details>
              );
            })}
          </div>
        </section>

        {hasPotentialSearchCost && (
          <section className="mt-4 flex gap-3 bg-gate-accent/10 p-3 text-xs leading-relaxed sketchy-border-thin">
            <IconQuestionMarkCircle className="mt-0.5 h-5 w-5 shrink-0 text-gate-accent" />
            <div>
              <strong className="block text-gate-text">{t('costBreakdown.searchAllowanceTitle')}</strong>
              <p className="mt-0.5 text-gate-muted-text">
                {t('costBreakdown.searchAllowanceBody', {
                  prompts: summary.searchPrompts,
                  queries: summary.searchQueries,
                  cost: formatUsd(summary.potentialSearchCostUsd),
                })}
              </p>
            </div>
          </section>
        )}

        {summary.legacyEstimateUsd > 0 && (
          <section className="mt-4 flex gap-3 bg-gate-input-bg/45 p-3 text-xs leading-relaxed sketchy-border-thin">
            <IconClock className="mt-0.5 h-5 w-5 shrink-0 text-gate-muted-text" />
            <div>
              <strong className="block text-gate-text">
                {t('costBreakdown.legacyTitle', { cost: formatUsd(summary.legacyEstimateUsd) })}
              </strong>
              <p className="mt-0.5 text-gate-muted-text">{t('costBreakdown.legacyBody')}</p>
            </div>
          </section>
        )}

        <section className="mt-5 bg-gate-input-bg/35 p-4 text-xs leading-relaxed sketchy-border-thin">
          <h3 className="flex items-center gap-2 font-sketch text-sm font-semibold text-gate-text">
            <IconQuestionMarkCircle className="h-4 w-4 text-gate-accent" />
            {t('costBreakdown.howCalculated')}
          </h3>
          <ul className="mt-2 space-y-1.5 text-gate-muted-text">
            <li>• {t('costBreakdown.scopePaid')}</li>
            <li>• {t('costBreakdown.scopeDevice')}</li>
            <li>• {t('costBreakdown.scopeMetadata')}</li>
            <li>• {t('costBreakdown.scopeDiscounts')}</li>
          </ul>
          <button
            type="button"
            onClick={() => void openExternalUrl(summary.pricingSourceUrl)}
            className="mt-3 inline-flex items-center gap-1.5 font-semibold text-gate-accent hover:underline focus:outline-none focus:ring-2 focus:ring-gate-accent"
          >
            <IconBookOpen className="h-3.5 w-3.5" />
            {t('costBreakdown.viewPricingSource')}
          </button>
        </section>
      </div>

      <footer className="flex shrink-0 flex-col gap-2 border-t border-gate-muted-text/15 bg-gate-bg px-4 py-3 sm:flex-row sm:items-center sm:justify-between sm:px-6">
        <p className="text-[10px] leading-relaxed text-gate-muted-text">{t('costBreakdown.billingAuthority')}</p>
        <button
          type="button"
          onClick={() => void openExternalUrl(GOOGLE_BILLING_URL)}
          className="inline-flex min-h-10 shrink-0 items-center justify-center gap-2 bg-gate-btn-bg px-4 py-2 text-xs font-semibold text-gate-btn-text transition-opacity hover:opacity-85 focus:outline-none focus:ring-2 focus:ring-gate-accent sketchy-border-thin"
        >
          <IconCreditCard className="h-4 w-4" />
          {t('costBreakdown.openGoogleBilling')}
        </button>
      </footer>
    </div>
  );
};

export default CostBreakdownView;
