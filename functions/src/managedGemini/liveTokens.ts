// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Scoped Live token minting, fixed-window billing and failure rollback. */

import {
  getLiveOpenOrigin,
  type LiveOpenReason,
} from '../../../shared/liveOpenReason';
import type { AppUser } from '../auth';
import { appConfig } from '../config';
import {
  requireAllowedManagedModel,
  requireManagedLiveOpenReason,
  requireSafeManagedLiveConfig
} from '../geminiPolicy';
import { createHttpError } from '../http';
import {
  releaseManagedReservation,
  reserveManagedCredits,
  settleManagedReservation
} from '../managedBilling';
import {
  calculateManagedLiveWindowCredits,
  calculateManagedLiveWindowUsd,
  getManagedLiveWindowTokenBudget
} from '../pricing';
import { getGeminiClient } from './client';
import { releaseManagedLiveLease, reserveManagedLiveLease } from './liveLeases';

export const createManagedLiveToken = async (params: {
  uid: string;
  user: AppUser;
  model: string;
  config?: Record<string, unknown>;
  purpose?: 'live';
  durationSeconds?: number;
  liveOpenReason: unknown;
}) => {
  const liveOpenReason: LiveOpenReason = requireManagedLiveOpenReason(params.liveOpenReason);
  const liveOpenMetadata = {
    liveOpenTrigger: liveOpenReason.trigger,
    liveOpenOrigin: getLiveOpenOrigin(liveOpenReason.trigger),
    liveOpenRequestId: liveOpenReason.requestId,
    liveOpenRequestedAt: liveOpenReason.requestedAt,
  };
  const model = requireAllowedManagedModel(
    params.model,
    appConfig.managedAllowedLiveModels,
    'live audio',
  );
  const liveConfig = requireSafeManagedLiveConfig(params.config);
  const liveWindowSeconds = appConfig.managedLiveTokenLifetimeSeconds;
  const fixedCredits = calculateManagedLiveWindowCredits(liveWindowSeconds);
  const billedUsd = calculateManagedLiveWindowUsd(liveWindowSeconds);
  const liveTokenBudget = getManagedLiveWindowTokenBudget(liveWindowSeconds);

  const lease = await reserveManagedLiveLease({
    uid: params.uid,
    purpose: 'live',
    durationMs: liveWindowSeconds * 1000,
    metadata: liveOpenMetadata,
  });

  let reservation: Awaited<ReturnType<typeof reserveManagedCredits>>;
  try {
    reservation = await reserveManagedCredits({
      uid: params.uid,
      user: params.user,
      operation: 'liveToken',
      model,
      estimatedCredits: fixedCredits,
      estimatedUsd: billedUsd,
      metadata: {
        purpose: 'live',
        leaseId: lease.leaseId,
        requestedDurationSeconds: params.durationSeconds || null,
        maxWindowSeconds: liveWindowSeconds,
        ...liveOpenMetadata,
        ...liveTokenBudget,
      },
    });
  } catch (error) {
    await releaseManagedLiveLease(params.uid, lease.leaseId).catch(() => undefined);
    throw error;
  }

  const expireTime = new Date(lease.expiresAt).toISOString();

  try {
    if (lease.expiresAt <= Date.now()) throw createHttpError(409, 'The managed Live lease expired before token creation.');
    const tokenResponse = await getGeminiClient().authTokens.create({
      config: {
        uses: appConfig.geminiLiveTokenUses,
        expireTime,
        httpOptions: {
          apiVersion: 'v1alpha',
        },
        liveConnectConstraints: {
          model,
          ...(liveConfig ? { config: liveConfig } : {}),
        },
      },
    } as any);

    const token = typeof (tokenResponse as any)?.name === 'string'
      ? (tokenResponse as any).name
      : (typeof (tokenResponse as any)?.token === 'string' ? (tokenResponse as any).token : '');
    if (!token) {
      throw createHttpError(500, 'Backend could not mint a Gemini live token.');
    }

    const billingSummary = await settleManagedReservation({
      uid: params.uid,
      reservationId: reservation.reservationId,
      billedCredits: fixedCredits,
      billedUsd,
      operation: 'liveToken',
      model,
      metadata: {
        purpose: 'live',
        leaseId: lease.leaseId,
        uses: appConfig.geminiLiveTokenUses,
        maxWindowSeconds: liveWindowSeconds,
        ...liveOpenMetadata,
        ...liveTokenBudget,
      },
    });

    return {
      leaseId: lease.leaseId,
      token,
      expiresAt: typeof (tokenResponse as any)?.expireTime === 'string'
        ? (tokenResponse as any).expireTime
        : expireTime,
      uses: appConfig.geminiLiveTokenUses,
      billingSummary,
    };
  } catch (error) {
    await releaseManagedReservation(params.uid, reservation.reservationId, 'live-token-mint-failed')
      .catch(() => undefined);
    await releaseManagedLiveLease(params.uid, lease.leaseId).catch(() => undefined);
    throw error;
  }
};
