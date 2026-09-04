// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import {
  createHash,
  randomBytes,
  randomUUID,
  timingSafeEqual,
} from 'node:crypto';
import { FieldValue } from 'firebase-admin/firestore';
import type { AppUser } from './auth';
import {
  getLiveOpenOrigin,
  type LiveOpenReason,
} from '../../shared/liveOpenReason';
import {
  createLiveGatewayUsageCheckpoint,
  getLiveGatewayBillableUsage,
  mergeLiveGatewayUsageCheckpoints,
  type LiveGatewayUsageCheckpoint,
} from '../../shared/billing/liveGateway';
import { usdToCredits as usdToCreditsAtRate } from '../../shared/pricing/credits';
import { appConfig } from './config';
import { adminDb } from './firebase';
import {
  releaseManagedLiveLease,
  reserveManagedLiveLease,
} from './gemini';
import {
  requireAllowedManagedModel,
  requireManagedLiveOpenReason,
  requireSafeManagedLiveConfig,
} from './geminiPolicy';
import { createHttpError, getErrorMessage } from './http';
import {
  MANAGED_RUNTIME_RETENTION_MS,
  liveGatewaySessionRef,
  liveGatewaySessionsCollection,
  liveGatewayTicketRef,
  liveGatewayTicketsCollection,
  timestampFromMillis,
} from './managedData';
import {
  releaseManagedReservation,
  reserveManagedCredits,
  settleManagedReservation,
  type ManagedBillingSummary,
} from './managedBilling';
import {
  calculateManagedLiveGatewayWindowCredits,
  calculateManagedLiveGatewayWindowUsd,
  getManagedLiveGatewayTokenBudget,
  pricingEffectiveAt,
  usageMetadataToUsd,
} from './pricing';

const TICKET_SECRET_BYTES = 32;
const FINALIZATION_RECLAIM_MS = 30_000;

interface ManagedLiveGatewayTicketRecord {
  uid: string;
  status: 'issued' | 'consumed' | 'expired';
  secretHash?: string;
  reservationId: string;
  leaseId: string;
  model: string;
  config?: Record<string, unknown>;
  createdAt: number;
  expiresAt: number;
  sessionExpiresAt: number;
  consumedAt: number | null;
  metadata: Record<string, unknown>;
  creditsPerUsd: number;
  pricingEffectiveAt: string;
}

interface ManagedLiveGatewaySessionRecord {
  uid: string;
  status: 'active' | 'finalizing' | 'settled' | 'released';
  ticketId: string;
  reservationId: string;
  leaseId: string;
  model: string;
  createdAt: number;
  deadlineAt: number;
  updatedAt: number;
  checkpoint: LiveGatewayUsageCheckpoint;
  metadata: Record<string, unknown>;
  creditsPerUsd?: number;
  pricingEffectiveAt?: string;
  finalizationClaimId?: string;
  finalizationClaimedAt?: number;
  finalizedAt?: number;
  finalizationReason?: string;
  finalizationError?: string | null;
  billedCredits?: number;
  billedUsd?: number;
  usageSource?: string;
  billingSummary?: ManagedBillingSummary;
}

export interface ManagedLiveGatewayTicketResponse {
  transport: 'gateway';
  gatewayUrl: string;
  ticket: string;
  ticketExpiresAt: string;
  sessionExpiresAt: string;
  billingSummary: ManagedBillingSummary;
}

export interface ConsumedManagedLiveGatewayTicket {
  sessionId: string;
  uid: string;
  model: string;
  config?: Record<string, unknown>;
  deadlineAt: number;
  pricingEffectiveAt: string;
}

export interface ManagedLiveGatewayFinalization {
  status: 'finalizing' | 'settled' | 'released';
  billedCredits: number;
  billedUsd: number;
  usefulOutput: boolean;
  usageSource: string;
  billingSummary?: ManagedBillingSummary;
}

const sha256 = (value: string): string => createHash('sha256').update(value).digest('hex');

const safeHashMatch = (actual: string, expected: string): boolean => {
  const actualBytes = Buffer.from(actual, 'hex');
  const expectedBytes = Buffer.from(expected, 'hex');
  return actualBytes.length === expectedBytes.length && timingSafeEqual(actualBytes, expectedBytes);
};

