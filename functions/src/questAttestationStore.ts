// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { createHash } from 'node:crypto';
import { Timestamp, type Firestore } from 'firebase-admin/firestore';
import { createHttpError } from './http';
import { type QuestAttestationStore, type QuestChallenge, questUnavailable } from './questAttestation';

export const QUEST_CHALLENGES_COLLECTION = 'questAttestationChallenges';
export const QUEST_RATE_COLLECTION = 'questAttestationRateWindows';
const WINDOW_MS = 60_000;

export const createQuestAttestationStore = (db: Firestore): QuestAttestationStore => {
  const transact = async (
    operation: 'challenge' | 'exchange', subject: string, nonceHash: string, now: number,
    challenge?: QuestChallenge,
  ): Promise<QuestChallenge | null> => {
    const subjectHash = createHash('sha256').update(subject).digest('hex');
    const rateRef = db.collection(QUEST_RATE_COLLECTION).doc(`${operation}-${subjectHash}`);
    const nonceRef = db.collection(QUEST_CHALLENGES_COLLECTION).doc(nonceHash);
    try {
      return await db.runTransaction(async tx => {
        const rate = (await tx.get(rateRef)).data();
        const startedAt = rate && Number.isSafeInteger(rate.startedAt)
          && rate.startedAt <= now && now - rate.startedAt < WINDOW_MS ? rate.startedAt : now;
        const count = rate && startedAt === rate.startedAt ? rate.count : 0;
        if (!Number.isSafeInteger(count) || count < 0) throw questUnavailable();
        if (count >= (operation === 'challenge' ? 10 : 30)) {
          throw createHttpError(429, 'Too many Quest verification requests. Try again later.', 'quest-attestation/rate-limited');
        }
        const stored = operation === 'exchange' ? (await tx.get(nonceRef)).data() : null;
        tx.set(rateRef, { startedAt, count: count + 1, purgeAt: Timestamp.fromMillis(startedAt + 2 * WINDOW_MS) });
        if (challenge) {
          tx.create(nonceRef, { ...challenge, purgeAt: Timestamp.fromMillis(challenge.expiresAt) });
          return challenge;
        }
        // Even missing/expired nonce attempts consume an allowance, committed
        // before the service reports rejection. TTL cleanup is not expiry logic.
        if (!stored) return null;
        tx.delete(nonceRef);
        if (!Number.isSafeInteger(stored.createdAt) || !Number.isSafeInteger(stored.expiresAt)
          || stored.createdAt > now || stored.expiresAt <= now) return null;
        return { createdAt: stored.createdAt, expiresAt: stored.expiresAt };
      });
    } catch (error) {
      if ((error as { status?: unknown })?.status === 429) throw error;
      throw questUnavailable();
    }
  };
  return {
    issue: async (subject, nonceHash, challenge) => { await transact('challenge', subject, nonceHash, challenge.createdAt, challenge); },
    consume: (subject, nonceHash, now) => transact('exchange', subject, nonceHash, now),
  };
};
