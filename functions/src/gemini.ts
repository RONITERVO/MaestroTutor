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
import { estimateReservationUsd, usageMetadataToUsd, usdToCredits, creditsToUsd } from './pricing';

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

  const finalized = await withManagedReservation({
    ...params,
    execute: async () => {
      const stream = await getGeminiClient().models.generateContentStream({
        model: params.model,
        contents: params.contents,
        ...(params.config ? { config: params.config } : {}),
      } as any);

      let latestChunk: any = null;
      for await (const chunk of stream) {
        latestChunk = chunk;
        response.write(`${JSON.stringify({ type: 'chunk', chunk })}\n`);
      }

      return latestChunk || {};
    },
    finalize: async (reservationId, latestChunk) => {
      const usageMetadata = latestChunk?.usageMetadata as Record<string, unknown> | undefined;
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
      return { result: latestChunk, billingSummary };
    },
  });

  response.write(`${JSON.stringify({
    type: 'final',
    result: serializeGenerateContentResponse(finalized.result, finalized.billingSummary),
  })}\n`);
  response.end();
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

  const extension = (mimeType.split('/')[1] || 'bin').replace(/[^a-z0-9]/gi, '');
  const tempPath = path.join(os.tmpdir(), `${randomUUID()}.${extension || 'bin'}`);
  const buffer = Buffer.from(dataUrl.slice(base64Index + 1), 'base64');
  await fs.writeFile(tempPath, buffer);
  return {
    path: tempPath,
    filename: displayName || path.basename(tempPath),
  };
};

export const uploadManagedMedia = async (params: {
  uid: string;
  dataUrl: string;
  mimeType: string;
  displayName?: string;
}) => {
  const tempFile = await dataUrlToTemporaryFile(params.dataUrl, params.mimeType, params.displayName);
  try {
    const uploaded = await getGeminiClient().files.upload({
      file: tempFile.path,
      config: {
        mimeType: params.mimeType,
        displayName: tempFile.filename,
      },
    });

    const fileName = normalizeGeminiFileName(uploaded?.name || uploaded?.uri || '');
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
      createdAt: Date.now(),
      lastCheckedAt: Date.now(),
      deletedAt: null,
      state: 'active',
    }, { merge: true });

    return {
      uri: uploaded.uri,
      mimeType: uploaded.mimeType,
    };
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

      await managedFileRef(fileName).set({
        lastCheckedAt: Date.now(),
        state: deleted ? 'deleted' : (active ? 'active' : 'processing'),
        ...(deleted ? { deletedAt: Date.now() } : {}),
      }, { merge: true });
    } catch (error) {
      if (isNotFoundError(error)) {
        statuses[uri] = { deleted: true, active: false };
        await managedFileRef(fileName).set({
          lastCheckedAt: Date.now(),
          state: 'deleted',
          deletedAt: Date.now(),
        }, { merge: true });
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

  try {
    await getGeminiClient().files.delete({ name: fileName });
  } catch (error) {
    if (!isNotFoundError(error)) {
      throw error;
    }
  }

  await managedFileRef(fileName).set({
    deletedAt: Date.now(),
    lastCheckedAt: Date.now(),
    state: 'deleted',
  }, { merge: true });

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
      await doc.ref.set({
        deletedAt: Date.now(),
        lastCheckedAt: Date.now(),
        state: 'deleted',
      }, { merge: true });
      deletedCount += 1;
    } catch (error) {
      if (isNotFoundError(error)) {
        await doc.ref.set({
          deletedAt: Date.now(),
          lastCheckedAt: Date.now(),
          state: 'deleted',
        }, { merge: true });
        deletedCount += 1;
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
