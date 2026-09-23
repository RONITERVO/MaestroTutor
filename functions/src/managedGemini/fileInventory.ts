// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { FieldPath } from 'firebase-admin/firestore';
import { adminDb } from '../firebase';
import { createHttpError } from '../http';
import { accountDeletionClaimRef, managedFileQuotaRef, managedFilesCollection } from '../managedData';

const INVENTORY_VERSION = 1;

/** Backfill once: missing deletedAt means active, but Firestore's null query
 * omits it. Transactions prevent this migration from undoing concurrent deletes. */
export const ensureManagedFileInventory = async (uid: string): Promise<void> => {
  const quotaRef = managedFileQuotaRef(uid);
  if ((await quotaRef.get()).data()?.fileInventoryVersion === INVENTORY_VERSION) return;
  let cursor: FirebaseFirestore.QueryDocumentSnapshot | undefined;
  while (true) {
    let query = managedFilesCollection(uid).orderBy(FieldPath.documentId()).limit(200);
    if (cursor) query = query.startAfter(cursor);
    const page = await adminDb.runTransaction(async transaction => {
      const [snapshot, deletionClaim] = await Promise.all([
        transaction.get(query), transaction.get(accountDeletionClaimRef(uid)),
      ]);
      if (deletionClaim.exists) throw createHttpError(409, 'This managed account is being deleted.');
      for (const doc of snapshot.docs) {
        if (doc.data().deletedAt === undefined) transaction.update(doc.ref, { deletedAt: null });
      }
      return snapshot;
    });
    if (page.size < 200) break;
    cursor = page.docs[page.docs.length - 1];
  }
  await adminDb.runTransaction(async transaction => {
    if ((await transaction.get(accountDeletionClaimRef(uid))).exists) {
      throw createHttpError(409, 'This managed account is being deleted.');
    }
    transaction.set(quotaRef, { fileInventoryVersion: INVENTORY_VERSION }, { merge: true });
  });
};

/** Once normalized, retained deleted history never enters active-file reads. */
export const listActiveManagedFileSnapshots = async (
  uid: string,
  transaction?: FirebaseFirestore.Transaction,
  maximum = Number.POSITIVE_INFINITY,
): Promise<FirebaseFirestore.QueryDocumentSnapshot[]> => {
  // Admission normalizes before opening its transaction; eviction does so here.
  if (!transaction) await ensureManagedFileInventory(uid);
  const active: FirebaseFirestore.QueryDocumentSnapshot[] = [];
  let cursor: FirebaseFirestore.QueryDocumentSnapshot | undefined;
  while (active.length < maximum) {
    let query = managedFilesCollection(uid).where('deletedAt', '==', null)
      .orderBy(FieldPath.documentId()).limit(Math.min(200, maximum - active.length));
    if (cursor) query = query.startAfter(cursor);
    const page = transaction ? await transaction.get(query) : await query.get();
    active.push(...page.docs);
    if (page.size < 200) break;
    cursor = page.docs[page.docs.length - 1];
  }
  return active;
};
