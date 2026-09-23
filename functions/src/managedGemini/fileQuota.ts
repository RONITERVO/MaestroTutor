// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Transactional file quota and deletion accounting. Keep record/quota writes atomic. */

import { appConfig } from '../config';
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

const readActiveManagedFileCount = (value: unknown): number => {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed <= 0) return 0;
  return Math.floor(parsed);
};

export const reserveManagedUploadSlot = async (uid: string): Promise<void> => {
  await ensureManagedUserDocument(uid);
  await adminDb.runTransaction(async (transaction: any) => {
    const summaryRef = managedFileQuotaRef(uid);
    const [summarySnapshot, deletionClaim] = await Promise.all([
      transaction.get(summaryRef),
      transaction.get(accountDeletionClaimRef(uid)),
    ]);
    if (deletionClaim.exists) {
      throw createHttpError(409, 'This managed account is being deleted.');
    }
    const currentCount = readActiveManagedFileCount(summarySnapshot.data()?.activeManagedFileCount);
    if (currentCount >= appConfig.managedMaxActiveFilesPerUser) {
      throw createHttpError(
        403,
        `Managed upload quota reached. Delete files before uploading more than ${appConfig.managedMaxActiveFilesPerUser} active files.`
      );
    }

    transaction.set(summaryRef, {
      activeManagedFileCount: currentCount + 1,
      updatedAt: Date.now(),
    }, { merge: true });
  });
};

export const releaseManagedUploadSlot = async (uid: string): Promise<void> => {
  await adminDb.runTransaction(async (transaction: any) => {
    const summaryRef = managedFileQuotaRef(uid);
    const summarySnapshot = await transaction.get(summaryRef);
    const currentCount = readActiveManagedFileCount(summarySnapshot.data()?.activeManagedFileCount);
    transaction.set(summaryRef, {
      activeManagedFileCount: Math.max(0, currentCount - 1),
      updatedAt: Date.now(),
    }, { merge: true });
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
    transaction.set(summaryRef, {
      activeManagedFileCount: Math.max(0, currentCount - 1),
      updatedAt: Date.now(),
    }, { merge: true });
    return true;
  })
);