const gatewayWebSocketUrl = (): string => {
  if (!appConfig.managedLiveGatewayUrl) {
    throw createHttpError(503, 'Managed Live is unavailable until its metered gateway is configured.');
  }
  let url: URL;
  try {
    url = new URL(appConfig.managedLiveGatewayUrl);
  } catch {
    throw createHttpError(500, 'MANAGED_LIVE_GATEWAY_URL is invalid.');
  }
  if (url.protocol === 'https:') url.protocol = 'wss:';
  if (url.protocol !== 'wss:' || url.username || url.password || url.search || url.hash) {
    throw createHttpError(500, 'MANAGED_LIVE_GATEWAY_URL must be a clean HTTPS/WSS URL.');
  }
  return url.toString().replace(/\/$/, '');
};

const parseTicket = (value: string): { ticketId: string; secret: string } => {
  const match = /^([A-Za-z0-9_-]{20,80})\.([A-Za-z0-9_-]{32,100})$/.exec(value.trim());
  if (!match) throw createHttpError(401, 'Invalid managed Live gateway ticket.');
  return { ticketId: match[1], secret: match[2] };
};

const liveOpenMetadata = (reason: LiveOpenReason): Record<string, unknown> => ({
  liveOpenTrigger: reason.trigger,
  liveOpenOrigin: getLiveOpenOrigin(reason.trigger),
  liveOpenRequestId: reason.requestId,
  liveOpenRequestedAt: reason.requestedAt,
});

export const createManagedLiveGatewayTicket = async (params: {
  uid: string;
  user: AppUser;
  model: string;
  config?: Record<string, unknown>;
  liveOpenReason: unknown;
}): Promise<ManagedLiveGatewayTicketResponse> => {
  const gatewayUrl = gatewayWebSocketUrl();
  const reason = requireManagedLiveOpenReason(params.liveOpenReason);
  const metadata = liveOpenMetadata(reason);
  const model = requireAllowedManagedModel(
    params.model,
    appConfig.managedAllowedLiveModels,
    'live audio',
  );
  const config = requireSafeManagedLiveConfig(params.config);
  const windowSeconds = appConfig.managedLiveTokenLifetimeSeconds;
  const estimatedCredits = calculateManagedLiveGatewayWindowCredits(windowSeconds);
  const estimatedUsd = calculateManagedLiveGatewayWindowUsd(windowSeconds);
  const budget = getManagedLiveGatewayTokenBudget(windowSeconds);
  const ticketId = randomUUID();
  const secret = randomBytes(TICKET_SECRET_BYTES).toString('base64url');
  const currentTime = Date.now();
  const expiresAt = currentTime + appConfig.managedLiveGatewayTicketSeconds * 1_000;
  const sessionExpiresAt = currentTime + windowSeconds * 1_000;

  const lease = await reserveManagedLiveLease({
    uid: params.uid,
    purpose: 'live',
    durationMs: windowSeconds * 1_000,
    metadata: { ...metadata, ticketId, transport: 'gateway' },
  });
  let reservationId = '';
  try {
    const reservation = await reserveManagedCredits({
      uid: params.uid,
      user: params.user,
      operation: 'liveGateway',
      model,
      estimatedCredits,
      estimatedUsd,
      metadata: {
        purpose: 'live',
        leaseId: lease.leaseId,
        ticketId,
        maxWindowSeconds: windowSeconds,
        transport: 'gateway',
        creditsPerUsd: appConfig.managedCreditsPerUsd,
        pricingEffectiveAt,
        ...metadata,
        ...budget,
      },
    });
    reservationId = reservation.reservationId;
    const record: ManagedLiveGatewayTicketRecord = {
      uid: params.uid,
      status: 'issued',
      secretHash: sha256(secret),
      reservationId,
      leaseId: lease.leaseId,
      model,
      ...(config ? { config } : {}),
      createdAt: currentTime,
      expiresAt,
      sessionExpiresAt,
      consumedAt: null,
      metadata,
      creditsPerUsd: appConfig.managedCreditsPerUsd,
      pricingEffectiveAt,
    };
    await liveGatewayTicketRef(ticketId).create({
      ...record,
      purgeAt: timestampFromMillis(sessionExpiresAt + MANAGED_RUNTIME_RETENTION_MS),
    });
    return {
      transport: 'gateway',
      gatewayUrl,
      ticket: `${ticketId}.${secret}`,
      ticketExpiresAt: new Date(expiresAt).toISOString(),
      sessionExpiresAt: new Date(sessionExpiresAt).toISOString(),
      billingSummary: reservation.billingSummary,
    };
  } catch (error) {
    if (reservationId) {
      await releaseManagedReservation(params.uid, reservationId, 'live-gateway-ticket-failed')
        .catch(() => undefined);
    }
    await releaseManagedLiveLease(params.uid, lease.leaseId).catch(() => undefined);
    throw error;
  }
};

