// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type {
  ManagedAccountSummaryResponse,
  ManagedBillingLedgerEntry,
  ManagedUsageLedgerEntry,
} from '../core/contracts/backend';
import type { HeadlessClient } from './client';
import { DEFAULT_GEMINI_PRICING } from '../../shared/pricing/registry';
import { calculateGeminiUsageCost } from '../../shared/pricing/usage';
import { usdToCredits } from '../../shared/pricing/credits';

export interface ManagedJourneyBillingSnapshot {
  account: ManagedAccountSummaryResponse;
  usageEntries: ManagedUsageLedgerEntry[];
  billingEntries: ManagedBillingLedgerEntry[];
}

export interface ManagedJourneyBillingEvidence {
  applicable: true;
  passed: boolean;
  mismatches: string[];
  creditsSpent: number;
  usdSpent: number;
  newUsageEntries: number;
  newChargeEntries: number;
  usageCredits: number;
  chargeCredits: number;
  reservedCreditsBefore: number;
  reservedCreditsAfter: number;
}

export interface ManagedJourneyFailureBillingEvidence extends ManagedJourneyBillingEvidence {
  failedLiveRequestIds: string[];
  failedLiveChargeEntries: number;
  failedLiveCredits: number;
}

export interface ManagedLiveBillingEvidence {
  applicable: true;
  passed: boolean;
  mismatches: string[];
  requestId: string;
  usageRows: number;
  chargeRows: number;
  releaseRows: number;
  billedCredits: number;
  chargedCredits: number;
  shortfallCredits: number;
  billedUsd: number;
  recalculatedUsd: number;
  creditsSpent: number;
  availableCreditsSpent: number;
  reservedCreditsBefore: number;
  reservedCreditsAfter: number;
  usageSource: string | null;
  providerTurnCompleteCount: number;
  providerUsageTurnCount: number;
  clientTurnBoundaryCount: number;
  inputAudioBytes: number;
  inputVideoFrameCount: number;
  outputAudioBytes: number;
  providerTotalTokenCount: number;
}

const sum = <T>(items: T[], select: (item: T) => number): number => items.reduce(
  (total, item) => total + (Number.isFinite(select(item)) ? select(item) : 0),
  0,
);

const roundUsd = (value: number): number => Math.round(value * 1_000_000) / 1_000_000;

const metadataNumber = (metadata: Record<string, unknown> | undefined, key: string): number => {
  const value = Number(metadata?.[key] || 0);
  return Number.isFinite(value) && value >= 0 ? value : 0;
};

export const captureManagedJourneyBilling = async (
  client: HeadlessClient,
  operationId: string,
): Promise<ManagedJourneyBillingSnapshot> => {
  const [account, ledgers] = await Promise.all([
    client.account.refreshAccount(operationId),
    client.account.listLedgers(200, operationId),
  ]);
  return {
    account,
    usageEntries: ledgers.usage.entries,
    billingEntries: ledgers.billing.entries,
  };
};

export const waitForManagedJourneyBillingSettlement = async (
  client: HeadlessClient,
  operationId: string,
  attempts = 20,
  intervalMs = 500,
): Promise<ManagedJourneyBillingSnapshot> => {
  for (let attempt = 1; attempt <= attempts; attempt += 1) {
    const snapshot = await captureManagedJourneyBilling(client, operationId);
    if (snapshot.account.account.billingSummary.reservedCredits === 0) return snapshot;
    if (attempt < attempts) await client.runtime.clock.sleep(intervalMs);
  }
  throw new Error('Managed journey finished with credits still reserved after the settlement wait.');
};

