// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Account cleanup and detached remote-deletion retries, including legacy metadata paths. */

import { FieldPath } from 'firebase-admin/firestore';
import { createHash } from 'node:crypto';
import { adminDb } from '../firebase';
import { getErrorMessage } from '../http';
import {
  MANAGED_RUNTIME_RETENTION_MS,
  cleanupJobsCollection,
  managedFilesCollection,
  timestampFromMillis
} from '../managedData';
import { getGeminiClient } from './client';
import { isNotFoundError, normalizeGeminiFileName } from './fileIdentity';
import { deleteManagedFileByName } from './fileLifecycle';

const FILE_CLEANUP_BATCH_SIZE = 200;

export const clearManagedFiles = async (uid: string) => {
  let deletedCount = 0;
  let failedCount = 0;
  const failedNames: string[] = [];
  const cleanedMetadataIds: string[] = [];
  let lastDocument: FirebaseFirestore.QueryDocumentSnapshot | undefined;

  while (true) {
    let query = managedFilesCollection(uid)
      .orderBy(FieldPath.documentId())
      .limit(FILE_CLEANUP_BATCH_SIZE);
    if (lastDocument) {
      query = query.startAfter(lastDocument);
    }
    const snapshot = await query.get();
    if (snapshot.empty) break;
    lastDocument = snapshot.docs[snapshot.docs.length - 1];

    for (const doc of snapshot.docs) {
      const data = doc.data() as {
        name?: string;
        deletedAt?: number | null;
        cleanupAttempts?: number;
      };
      const fileName = typeof data.name === 'string' ? data.name : '';
      if (data.deletedAt) {
        cleanedMetadataIds.push(doc.id);
        continue;
      }

      try {
        if (!fileName) {
          throw new Error('Managed file record has no remote file name.');
        }
        if (!await deleteManagedFileByName(uid, fileName)) {
          throw new Error('Managed file metadata could not be matched for remote cleanup.');
        }
        deletedCount += 1;
        cleanedMetadataIds.push(doc.id);
      } catch (error) {
        failedCount += 1;
        failedNames.push(fileName || doc.id);
        await doc.ref.set({
          cleanupPending: true,
          cleanupAttempts: Math.max(0, Number(data.cleanupAttempts || 0)) + 1,
          cleanupLastAttemptAt: Date.now(),
          cleanupLastError: getErrorMessage(error).slice(0, 1_000),
        }, { merge: true });
      }
    }

    if (snapshot.size < FILE_CLEANUP_BATCH_SIZE) break;
  }

  return { deletedCount, failedCount, failedNames, cleanedMetadataIds };
};

/** Defensively clean paths referenced by the undeployed, code-only v1 draft. */
export const clearLegacyManagedFiles = async (uid: string) => {
  let deletedCount = 0;
  let failedCount = 0;
  const failedNames: string[] = [];
  const cleanedMetadataIds: string[] = [];
  let lastDocument: FirebaseFirestore.QueryDocumentSnapshot | undefined;

  while (true) {
    let query = adminDb.collection('managedFiles')
      .where('uid', '==', uid)
      .orderBy(FieldPath.documentId())
      .limit(FILE_CLEANUP_BATCH_SIZE);
    if (lastDocument) query = query.startAfter(lastDocument);
    const snapshot = await query.get();
    if (snapshot.empty) break;
    lastDocument = snapshot.docs[snapshot.docs.length - 1];

    for (const doc of snapshot.docs) {
      const data = doc.data() as { name?: unknown; uri?: unknown; deletedAt?: unknown };
      const name = typeof data.name === 'string'
        ? data.name
        : (typeof data.uri === 'string' ? normalizeGeminiFileName(data.uri) || '' : '');
      if (data.deletedAt) {
        cleanedMetadataIds.push(doc.id);
        continue;
      }
      try {
        if (!name) throw new Error('Legacy managed file record has no remote file name.');
        try {
          await getGeminiClient().files.delete({ name });
        } catch (error) {
          if (!isNotFoundError(error)) throw error;
        }
        deletedCount += 1;
        cleanedMetadataIds.push(doc.id);
      } catch (error) {
        failedCount += 1;
        if (name) failedNames.push(name);
        // The retry job now owns any recoverable remote name. Do not retain a
        // abandoned v1-path document containing the deleted user's UID.
        cleanedMetadataIds.push(doc.id);
      }
    }

    if (snapshot.size < FILE_CLEANUP_BATCH_SIZE) break;
  }

  return { deletedCount, failedCount, failedNames, cleanedMetadataIds };
};

/**
 * Preserve remote deletions that outlive a user root (notably account
 * deletion). Jobs contain only the opaque Gemini file name, never user data.
 */
export const queueManagedFileCleanupJobs = async (fileNames: string[]): Promise<number> => {
  const uniqueNames = [...new Set(fileNames.map((name) => name.trim()).filter(Boolean))];
  if (uniqueNames.length === 0) return 0;

  const currentTime = Date.now();
  for (let offset = 0; offset < uniqueNames.length; offset += FILE_CLEANUP_BATCH_SIZE) {
    const batch = adminDb.batch();
    for (const name of uniqueNames.slice(offset, offset + FILE_CLEANUP_BATCH_SIZE)) {
      const jobId = createHash('sha256').update(name).digest('hex');
      batch.set(cleanupJobsCollection().doc(jobId), {
        kind: 'gemini-file-delete',
        name,
        status: 'pending',
        attempts: 0,
        createdAt: currentTime,
        updatedAt: currentTime,
        retryAt: timestampFromMillis(currentTime),
      }, { merge: true });
    }
    await batch.commit();
  }
  return uniqueNames.length;
};

export const retryManagedFileCleanupJobs = async (limit = 50): Promise<{
  attempted: number;
  completed: number;
}> => {
  const currentTime = Date.now();
  const snapshot = await cleanupJobsCollection()
    .where('status', '==', 'pending')
    .where('retryAt', '<=', timestampFromMillis(currentTime))
    .limit(Math.max(1, Math.min(200, Math.floor(limit))))
    .get();

  let completed = 0;
  for (const doc of snapshot.docs) {
    const data = doc.data() as { name?: unknown; attempts?: unknown };
    const name = typeof data.name === 'string' ? data.name : '';
    try {
      if (!name) throw new Error('Cleanup job has no Gemini file name.');
      try {
        await getGeminiClient().files.delete({ name });
      } catch (error) {
        if (!isNotFoundError(error)) throw error;
      }

      const completedAt = Date.now();
      await doc.ref.set({
        status: 'completed',
        completedAt,
        updatedAt: completedAt,
        lastError: null,
        purgeAt: timestampFromMillis(completedAt + MANAGED_RUNTIME_RETENTION_MS),
      }, { merge: true });
      completed += 1;
    } catch (error) {
      const attempts = Math.max(0, Number(data.attempts || 0)) + 1;
      const retryDelayMs = Math.min(24 * 60 * 60 * 1_000, 60_000 * (2 ** Math.min(attempts, 10)));
      const failedAt = Date.now();
      await doc.ref.set({
        attempts,
        updatedAt: failedAt,
        lastAttemptAt: failedAt,
        lastError: getErrorMessage(error).slice(0, 1_000),
        retryAt: timestampFromMillis(failedAt + retryDelayMs),
      }, { merge: true });
    }
  }

  return { attempted: snapshot.size, completed };
};