export const consumeManagedLiveGatewayTicket = async (
  ticketValue: string,
  expectedPricingEffectiveAt?: string,
): Promise<ConsumedManagedLiveGatewayTicket> => {
  const { ticketId, secret } = parseTicket(ticketValue);
  const currentTime = Date.now();
  const result = await adminDb.runTransaction(async (transaction) => {
    const ticketRef = liveGatewayTicketRef(ticketId);
    const sessionRef = liveGatewaySessionRef(ticketId);
    const snapshot = await transaction.get(ticketRef);
    if (!snapshot.exists) throw createHttpError(401, 'Invalid managed Live gateway ticket.');
    const ticket = snapshot.data() as ManagedLiveGatewayTicketRecord;
    if (ticket.status !== 'issued') {
      throw createHttpError(409, 'Managed Live gateway ticket was already used.');
    }
    if (
      typeof ticket.secretHash !== 'string'
      || !safeHashMatch(sha256(secret), ticket.secretHash)
    ) {
      throw createHttpError(401, 'Invalid managed Live gateway ticket.');
    }
    if (
      expectedPricingEffectiveAt
      && ticket.pricingEffectiveAt !== expectedPricingEffectiveAt
    ) {
      throw createHttpError(409, 'Managed Live gateway pricing version does not match the ticket issuer.');
    }
    if (ticket.expiresAt <= currentTime || ticket.sessionExpiresAt <= currentTime) {
      transaction.set(ticketRef, {
        status: 'expired',
        secretHash: FieldValue.delete(),
        config: FieldValue.delete(),
        updatedAt: currentTime,
      }, { merge: true });
      return { kind: 'expired' as const, ticket };
    }
    const session: ManagedLiveGatewaySessionRecord = {
      uid: ticket.uid,
      status: 'active',
      ticketId,
      reservationId: ticket.reservationId,
      leaseId: ticket.leaseId,
      model: ticket.model,
      createdAt: currentTime,
      deadlineAt: ticket.sessionExpiresAt,
      updatedAt: currentTime,
      checkpoint: createLiveGatewayUsageCheckpoint(),
      metadata: ticket.metadata,
      creditsPerUsd: ticket.creditsPerUsd,
      pricingEffectiveAt: ticket.pricingEffectiveAt,
    };
    transaction.set(ticketRef, {
      status: 'consumed',
      consumedAt: currentTime,
      secretHash: FieldValue.delete(),
      config: FieldValue.delete(),
      updatedAt: currentTime,
    }, { merge: true });
    transaction.create(sessionRef, {
      ...session,
      purgeAt: timestampFromMillis(ticket.sessionExpiresAt + MANAGED_RUNTIME_RETENTION_MS),
    });
    return { kind: 'active' as const, session, config: ticket.config };
  });

  if (result.kind === 'expired') {
    await Promise.allSettled([
      releaseManagedReservation(result.ticket.uid, result.ticket.reservationId, 'live-gateway-ticket-expired'),
      releaseManagedLiveLease(result.ticket.uid, result.ticket.leaseId),
    ]);
    throw createHttpError(410, 'Managed Live gateway ticket expired.');
  }
  return {
    sessionId: ticketId,
    uid: result.session.uid,
    model: result.session.model,
    ...(result.config ? { config: result.config } : {}),
    deadlineAt: result.session.deadlineAt,
    pricingEffectiveAt: result.session.pricingEffectiveAt || pricingEffectiveAt,
  };
};

export const checkpointManagedLiveGatewaySession = async (
  sessionId: string,
  checkpoint: LiveGatewayUsageCheckpoint,
): Promise<void> => {
  const currentTime = Date.now();
  await adminDb.runTransaction(async (transaction) => {
    const ref = liveGatewaySessionRef(sessionId);
    const snapshot = await transaction.get(ref);
    if (!snapshot.exists) throw createHttpError(404, 'Managed Live gateway session was not found.');
    const session = snapshot.data() as ManagedLiveGatewaySessionRecord;
    if (session.status !== 'active') return;
    transaction.set(ref, {
      checkpoint: mergeLiveGatewayUsageCheckpoints(session.checkpoint, checkpoint),
      updatedAt: currentTime,
    }, { merge: true });
  });
};