/** Proves ledger consistency and that no reservation was stranded. */
export const evaluateManagedJourneyBilling = (
  before: ManagedJourneyBillingSnapshot,
  after: ManagedJourneyBillingSnapshot,
): ManagedJourneyBillingEvidence => {
  const beforeSummary = before.account.account.billingSummary;
  const afterSummary = after.account.account.billingSummary;
  const priorUsageIds = new Set(before.usageEntries.map(entry => entry.id));
  const priorBillingIds = new Set(before.billingEntries.map(entry => entry.id));
  const newUsage = after.usageEntries.filter(entry => !priorUsageIds.has(entry.id));
  const newCharges = after.billingEntries.filter(entry => (
    !priorBillingIds.has(entry.id) && entry.kind === 'charge'
  ));
  const creditsSpent = afterSummary.lifetimeSpentCredits - beforeSummary.lifetimeSpentCredits;
  const usdSpent = roundUsd(afterSummary.lifetimeSpentUsd - beforeSummary.lifetimeSpentUsd);
  const availableCreditsSpent = beforeSummary.availableCredits - afterSummary.availableCredits;
  const usageCredits = sum(newUsage, entry => entry.billedCredits);
  const usageUsd = roundUsd(sum(newUsage, entry => entry.billedUsd));
  const chargeCredits = sum(newCharges, entry => entry.credits);
  const chargeUsd = roundUsd(sum(newCharges, entry => entry.usd));
  const mismatches: string[] = [];

  if (beforeSummary.reservedCredits !== 0) mismatches.push('The managed journey began with reserved credits.');
  if (afterSummary.reservedCredits !== 0) mismatches.push('The managed journey left reserved credits behind.');
  if (creditsSpent <= 0 || newUsage.length === 0 || newCharges.length === 0) {
    mismatches.push('The managed journey produced no complete paid-usage evidence.');
  }
  if (availableCreditsSpent !== creditsSpent) mismatches.push('Available-credit and lifetime-spend deltas differ.');
  if (usageCredits !== creditsSpent) mismatches.push('Usage-ledger credits do not equal the account spend delta.');
  if (chargeCredits !== creditsSpent) mismatches.push('Charge-ledger credits do not equal the account spend delta.');
  if (usageUsd !== usdSpent) mismatches.push('Usage-ledger USD does not equal the account spend delta.');
  if (chargeUsd !== usdSpent) mismatches.push('Charge-ledger USD does not equal the account spend delta.');
  if (newUsage.some(entry => (
    !entry.operation?.trim()
    || !entry.model?.trim()
    || entry.billedCredits < 0
    || entry.billedUsd < 0
  ))) {
    mismatches.push('A new usage entry is missing identity or has a negative charge.');
  }

  return {
    applicable: true,
    passed: mismatches.length === 0,
    mismatches,
    creditsSpent,
    usdSpent,
    newUsageEntries: newUsage.length,
    newChargeEntries: newCharges.length,
    usageCredits,
    chargeCredits,
    reservedCreditsBefore: beforeSummary.reservedCredits,
    reservedCreditsAfter: afterSummary.reservedCredits,
  };
};

/**
 * Correlate one provider-backed Live socket with the exact usage and balance
 * rows it produced. Broad journey reconciliation can pass even when a
 * particular persistent socket was omitted, duplicated, or undercharged.
 */
