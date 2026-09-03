// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type {
  ManagedAccountSummaryResponse,
  ManagedBillingLedgerEntry,
  ManagedUsageLedgerEntry,
} from '../core/contracts/backend';
import type { HeadlessClient } from './client';

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

const sum = <T>(items: T[], select: (item: T) => number): number => items.reduce(
  (total, item) => total + (Number.isFinite(select(item)) ? select(item) : 0),
  0,
);

const roundUsd = (value: number): number => Math.round(value * 1_000_000) / 1_000_000;

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
