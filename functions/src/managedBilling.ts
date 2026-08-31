// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import type { AppUser } from './auth';
import { appConfig, getReservationTtlMs } from './config';
import { adminDb } from './firebase';
import { createHttpError } from './http';
import {
  type BillingSummary,
  EMPTY_BILLING_SUMMARY as SHARED_EMPTY_SUMMARY,
  applyGrant,
  applyRelease,
  applyReservation,
  applySettlement,
  normalizeBillingSummary,
} from '../../shared/billing/ledger';

/**
 * Re-exported from the shared ledger so the balance shape and the rules that
 * govern it cannot drift apart. The arithmetic lives in
 * shared/billing/ledger.ts, where it is exhaustively tested; this file is only
 * responsible for persisting the result.
 */
export type ManagedBillingSummary = BillingSummary;

/** Where a grant came from. Both storefronts fund the same credit balance. */
export type PurchasePlatform = 'google-play' | 'stripe';

export interface EntitlementRecord {
  id: string;
  platform: PurchasePlatform;
  productId: string;
  creditsGranted: number;
  purchaseToken: string | null;
  orderId: string | null;
  createdAt: number;
}

interface ReservationRecord {
  uid: string;
  status: 'active' | 'settled' | 'released';
  operation: string;
  model: string;
  reservedCredits: number;
  reservedUsd: number;
  createdAt: number;
  expiresAt: number;
  settledAt?: number;
  releasedAt?: number;
  billedCredits?: number;
  billedUsd?: number;
  metadata?: Record<string, unknown>;
}

const nowMs = (): number => Date.now();

const userDoc = (uid: string) => adminDb.collection('users').doc(uid);
const accountSummaryRef = (uid: string) => userDoc(uid).collection('account').doc('summary');
const entitlementsCollection = (uid: string) => userDoc(uid).collection('entitlements');
const billingLedgerCollection = (uid: string) => userDoc(uid).collection('billingLedger');
const usageLedgerCollection = (uid: string) => userDoc(uid).collection('usageLedger');
const reservationsCollection = () => adminDb.collection('managedReservations');

export const EMPTY_BILLING_SUMMARY: ManagedBillingSummary = SHARED_EMPTY_SUMMARY;

const mergeBillingSummary = (value: unknown): ManagedBillingSummary => (
  normalizeBillingSummary(value)
);

const billingSummaryMatches = (value: unknown, expected: ManagedBillingSummary): boolean => {
  if (!value || typeof value !== 'object') return false;
  const current = value as Partial<ManagedBillingSummary>;
  return (Object.keys(EMPTY_BILLING_SUMMARY) as Array<keyof ManagedBillingSummary>)
    .every((key) => current[key] === expected[key]);
};

const userMatches = (value: unknown, expected: AppUser): boolean => {
  if (!value || typeof value !== 'object') return false;
  const current = value as Partial<AppUser>;
  return current.id === expected.id
    && current.email === expected.email
    && current.displayName === expected.displayName
    && current.photoUrl === expected.photoUrl;
};

const clampLimit = (limit: number | undefined, fallback = 50): number => {
  const parsed = Number(limit);
  if (!Number.isFinite(parsed)) return fallback;
  return Math.max(1, Math.min(200, Math.floor(parsed)));
};

const ensureAccountSummary = async (uid: string, user: AppUser): Promise<ManagedBillingSummary> => {
  const ref = accountSummaryRef(uid);
  return adminDb.runTransaction(async (transaction) => {
    const snapshot = await transaction.get(ref);
    const data = snapshot.data() || {};
    const billingSummary = snapshot.exists
      ? mergeBillingSummary(data.billingSummary)
      : { ...EMPTY_BILLING_SUMMARY, updatedAt: nowMs() };

    if (
      !snapshot.exists
      || !billingSummaryMatches(data.billingSummary, billingSummary)
      || !userMatches(data.user, user)
    ) {
      transaction.set(ref, { user, billingSummary }, { merge: true });
    }

    return billingSummary;
  });
};

const listEntitlements = async (uid: string): Promise<EntitlementRecord[]> => {
  const snapshot = await entitlementsCollection(uid).orderBy('createdAt', 'desc').limit(100).get();
  return snapshot.docs.map((doc: any) => doc.data() as EntitlementRecord);
};

