// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

/**
 * Canonical Firestore paths for managed mode.
 *
 * Keep these in one place. The first managed-mode draft repeated path strings
 * across billing, Gemini and account deletion, which is how deletion ended up
 * targeting a collection that the billing code no longer wrote.
 */

import { createHash } from 'node:crypto';
import { Timestamp } from 'firebase-admin/firestore';
import { adminDb } from './firebase';
import { makePurchaseClaimId } from '../../shared/billing/purchaseClaims';

export const MANAGED_SCHEMA_VERSION = 2;

const DAY_MS = 24 * 60 * 60 * 1_000;

/** Operational duplicates may be removed after the canonical ledgers exist. */
export const MANAGED_OPERATIONAL_RETENTION_MS = 90 * DAY_MS;
/** Deleted file/lease metadata is useful briefly for support and reconciliation. */
export const MANAGED_RUNTIME_RETENTION_MS = 30 * DAY_MS;
/** Safety reports have a bounded, documented moderation window. */
export const MANAGED_REPORT_RETENTION_MS = 365 * DAY_MS;

export const timestampFromMillis = (milliseconds: number): Timestamp => (
  Timestamp.fromMillis(Math.max(0, Math.floor(milliseconds)))
);

export const managedUserRef = (uid: string) => adminDb.collection('users').doc(uid);
export const managedAccountRef = (uid: string) => (
  managedUserRef(uid).collection('managedAccounts').doc('default')
);
export const managedEntitlementsCollection = (uid: string) => (
  managedAccountRef(uid).collection('entitlements')
);
export const managedBillingEventsCollection = (uid: string) => (
  managedAccountRef(uid).collection('billingEvents')
);
export const managedUsageEventsCollection = (uid: string) => (
  managedAccountRef(uid).collection('usageEvents')
);
export const managedReservationsCollection = (uid: string) => (
  managedAccountRef(uid).collection('reservations')
);
export const managedReservationRef = (uid: string, reservationId: string) => (
  managedReservationsCollection(uid).doc(reservationId)
);

export const managedFilesCollection = (uid: string) => managedUserRef(uid).collection('files');
export const managedFileRef = (uid: string, fileName: string) => (
  managedFilesCollection(uid).doc(createHash('sha256').update(fileName).digest('hex'))
);
export const managedLiveLeaseRef = (uid: string, leaseId: string) => (
  managedUserRef(uid).collection('liveLeases').doc(leaseId)
);
export const managedFileQuotaRef = (uid: string) => (
  managedUserRef(uid).collection('runtime').doc('fileQuota')
);
export const managedLiveQuotaRef = (uid: string) => (
  managedUserRef(uid).collection('runtime').doc('liveQuota')
);

export const purchaseClaimsCollection = () => adminDb.collection('purchaseClaims');
export const purchaseClaimId = makePurchaseClaimId;
export const checkoutGrantsCollection = () => adminDb.collection('checkoutGrants');
export const reportsCollection = () => adminDb.collection('reports');
export const rateLimitWindowsCollection = () => adminDb.collection('rateLimitWindows');
export const cleanupJobsCollection = () => adminDb.collection('cleanupJobs');
export const accountDeletionClaimRef = (uid: string) => (
  adminDb.collection('accountDeletionClaims')
    .doc(createHash('sha256').update(uid).digest('hex'))
);

const isAlreadyExistsError = (error: unknown): boolean => {
  const code = (error as { code?: unknown })?.code;
  return code === 6 || code === '6' || code === 'already-exists';
};

/**
 * Materialize the user root without turning it into another hot document.
 *
 * Existing roots are read but not rewritten on ordinary requests. Concurrent
 * cold starts may race to create the root; `create` makes that race harmless
 * without overwriting lifecycle state. The deletion claim read prevents a
 * delayed request from recreating a deleted user tree.
 */
export const ensureManagedUserDocument = async (uid: string): Promise<void> => {
  const ref = managedUserRef(uid);
  const [snapshot, deletionClaim] = await Promise.all([
    ref.get(),
    accountDeletionClaimRef(uid).get(),
  ]);
  if (deletionClaim.exists || snapshot.data()?.status === 'deleting') {
    const error = new Error('This managed account is being deleted.') as Error & { status?: number };
    error.status = 409;
    throw error;
  }
  if (!snapshot.exists) {
    const createdAt = Date.now();
    try {
      await ref.create({
        createdAt,
        updatedAt: createdAt,
        status: 'active',
        schemaVersion: MANAGED_SCHEMA_VERSION,
      });
    } catch (error) {
      if (!isAlreadyExistsError(error)) throw error;
    }
  }
};
