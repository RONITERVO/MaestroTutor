// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Transactional file quota and deletion accounting. Keep record/quota writes atomic. */

import { appConfig } from '../config';
import { randomUUID } from 'node:crypto';
import { adminDb } from '../firebase';
import { createHttpError } from '../http';
import {
  MANAGED_RUNTIME_RETENTION_MS,
  accountDeletionClaimRef,
  ensureManagedUserDocument,
  managedFileQuotaRef,
  managedFileRef,
  timestampFromMillis
} from '../managedData';
import { ensureManagedFileInventory, listActiveManagedFileSnapshots } from './fileInventory';

const readActiveManagedFileCount = (value: unknown): number => {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed <= 0) return 0;
  return Math.floor(parsed);
};

// Longer than the Functions request lifetime, but finite after a crashed upload.
export const UPLOAD_SLOT_LIFETIME_MS = 15 * 60 * 1000;
interface UploadSlot { id: string; expiresAt: number }
const readUploadSlots = (value: unknown): UploadSlot[] => Array.isArray(value)
  ? value.filter((slot): slot is UploadSlot => typeof slot?.id === 'string' && Number.isFinite(slot.expiresAt))
  : [];

export const reserveManagedUploadSlot = async (uid: string): Promise<string> => {
  await ensureManagedUserDocument(uid);
  await ensureManagedFileInventory(uid);
  const slotId = randomUUID();
  return adminDb.runTransaction(async (transaction: any) => {
    const summaryRef = managedFileQuotaRef(uid);
    const [summarySnapshot, deletionClaim, files] = await Promise.all([
      transaction.get(summaryRef),
      transaction.get(accountDeletionClaimRef(uid)),
      // Include legacy records and stop once enough active files deny admission.
      listActiveManagedFileSnapshots(uid, transaction, appConfig.managedMaxActiveFilesPerUser + 1),
    ]);
    if (deletionClaim.exists) {
      throw createHttpError(409, 'This managed account is being deleted.');
    }
    const slots = readUploadSlots(summarySnapshot.data()?.pendingUploadSlots).filter(slot => slot.expiresAt > Date.now());
    const currentCount = files.length + slots.length;
    if (currentCount >= appConfig.managedMaxActiveFilesPerUser) {
      throw createHttpError(
        403,
        `Managed upload quota reached. Delete files before uploading more than ${appConfig.managedMaxActiveFilesPerUser} active files.`
      );
    }

    transaction.set(summaryRef, {
      activeManagedFileCount: currentCount + 1,
      pendingUploadSlots: [...slots, { id: slotId, expiresAt: Date.now() + UPLOAD_SLOT_LIFETIME_MS }],
      updatedAt: Date.now(),
    }, { merge: true });
    return slotId;
  });
};

export const releaseManagedUploadSlot = async (uid: string, slotId: string): Promise<void> => {
  await adminDb.runTransaction(async (transaction: any) => {
    const summaryRef = managedFileQuotaRef(uid);
    const summarySnapshot = await transaction.get(summaryRef);
    // A delayed failure must not recreate a deleted account's runtime subtree.
    if (!summarySnapshot.exists) return;
    const slots = readUploadSlots(summarySnapshot.data()?.pendingUploadSlots);
    if (!slots.some(slot => slot.id === slotId)) return;
    const currentCount = readActiveManagedFileCount(summarySnapshot.data()?.activeManagedFileCount);
    transaction.update(summaryRef, {
      activeManagedFileCount: Math.max(0, currentCount - 1),
      pendingUploadSlots: slots.filter(slot => slot.id !== slotId),
      updatedAt: Date.now(),
    });
  });
};

/** Transfer the counted slot to durable provider metadata in the same transaction. */
export const commitManagedUploadSlot = async (
  uid: string, slotId: string, fileName: string, metadata: Record<string, unknown>,
): Promise<void> => {
  await adminDb.runTransaction(async (transaction: any) => {
    const summaryRef = managedFileQuotaRef(uid);
    const [summary, deletionClaim] = await Promise.all([
      transaction.get(summaryRef), transaction.get(accountDeletionClaimRef(uid)),
    ]);
    if (deletionClaim.exists) throw createHttpError(409, 'This managed account is being deleted.');
    const slots = readUploadSlots(summary.data()?.pendingUploadSlots);
    const slot = slots.find(value => value.id === slotId);
    if (!slot || slot.expiresAt <= Date.now()) throw createHttpError(409, 'The managed upload reservation expired.');
    transaction.set(managedFileRef(uid, fileName), { ...metadata, uid, name: fileName }, { merge: true });
    transaction.update(summaryRef, { pendingUploadSlots: slots.filter(value => value.id !== slotId), updatedAt: Date.now() });
  });
};

export const markManagedFileDeleted = async (uid: string, fileName: string): Promise<boolean> => (
  adminDb.runTransaction(async (transaction: any) => {
    const fileRef = managedFileRef(uid, fileName);
    const summaryRef = managedFileQuotaRef(uid);
    const [fileSnapshot, summarySnapshot] = await Promise.all([
      transaction.get(fileRef),
      transaction.get(summaryRef),
    ]);

    const fileData = fileSnapshot.data();
    if (!fileSnapshot.exists || fileData?.uid !== uid || fileData?.deletedAt) {
      return false;
    }

    const currentCount = readActiveManagedFileCount(summarySnapshot.data()?.activeManagedFileCount);
    transaction.set(fileRef, {
      deletedAt: Date.now(),
      lastCheckedAt: Date.now(),
      state: 'deleted',
      cleanupPending: false,
      cleanupLastError: null,
      purgeAt: timestampFromMillis(Date.now() + MANAGED_RUNTIME_RETENTION_MS),
    }, { merge: true });
    if (summarySnapshot.exists) {
      transaction.update(summaryRef, {
        activeManagedFileCount: Math.max(0, currentCount - 1),
        updatedAt: Date.now(),
      });
    }
    return true;
  })
);
