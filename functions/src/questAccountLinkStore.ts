// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { Timestamp, type Firestore } from 'firebase-admin/firestore';
import { createHttpError, getHttpErrorCode } from './http';
import { accountDeletionClaimId, QUEST_ACCOUNT_LINKS_COLLECTION } from './managedData';
import { linkRejected, linkUnavailable, questLinkHash, QUEST_LINK_TTL_MS, type QuestLinkRecord, type QuestLinkStore } from './questAccountLink';

export const QUEST_LINKS_COLLECTION = QUEST_ACCOUNT_LINKS_COLLECTION;
export const QUEST_LINK_RATES_COLLECTION = 'questAccountLinkRateWindows';
const live = (value: FirebaseFirestore.DocumentData | undefined, now: number): QuestLinkRecord => {
  if (!value || !Number.isSafeInteger(value.createdAt) || !Number.isSafeInteger(value.expiresAt)
    || value.createdAt > now || value.expiresAt <= now || value.expiresAt - value.createdAt !== QUEST_LINK_TTL_MS
    || !['pending', 'approved'].includes(value.state) || !/^[a-f0-9]{64}$/.test(value.secretHash || '')
    || (value.state === 'approved' && (typeof value.uid !== 'string' || !value.uid || value.uid.length > 128))) throw linkRejected();
  return value as QuestLinkRecord;
};
const safe = async <T>(action: () => Promise<T>): Promise<T> => {
  try { return await action(); } catch (error) {
    if (getHttpErrorCode(error)?.startsWith('quest-link/')) throw error;
    throw linkUnavailable();
  }
};
export const createQuestAccountLinkStore = (db: Firestore): QuestLinkStore => {
  const ref = (hash: string) => db.collection(QUEST_LINKS_COLLECTION).doc(hash);
  const accountAllowed = async (tx: FirebaseFirestore.Transaction, uid: string) => {
    const deletion = await tx.get(db.collection('accountDeletionClaims').doc(accountDeletionClaimId(uid)));
    const user = await tx.get(db.collection('users').doc(uid));
    if (deletion.exists || user.data()?.status === 'deleting') throw linkRejected();
  };
  const device = (value: FirebaseFirestore.DocumentData | undefined, hash: string, now: number) => {
    const record = live(value, now);
    if (record.secretHash !== hash) throw linkRejected();
    return record;
  };
  return {
    throttle: (subject, bucket, limit, now) => safe(() => db.runTransaction(async tx => {
      const reference = db.collection(QUEST_LINK_RATES_COLLECTION).doc(questLinkHash(`${subject}\0${bucket}`));
      const stored = (await tx.get(reference)).data();
      const startedAt = stored && Number.isSafeInteger(stored.startedAt) && stored.startedAt <= now
        && now - stored.startedAt < 60_000 ? stored.startedAt : now;
      const count = stored && stored.startedAt === startedAt ? stored.count : 0;
      if (!Number.isSafeInteger(count) || count < 0) throw linkUnavailable();
      if (count >= limit) throw createHttpError(429, 'Too many account-link requests. Try again later.', 'quest-link/rate-limited');
      tx.set(reference, { startedAt, count: count + 1, purgeAt: Timestamp.fromMillis(startedAt + 120_000) });
    })),
    issue: (hash, record) => safe(() => db.runTransaction(async tx => {
      if ((await tx.get(ref(hash))).exists) return false;
      tx.create(ref(hash), { ...record, purgeAt: Timestamp.fromMillis(record.expiresAt) });
      return true;
    })),
    status: (hash, secret, now) => safe(async () => {
      const record = device((await ref(hash).get()).data(), secret, now);
      return { state: record.state as 'pending' | 'approved', expiresAt: record.expiresAt };
    }),
    approve: (hash, uid, now) => safe(() => db.runTransaction(async tx => {
      const record = live((await tx.get(ref(hash))).data(), now);
      if (record.state !== 'pending') throw linkRejected();
      await accountAllowed(tx, uid);
      tx.update(ref(hash), { state: 'approved', uid });
    })),
    claim: (hash, secret, now) => safe(() => db.runTransaction(async tx => {
      const record = device((await tx.get(ref(hash))).data(), secret, now);
      if (record.state !== 'approved' || !record.uid) throw linkRejected();
      await accountAllowed(tx, record.uid);
      tx.set(ref(hash), { createdAt: record.createdAt, expiresAt: record.expiresAt,
        state: 'consumed', purgeAt: Timestamp.fromMillis(record.expiresAt) });
      return { uid: record.uid, expiresAt: record.expiresAt };
    })),
    cancel: (hash, secret, now) => safe(() => db.runTransaction(async tx => {
      const record = device((await tx.get(ref(hash))).data(), secret, now);
      tx.set(ref(hash), { createdAt: record.createdAt, expiresAt: record.expiresAt,
        state: 'cancelled', purgeAt: Timestamp.fromMillis(record.expiresAt) });
    })),
  };
};