export const finalizeManagedLiveGatewaySession = async (
  sessionId: string,
  reason: string,
  finalCheckpoint?: LiveGatewayUsageCheckpoint,
): Promise<ManagedLiveGatewayFinalization> => {
  const claimId = randomUUID();
  const currentTime = Date.now();
  const claim = await adminDb.runTransaction(async (transaction) => {
    const ref = liveGatewaySessionRef(sessionId);
    const snapshot = await transaction.get(ref);
    if (!snapshot.exists) throw createHttpError(404, 'Managed Live gateway session was not found.');
    const storedSession = snapshot.data() as ManagedLiveGatewaySessionRecord;
    const session = finalCheckpoint
      ? {
          ...storedSession,
          checkpoint: mergeLiveGatewayUsageCheckpoints(storedSession.checkpoint, finalCheckpoint),
        }
      : storedSession;
    if (session.status === 'settled' || session.status === 'released') {
      return { kind: 'complete' as const, session };
    }
    if (
      session.status === 'finalizing'
      && Number(session.finalizationClaimedAt || 0) > currentTime - FINALIZATION_RECLAIM_MS
    ) {
      return { kind: 'pending' as const, session };
    }
    transaction.set(ref, {
      status: 'finalizing',
      checkpoint: session.checkpoint,
      finalizationClaimId: claimId,
      finalizationClaimedAt: currentTime,
      finalizationReason: reason.slice(0, 100),
      finalizationError: null,
      updatedAt: currentTime,
    }, { merge: true });
    return { kind: 'claimed' as const, session };
  });

  if (claim.kind === 'complete') {
    return {
      status: claim.session.status === 'settled' ? 'settled' : 'released',
      billedCredits: Number(claim.session.billedCredits || 0),
      billedUsd: Number(claim.session.billedUsd || 0),
      usefulOutput: Boolean(claim.session.checkpoint?.usefulOutput),
      usageSource: claim.session.usageSource || 'none',
      ...(claim.session.billingSummary ? { billingSummary: claim.session.billingSummary } : {}),
    };
  }
  if (claim.kind === 'pending') {
    return {
      status: 'finalizing',
      billedCredits: 0,
      billedUsd: 0,
      usefulOutput: Boolean(claim.session.checkpoint?.usefulOutput),
      usageSource: 'pending',
    };
  }

  const session = claim.session;
  const billable = getLiveGatewayBillableUsage(session.checkpoint);
  const billedUsd = billable.billable
    ? usageMetadataToUsd(
        session.model,
        billable.usageMetadata as unknown as Record<string, unknown>,
        'live',
      )
    : 0;
  const creditsPerUsd = Number(session.creditsPerUsd || appConfig.managedCreditsPerUsd);
  const billedCredits = usdToCreditsAtRate(billedUsd, creditsPerUsd);
  try {
    const billingSummary = billedCredits > 0
      ? await settleManagedReservation({
          uid: session.uid,
          reservationId: session.reservationId,
          billedCredits,
          billedUsd,
          operation: 'liveGateway',
          model: session.model,
          metadata: {
            leaseId: session.leaseId,
            ticketId: session.ticketId,
            transport: 'gateway',
            finalizationReason: reason.slice(0, 100),
            usefulOutput: session.checkpoint.usefulOutput,
            usageSource: billable.source,
            creditsPerUsd,
            pricingEffectiveAt: session.pricingEffectiveAt || pricingEffectiveAt,
            providerMessageCount: session.checkpoint.providerMessageCount,
            providerTurnComplete: session.checkpoint.providerTurnComplete,
            providerTurnCompleteCount: session.checkpoint.providerTurnCompleteCount,
            providerUsageTurnCount: session.checkpoint.providerTurnUsage?.length || 0,
            clientTurnBoundaryCount: session.checkpoint.clientTurnBoundaryCount || 0,
            inputAudioBytes: session.checkpoint.inputAudioBytes,
            inputVideoBytes: session.checkpoint.inputVideoBytes || 0,
            inputVideoFrameCount: session.checkpoint.inputVideoFrameCount || 0,
            outputAudioBytes: session.checkpoint.outputAudioBytes,
            ...session.metadata,
            ...billable.usageMetadata,
          },
        })
      : await releaseManagedReservation(
          session.uid,
          session.reservationId,
          `live-gateway-${reason.slice(0, 60)}`,
        );
    await releaseManagedLiveLease(session.uid, session.leaseId).catch(() => undefined);
    const finalizedAt = Date.now();
    const status = billedCredits > 0 ? 'settled' as const : 'released' as const;
    await liveGatewaySessionRef(sessionId).set({
      status,
      finalizedAt,
      updatedAt: finalizedAt,
      billedCredits,
      billedUsd,
      usageSource: billable.source,
      billingSummary,
      finalizationError: null,
      // Scrub content that could have been persisted by a previous gateway
      // revision; only operational billing evidence belongs in this record.
      config: FieldValue.delete(),
      purgeAt: timestampFromMillis(finalizedAt + MANAGED_RUNTIME_RETENTION_MS),
    }, { merge: true });
    return {
      status,
      billedCredits,
      billedUsd,
      usefulOutput: session.checkpoint.usefulOutput,
      usageSource: billable.source,
      billingSummary,
    };
  } catch (error) {
    // Update only an existing record. Account deletion may have removed the
    // top-level session while provider shutdown was still in flight; a plain
    // merge-set would recreate an orphan after deletion.
    await adminDb.runTransaction(async (transaction) => {
      const ref = liveGatewaySessionRef(sessionId);
      const snapshot = await transaction.get(ref);
      if (!snapshot.exists) return;
      transaction.set(ref, {
        finalizationError: getErrorMessage(error).slice(0, 1_000),
        updatedAt: Date.now(),
      }, { merge: true });
    }).catch(() => undefined);
    throw error;
  }
};

