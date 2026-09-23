// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { FieldPath } from 'firebase-admin/firestore';
import { managedFilesCollection } from '../managedData';

/** Missing deletedAt in older records means active, just as in account cleanup.
 * Page by document ID because querying deletedAt == null excludes those records. */
export const listActiveManagedFileSnapshots = async (
  uid: string,
  transaction?: FirebaseFirestore.Transaction,
  maximum = Number.POSITIVE_INFINITY,
): Promise<FirebaseFirestore.QueryDocumentSnapshot[]> => {
  const active: FirebaseFirestore.QueryDocumentSnapshot[] = [];
  let cursor: FirebaseFirestore.QueryDocumentSnapshot | undefined;
  while (active.length < maximum) {
    let query = managedFilesCollection(uid).orderBy(FieldPath.documentId()).limit(200);
    if (cursor) query = query.startAfter(cursor);
    const page = transaction ? await transaction.get(query) : await query.get();
    for (const doc of page.docs) {
      if (!doc.data().deletedAt) active.push(doc);
      if (active.length >= maximum) break;
    }
    if (page.size < 200) break;
    cursor = page.docs[page.docs.length - 1];
  }
  return active;
};
