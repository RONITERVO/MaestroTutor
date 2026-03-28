// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { randomUUID } from 'node:crypto';
import { promises as fs } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import type { Response } from 'express';
import { GoogleGenAI } from '@google/genai';
import type { AppUser } from './auth';
import { appConfig } from './config';
import { adminDb } from './firebase';
import { createHttpError } from './http';
import {
  chargeManagedCredits,
  getManagedAccountState,
  releaseManagedReservation,
  reserveManagedCredits,
  settleManagedReservation,
  sweepExpiredReservationsForUser,
} from './managedBilling';
import {
  estimateReservationUsd,
  usageMetadataToUsd,
  uploadBytesToCredits,
  uploadBytesToUsd,
  usdToCredits,
  creditsToUsd,
} from './pricing';

const STREAM_CONTENT_TYPE = 'application/x-ndjson; charset=utf-8';
const FILE_ACTIVE_TIMEOUT_MS = 60_000;
const FILE_ACTIVE_POLL_MS = 1_000;

const getGeminiClient = (): GoogleGenAI => {
  if (!appConfig.geminiApiKey) {
    throw createHttpError(500, 'GEMINI_API_KEY is not configured on the backend.');
  }
  return new GoogleGenAI({ apiKey: appConfig.geminiApiKey });
};

const normalizeGeminiFileName = (nameOrUri: string): string | null => {
  const trimmed = (nameOrUri || '').trim();
  if (!trimmed) return null;
  if (trimmed.startsWith('files/')) return trimmed;
  const uriMatch = /\/files\/([^?\s]+)/.exec(trimmed);
  if (uriMatch?.[1]) {
    return `files/${uriMatch[1]}`;
  }
  return null;
};

const fileDocId = (name: string): string => Buffer.from(name, 'utf8').toString('base64url');

const managedFileRef = (name: string) => adminDb.collection('managedFiles').doc(fileDocId(name));
const managedAccountSummaryRef = (uid: string) =>
  adminDb.collection('users').doc(uid).collection('account').doc('summary');

const isNotFoundError = (error: unknown): boolean => {
  const status = Number((error as { status?: unknown })?.status);
  const message = error instanceof Error ? error.message.toLowerCase() : '';
  return status === 403 || status === 404 || message.includes('not found') || message.includes('forbidden');
};

const countPromptTokens = async (
  model: string,
  contents: unknown,
  config?: Record<string, unknown>
): Promise<number> => {
  try {
    const result = await getGeminiClient().models.countTokens({
      model,
      contents,
      ...(config ? { config } : {}),
    } as any);
    return Math.max(0, Number((result as any)?.totalTokens || (result as any)?.tokenCount || 0));
  } catch {
    return 0;
  }
};

const readActiveManagedFileCount = (value: unknown): number => {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed <= 0) return 0;
  return Math.floor(parsed);
};

const reserveManagedUploadSlot = async (uid: string, user?: AppUser): Promise<void> => {
  await adminDb.runTransaction(async (transaction: any) => {
    const summaryRef = managedAccountSummaryRef(uid);
    const summarySnapshot = await transaction.get(summaryRef);
    const currentCount = readActiveManagedFileCount(summarySnapshot.data()?.activeManagedFileCount);
    if (currentCount >= appConfig.managedMaxActiveFilesPerUser) {
      throw createHttpError(
        403,
        `Managed upload quota reached. Delete files before uploading more than ${appConfig.managedMaxActiveFilesPerUser} active files.`
      );
    }

    transaction.set(summaryRef, {
      ...(user ? { user } : {}),
      activeManagedFileCount: currentCount + 1,
    }, { merge: true });
  });
};

const releaseManagedUploadSlot = async (uid: string): Promise<void> => {
  await adminDb.runTransaction(async (transaction: any) => {
    const summaryRef = managedAccountSummaryRef(uid);
    const summarySnapshot = await transaction.get(summaryRef);
    const currentCount = readActiveManagedFileCount(summarySnapshot.data()?.activeManagedFileCount);
    transaction.set(summaryRef, {
      activeManagedFileCount: Math.max(0, currentCount - 1),
    }, { merge: true });
  });
};

const markManagedFileDeleted = async (uid: string, fileName: string): Promise<boolean> => (
  adminDb.runTransaction(async (transaction: any) => {
    const fileRef = managedFileRef(fileName);
    const summaryRef = managedAccountSummaryRef(uid);
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
    }, { merge: true });
    transaction.set(summaryRef, {
      activeManagedFileCount: Math.max(0, currentCount - 1),
    }, { merge: true });
    return true;
  })
);