export const sweepExpiredReservationsForUser = async (uid: string): Promise<void> => {
  const snapshot = await reservationsCollection()
    .where('uid', '==', uid)
    .where('status', '==', 'active')
    .where('expiresAt', '<=', nowMs())
    .limit(25)
    .get();

  for (const doc of snapshot.docs) {
    await releaseManagedReservation(uid, doc.id, 'expired');
  }
};

export const sweepExpiredReservations = async (limit = 50): Promise<number> => {
  const snapshot = await reservationsCollection()
    .where('status', '==', 'active')
    .where('expiresAt', '<=', nowMs())
    .limit(clampLimit(limit, 50))
    .get();

  for (const doc of snapshot.docs) {
    const reservation = doc.data() as ReservationRecord;
    await releaseManagedReservation(reservation.uid, doc.id, 'expired');
  }

  return snapshot.size;
};

export const countExpiredReservations = async (): Promise<number> => {
  const snapshot = await reservationsCollection()
    .where('status', '==', 'active')
    .where('expiresAt', '<=', nowMs())
    .count()
    .get();
  return snapshot.data().count;
};

export const getManagedAccountState = async (uid: string, user: AppUser) => {
  await sweepExpiredReservationsForUser(uid);
  const billingSummary = await ensureAccountSummary(uid, user);
  const entitlements = await listEntitlements(uid);
  return { user, billingSummary, entitlements };
};

export const listManagedUsageLedger = async (uid: string, limit?: number) => {
  const snapshot = await usageLedgerCollection(uid)
    .orderBy('createdAt', 'desc')
    .limit(clampLimit(limit))
    .get();
  return snapshot.docs.map((doc: any) => ({ id: doc.id, ...doc.data() }));
};

export const listManagedBillingLedger = async (uid: string, limit?: number) => {
  const snapshot = await billingLedgerCollection(uid)
    .orderBy('createdAt', 'desc')
    .limit(clampLimit(limit))
    .get();
  return snapshot.docs.map((doc: any) => ({ id: doc.id, ...doc.data() }));
};

export const reserveManagedCredits = async (params: {
  uid: string;
  user: AppUser;
  operation: string;
  model: string;
  estimatedCredits: number;
  estimatedUsd: number;
  metadata?: Record<string, unknown>;
}): Promise<{ reservationId: string; billingSummary: ManagedBillingSummary }> => {
  if (params.estimatedCredits <= 0) {
    const summary = await ensureAccountSummary(params.uid, params.user);
    return { reservationId: '', billingSummary: summary };
  }

  const summaryRef = accountSummaryRef(params.uid);
  const reservationRef = reservationsCollection().doc();
  const currentTime = nowMs();
  const expiresAt = currentTime + getReservationTtlMs();

  const billingSummary = await adminDb.runTransaction(async (transaction) => {
    const summarySnapshot = await transaction.get(summaryRef);
    const currentSummary = mergeBillingSummary(summarySnapshot.data()?.billingSummary);

    const outcome = applyReservation(currentSummary, params.estimatedCredits, currentTime);
    if (!outcome.ok) {
      throw createHttpError(
        402,
        `Not enough Maestro credits to start this request. ${outcome.shortfallCredits} more needed.`
      );
    }
    const nextSummary = outcome.summary;

    transaction.set(summaryRef, {
      user: params.user,
      billingSummary: nextSummary,
    }, { merge: true });

    transaction.set(reservationRef, {
      uid: params.uid,
      status: 'active',
      operation: params.operation,
      model: params.model,
      reservedCredits: params.estimatedCredits,
      reservedUsd: params.estimatedUsd,
      createdAt: currentTime,
      expiresAt,
      metadata: params.metadata || {},
    } satisfies ReservationRecord);

    return nextSummary;
  });

  return { reservationId: reservationRef.id, billingSummary };
};

