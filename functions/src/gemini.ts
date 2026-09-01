// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { createHash, randomUUID } from 'node:crypto';
import { promises as fs } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import type { Response } from 'express';
import { GoogleGenAI } from '@google/genai';
import { FieldPath } from 'firebase-admin/firestore';
import type { AppUser } from './auth';
import { appConfig } from './config';
import { adminDb } from './firebase';
import {
  applyManagedGenerationLimits,
  collectGeminiFileUris,
  requireAllowedManagedModel,
  requirePricedManagedGenerationModel,
  requireSafeManagedLiveConfig,
  resolvePinnedManagedGenerationModel,
  resolveManagedContentOperation,
  usesManagedGoogleSearch,
} from './geminiPolicy';
import { createHttpError, getErrorMessage } from './http';
import {
  MANAGED_RUNTIME_RETENTION_MS,
  accountDeletionClaimRef,
  cleanupJobsCollection,
  ensureManagedUserDocument,
  managedFileQuotaRef,
  managedFileRef,
  managedFilesCollection,
  managedLiveLeaseRef,
  managedLiveQuotaRef,
  timestampFromMillis,
} from './managedData';
import {
  releaseManagedReservation,
  reserveManagedCredits,
  settleManagedReservation,
  sweepExpiredReservationsForUser,
} from './managedBilling';
import {
  calculateManagedLiveWindowCredits,
  calculateManagedLiveWindowUsd,
  estimateReservationUsd,
  getManagedLiveWindowTokenBudget,
  usageMetadataToUsd,
  uploadBytesToCredits,
  uploadBytesToUsd,
  usdToCredits,
  creditsToUsd,
} from './pricing';

const STREAM_CONTENT_TYPE = 'application/x-ndjson; charset=utf-8';
const FILE_ACTIVE_TIMEOUT_MS = 60_000;
const FILE_ACTIVE_POLL_MS = 1_000;
const MAX_FILE_STATUS_URIS = 100;
const MAX_REFERENCED_FILE_URIS = 20;
const FILE_STATUS_BATCH_SIZE = 10;
const FILE_CLEANUP_BATCH_SIZE = 200;

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
    const tokenCount = Number((result as any)?.totalTokens ?? (result as any)?.tokenCount);
    if (!Number.isFinite(tokenCount) || tokenCount < 0) {
      throw new Error('Gemini countTokens returned no usable token count.');
    }
    return Math.floor(tokenCount);
  } catch (error) {
    console.error('[billing] Prompt token count failed; generation was not started.', error);
    throw createHttpError(502, 'The backend could not price this prompt before generation.');
  }
};

const readActiveManagedFileCount = (value: unknown): number => {
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed <= 0) return 0;
  return Math.floor(parsed);
};

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

const reserveManagedLiveLease = async (params: {
  uid: string;
  purpose: 'live' | 'music';
  durationMs: number;
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
      purgeAt: timestampFromMillis(lease.expiresAt + MANAGED_RUNTIME_RETENTION_MS),
    }, { merge: true });
  });

  return lease;
};


/**
 * How many images a response actually produced.
 *
 * Images are billed per image, not per output token, so this number is the
 * difference between charging cents and charging a rounding error. Counted from
 * the response rather than assumed from the operation, so a request that asked
 * for an image and got none is not billed for one.
 */
const countGeneratedImages = (response: unknown): number => {
  const candidates = (response as { candidates?: unknown })?.candidates;
  if (!Array.isArray(candidates)) return 0;
  let images = 0;
  for (const candidate of candidates) {
    const parts = (candidate as { content?: { parts?: unknown } })?.content?.parts;
    if (!Array.isArray(parts)) continue;
    for (const part of parts) {
      const inline = (part as { inlineData?: { mimeType?: string } })?.inlineData;
      if (inline?.mimeType && inline.mimeType.startsWith('image/')) images += 1;
    }
  }
  return images;
};

