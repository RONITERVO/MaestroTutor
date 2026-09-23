// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Upload transaction: temporary bytes, quota, reservation, provider processing and rollback. */

import { randomUUID } from 'node:crypto';
import { promises as fs } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import type { AppUser } from '../auth';
import { appConfig } from '../config';
import { createHttpError } from '../http';
import {
  releaseManagedReservation,
  reserveManagedCredits,
  settleManagedReservation
} from '../managedBilling';
import {
  managedFileRef
} from '../managedData';
import {
  uploadBytesToCredits,
  uploadBytesToUsd
} from '../pricing';
import { getGeminiClient } from './client';
import { normalizeGeminiFileName } from './fileIdentity';
import { evictManagedFilesForUpload } from './fileLifecycle';
import { markManagedFileDeleted, releaseManagedUploadSlot, reserveManagedUploadSlot } from './fileQuota';

const FILE_ACTIVE_TIMEOUT_MS = 60_000;

const FILE_ACTIVE_POLL_MS = 1_000;

const waitForManagedFileActive = async (name: string): Promise<any> => {
  const startedAt = Date.now();
  while (Date.now() - startedAt < FILE_ACTIVE_TIMEOUT_MS) {
    const file = await getGeminiClient().files.get({ name });
    if (file?.state === 'ACTIVE') return file;
    if (file?.state === 'FAILED') {
      throw createHttpError(500, `Uploaded Gemini file failed processing: ${name}`);
    }
    await new Promise((resolve) => setTimeout(resolve, FILE_ACTIVE_POLL_MS));
  }
  throw createHttpError(504, `Timed out waiting for Gemini file ${name} to become active.`);
};

const dataUrlToTemporaryFile = async (
  dataUrl: string,
  mimeType: string,
  maxBytes: number,
  displayName?: string,
) => {
  const base64Index = dataUrl.indexOf(',');
  if (base64Index === -1) {
    throw createHttpError(400, 'Invalid base64 data URL.');
  }

  const buffer = Buffer.from(dataUrl.slice(base64Index + 1), 'base64');
  if (!buffer.length) {
    throw createHttpError(400, 'Uploaded media payload is empty.');
  }
  if (buffer.length > maxBytes) {
    throw createHttpError(413, 'Uploaded media exceeds the managed upload size limit.');
  }

  const extension = (mimeType.split('/')[1] || 'bin').replace(/[^a-z0-9]/gi, '');
  const tempPath = path.join(os.tmpdir(), `${randomUUID()}.${extension || 'bin'}`);
  await fs.writeFile(tempPath, buffer);
  return {
    path: tempPath,
    sizeBytes: buffer.length,
    filename: displayName || path.basename(tempPath),
  };
};

export const uploadManagedMedia = async (params: {
  uid: string;
  user?: AppUser;
  dataUrl: string;
  mimeType: string;
  displayName?: string;
}) => {
  const tempFile = await dataUrlToTemporaryFile(
    params.dataUrl,
    params.mimeType,
    appConfig.managedMaxUploadBytes,
    params.displayName,
  );

  const uploadCredits = uploadBytesToCredits(tempFile.sizeBytes);
  const uploadUsd = uploadBytesToUsd(tempFile.sizeBytes);
  const actingUser = params.user || { id: params.uid, email: null, displayName: null, photoUrl: null };

  let fileName: string | null = null;
  let reservationId = '';
  let slotReserved = false;
  let fileRecordCreated = false;
  try {
    await evictManagedFilesForUpload(params.uid, 1);
    try {
      await reserveManagedUploadSlot(params.uid);
    } catch (error) {
      if (Number((error as { status?: unknown })?.status) === 403) {
        await evictManagedFilesForUpload(params.uid, 1);
        await reserveManagedUploadSlot(params.uid);
      } else {
        throw error;
      }
    }
    slotReserved = true;

    const reservation = await reserveManagedCredits({
      uid: params.uid,
      user: actingUser,
      operation: 'uploadMedia',
      model: 'managed-upload',
      estimatedCredits: uploadCredits,
      estimatedUsd: uploadUsd,
      metadata: {
        mimeType: params.mimeType,
        sizeBytes: tempFile.sizeBytes,
      },
    });
    reservationId = reservation.reservationId;

    const uploaded = await getGeminiClient().files.upload({
      file: tempFile.path,
      config: {
        mimeType: params.mimeType,
        displayName: tempFile.filename,
      },
    });

    fileName = normalizeGeminiFileName(uploaded?.name || uploaded?.uri || '');
    if (!fileName || !uploaded?.uri || !uploaded?.mimeType) {
      throw createHttpError(500, 'Gemini upload did not return the expected file metadata.');
    }

    if (uploaded.state !== 'ACTIVE') {
      await waitForManagedFileActive(fileName);
    }

    await managedFileRef(params.uid, fileName).set({
      uid: params.uid,
      name: fileName,
      uri: uploaded.uri,
      mimeType: uploaded.mimeType,
      displayName: tempFile.filename,
      sizeBytes: tempFile.sizeBytes,
      createdAt: Date.now(),
      lastCheckedAt: Date.now(),
      deletedAt: null,
      state: 'active',
    }, { merge: true });
    fileRecordCreated = true;

    const billingSummary = await settleManagedReservation({
      uid: params.uid,
      reservationId,
      billedCredits: uploadCredits,
      billedUsd: uploadUsd,
      operation: 'uploadMedia',
      model: 'managed-upload',
      metadata: {
        fileName,
        mimeType: uploaded.mimeType,
        sizeBytes: tempFile.sizeBytes,
      },
    });

    return {
      uri: uploaded.uri,
      mimeType: uploaded.mimeType,
      billingSummary,
    };
  } catch (error) {
    if (fileName) {
      try {
        await getGeminiClient().files.delete({ name: fileName });
      } catch {
        // Ignore cleanup failures and preserve the original error.
      }
    }

    if (fileRecordCreated && fileName) {
      const didRelease = await markManagedFileDeleted(params.uid, fileName).catch(() => false);
      if (didRelease) {
        slotReserved = false;
      }
    }
    if (slotReserved) {
      await releaseManagedUploadSlot(params.uid).catch(() => undefined);
    }

    if (reservationId) {
      await releaseManagedReservation(params.uid, reservationId, 'upload-failed')
        .catch(() => undefined);
    }
    throw error;
  } finally {
    await fs.unlink(tempFile.path).catch(() => undefined);
  }
};
