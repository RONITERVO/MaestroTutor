// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { getGeminiModels } from '../core/config/models';
import { LIVE_OPEN_TRIGGER, createLiveOpenReason } from '../../shared/liveOpenReason';
import type { HeadlessClient } from './client';
import {
  captureManagedJourneyBilling,
  type ManagedJourneyBillingSnapshot,
  waitForManagedJourneyBillingSettlement,
} from './managedJourneyBilling';

export interface ManagedLiveNoOutputEvidence {
  passed: boolean;
  mismatches: string[];
  requestId: string;
  availableCreditsBefore: number;
  availableCreditsAfter: number;
  lifetimeSpentCreditsBefore: number;
  lifetimeSpentCreditsAfter: number;
  reservedCreditsBefore: number;
  reservedCreditsAfter: number;
  usageRows: number;
  chargeRows: number;
  releaseRows: number;
  releasedCredits: number;
  callbackError: string | null;
}

const isRequest = (metadata: Record<string, unknown> | undefined, requestId: string): boolean => (
  metadata?.liveOpenRequestId === requestId
);

export const evaluateManagedLiveNoOutput = (
  before: ManagedJourneyBillingSnapshot,
  after: ManagedJourneyBillingSnapshot,
  requestId: string,
  callbackError: string | null = null,
): ManagedLiveNoOutputEvidence => {
  const beforeSummary = before.account.account.billingSummary;
  const afterSummary = after.account.account.billingSummary;
  const priorUsageIds = new Set(before.usageEntries.map(entry => entry.id));
  const priorBillingIds = new Set(before.billingEntries.map(entry => entry.id));
  const usageRows = after.usageEntries.filter(entry => (
    !priorUsageIds.has(entry.id)
    && entry.operation === 'liveGateway'
    && isRequest(entry.metadata, requestId)
  ));
  const chargeRows = after.billingEntries.filter(entry => (
    !priorBillingIds.has(entry.id)
    && entry.kind === 'charge'
    && entry.metadata?.operation === 'liveGateway'
    && isRequest(entry.metadata, requestId)
  ));
  const releaseRows = after.billingEntries.filter(entry => (
    !priorBillingIds.has(entry.id)
    && entry.kind === 'reservation-release'
    && entry.metadata?.operation === 'liveGateway'
    && isRequest(entry.metadata, requestId)
  ));
  const releasedCredits = releaseRows.reduce((total, entry) => total + entry.credits, 0);
  const mismatches: string[] = [];

  if (callbackError) mismatches.push(`The gateway reported an error: ${callbackError}`);
  if (beforeSummary.reservedCredits !== 0) mismatches.push('The canary began with reserved credits.');
  if (afterSummary.reservedCredits !== 0) mismatches.push('The canary left reserved credits behind.');
  if (afterSummary.availableCredits !== beforeSummary.availableCredits) {
    mismatches.push('A no-output session changed available credits.');
  }
  if (afterSummary.lifetimeSpentCredits !== beforeSummary.lifetimeSpentCredits) {
    mismatches.push('A no-output session changed lifetime spend.');
  }
  if (usageRows.length !== 0) mismatches.push('A no-output session created a usage row.');
  if (chargeRows.length !== 0) mismatches.push('A no-output session created a charge row.');
  if (releaseRows.length !== 1 || releasedCredits <= 0) {
    mismatches.push('The maximum reservation was not released exactly once with auditable request metadata.');
  }

  return {
    passed: mismatches.length === 0,
    mismatches,
    requestId,
    availableCreditsBefore: beforeSummary.availableCredits,
    availableCreditsAfter: afterSummary.availableCredits,
    lifetimeSpentCreditsBefore: beforeSummary.lifetimeSpentCredits,
    lifetimeSpentCreditsAfter: afterSummary.lifetimeSpentCredits,
    reservedCreditsBefore: beforeSummary.reservedCredits,
    reservedCreditsAfter: afterSummary.reservedCredits,
    usageRows: usageRows.length,
    chargeRows: chargeRows.length,
    releaseRows: releaseRows.length,
    releasedCredits,
    callbackError,
  };
};

const errorMessage = (error: unknown): string => (
  error instanceof Error ? error.message : String(error)
);

/**
 * Opens the same managed Live client transport as the UI, sends no content, and
 * closes after readiness. This is a real-provider money-safety canary: setup by
 * itself must release the conservative reservation without a usage charge.
 */
export const runManagedLiveNoOutputCanary = async (
  client: HeadlessClient,
  options: { model?: string; timeoutMs?: number } = {},
): Promise<ManagedLiveNoOutputEvidence> => {
  if (client.accessMode !== 'managed') throw new Error('The no-output canary requires managed access.');
  const operationId = client.runtime.ids.create('live-no-output-canary');
  const requestId = client.runtime.ids.create('live-no-output');
  const before = await captureManagedJourneyBilling(client, operationId);
  if (before.account.account.billingSummary.reservedCredits !== 0) {
    throw new Error('Refusing to run the no-output canary while another operation has credits reserved.');
  }

  let callbackError: string | null = null;
  let closeResolved = false;
  let resolveClose: () => void = () => undefined;
  const closed = new Promise<void>((resolve) => { resolveClose = resolve; });
  const session = await client.ai.live.connect({
    model: options.model || getGeminiModels().audio.conversation,
    liveOpenReason: createLiveOpenReason(LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE, {
      requestId,
      now: new Date(client.runtime.clock.now()),
    }),
    config: {
      responseModalities: ['AUDIO'],
      inputAudioTranscription: {},
      outputAudioTranscription: {},
    },
    callbacks: {
      onerror: (error: unknown) => { callbackError = errorMessage(error); },
      onclose: () => {
        if (!closeResolved) {
          closeResolved = true;
          resolveClose();
        }
      },
    },
  });
  session.close();

  const timeoutMs = Math.max(1_000, options.timeoutMs ?? 20_000);
  let timeout: ReturnType<typeof globalThis.setTimeout> | undefined;
  try {
    await Promise.race([
      closed,
      new Promise<void>((_, reject) => {
        timeout = globalThis.setTimeout(
          () => reject(new Error('Managed Live no-output canary timed out while closing.')),
          timeoutMs,
        );
      }),
    ]);
  } finally {
    if (timeout) globalThis.clearTimeout(timeout);
  }
  const after = await waitForManagedJourneyBillingSettlement(client, operationId, 30, 500);
  return evaluateManagedLiveNoOutput(before, after, requestId, callbackError);
};