export const releaseManagedReservation = async (
  uid: string,
  reservationId: string,
  reason: string
): Promise<ManagedBillingSummary> => {
  const summaryRef = accountSummaryRef(uid);
  const reservationRef = reservationsCollection().doc(reservationId);
  const billingLedgerRef = billingLedgerCollection(uid).doc();
  const currentTime = nowMs();

  return adminDb.runTransaction(async (transaction) => {
    const [summarySnapshot, reservationSnapshot] = await Promise.all([
      transaction.get(summaryRef),
      transaction.get(reservationRef),
    ]);

    const currentSummary = mergeBillingSummary(summarySnapshot.data()?.billingSummary);
    if (!reservationSnapshot.exists) {
      return currentSummary;
    }

    const reservation = reservationSnapshot.data() as ReservationRecord;
    if (reservation.uid !== uid || reservation.status !== 'active') {
      return currentSummary;
    }

    const nextSummary = applyRelease(currentSummary, reservation.reservedCredits, currentTime);

    transaction.set(summaryRef, { billingSummary: nextSummary }, { merge: true });
    transaction.set(reservationRef, {
      status: 'released',
      releasedAt: currentTime,
      metadata: {
        ...(reservation.metadata || {}),
        releaseReason: reason,
      },
    }, { merge: true });
    transaction.set(billingLedgerRef, {
      kind: 'reservation-release',
      credits: reservation.reservedCredits,
      usd: reservation.reservedUsd,
      productId: null,
      createdAt: currentTime,
      metadata: {
        reservationId,
        operation: reservation.operation,
        reason,
      },
    });

    return nextSummary;
  });
};

export const settleManagedReservation = async (params: {
  uid: string;
  reservationId: string;
  billedCredits: number;
  billedUsd: number;
  operation: string;
  model: string;
  metadata?: Record<string, unknown>;
}): Promise<ManagedBillingSummary> => {
  if (!params.reservationId) {
    throw createHttpError(500, 'Missing managed reservation id.');
  }

  const summaryRef = accountSummaryRef(params.uid);
  const reservationRef = reservationsCollection().doc(params.reservationId);
  const usageLedgerRef = usageLedgerCollection(params.uid).doc();
  const billingLedgerRef = billingLedgerCollection(params.uid).doc();
  const currentTime = nowMs();

  return adminDb.runTransaction(async (transaction) => {
    const [summarySnapshot, reservationSnapshot] = await Promise.all([
      transaction.get(summaryRef),
      transaction.get(reservationRef),
    ]);

    const currentSummary = mergeBillingSummary(summarySnapshot.data()?.billingSummary);
    if (!reservationSnapshot.exists) {
      return currentSummary;
    }

    const reservation = reservationSnapshot.data() as ReservationRecord;
    if (reservation.uid !== params.uid || reservation.status !== 'active') {
      return currentSummary;
    }

    const settlement = applySettlement(currentSummary, {
      reservedCredits: reservation.reservedCredits,
      billedCredits: params.billedCredits,
      billedUsd: params.billedUsd,
    }, currentTime);
    const nextSummary = settlement.summary;

    if (settlement.shortfallCredits > 0) {
      // The estimate did not cover the real cost and the balance could not
      // absorb the rest. Charging beyond the balance would leave it negative,
      // which silently eats the user's next purchase, so the remainder is
      // recorded instead of taken.
      console.warn(
        `[billing] Reservation ${params.reservationId} overran by `
        + `${settlement.shortfallCredits} credits (${params.operation}, ${params.model}). `
        + 'Review the reservation estimate for this operation.'
      );
    }

    transaction.set(summaryRef, { billingSummary: nextSummary }, { merge: true });
    transaction.set(reservationRef, {
      status: 'settled',
      settledAt: currentTime,
      billedCredits: params.billedCredits,
      chargedCredits: settlement.chargedCredits,
      shortfallCredits: settlement.shortfallCredits,
      billedUsd: params.billedUsd,
      metadata: {
        ...(reservation.metadata || {}),
        ...(params.metadata || {}),
      },
    }, { merge: true });
    transaction.set(usageLedgerRef, {
      operation: params.operation,
      model: params.model,
      // What the request cost and what the account could pay are recorded
      // separately: on the rare overrun they differ, and a ledger that reported
      // only the cost would not reconcile against the balance.
      billedCredits: params.billedCredits,
      chargedCredits: settlement.chargedCredits,
      shortfallCredits: settlement.shortfallCredits,
      billedUsd: params.billedUsd,
      createdAt: currentTime,
      metadata: {
        reservationId: params.reservationId,
        ...(params.metadata || {}),
      },
    });
    transaction.set(billingLedgerRef, {
      kind: 'charge',
      // The balance moved by exactly this much.
      credits: settlement.chargedCredits,
      billedCredits: params.billedCredits,
      shortfallCredits: settlement.shortfallCredits,
      usd: params.billedUsd,
      productId: null,
      createdAt: currentTime,
      metadata: {
        reservationId: params.reservationId,
        operation: params.operation,
        model: params.model,
        ...(params.metadata || {}),
      },
    });

    return nextSummary;
  });
};

