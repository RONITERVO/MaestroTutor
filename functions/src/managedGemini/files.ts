// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Public file status and delete operations over the ownership and quota services. */

import { createHttpError } from '../http';
import {
  managedFileRef
} from '../managedData';
import { getGeminiClient } from './client';
import { hasManagedFileExpired, isNotFoundError, normalizeGeminiFileName } from './fileIdentity';
import { deleteManagedFileByName } from './fileLifecycle';
import { markManagedFileDeleted } from './fileQuota';

const MAX_FILE_STATUS_URIS = 100;

const FILE_STATUS_BATCH_SIZE = 10;

export const getManagedFileStatuses = async (uid: string, uris: string[]) => {
  if (uris.length > MAX_FILE_STATUS_URIS) {
    throw createHttpError(400, `At most ${MAX_FILE_STATUS_URIS} file URIs may be checked at once.`);
  }
  const statuses: Record<string, { deleted: boolean; active: boolean }> = Object.create(null);

  for (let index = 0; index < uris.length; index += FILE_STATUS_BATCH_SIZE) {
    const batch = uris.slice(index, index + FILE_STATUS_BATCH_SIZE);
    await Promise.all(batch.map(async (uri) => {
      const fileName = normalizeGeminiFileName(uri);
      if (!fileName) {
        statuses[uri] = { deleted: true, active: false };
        return;
      }

      const snapshot = await managedFileRef(uid, fileName).get();
      const data = snapshot.data();
      if (!snapshot.exists || data?.uid !== uid || data?.deletedAt) {
        statuses[uri] = { deleted: true, active: false };
        return;
      }
      if (hasManagedFileExpired(data)) {
        await markManagedFileDeleted(uid, fileName);
        statuses[uri] = { deleted: true, active: false };
        return;
      }

      try {
        const file = await getGeminiClient().files.get({ name: fileName });
        const active = file?.state === 'ACTIVE';
        const deleted = file?.state === 'FAILED';
        statuses[uri] = { deleted, active };

        if (deleted) {
          await snapshot.ref.update({ state: 'failed', lastCheckedAt: Date.now() });
          await deleteManagedFileByName(uid, fileName);
        } else {
          await snapshot.ref.update({
            lastCheckedAt: Date.now(),
            state: active ? 'active' : 'processing',
          });
        }
      } catch (error) {
        if (isNotFoundError(error)) {
          statuses[uri] = { deleted: true, active: false };
          await markManagedFileDeleted(uid, fileName);
          return;
        }
        throw error;
      }
    }));
  }

  return { statuses };
};

export const deleteManagedFile = async (uid: string, nameOrUri: string) => {
  const fileName = normalizeGeminiFileName(nameOrUri);
  if (!fileName) {
    return { ok: false };
  }
  return { ok: await deleteManagedFileByName(uid, fileName) };
};
