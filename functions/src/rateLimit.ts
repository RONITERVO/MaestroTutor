// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Per-user request throttling.
 *
 * Credits bound what a user can *spend*, but not how fast they can ask. Those
 * are different failure modes: a client stuck in a retry loop burns no credits
 * on requests that fail before reservation, yet still costs function
 * invocations, Firestore reads and Play API quota, and does so at whatever rate
 * the loop spins. The draft had exactly such a loop in the live reconnect path,
 * with no ceiling on either side.
 *
 * A fixed window in Firestore is deliberate over an in-memory counter: Cloud
 * Functions scale horizontally, so an in-memory limit is per-instance and a
 * caller simply lands on a fresh instance. The cost is one small transaction
 * per request, which is the point at which the limit becomes real.
 */

import { createHash } from 'node:crypto';
import { adminDb } from './firebase';
import { createHttpError } from './http';
import {
  type ManagedRateLimitBucket,
  rateLimitWindowId,
  rateLimitWindowsCollection,
  timestampFromMillis,
} from './managedData';

const WINDOW_MS = 60_000;

interface RateWindow {
  windowStartedAt: number;
  count: number;
  purgeAt?: FirebaseFirestore.Timestamp;
}

/**
 * Consume one unit against `bucket` for `uid`.
 *
 * Throws 429 when the window is exhausted. Fails open on infrastructure errors:
 * a throttle that takes the service down when Firestore hiccups is worse than
 * one that briefly lets traffic through, and every path behind this still has
 * to reserve credits before it can spend anything.
 */
export const consumeRateLimit = async (params: {
  uid: string;
  bucket: ManagedRateLimitBucket;
  limitPerMinute: number;
}): Promise<void> => {
  const subjectHash = createHash('sha256').update(params.uid).digest('hex');
  const ref = rateLimitWindowsCollection()
    .doc(rateLimitWindowId(params.uid, params.bucket));
  const now = Date.now();

  try {
    await adminDb.runTransaction(async (transaction: any) => {
      const snapshot = await transaction.get(ref);
      const existing = snapshot.exists ? snapshot.data() as RateWindow : null;

      const withinWindow = existing && now - existing.windowStartedAt < WINDOW_MS;
      const next: RateWindow = withinWindow
        ? {
          windowStartedAt: existing!.windowStartedAt,
          count: existing!.count + 1,
          purgeAt: timestampFromMillis(existing!.windowStartedAt + (2 * WINDOW_MS)),
        }
        : {
          windowStartedAt: now,
          count: 1,
          purgeAt: timestampFromMillis(now + (2 * WINDOW_MS)),
        };

      if (next.count > params.limitPerMinute) {
        const retryAfterSeconds = Math.max(
          1,
          Math.ceil((next.windowStartedAt + WINDOW_MS - now) / 1000),
        );
        throw createHttpError(
          429,
          `Too many requests. Try again in ${retryAfterSeconds}s.`,
        );
      }

      transaction.set(ref, { ...next, subjectHash, bucket: params.bucket });
    });
  } catch (error) {
    if ((error as { status?: number })?.status === 429) throw error;
    console.error('[rateLimit] Throttle check failed; allowing the request.', error);
  }
};