/**
 * Add purchased credits, exactly once.
 *
 * `purchaseToken` is whatever uniquely identifies the purchase in its
 * storefront — a Play purchase token, or a Stripe checkout session id. It is
 * the idempotency key: the dedupe document is read inside the transaction, so
 * a replayed webhook or a retried verification is a no-op rather than free
 * credits. Both storefronts share the collection so a key can never collide
 * across providers unnoticed.
 */
export const grantPurchasedCredits = async (params: {
  uid: string;
  user: AppUser;
  purchaseToken: string;
  productId: string;
  orderId: string | null;
  creditsGranted: number;
  platform?: PurchasePlatform;
  rawPurchase: Record<string, unknown>;
  rawVerification: Record<string, unknown>;
}): Promise<{ alreadyProcessed: boolean; grantedCredits: number; billingSummary: ManagedBillingSummary }> => {
  const platform: PurchasePlatform = params.platform || 'google-play';
  const purchaseRef = adminDb.collection('processedPurchases').doc(params.purchaseToken);
  const summaryRef = accountSummaryRef(params.uid);
  const entitlementRef = entitlementsCollection(params.uid).doc(params.purchaseToken);
  const billingLedgerRef = billingLedgerCollection(params.uid).doc();
  const currentTime = nowMs();

  const transactionResult = await adminDb.runTransaction(async (transaction) => {
    const [purchaseSnapshot, summarySnapshot] = await Promise.all([
      transaction.get(purchaseRef),
      transaction.get(summaryRef),
    ]);

    const currentSummary = mergeBillingSummary(summarySnapshot.data()?.billingSummary);
    if (purchaseSnapshot.exists) {
      const existing = purchaseSnapshot.data() as { uid?: string };
      if (existing.uid && existing.uid !== params.uid) {
        throw createHttpError(409, 'This Google Play purchase token is already linked to another account.');
      }
      return {
        alreadyProcessed: true,
        grantedCredits: 0,
        billingSummary: currentSummary,
      };
    }

    const nextSummary = applyGrant(currentSummary, {
      credits: params.creditsGranted,
      productId: params.productId,
    }, currentTime);

    transaction.set(summaryRef, {
      user: params.user,
      billingSummary: nextSummary,
    }, { merge: true });
    transaction.set(purchaseRef, {
      uid: params.uid,
      platform,
      productId: params.productId,
      purchaseToken: params.purchaseToken,
      orderId: params.orderId,
      creditsGranted: params.creditsGranted,
      packageName: appConfig.googlePlayPackageName,
      createdAt: currentTime,
      rawPurchase: params.rawPurchase,
      rawVerification: params.rawVerification,
    });
    transaction.set(entitlementRef, {
      id: params.purchaseToken,
      platform,
      productId: params.productId,
      creditsGranted: params.creditsGranted,
      purchaseToken: params.purchaseToken,
      orderId: params.orderId,
      createdAt: currentTime,
    } satisfies EntitlementRecord);
    transaction.set(billingLedgerRef, {
      kind: 'purchase',
      credits: params.creditsGranted,
      usd: 0,
      productId: params.productId,
      createdAt: currentTime,
      metadata: {
        purchaseToken: params.purchaseToken,
        orderId: params.orderId,
      },
    });

    return {
      alreadyProcessed: false,
      grantedCredits: params.creditsGranted,
      billingSummary: nextSummary,
    };
  });

  return transactionResult;
};