const serializeGenerateContentResponse = (
  response: any,
  billingSummary: unknown
) => ({
  text: typeof response?.text === 'string' ? response.text : '',
  candidates: Array.isArray(response?.candidates) ? response.candidates : [],
  usageMetadata: response?.usageMetadata || undefined,
  billingSummary,
});

const withManagedReservation = async <T>(params: {
  uid: string;
  user: AppUser;
  operation: string;
  model: string;
  contents: unknown;
  config?: Record<string, unknown>;
  execute: (reservationId: string) => Promise<T>;
  finalize: (reservationId: string, result: T) => Promise<{ result: T; billingSummary: unknown }>;
}): Promise<{ result: T; billingSummary: unknown }> => {
  await sweepExpiredReservationsForUser(params.uid);

  const promptTokens = await countPromptTokens(params.model, params.contents, params.config);
  const estimatedUsd = estimateReservationUsd({
    model: params.model,
    promptTokens,
    operation: params.operation,
  });
  const estimatedCredits = usdToCredits(estimatedUsd);

  const reservation = await reserveManagedCredits({
    uid: params.uid,
    user: params.user,
    operation: params.operation,
    model: params.model,
    estimatedCredits,
    estimatedUsd,
    metadata: { promptTokens },
  });

  try {
    const result = await params.execute(reservation.reservationId);
    const finalized = await params.finalize(reservation.reservationId, result);
    return finalized;
  } catch (error) {
    try {
      await releaseManagedReservation(params.uid, reservation.reservationId, 'request-failed');
    } catch {
      // Preserve the original request failure.
    }
    throw error;
  }
};

export const generateManagedContent = async (params: {
  uid: string;
  user: AppUser;
  model: string;
  contents: unknown;
  config?: Record<string, unknown>;
  operation: string;
}) => {
  const finalized = await withManagedReservation({
    ...params,
    execute: async () => (
      getGeminiClient().models.generateContent({
        model: params.model,
        contents: params.contents,
        ...(params.config ? { config: params.config } : {}),
      } as any)
    ),
    finalize: async (reservationId, response) => {
      const usageMetadata = response?.usageMetadata as Record<string, unknown> | undefined;
      const billedUsd = usageMetadataToUsd(params.model, usageMetadata, params.operation);
      const billedCredits = usdToCredits(billedUsd);
      const billingSummary = await settleManagedReservation({
        uid: params.uid,
        reservationId,
        billedCredits,
        billedUsd,
        operation: params.operation,
        model: params.model,
        metadata: {
          promptTokenCount: usageMetadata?.promptTokenCount,
          candidatesTokenCount: usageMetadata?.candidatesTokenCount,
        },
      });
      return { result: response, billingSummary };
    },
  });

  return serializeGenerateContentResponse(finalized.result, finalized.billingSummary);
};

