// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Durable provider cleanup outlives user metadata without retaining user identifiers. */
import { createHash } from 'node:crypto';
import { adminDb } from '../firebase';
import { getErrorMessage } from '../http';
import { MANAGED_RUNTIME_RETENTION_MS, cleanupJobsCollection, managedFileRef, timestampFromMillis } from '../managedData';
import { getGeminiClient } from './client';
import { hasManagedFileExpired, isNotFoundError, normalizeGeminiFileName } from './fileIdentity';
import { markManagedFileDeleted } from './fileQuota';

const FILE_CLEANUP_BATCH_SIZE = 200;

/**
 * Preserve remote deletions that outlive a user root (notably account
 * deletion). Jobs contain only the opaque Gemini file name, never user data.
 */
export const queueManagedFileCleanupJobs = async (fileNames: string[]): Promise<number> => {
  const uniqueNames = [...new Set(fileNames.map(name => normalizeGeminiFileName(name)).filter((name): name is string => Boolean(name)))];
  if (uniqueNames.length === 0) return 0;

  const currentTime = Date.now();
  for (let offset = 0; offset < uniqueNames.length; offset += FILE_CLEANUP_BATCH_SIZE) {
    await Promise.all(uniqueNames.slice(offset, offset + FILE_CLEANUP_BATCH_SIZE).map(async name => {
      const jobId = createHash('sha256').update(name).digest('hex');
      const ref = cleanupJobsCollection().doc(jobId);
      await adminDb.runTransaction(async transaction => {
        // Repeated request rollback must not erase a worker's attempts/backoff,
        // or turn an already completed deletion back into a pending job.
        if ((await transaction.get(ref)).exists) return;
        transaction.create(ref, {
        kind: 'gemini-file-delete',
        name,
        status: 'pending',
        attempts: 0,
        createdAt: currentTime,
        updatedAt: currentTime,
        retryAt: timestampFromMillis(currentTime),
        });
      });
    }));
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
    const data = doc.data() as { name?: unknown; attempts?: unknown; createdAt?: unknown };
    const name = typeof data.name === 'string' ? data.name : '';
    try {
      if (!name) throw new Error('Cleanup job has no Gemini file name.');
      try {
        // Job creation is later than file creation, so after another 48 hours
        // the provider's retention window proves expiry even for ambiguous 403s.
        if (!hasManagedFileExpired(data)) await getGeminiClient().files.delete({ name });
      } catch (error) {
        if (!isNotFoundError(error)) throw error;
      }

      // Jobs retain no user identifier. Resolve any surviving canonical owner
      // only after remote absence is confirmed; deleted accounts stay absent.
      const owners = await adminDb.collectionGroup('files').where('name', '==', name).get();
      for (const owner of owners.docs) {
        const uid = owner.data().uid;
        if (typeof uid === 'string' && uid && owner.ref.path === managedFileRef(uid, name).path) {
          await markManagedFileDeleted(uid, name);
        }
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
