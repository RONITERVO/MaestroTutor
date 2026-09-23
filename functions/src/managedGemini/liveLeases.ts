// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Shared Live/music concurrency leases and account-deletion fences. No provider or billing calls. */

import { randomUUID } from 'node:crypto';
import { appConfig } from '../config';
import { adminDb } from '../firebase';
import { createHttpError } from '../http';
import {
  MANAGED_RUNTIME_RETENTION_MS,
  accountDeletionClaimRef,
  ensureManagedUserDocument,
  managedLiveLeaseRef,
  managedLiveQuotaRef,
  timestampFromMillis
} from '../managedData';

interface ManagedLiveLeaseRecord {
  leaseId: string;
  purpose: 'live' | 'music';
  expiresAt: number;
}

const readActiveManagedLiveLeases = (value: unknown, now = Date.now()): ManagedLiveLeaseRecord[] => {
  if (!Array.isArray(value)) return [];
  return value
    .map((item) => {
      if (!item || typeof item !== 'object') return null;
      const leaseId = typeof (item as { leaseId?: unknown }).leaseId === 'string'
        ? (item as { leaseId: string }).leaseId
        : '';
      const purpose = (item as { purpose?: unknown }).purpose === 'music' ? 'music' : 'live';
      const expiresAt = Number((item as { expiresAt?: unknown }).expiresAt || 0);
      if (!leaseId || !Number.isFinite(expiresAt) || expiresAt <= now) return null;
      return { leaseId, purpose, expiresAt };
    })
    .filter((item): item is ManagedLiveLeaseRecord => Boolean(item));
};

export const reserveManagedLiveLease = async (params: {
  uid: string;
  purpose: 'live' | 'music';
  durationMs: number;
  metadata?: Record<string, unknown>;
}): Promise<ManagedLiveLeaseRecord> => {
  await ensureManagedUserDocument(params.uid);
  const currentTime = Date.now();
  const lease: ManagedLiveLeaseRecord = {
    leaseId: randomUUID(),
    purpose: params.purpose,
    expiresAt: currentTime + params.durationMs,
  };

  await adminDb.runTransaction(async (transaction: any) => {
    const summaryRef = managedLiveQuotaRef(params.uid);
    const [summarySnapshot, deletionClaim] = await Promise.all([
      transaction.get(summaryRef),
      transaction.get(accountDeletionClaimRef(params.uid)),
    ]);
    if (deletionClaim.exists) {
      throw createHttpError(409, 'This managed account is being deleted.');
    }
    const currentLeases = readActiveManagedLiveLeases(summarySnapshot.data()?.activeManagedLiveLeases, currentTime);
    if (currentLeases.length >= appConfig.managedMaxActiveLiveSockets) {
      throw createHttpError(
        429,
        `Too many active managed live sockets. Close an existing live session and retry. Maximum active sockets per user: ${appConfig.managedMaxActiveLiveSockets}.`
      );
    }

    transaction.set(summaryRef, {
      activeManagedLiveLeases: [...currentLeases, lease],
      updatedAt: currentTime,
    }, { merge: true });
    transaction.set(managedLiveLeaseRef(params.uid, lease.leaseId), {
      uid: params.uid,
      purpose: params.purpose,
      createdAt: currentTime,
      expiresAt: lease.expiresAt,
      releasedAt: null,
      metadata: params.metadata || {},
      purgeAt: timestampFromMillis(lease.expiresAt + MANAGED_RUNTIME_RETENTION_MS),
    }, { merge: true });
  });

  return lease;
};

export const releaseManagedLiveLease = async (uid: string, leaseId: string): Promise<{ ok: boolean }> => {
  if (!leaseId.trim()) return { ok: false };
  const currentTime = Date.now();
  const released = await adminDb.runTransaction(async (transaction: any) => {
    const summaryRef = managedLiveQuotaRef(uid);
    const leaseRef = managedLiveLeaseRef(uid, leaseId);
    const [summarySnapshot, leaseSnapshot] = await Promise.all([
      transaction.get(summaryRef),
      transaction.get(leaseRef),
    ]);
    if (!leaseSnapshot.exists) return false;
    const currentLeases = readActiveManagedLiveLeases(summarySnapshot.data()?.activeManagedLiveLeases, currentTime);
    transaction.set(summaryRef, {
      activeManagedLiveLeases: currentLeases.filter((lease) => lease.leaseId !== leaseId),
      updatedAt: currentTime,
    }, { merge: true });
    transaction.set(leaseRef, {
      releasedAt: currentTime,
      purgeAt: timestampFromMillis(currentTime + MANAGED_RUNTIME_RETENTION_MS),
    }, { merge: true });
    return true;
  });
  return { ok: released };
};