export const streamManagedContent = async (params: {
  uid: string;
  user: AppUser;
  model: string;
  contents: unknown;
  config?: Record<string, unknown>;
  operation: string;
  response: Response;
}) => {
  const response = params.response;
  response.setHeader('Content-Type', STREAM_CONTENT_TYPE);
  response.setHeader('Cache-Control', 'no-store, no-transform');
  response.setHeader('X-Accel-Buffering', 'no');

  await sweepExpiredReservationsForUser(params.uid);

  const promptTokens = await countPromptTokens(params.model, params.contents, params.config);
  const estimatedUsd = estimateReservationUsd({
    model: params.model,
    promptTokens,
    operation: params.operation,
  });
  const estimatedCredits = usdToCredits(estimatedUsd);

  const reservation = await reserveManagedCredits({
    uid: params.uid,
    user: params.user,
    operation: params.operation,
    model: params.model,
    estimatedCredits,
    estimatedUsd,
    metadata: { promptTokens },
  });

  let latestChunk: any = null;
  let deliveredAnyChunk = false;
  let generationCompleted = false;
  let clientDisconnected = false;
  let streamFinished = false;

  const markDisconnected = () => {
    if (!streamFinished) {
      clientDisconnected = true;
    }
  };

  response.once('close', markDisconnected);
  response.once('error', markDisconnected);

  try {
    const stream = await getGeminiClient().models.generateContentStream({
      model: params.model,
      contents: params.contents,
      ...(params.config ? { config: params.config } : {}),
    } as any);

    for await (const chunk of stream) {
      latestChunk = chunk;
      if (clientDisconnected || response.destroyed || !response.writable) {
        clientDisconnected = true;
        break;
      }

      response.write(`${JSON.stringify({ type: 'chunk', chunk })}\n`);
      deliveredAnyChunk = true;
    }

    generationCompleted = !clientDisconnected;

    if (generationCompleted) {
      const usageMetadata = latestChunk?.usageMetadata as Record<string, unknown> | undefined;
      const billedUsd = usageMetadataToUsd(params.model, usageMetadata, params.operation);
      const billedCredits = usdToCredits(billedUsd);
      const billingSummary = await settleManagedReservation({
        uid: params.uid,
        reservationId: reservation.reservationId,
        billedCredits,
        billedUsd,
        operation: params.operation,
        model: params.model,
        metadata: {
          promptTokenCount: usageMetadata?.promptTokenCount,
          candidatesTokenCount: usageMetadata?.candidatesTokenCount,
          disconnectRecovered: false,
        },
      });

      if (!clientDisconnected && !response.destroyed && response.writable) {
        response.write(`${JSON.stringify({
          type: 'final',
          result: serializeGenerateContentResponse(latestChunk || {}, billingSummary),
        })}\n`);
        streamFinished = true;
        response.end();
      }
      return;
    }

    if (deliveredAnyChunk || latestChunk != null) {
      await settleManagedReservation({
        uid: params.uid,
        reservationId: reservation.reservationId,
        billedCredits: estimatedCredits,
        billedUsd: estimatedUsd,
        operation: params.operation,
        model: params.model,
        metadata: {
          promptTokens,
          disconnectRecovered: true,
          partialStreamDelivered: deliveredAnyChunk,
        },
      });
      return;
    }

    await releaseManagedReservation(
      params.uid,
      reservation.reservationId,
      'stream-disconnected-before-output'
    );
  } catch (error) {
    if (deliveredAnyChunk || latestChunk != null) {
      await settleManagedReservation({
        uid: params.uid,
        reservationId: reservation.reservationId,
        billedCredits: estimatedCredits,
        billedUsd: estimatedUsd,
        operation: params.operation,
        model: params.model,
        metadata: {
          promptTokens,
          streamFailedAfterOutput: true,
          partialStreamDelivered: deliveredAnyChunk,
        },
      });
      if (!response.destroyed && response.writable && !response.writableEnded) {
        streamFinished = true;
        response.end();
      }
      return;
    }

    await releaseManagedReservation(params.uid, reservation.reservationId, 'request-failed');
    throw error;
  } finally {
    streamFinished = true;
    response.off('close', markDisconnected);
    response.off('error', markDisconnected);
  }
};

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