export const recoverManagedLiveGatewayBilling = async (limit = 100): Promise<{
  expiredTickets: number;
  finalizedSessions: number;
  failed: number;
}> => {
  const currentTime = Date.now();
  const boundedLimit = Math.max(1, Math.min(200, Math.floor(limit)));
  const [ticketSnapshot, sessionSnapshot] = await Promise.all([
    liveGatewayTicketsCollection().where('status', '==', 'issued')
      .where('expiresAt', '<=', currentTime).orderBy('expiresAt').limit(boundedLimit).get(),
    liveGatewaySessionsCollection().where('status', 'in', ['active', 'finalizing'])
      .where('deadlineAt', '<=', currentTime).orderBy('deadlineAt').limit(boundedLimit).get(),
  ]);
  let expiredTickets = 0;
  let finalizedSessions = 0;
  let failed = 0;
  for (const doc of ticketSnapshot.docs) {
    try {
      const claimed = await adminDb.runTransaction(async (transaction) => {
        const snapshot = await transaction.get(doc.ref);
        if (!snapshot.exists) return null;
        const ticket = snapshot.data() as ManagedLiveGatewayTicketRecord;
        if (ticket.status !== 'issued' || ticket.expiresAt > currentTime) return null;
        transaction.set(doc.ref, {
          status: 'expired',
          secretHash: FieldValue.delete(),
          config: FieldValue.delete(),
          updatedAt: currentTime,
        }, { merge: true });
        return ticket;
      });
      if (!claimed) continue;
      await Promise.all([
        releaseManagedReservation(claimed.uid, claimed.reservationId, 'live-gateway-ticket-expired'),
        releaseManagedLiveLease(claimed.uid, claimed.leaseId),
      ]);
      expiredTickets += 1;
    } catch (error) {
      failed += 1;
      console.error('[live-gateway] Failed to expire unused ticket.', { ticketId: doc.id, error });
    }
  }
  for (const doc of sessionSnapshot.docs) {
    const session = doc.data() as ManagedLiveGatewaySessionRecord;
    if (session.status !== 'active' && session.status !== 'finalizing') continue;
    try {
      const result = await finalizeManagedLiveGatewaySession(doc.id, 'recovery-deadline');
      if (result.status !== 'finalizing') finalizedSessions += 1;
    } catch (error) {
      failed += 1;
      console.error('[live-gateway] Failed to recover session billing.', { sessionId: doc.id, error });
    }
  }
  return { expiredTickets, finalizedSessions, failed };
};
