// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Owned-file validation and remote eviction. Quota transactions belong to fileQuota. */

import { appConfig } from '../config';
import { adminDb } from '../firebase';
import {
  collectGeminiFileUris
} from '../geminiPolicy';
import { createHttpError } from '../http';
import {
  managedFileRef,
  managedFilesCollection
} from '../managedData';
import { getGeminiClient } from './client';
import { isNotFoundError, normalizeGeminiFileName } from './fileIdentity';
import { markManagedFileDeleted } from './fileQuota';

const MAX_REFERENCED_FILE_URIS = 20;

const listActiveManagedFilesForUser = async (uid: string) => {
  const snapshot = await managedFilesCollection(uid)
    .where('deletedAt', '==', null)
    .limit(appConfig.managedMaxActiveFilesPerUser + 5)
    .get();

  return snapshot.docs.map((doc) => ({
    ref: doc.ref,
    name: typeof doc.data().name === 'string' ? doc.data().name as string : '',
    createdAt: Number(doc.data().createdAt || 0),
    lastCheckedAt: Number(doc.data().lastCheckedAt || 0),
  }));
};

export const deleteManagedFileByName = async (uid: string, fileName: string): Promise<boolean> => {
  const snapshot = await managedFileRef(uid, fileName).get();
  const data = snapshot.data();
  if (!snapshot.exists || data?.uid !== uid) {
    return false;
  }
  if (data?.deletedAt) {
    return true;
  }

  try {
    await getGeminiClient().files.delete({ name: fileName });
  } catch (error) {
    if (!isNotFoundError(error)) {
      throw error;
    }
  }

  await markManagedFileDeleted(uid, fileName);
  return true;
};

export const evictManagedFilesForUpload = async (uid: string, slotsNeeded = 1): Promise<number> => {
  const activeFiles = await listActiveManagedFilesForUser(uid);
  const overflow = activeFiles.length + Math.max(1, slotsNeeded) - appConfig.managedMaxActiveFilesPerUser;
  if (overflow <= 0) {
    return 0;
  }

  const evictionCandidates = activeFiles
    .filter((file) => file.name)
    .sort((left, right) => {
      const leftKey = left.lastCheckedAt || left.createdAt || 0;
      const rightKey = right.lastCheckedAt || right.createdAt || 0;
      return leftKey - rightKey;
    })
    .slice(0, overflow);

  let evictedCount = 0;
  for (const file of evictionCandidates) {
    if (await deleteManagedFileByName(uid, file.name)) {
      evictedCount += 1;
    }
  }

  return evictedCount;
};

export const requireOwnedManagedContentFiles = async (
  uid: string,
  contents: unknown,
  config?: Record<string, unknown>,
): Promise<void> => {
  const referencedUris = collectGeminiFileUris({ contents, config });
  if (referencedUris.length > MAX_REFERENCED_FILE_URIS) {
    throw createHttpError(
      400,
      `At most ${MAX_REFERENCED_FILE_URIS} managed files may be referenced in one request.`,
    );
  }

  const references = referencedUris.map((uri) => {
    const name = normalizeGeminiFileName(uri);
    if (!name) {
      throw createHttpError(400, 'A generation request contains an invalid Gemini file URI.');
    }
    return { uri, name, ref: managedFileRef(uid, name) };
  });
  if (references.length === 0) return;

  const snapshots = await adminDb.getAll(...references.map((reference) => reference.ref));
  snapshots.forEach((snapshot: any, index: number) => {
    const reference = references[index];
    const data = snapshot.data();
    if (
      !snapshot.exists
      || data?.uid !== uid
      || data?.name !== reference.name
      || data?.uri !== reference.uri
      || data?.deletedAt
      || data?.state !== 'active'
    ) {
      throw createHttpError(403, 'A referenced Gemini file is not an active file owned by this account.');
    }
  });
};
