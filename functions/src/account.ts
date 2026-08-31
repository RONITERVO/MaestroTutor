// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import type { Request } from 'express';
import { FieldValue } from 'firebase-admin/firestore';
import type { AppUser, AuthContext } from './auth';
import { adminAuth, adminDb } from './firebase';
import { clearLegacyManagedFiles, clearManagedFiles, queueManagedFileCleanupJobs } from './gemini';
import { createHttpError } from './http';
import {
  MANAGED_REPORT_RETENTION_MS,
  MANAGED_RATE_LIMIT_BUCKETS,
  MANAGED_SCHEMA_VERSION,
  accountDeletionClaimRef,
  checkoutGrantsCollection,
  managedFilesCollection,
  managedReservationsCollection,
  managedUserRef,
  purchaseClaimId,
  purchaseClaimsCollection,
  rateLimitWindowId,
  rateLimitWindowsCollection,
  reportsCollection,
  timestampFromMillis,
} from './managedData';
import { releaseManagedReservation, sweepExpiredReservationsForUser } from './managedBilling';
import { deleteManagedStripeCustomers } from './stripeBilling';

export interface ManagedAccountDeletionResult {
  ok: true;
  deletedAt: number;
  releasedReservationCount: number;
  deletedReservationCount: number;
  deletedManagedFileCount: number;
  anonymizedPurchaseCount: number;
  anonymizedReportCount: number;
  remoteManagedFileFailures: number;
  queuedRemoteCleanupCount: number;
  deletedStripeCustomerCount: number;
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

const deleteDocumentsById = async (
  collection: FirebaseFirestore.CollectionReference<FirebaseFirestore.DocumentData>,
  documentIds: string[],
  batchSize = 200,
): Promise<number> => {
  let deletedCount = 0;
  for (let index = 0; index < documentIds.length; index += batchSize) {
    const ids = documentIds.slice(index, index + batchSize);
    const batch = adminDb.batch();
    ids.forEach((id) => batch.delete(collection.doc(id)));
    await batch.commit();
    deletedCount += ids.length;
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

/**
 * Defensively replace any record at a code-only v1 path whose document id is a
 * raw store token, then remove the credential-bearing document path.
 */
const anonymizeLegacyPurchaseClaims = async (
  collectionName: string,
  uid: string,
  deletedAt: number,
  batchSize = 200,
): Promise<number> => {
  let migratedCount = 0;
  while (true) {
    const snapshot = await adminDb.collection(collectionName)
      .where('uid', '==', uid)
      .limit(batchSize)
      .get();
    if (snapshot.empty) break;

    for (const doc of snapshot.docs) {
      const data = doc.data();
      const platform = data.platform === 'stripe' ? 'stripe' : 'google-play';
      const claimId = purchaseClaimId(platform, doc.id);
      const claimRef = purchaseClaimsCollection().doc(claimId);
      await adminDb.runTransaction(async (transaction) => {
        const existingClaim = await transaction.get(claimRef);
        const existingUid = existingClaim.data()?.uid;
        if (!existingClaim.exists || existingUid === uid || existingUid == null) {
          transaction.set(claimRef, {
            uid: null,
            platform,
            externalIdHash: claimId,
            productId: typeof data.productId === 'string' ? data.productId : null,
            creditsGranted: Number(data.creditsGranted || 0) || 0,
            createdAt: Number(data.createdAt || 0) || deletedAt,
            orderId: FieldValue.delete(),
            rawPurchase: FieldValue.delete(),
            rawVerification: FieldValue.delete(),
            deletedUser: true,
            accountDeletedAt: deletedAt,
          }, { merge: true });
        }
        transaction.delete(doc.ref);
      });
      migratedCount += 1;
    }
  }
  return migratedCount;
};

const releaseActiveReservationsForUser = async (uid: string): Promise<number> => {
  let releasedCount = 0;

  while (true) {
    const snapshot = await managedReservationsCollection(uid)
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

  // If cleanup fails midway, the real root document records the lifecycle
  // state instead of leaving an apparently active account with missing data.
  const deletionBatch = adminDb.batch();
  deletionBatch.set(managedUserRef(params.uid), {
    status: 'deleting',
    updatedAt: deletedAt,
  }, { merge: true });
  deletionBatch.create(accountDeletionClaimRef(params.uid), {
    createdAt: deletedAt,
    schemaVersion: MANAGED_SCHEMA_VERSION,
  });
  try {
    await deletionBatch.commit();
  } catch (error) {
    // A retry after partial cleanup should reuse the existing tombstone.
    const code = (error as { code?: unknown })?.code;
    if (code !== 6 && code !== '6' && code !== 'already-exists') throw error;
    await managedUserRef(params.uid).set({ status: 'deleting', updatedAt: deletedAt }, { merge: true });
  }

  // Stripe keeps the email and Firebase UID on its customer object. Removing
  // only Firestore would therefore not be a complete account deletion.
  const deletedStripeCustomerCount = await deleteManagedStripeCustomers(params.uid);

  await sweepExpiredReservationsForUser(params.uid);
  const releasedReservationCount = await releaseActiveReservationsForUser(params.uid);
  const [managedFileCleanup, legacyManagedFileCleanup] = await Promise.all([
    clearManagedFiles(params.uid),
    clearLegacyManagedFiles(params.uid),
  ]);

  // Persist retry ownership before removing metadata. If this write fails the
  // deletion stops while the source records are still available for a retry.
  const queuedRemoteCleanupCount = await queueManagedFileCleanupJobs(
    [...managedFileCleanup.failedNames, ...legacyManagedFileCleanup.failedNames],
  );

  const deletedCurrentManagedFileCount = await deleteDocumentsById(
    managedFilesCollection(params.uid),
    managedFileCleanup.cleanedMetadataIds,
  );
  const deletedLegacyManagedFileCount = await deleteDocumentsById(
    adminDb.collection('managedFiles'),
    legacyManagedFileCleanup.cleanedMetadataIds,
  );
  const deletedManagedFileCount = deletedCurrentManagedFileCount + deletedLegacyManagedFileCount;

  await deleteDocumentsById(
    rateLimitWindowsCollection(),
    MANAGED_RATE_LIMIT_BUCKETS.map((bucket) => rateLimitWindowId(params.uid, bucket)),
  );

  const deletedCurrentReservationCount = await deleteQueryDocuments(
    () => managedReservationsCollection(params.uid),
  );
  const deletedLegacyReservationCount = await deleteQueryDocuments(
    () => adminDb.collection('managedReservations').where('uid', '==', params.uid),
  );
  const deletedReservationCount = deletedCurrentReservationCount + deletedLegacyReservationCount;

  const anonymizedCurrentPurchaseCount = await patchQueryDocuments(
    () => purchaseClaimsCollection().where('uid', '==', params.uid),
    (data) => ({
      uid: null,
      orderId: null,
      rawPurchase: FieldValue.delete(),
      rawVerification: FieldValue.delete(),
      deletedUser: true,
      accountDeletedAt: deletedAt,
      creditsGranted: Number(data.creditsGranted || 0) || 0,
      productId: typeof data.productId === 'string' ? data.productId : null,
      createdAt: Number(data.createdAt || 0) || deletedAt,
    }),
  );

  // Prevent a checkout that completes after account deletion from recreating
  // a balance for a Firebase Auth user that no longer exists.
  await patchQueryDocuments(
    () => checkoutGrantsCollection().where('uid', '==', params.uid),
    () => ({
      uid: null,
      accountDeleted: true,
      accountDeletedAt: deletedAt,
    }),
  );
  await patchQueryDocuments(
    () => adminDb.collection('stripeCheckoutGrants').where('uid', '==', params.uid),
    () => ({ uid: null, accountDeleted: true, accountDeletedAt: deletedAt }),
  );

  // The undeployed v1 draft put the raw store token in the document path.
  // Preserve idempotency defensively before removing any such document.
  const anonymizedLegacyProcessedPurchaseCount = await anonymizeLegacyPurchaseClaims(
    'processedPurchases',
    params.uid,
    deletedAt,
  );
  const anonymizedLegacyPurchaseCount = await anonymizeLegacyPurchaseClaims(
    'googlePlayPurchases',
    params.uid,
    deletedAt,
  );
  const anonymizedPurchaseCount = anonymizedCurrentPurchaseCount
    + anonymizedLegacyProcessedPurchaseCount
    + anonymizedLegacyPurchaseCount;

  const anonymizedCurrentReportCount = await patchQueryDocuments(
    () => reportsCollection().where('uid', '==', params.uid),
    () => ({
      uid: null,
      user: null,
      accountDeletedAt: deletedAt,
      accountDeleted: true,
    }),
  );

  // Cover reports written by the v1 preview as well.
  const anonymizedLegacyReportCount = await patchQueryDocuments(
    () => adminDb.collection('aiContentReports').where('uid', '==', params.uid),
    () => ({ uid: null, user: null, accountDeletedAt: deletedAt, accountDeleted: true }),
  );
  const anonymizedReportCount = anonymizedCurrentReportCount + anonymizedLegacyReportCount;

  await adminDb.recursiveDelete(managedUserRef(params.uid));

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
    remoteManagedFileFailures: managedFileCleanup.failedCount + legacyManagedFileCleanup.failedCount,
    queuedRemoteCleanupCount,
    deletedStripeCustomerCount,
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
  const reportRef = reportsCollection().doc();

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
    purgeAt: timestampFromMillis(createdAt + MANAGED_REPORT_RETENTION_MS),
  });

  return {
    ok: true,
    reportId: reportRef.id,
    createdAt,
  };
};
