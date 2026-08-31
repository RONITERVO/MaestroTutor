// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import type { Request } from 'express';
import type { AppUser, AuthContext } from './auth';
import { adminAuth, adminDb } from './firebase';
import { clearManagedFiles } from './gemini';
import { createHttpError } from './http';
import { releaseManagedReservation, sweepExpiredReservationsForUser } from './managedBilling';

export interface ManagedAccountDeletionResult {
  ok: true;
  deletedAt: number;
  releasedReservationCount: number;
  deletedReservationCount: number;
  deletedManagedFileCount: number;
  anonymizedPurchaseCount: number;
  anonymizedReportCount: number;
  remoteManagedFileFailures: number;
}

export interface AiContentReportResult {
  ok: true;
  reportId: string;
  createdAt: number;
}

const MAX_TEXT_EXCERPT_LENGTH = 4_000;
const MAX_NOTES_LENGTH = 2_000;
const MAX_ID_LENGTH = 200;
const MAX_MODEL_LENGTH = 200;
const MAX_SURFACE_LENGTH = 100;

const userDoc = (uid: string) => adminDb.collection('users').doc(uid);

const trimString = (value: unknown, maxLength: number): string => {
  if (typeof value !== 'string') return '';
  const trimmed = value.trim();
  return trimmed.length > maxLength ? trimmed.slice(0, maxLength) : trimmed;
};

const optionalTimestamp = (value: unknown): number | null => {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? Math.floor(parsed) : null;
};

const deleteQueryDocuments = async (
  createQuery: () => FirebaseFirestore.Query<FirebaseFirestore.DocumentData>,
  batchSize = 200
): Promise<number> => {
  let deletedCount = 0;
  while (true) {
    const snapshot = await createQuery().limit(batchSize).get();
    if (snapshot.empty) break;

    const batch = adminDb.batch();
    snapshot.docs.forEach((doc) => batch.delete(doc.ref));
    await batch.commit();
    deletedCount += snapshot.size;

    if (snapshot.size < batchSize) break;
  }
  return deletedCount;
};

const patchQueryDocuments = async (
  createQuery: () => FirebaseFirestore.Query<FirebaseFirestore.DocumentData>,
  buildPatch: (data: FirebaseFirestore.DocumentData) => FirebaseFirestore.UpdateData<FirebaseFirestore.DocumentData>,
  batchSize = 200
): Promise<number> => {
  let updatedCount = 0;
  while (true) {
    const snapshot = await createQuery().limit(batchSize).get();
    if (snapshot.empty) break;

    const batch = adminDb.batch();
    snapshot.docs.forEach((doc) => batch.set(doc.ref, buildPatch(doc.data()), { merge: true }));
    await batch.commit();
    updatedCount += snapshot.size;

    if (snapshot.size < batchSize) break;
  }
  return updatedCount;
};

const releaseActiveReservationsForUser = async (uid: string): Promise<number> => {
  let releasedCount = 0;

  while (true) {
    const snapshot = await adminDb.collection('managedReservations')
      .where('uid', '==', uid)
      .where('status', '==', 'active')
      .limit(25)
      .get();

    if (snapshot.empty) break;

    for (const doc of snapshot.docs) {
      await releaseManagedReservation(uid, doc.id, 'account-deleted');
      releasedCount += 1;
    }
  }

  return releasedCount;
};

const isAuthUserNotFound = (error: unknown): boolean => (
  typeof error === 'object'
  && error !== null
  && typeof (error as { code?: unknown }).code === 'string'
  && (error as { code: string }).code === 'auth/user-not-found'
);