export const evaluateManagedLiveBilling = (
  before: ManagedJourneyBillingSnapshot,
  after: ManagedJourneyBillingSnapshot,
  requestId: string,
  expected: {
    connectedTurns: number;
    sentAudioBytes: number;
    sentVideoFrames: number;
    receivedAudioBytes: number;
  },
): ManagedLiveBillingEvidence => {
  const beforeSummary = before.account.account.billingSummary;
  const afterSummary = after.account.account.billingSummary;
  const priorUsageIds = new Set(before.usageEntries.map(entry => entry.id));
  const priorBillingIds = new Set(before.billingEntries.map(entry => entry.id));
  const matchesRequest = (metadata: Record<string, unknown> | undefined): boolean => (
    metadata?.liveOpenRequestId === requestId
  );
  const usageRows = after.usageEntries.filter(entry => (
    !priorUsageIds.has(entry.id)
    && entry.operation === 'liveGateway'
    && matchesRequest(entry.metadata)
  ));
  const chargeRows = after.billingEntries.filter(entry => (
    !priorBillingIds.has(entry.id)
    && entry.kind === 'charge'
    && entry.metadata?.operation === 'liveGateway'
    && matchesRequest(entry.metadata)
  ));
  const releaseRows = after.billingEntries.filter(entry => (
    !priorBillingIds.has(entry.id)
    && entry.kind === 'reservation-release'
    && entry.metadata?.operation === 'liveGateway'
    && matchesRequest(entry.metadata)
  ));
  const usage = usageRows[0];
  const charge = chargeRows[0];
  const metadata = usage?.metadata;
  const billedCredits = Number(usage?.billedCredits || 0);
  const chargedCredits = Number(usage?.chargedCredits ?? charge?.credits ?? 0);
  const shortfallCredits = Number(usage?.shortfallCredits ?? charge?.shortfallCredits ?? 0);
  const billedUsd = roundUsd(Number(usage?.billedUsd || 0));
  const creditsSpent = afterSummary.lifetimeSpentCredits - beforeSummary.lifetimeSpentCredits;
  const availableCreditsSpent = beforeSummary.availableCredits - afterSummary.availableCredits;
  const usageSource = typeof metadata?.usageSource === 'string' ? metadata.usageSource : null;
  const providerTurnCompleteCount = metadataNumber(metadata, 'providerTurnCompleteCount');
  const providerUsageTurnCount = metadataNumber(metadata, 'providerUsageTurnCount');
  const clientTurnBoundaryCount = metadataNumber(metadata, 'clientTurnBoundaryCount');
  const inputAudioBytes = metadataNumber(metadata, 'inputAudioBytes');
  const inputVideoFrameCount = metadataNumber(metadata, 'inputVideoFrameCount');
  const outputAudioBytes = metadataNumber(metadata, 'outputAudioBytes');
  const providerTotalTokenCount = metadataNumber(metadata, 'totalTokenCount');
  const creditsPerUsd = metadataNumber(metadata, 'creditsPerUsd');
  const recalculated = usage
    ? calculateGeminiUsageCost({
        configuredModel: usage.model,
        usageMetadata: metadata,
      }, DEFAULT_GEMINI_PRICING).modelCostUsd
    : 0;
  const recalculatedUsd = roundUsd(recalculated);
  const recalculatedCredits = creditsPerUsd > 0
    ? usdToCredits(recalculatedUsd, creditsPerUsd)
    : 0;
  const mismatches: string[] = [];

  if (beforeSummary.reservedCredits !== 0) mismatches.push('The managed Live proof began with reserved credits.');
  if (afterSummary.reservedCredits !== 0) mismatches.push('The managed Live proof left reserved credits behind.');
  if (usageRows.length !== 1) mismatches.push(`Expected one request-correlated Live usage row, found ${usageRows.length}.`);
  if (chargeRows.length !== 1) mismatches.push(`Expected one request-correlated Live charge row, found ${chargeRows.length}.`);
  if (releaseRows.length !== 0) mismatches.push(`Successful Live proof unexpectedly produced ${releaseRows.length} release row(s).`);
  if (billedCredits <= 0 || billedUsd <= 0) mismatches.push('The successful Live proof did not record a positive provider-derived cost.');
  if (shortfallCredits !== 0 || chargedCredits !== billedCredits) {
    mismatches.push('The Live reservation did not fully cover the provider-derived charge.');
  }
  if (creditsSpent !== chargedCredits || availableCreditsSpent !== chargedCredits) {
    mismatches.push('The Live account balance delta does not equal its correlated charge.');
  }
  if (Number(charge?.credits || 0) !== chargedCredits || roundUsd(Number(charge?.usd || 0)) !== billedUsd) {
    mismatches.push('The Live charge ledger does not match its usage ledger.');
  }
  if (recalculatedUsd !== billedUsd || recalculatedCredits !== billedCredits) {
    mismatches.push('The Live charge does not match the checked-in pricing registry and credit conversion.');
  }
  if (!usageSource || usageSource === 'none') mismatches.push('The Live usage row has no billable evidence source.');
  if (providerTurnCompleteCount !== expected.connectedTurns) {
    mismatches.push(
      `The gateway accounted for ${providerTurnCompleteCount} completed provider turn(s), expected ${expected.connectedTurns}.`,
    );
  }
  if (providerUsageTurnCount !== expected.connectedTurns) {
    mismatches.push(
      `The gateway priced ${providerUsageTurnCount} provider turn snapshot(s), expected ${expected.connectedTurns}.`,
    );
  }
  if (clientTurnBoundaryCount !== expected.connectedTurns) {
    mismatches.push(
      `The gateway accepted ${clientTurnBoundaryCount} client turn boundary/boundaries, expected ${expected.connectedTurns}.`,
    );
  }
  if (inputAudioBytes !== expected.sentAudioBytes) {
    mismatches.push(`The gateway accounted for ${inputAudioBytes} input byte(s), expected ${expected.sentAudioBytes}.`);
  }
  if (inputVideoFrameCount !== expected.sentVideoFrames) {
    mismatches.push(
      `The gateway accounted for ${inputVideoFrameCount} video frame(s), expected ${expected.sentVideoFrames}.`,
    );
  }
  if (outputAudioBytes !== expected.receivedAudioBytes) {
    mismatches.push(`The gateway accounted for ${outputAudioBytes} output byte(s), expected ${expected.receivedAudioBytes}.`);
  }
  if (usageSource?.startsWith('provider') && providerTotalTokenCount <= 0) {
    mismatches.push('Provider-priced Live usage did not include a positive cumulative token total.');
  }

  return {
    applicable: true,
    passed: mismatches.length === 0,
    mismatches,
    requestId,
    usageRows: usageRows.length,
    chargeRows: chargeRows.length,
    releaseRows: releaseRows.length,
    billedCredits,
    chargedCredits,
    shortfallCredits,
    billedUsd,
    recalculatedUsd,
    creditsSpent,
    availableCreditsSpent,
    reservedCreditsBefore: beforeSummary.reservedCredits,
    reservedCreditsAfter: afterSummary.reservedCredits,
    usageSource,
    providerTurnCompleteCount,
    providerUsageTurnCount,
    clientTurnBoundaryCount,
    inputAudioBytes,
    inputVideoFrameCount,
    outputAudioBytes,
    providerTotalTokenCount,
  };
};