const countGoogleSearchQueries = (response: unknown): number => {
  const candidates = (response as { candidates?: unknown })?.candidates;
  if (!Array.isArray(candidates)) return 0;
  return candidates.reduce((total, candidate) => {
    const queries = (candidate as {
      groundingMetadata?: { webSearchQueries?: unknown };
    })?.groundingMetadata?.webSearchQueries;
    return total + (Array.isArray(queries) ? queries.length : 0);
  }, 0);
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

const reserveManagedUploadSlot = async (uid: string): Promise<void> => {
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

const releaseManagedUploadSlot = async (uid: string): Promise<void> => {
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

const markManagedFileDeleted = async (uid: string, fileName: string): Promise<boolean> => (
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

const deleteManagedFileByName = async (uid: string, fileName: string): Promise<boolean> => {
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

const evictManagedFilesForUpload = async (uid: string, slotsNeeded = 1): Promise<number> => {
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

const serializeGenerateContentResponse = (
  response: any,
  billingSummary: unknown,
  modelVersionOverride?: string,
) => ({
  text: typeof response?.text === 'string' ? response.text : '',
  candidates: Array.isArray(response?.candidates) ? response.candidates : [],
  usageMetadata: response?.usageMetadata || undefined,
  modelVersion: modelVersionOverride
    || (typeof response?.modelVersion === 'string' ? response.modelVersion : undefined),
  promptFeedback: response?.promptFeedback || undefined,
  responseId: typeof response?.responseId === 'string' ? response.responseId : undefined,
  billingSummary,
});

const serializeGenerateContentChunk = (chunk: any): Record<string, unknown> => {
  const serialized = JSON.parse(JSON.stringify(chunk || {})) as Record<string, unknown>;
  if (typeof chunk?.text === 'string') {
    serialized.text = chunk.text;
  }
  return serialized;
};

const requireOwnedManagedContentFiles = async (
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
  const reservedSearchQueries = usesManagedGoogleSearch(params.config)
    ? appConfig.managedSearchReservationQueries
    : 0;
  const estimatedUsd = estimateReservationUsd({
    model: params.model,
    promptTokens,
    operation: params.operation,
    searchQueries: reservedSearchQueries,
    expectedOutputTokens: Number(params.config?.maxOutputTokens || 0),
  });
  const estimatedCredits = usdToCredits(estimatedUsd);

  const reservation = await reserveManagedCredits({
    uid: params.uid,
    user: params.user,
    operation: params.operation,
    model: params.model,
    estimatedCredits,
    estimatedUsd,
    metadata: {
      promptTokens,
      reservedSearchQueries,
      maxOutputTokens: Number(params.config?.maxOutputTokens || 0),
    },
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
}) => {
  const model = requirePricedManagedGenerationModel(
    requireAllowedManagedModel(
      resolvePinnedManagedGenerationModel(params.model),
      appConfig.managedAllowedGeminiModels,
      'generation',
    ),
  );
  const config = applyManagedGenerationLimits(
    params.config,
    appConfig.managedMaxOutputTokens,
  );
  const operation = resolveManagedContentOperation(config, false, model);
  await requireOwnedManagedContentFiles(params.uid, params.contents, config);

  const finalized = await withManagedReservation({
    ...params,
    model,
    operation,
    config,
    execute: async () => (
      getGeminiClient().models.generateContent({
        model,
        contents: params.contents,
        ...(config ? { config } : {}),
      } as any)
    ),
    finalize: async (reservationId, response) => {
      const usageMetadata = response?.usageMetadata as Record<string, unknown> | undefined;
      const resolvedModelVersion = typeof response?.modelVersion === 'string'
        ? response.modelVersion.trim() || undefined
        : undefined;
      const billedUsd = usageMetadataToUsd(
        model,
        usageMetadata,
        operation,
        countGeneratedImages(response),
        countGoogleSearchQueries(response),
        resolvedModelVersion,
      );
      const billedCredits = usdToCredits(billedUsd);
      const billingSummary = await settleManagedReservation({
        uid: params.uid,
        reservationId,
        billedCredits,
        billedUsd,
        operation,
        model: resolvedModelVersion || model,
        metadata: {
          requestedModel: model,
          resolvedModelVersion: resolvedModelVersion || null,
          promptTokenCount: usageMetadata?.promptTokenCount,
          candidatesTokenCount: usageMetadata?.candidatesTokenCount,
          searchQueries: countGoogleSearchQueries(response),
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
  response: Response;
}) => {
  const response = params.response;
  const model = requirePricedManagedGenerationModel(
    requireAllowedManagedModel(
      resolvePinnedManagedGenerationModel(params.model),
      appConfig.managedAllowedGeminiModels,
      'streaming generation',
    ),
  );
  const config = applyManagedGenerationLimits(
    params.config,
    appConfig.managedMaxOutputTokens,
  );
  const operation = resolveManagedContentOperation(config, true, model);
  await requireOwnedManagedContentFiles(params.uid, params.contents, config);
  await sweepExpiredReservationsForUser(params.uid);

  const promptTokens = await countPromptTokens(model, params.contents, config);
  const reservedSearchQueries = usesManagedGoogleSearch(config)
    ? appConfig.managedSearchReservationQueries
    : 0;
  const estimatedUsd = estimateReservationUsd({
    model,
    promptTokens,
    operation,
    searchQueries: reservedSearchQueries,
    expectedOutputTokens: Number(config.maxOutputTokens || 0),
  });
  const estimatedCredits = usdToCredits(estimatedUsd);

  const reservation = await reserveManagedCredits({
    uid: params.uid,
    user: params.user,
    operation,
    model,
    estimatedCredits,
    estimatedUsd,
    metadata: {
      promptTokens,
      reservedSearchQueries,
      maxOutputTokens: Number(config.maxOutputTokens || 0),
    },
  });

  response.setHeader('Content-Type', STREAM_CONTENT_TYPE);
  response.setHeader('Cache-Control', 'no-store, no-transform');
  response.setHeader('X-Accel-Buffering', 'no');

  let latestChunk: any = null;
  let resolvedModelVersion: string | undefined;
  let deliveredAnyChunk = false;
  // Images arrive spread across chunks, so they are tallied as they stream
  // rather than read off the final one.
  let streamedImageCount = 0;
  let streamedSearchQueryCount = 0;
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
      model,
      contents: params.contents,
      ...(config ? { config } : {}),
    } as any);

    for await (const chunk of stream) {
      latestChunk = chunk;
      if (typeof chunk?.modelVersion === 'string' && chunk.modelVersion.trim()) {
        resolvedModelVersion = chunk.modelVersion.trim();
      }
      streamedImageCount += countGeneratedImages(chunk);
      streamedSearchQueryCount = Math.max(
        streamedSearchQueryCount,
        countGoogleSearchQueries(chunk),
      );
      if (clientDisconnected || response.destroyed || !response.writable) {
        clientDisconnected = true;
        break;
      }

      response.write(`${JSON.stringify({
        type: 'chunk',
        chunk: serializeGenerateContentChunk(chunk),
      })}\n`);
      deliveredAnyChunk = true;
    }

    generationCompleted = !clientDisconnected;

    if (generationCompleted) {
      const usageMetadata = latestChunk?.usageMetadata as Record<string, unknown> | undefined;
      const billedUsd = usageMetadataToUsd(
        model,
        usageMetadata,
        operation,
        streamedImageCount,
        streamedSearchQueryCount,
        resolvedModelVersion,
      );
      const billedCredits = usdToCredits(billedUsd);
      const billingSummary = await settleManagedReservation({
        uid: params.uid,
        reservationId: reservation.reservationId,
        billedCredits,
        billedUsd,
        operation,
        model: resolvedModelVersion || model,
        metadata: {
          requestedModel: model,
          resolvedModelVersion: resolvedModelVersion || null,
          promptTokenCount: usageMetadata?.promptTokenCount,
          candidatesTokenCount: usageMetadata?.candidatesTokenCount,
          disconnectRecovered: false,
          searchQueries: streamedSearchQueryCount,
        },
      });

      if (!clientDisconnected && !response.destroyed && response.writable) {
        response.write(`${JSON.stringify({
          type: 'final',
          result: serializeGenerateContentResponse(
            latestChunk || {},
            billingSummary,
            resolvedModelVersion,
          ),
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
        operation,
        model: resolvedModelVersion || model,
        metadata: {
          requestedModel: model,
          resolvedModelVersion: resolvedModelVersion || null,
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
      const billingSummary = await settleManagedReservation({
        uid: params.uid,
        reservationId: reservation.reservationId,
        billedCredits: estimatedCredits,
        billedUsd: estimatedUsd,
        operation,
        model: resolvedModelVersion || model,
        metadata: {
          requestedModel: model,
          resolvedModelVersion: resolvedModelVersion || null,
          promptTokens,
          streamFailedAfterOutput: true,
          partialStreamDelivered: deliveredAnyChunk,
        },
      });
      if (!response.destroyed && response.writable && !response.writableEnded) {
        response.write(`${JSON.stringify({
          type: 'final',
          result: serializeGenerateContentResponse(
            latestChunk || {},
            billingSummary,
            resolvedModelVersion,
          ),
        })}\n`);
        response.write(`${JSON.stringify({
          type: 'error',
          message: getErrorMessage(error),
          status: Number((error as { status?: unknown })?.status) || 500,
        })}\n`);
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

export const getManagedFileStatuses = async (uid: string, uris: string[]) => {
  if (uris.length > MAX_FILE_STATUS_URIS) {
    throw createHttpError(400, `At most ${MAX_FILE_STATUS_URIS} file URIs may be checked at once.`);
  }
  const statuses: Record<string, { deleted: boolean; active: boolean }> = {};

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

      try {
        const file = await getGeminiClient().files.get({ name: fileName });
        const active = file?.state === 'ACTIVE';
        const deleted = file?.state === 'FAILED';
        statuses[uri] = { deleted, active };

        if (deleted) {
          await markManagedFileDeleted(uid, fileName);
        } else {
          await managedFileRef(uid, fileName).set({
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

export const createManagedLiveToken = async (params: {
  uid: string;
  user: AppUser;
  model: string;
  config?: Record<string, unknown>;
  purpose?: 'live' | 'music';
  durationSeconds?: number;
}) => {
  const purpose = params.purpose === 'music' ? 'music' : 'live';
  const model = requireAllowedManagedModel(
    params.model,
    purpose === 'music'
      ? appConfig.managedAllowedMusicModels
      : appConfig.managedAllowedLiveModels,
    purpose === 'music' ? 'music' : 'live audio',
  );
  const liveConfig = requireSafeManagedLiveConfig(params.config);
  const liveWindowSeconds = appConfig.managedLiveTokenLifetimeSeconds;
  const fixedCredits = purpose === 'music'
    ? appConfig.managedMusicSessionCredits
    : calculateManagedLiveWindowCredits(liveWindowSeconds);
  const billedUsd = purpose === 'music'
    ? creditsToUsd(fixedCredits)
    : calculateManagedLiveWindowUsd(liveWindowSeconds);
  const liveTokenBudget = purpose === 'live'
    ? getManagedLiveWindowTokenBudget(liveWindowSeconds)
    : null;

  const lease = await reserveManagedLiveLease({
    uid: params.uid,
    purpose,
    durationMs: liveWindowSeconds * 1000,
  });

  let reservation: Awaited<ReturnType<typeof reserveManagedCredits>>;
  try {
    reservation = await reserveManagedCredits({
      uid: params.uid,
      user: params.user,
      operation: purpose === 'music' ? 'liveTokenMusic' : 'liveToken',
      model,
      estimatedCredits: fixedCredits,
      estimatedUsd: billedUsd,
      metadata: {
        purpose,
        leaseId: lease.leaseId,
        requestedDurationSeconds: params.durationSeconds || null,
        maxWindowSeconds: liveWindowSeconds,
        ...(liveTokenBudget || {}),
      },
    });
  } catch (error) {
    await releaseManagedLiveLease(params.uid, lease.leaseId).catch(() => undefined);
    throw error;
  }

  const expireTime = new Date(Date.now() + liveWindowSeconds * 1000).toISOString();

  try {
    const tokenResponse = await getGeminiClient().authTokens.create({
      config: {
        uses: appConfig.geminiLiveTokenUses,
        expireTime,
        httpOptions: {
          apiVersion: 'v1alpha',
        },
        liveConnectConstraints: {
          model,
          ...(liveConfig ? { config: liveConfig } : {}),
        },
      },
    } as any);

    const token = typeof (tokenResponse as any)?.name === 'string'
      ? (tokenResponse as any).name
      : (typeof (tokenResponse as any)?.token === 'string' ? (tokenResponse as any).token : '');
    if (!token) {
      throw createHttpError(500, 'Backend could not mint a Gemini live token.');
    }

    const billingSummary = await settleManagedReservation({
      uid: params.uid,
      reservationId: reservation.reservationId,
      billedCredits: fixedCredits,
      billedUsd,
      operation: purpose === 'music' ? 'liveTokenMusic' : 'liveToken',
      model,
      metadata: {
        purpose,
        leaseId: lease.leaseId,
        uses: appConfig.geminiLiveTokenUses,
        maxWindowSeconds: liveWindowSeconds,
        ...(liveTokenBudget || {}),
      },
    });

    return {
      leaseId: lease.leaseId,
      token,
      expiresAt: typeof (tokenResponse as any)?.expireTime === 'string'
        ? (tokenResponse as any).expireTime
        : expireTime,
      uses: appConfig.geminiLiveTokenUses,
      billingSummary,
    };
  } catch (error) {
    await releaseManagedReservation(params.uid, reservation.reservationId, 'live-token-mint-failed')
      .catch(() => undefined);
    await releaseManagedLiveLease(params.uid, lease.leaseId).catch(() => undefined);
    throw error;
  }
};