export const deleteManagedAccount = async (params: {
  uid: string;
  user: AppUser;
}): Promise<ManagedAccountDeletionResult> => {
  const deletedAt = Date.now();

  await sweepExpiredReservationsForUser(params.uid);
  const releasedReservationCount = await releaseActiveReservationsForUser(params.uid);
  const managedFileCleanup = await clearManagedFiles(params.uid);

  const deletedManagedFileCount = await deleteQueryDocuments(
    () => adminDb.collection('managedFiles').where('uid', '==', params.uid),
  );

  const deletedReservationCount = await deleteQueryDocuments(
    () => adminDb.collection('managedReservations').where('uid', '==', params.uid),
  );

  const anonymizedPurchaseCount = await patchQueryDocuments(
    () => adminDb.collection('googlePlayPurchases').where('uid', '==', params.uid),
    (data) => ({
      uid: null,
      orderId: null,
      rawPurchase: null,
      rawVerification: null,
      deletedUser: true,
      accountDeletedAt: deletedAt,
      lastKnownEmail: null,
      lastKnownDisplayName: null,
      lastKnownPhotoUrl: null,
      creditsGranted: Number(data.creditsGranted || 0) || 0,
      productId: typeof data.productId === 'string' ? data.productId : null,
      packageName: typeof data.packageName === 'string' ? data.packageName : null,
      createdAt: Number(data.createdAt || 0) || deletedAt,
    }),
  );

  const anonymizedReportCount = await patchQueryDocuments(
    () => adminDb.collection('aiContentReports').where('uid', '==', params.uid),
    () => ({
      uid: null,
      user: null,
      accountDeletedAt: deletedAt,
      accountDeleted: true,
    }),
  );

  await adminDb.recursiveDelete(userDoc(params.uid));

  try {
    await adminAuth.deleteUser(params.uid);
  } catch (error) {
    if (!isAuthUserNotFound(error)) {
      throw error;
    }
  }

  return {
    ok: true,
    deletedAt,
    releasedReservationCount,
    deletedReservationCount,
    deletedManagedFileCount,
    anonymizedPurchaseCount,
    anonymizedReportCount,
    remoteManagedFileFailures: managedFileCleanup.failedCount,
  };
};

const coerceAccessMode = (value: unknown): 'byok' | 'managed' => (
  value === 'managed' ? 'managed' : 'byok'
);

const coerceReason = (value: unknown): string => {
  const reason = trimString(value, 50).toLowerCase();
  if (!reason) {
    throw createHttpError(400, 'A report reason is required.');
  }
  return reason;
};

const createAssistantExcerpt = (payload: {
  assistantText: string;
  rawAssistantResponse: string;
  surface: string;
}): string => {
  if (payload.assistantText) return payload.assistantText;
  if (payload.rawAssistantResponse) return payload.rawAssistantResponse;
  return `[${payload.surface || 'chat'} response with no text excerpt]`;
};

export const submitAiContentReport = async (params: {
  req: Request;
  auth: AuthContext | null;
  payload: Record<string, unknown>;
}): Promise<AiContentReportResult> => {
  const createdAt = Date.now();
  const assistantText = trimString(params.payload.assistantText, MAX_TEXT_EXCERPT_LENGTH);
  const rawAssistantResponse = trimString(params.payload.rawAssistantResponse, MAX_TEXT_EXCERPT_LENGTH);
  const surface = trimString(params.payload.surface, MAX_SURFACE_LENGTH) || 'chat';
  const reportRef = adminDb.collection('aiContentReports').doc();

  await reportRef.set({
    reportId: reportRef.id,
    createdAt,
    createdAtClient: optionalTimestamp(params.payload.createdAtClient),
    accessMode: coerceAccessMode(params.payload.accessMode),
    reason: coerceReason(params.payload.reason),
    notes: trimString(params.payload.notes, MAX_NOTES_LENGTH) || null,
    messageId: trimString(params.payload.messageId, MAX_ID_LENGTH) || null,
    surface,
    model: trimString(params.payload.model, MAX_MODEL_LENGTH) || null,
    assistantText: assistantText || null,
    rawAssistantResponse: rawAssistantResponse || null,
    assistantExcerpt: createAssistantExcerpt({ assistantText, rawAssistantResponse, surface }),
    uid: params.auth?.uid || null,
    user: params.auth?.user || null,
    requestMeta: {
      origin: trimString(params.req.headers.origin, 200) || null,
      userAgent: trimString(params.req.headers['user-agent'], 500) || null,
      hasAuth: Boolean(params.auth),
      hasAppCheckToken: Boolean(trimString(params.req.headers['x-firebase-appcheck'], MAX_ID_LENGTH)),
    },
  });

  return {
    ok: true,
    reportId: reportRef.id,
    createdAt,
  };
};