/**
 * A reconciled ledger can still be unfair. Mark a failed Live request as unsafe
 * when its request ID appears in any paid Live usage row. Keeping the legacy
 * operation name here makes this guard detect regressions from old deployments
 * as well as charges produced by the server-observed gateway.
 */
export const evaluateManagedJourneyFailureBilling = (
  before: ManagedJourneyBillingSnapshot,
  after: ManagedJourneyBillingSnapshot,
  failedLiveRequestIds: string[],
): ManagedJourneyFailureBillingEvidence => {
  const base = evaluateManagedJourneyBilling(before, after);
  const priorUsageIds = new Set(before.usageEntries.map(entry => entry.id));
  const failedIds = new Set(failedLiveRequestIds.filter(Boolean));
  const failedLiveUsage = after.usageEntries.filter((entry) => {
    if (
      priorUsageIds.has(entry.id)
      || (entry.operation !== 'liveGateway' && entry.operation !== 'liveToken')
    ) return false;
    const requestId = entry.metadata?.liveOpenRequestId;
    return typeof requestId === 'string' && failedIds.has(requestId);
  });
  const failedLiveCredits = sum(failedLiveUsage, entry => entry.billedCredits);
  const mismatches = [...base.mismatches];
  if (failedLiveCredits > 0) {
    mismatches.push(
      `${failedLiveUsage.length} failed Live attempt(s) consumed ${failedLiveCredits} managed credits.`,
    );
  }
  return {
    ...base,
    passed: base.passed && failedLiveCredits === 0,
    mismatches,
    failedLiveRequestIds: [...failedIds].sort(),
    failedLiveChargeEntries: failedLiveUsage.length,
    failedLiveCredits,
  };
};