const dataUrlToTemporaryFile = async (dataUrl: string, mimeType: string, displayName?: string) => {
  const base64Index = dataUrl.indexOf(',');
  if (base64Index === -1) {
    throw createHttpError(400, 'Invalid base64 data URL.');
  }

  const buffer = Buffer.from(dataUrl.slice(base64Index + 1), 'base64');
  if (!buffer.length) {
    throw createHttpError(400, 'Uploaded media payload is empty.');
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
  const tempFile = await dataUrlToTemporaryFile(params.dataUrl, params.mimeType, params.displayName);
  if (tempFile.sizeBytes > appConfig.managedMaxUploadBytes) {
    await fs.unlink(tempFile.path).catch(() => undefined);
    throw createHttpError(413, 'Uploaded media exceeds the managed upload size limit.');
  }

  const uploadCredits = uploadBytesToCredits(tempFile.sizeBytes);
  const uploadUsd = uploadBytesToUsd(tempFile.sizeBytes);
  const actingUser = params.user || { id: params.uid, email: null, displayName: null, photoUrl: null };

  let fileName: string | null = null;
  let reservationId = '';
  let slotReserved = false;
  let fileRecordCreated = false;
  try {
    await reserveManagedUploadSlot(params.uid, actingUser);
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

    await managedFileRef(fileName).set({
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

export const getManagedFileStatuses = async (uid: string, uris: string[]) => {
  const statuses: Record<string, { deleted: boolean; active: boolean }> = {};

  await Promise.all(uris.map(async (uri) => {
    const fileName = normalizeGeminiFileName(uri);
    if (!fileName) {
      statuses[uri] = { deleted: true, active: false };
      return;
    }

    const snapshot = await managedFileRef(fileName).get();
    const data = snapshot.data();
    if (!snapshot.exists || data?.uid !== uid || data?.deletedAt) {
      statuses[uri] = { deleted: true, active: false };
      return;
    }

    try {
      const file = await getGeminiClient().files.get({ name: fileName });
      const active = file?.state === 'ACTIVE';
      const deleted = file?.state === 'FAILED';
      statuses[uri] = { deleted, active };

      if (deleted) {
        await markManagedFileDeleted(uid, fileName);
      } else {
        await managedFileRef(fileName).set({
          lastCheckedAt: Date.now(),
          state: active ? 'active' : 'processing',
        }, { merge: true });
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

  return { statuses };
};

export const deleteManagedFile = async (uid: string, nameOrUri: string) => {
  const fileName = normalizeGeminiFileName(nameOrUri);
  if (!fileName) {
    return { ok: false };
  }

  const snapshot = await managedFileRef(fileName).get();
  const data = snapshot.data();
  if (!snapshot.exists || data?.uid !== uid) {
    return { ok: false };
  }
  if (data?.deletedAt) {
    return { ok: true };
  }

  try {
    await getGeminiClient().files.delete({ name: fileName });
  } catch (error) {
    if (!isNotFoundError(error)) {
      throw error;
    }
  }

  await markManagedFileDeleted(uid, fileName);
  return { ok: true };
};

export const clearManagedFiles = async (uid: string) => {
  const snapshot = await adminDb.collection('managedFiles')
    .where('uid', '==', uid)
    .where('deletedAt', '==', null)
    .limit(200)
    .get();

  let deletedCount = 0;
  let failedCount = 0;
  const failedNames: string[] = [];

  for (const doc of snapshot.docs) {
    const data = doc.data() as { name?: string };
    const fileName = typeof data.name === 'string' ? data.name : '';
    if (!fileName) continue;

    try {
      await getGeminiClient().files.delete({ name: fileName });
      if (await markManagedFileDeleted(uid, fileName)) {
        deletedCount += 1;
      }
    } catch (error) {
      if (isNotFoundError(error)) {
        if (await markManagedFileDeleted(uid, fileName)) {
          deletedCount += 1;
        }
        continue;
      }
      failedCount += 1;
      failedNames.push(fileName);
    }
  }

  return { deletedCount, failedCount, failedNames };
};

export const createManagedLiveToken = async (params: {
  uid: string;
  user: AppUser;
  purpose?: 'live' | 'music';
  durationSeconds?: number;
}) => {
  const purpose = params.purpose === 'music' ? 'music' : 'live';
  const fixedCredits = purpose === 'music'
    ? appConfig.managedMusicSessionCredits
    : appConfig.managedLiveSessionCredits;
  const accountState = await getManagedAccountState(params.uid, params.user);
  if (accountState.billingSummary.availableCredits < fixedCredits) {
    throw createHttpError(402, 'Not enough Maestro credits to start a live session.');
  }

  const expireTime = new Date(Date.now() + 15 * 60_000).toISOString();
  const tokenResponse = await getGeminiClient().authTokens.create({
    config: {
      uses: appConfig.geminiLiveTokenUses,
      expireTime,
      httpOptions: {
        apiVersion: 'v1alpha',
      },
    },
  } as any);

  const token = typeof (tokenResponse as any)?.name === 'string'
    ? (tokenResponse as any).name
    : (typeof (tokenResponse as any)?.token === 'string' ? (tokenResponse as any).token : '');
  if (!token) {
    throw createHttpError(500, 'Backend could not mint a Gemini live token.');
  }

  const billingSummary = await chargeManagedCredits({
    uid: params.uid,
    user: params.user,
    operation: purpose === 'music' ? 'liveTokenMusic' : 'liveToken',
    model: purpose === 'music' ? 'lyria-realtime-exp' : 'gemini-live',
    billedCredits: fixedCredits,
    billedUsd: creditsToUsd(fixedCredits),
    metadata: {
      purpose,
      requestedDurationSeconds: params.durationSeconds || null,
      uses: appConfig.geminiLiveTokenUses,
    },
  });

  return {
    token,
    expiresAt: typeof (tokenResponse as any)?.expireTime === 'string'
      ? (tokenResponse as any).expireTime
      : expireTime,
    uses: appConfig.geminiLiveTokenUses,
    billingSummary,
  };
};
