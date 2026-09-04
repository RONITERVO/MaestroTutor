// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import type { AppUser } from './auth';
import { FieldPath, type CollectionReference } from 'firebase-admin/firestore';
import { getReservationTtlMs } from './config';
import { adminDb } from './firebase';
import { createHttpError } from './http';
import {
  MANAGED_OPERATIONAL_RETENTION_MS,
  MANAGED_SCHEMA_VERSION,
  accountDeletionClaimRef,
  ensureManagedUserDocument,
  managedAccountRef,
  managedBillingEventsCollection,
  managedEntitlementsCollection,
  managedReservationRef,
  managedReservationsCollection,
  managedUsageEventsCollection,
  purchaseClaimId,
  purchaseClaimsCollection,
  timestampFromMillis,
} from './managedData';
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

/** New grants are Stripe-only; Google Play may appear only in legacy records. */
export type PurchasePlatform = 'stripe' | 'google-play';

export interface EntitlementRecord {
  id: string;
  platform: PurchasePlatform;
  productId: string;
  creditsGranted: number;
  /** Store credentials never leave the backend. */
  purchaseToken: null;
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

const clampLimit = (limit: number | undefined, fallback = 50): number => {
  const parsed = Number(limit);
  if (!Number.isFinite(parsed)) return fallback;
  return Math.max(1, Math.min(200, Math.floor(parsed)));
};

const ensureAccountSummary = async (uid: string): Promise<ManagedBillingSummary> => {
  await ensureManagedUserDocument(uid);
  const ref = managedAccountRef(uid);
  return adminDb.runTransaction(async (transaction) => {
    const snapshot = await transaction.get(ref);
    const data = snapshot.data() || {};
    const billingSummary = snapshot.exists
      ? mergeBillingSummary(data.billingSummary)
      : { ...EMPTY_BILLING_SUMMARY, updatedAt: nowMs() };

    if (
      !snapshot.exists
      || !billingSummaryMatches(data.billingSummary, billingSummary)
    ) {
      transaction.set(ref, {
        billingSummary,
        schemaVersion: MANAGED_SCHEMA_VERSION,
        updatedAt: billingSummary.updatedAt,
      }, { merge: true });
    }

    return billingSummary;
  });
};

const listEntitlements = async (uid: string): Promise<EntitlementRecord[]> => {
  const snapshot = await managedEntitlementsCollection(uid).orderBy('createdAt', 'desc').limit(100).get();
  return snapshot.docs.map((doc: any) => doc.data() as EntitlementRecord);
};

export const sweepExpiredReservationsForUser = async (uid: string): Promise<void> => {
  const snapshot = await managedReservationsCollection(uid)
    .where('status', '==', 'active')
    .where('expiresAt', '<=', nowMs())
    .limit(25)
    .get();

  for (const doc of snapshot.docs) {
    await releaseManagedReservation(uid, doc.id, 'expired');
  }
};

export const sweepExpiredReservations = async (limit = 50): Promise<number> => {
  const snapshot = await adminDb.collectionGroup('reservations')
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
  const snapshot = await adminDb.collectionGroup('reservations')
    .where('status', '==', 'active')
    .where('expiresAt', '<=', nowMs())
    .count()
    .get();
  return snapshot.data().count;
};

export const getManagedAccountState = async (uid: string, user: AppUser) => {
  await sweepExpiredReservationsForUser(uid);
  const billingSummary = await ensureAccountSummary(uid);
  const entitlements = await listEntitlements(uid);
  return { user, billingSummary, entitlements };
};

const ledgerPage = async (collection: CollectionReference, limit?: number, after?: string) => {
  const pageSize = clampLimit(limit);
  let query = collection.orderBy('createdAt', 'desc').orderBy(FieldPath.documentId(), 'desc');
  if (after !== undefined) {
    if (!/^[A-Za-z0-9_-]{1,128}$/.test(after)) throw createHttpError(400, 'Invalid ledger cursor.');
    // Resolve within this authenticated user's collection, never a client-supplied path.
    const cursor = await collection.doc(after).get();
    if (!cursor.exists) throw createHttpError(400, 'Ledger cursor no longer exists. Refresh the ledger.');
    query = query.startAfter(cursor);
  }
  const snapshot = await query.limit(pageSize + 1).get();
  const docs = snapshot.docs.slice(0, pageSize);
  return {
    entries: docs.map(doc => ({ ...doc.data(), id: doc.id })),
    nextCursor: snapshot.size > pageSize ? docs[docs.length - 1].id : null,
  };
};

export const listManagedUsageLedgerPage = (uid: string, limit?: number, after?: string) => (
  ledgerPage(managedUsageEventsCollection(uid), limit, after)
);
export const listManagedBillingLedgerPage = (uid: string, limit?: number, after?: string) => (
  ledgerPage(managedBillingEventsCollection(uid), limit, after)
);
export const listManagedUsageLedger = async (uid: string, limit?: number) => (
  (await listManagedUsageLedgerPage(uid, limit)).entries
);
export const listManagedBillingLedger = async (uid: string, limit?: number) => (
  (await listManagedBillingLedgerPage(uid, limit)).entries
);

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
    const summary = await ensureAccountSummary(params.uid);
    return { reservationId: '', billingSummary: summary };
  }

  await ensureManagedUserDocument(params.uid);
  const summaryRef = managedAccountRef(params.uid);
  const reservationRef = managedReservationsCollection(params.uid).doc();
  const currentTime = nowMs();
  const expiresAt = currentTime + getReservationTtlMs();

  const billingSummary = await adminDb.runTransaction(async (transaction) => {
    const [summarySnapshot, deletionClaim] = await Promise.all([
      transaction.get(summaryRef),
      transaction.get(accountDeletionClaimRef(params.uid)),
    ]);
    if (deletionClaim.exists) {
      throw createHttpError(409, 'This managed account is being deleted.');
    }
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
      billingSummary: nextSummary,
      schemaVersion: MANAGED_SCHEMA_VERSION,
      updatedAt: currentTime,
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
  const summaryRef = managedAccountRef(uid);
  const reservationRef = managedReservationRef(uid, reservationId);
  const billingLedgerRef = managedBillingEventsCollection(uid).doc();
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

    transaction.set(summaryRef, { billingSummary: nextSummary, updatedAt: currentTime }, { merge: true });
    transaction.set(reservationRef, {
      status: 'released',
      releasedAt: currentTime,
      purgeAt: timestampFromMillis(currentTime + MANAGED_OPERATIONAL_RETENTION_MS),
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
        ...(reservation.metadata || {}),
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

  const summaryRef = managedAccountRef(params.uid);
  const reservationRef = managedReservationRef(params.uid, params.reservationId);
  const usageLedgerRef = managedUsageEventsCollection(params.uid).doc();
  const billingLedgerRef = managedBillingEventsCollection(params.uid).doc();
  const currentTime = nowMs();

  return adminDb.runTransaction(async (transaction) => {
    const [summarySnapshot, reservationSnapshot, deletionClaim] = await Promise.all([
      transaction.get(summaryRef),
      transaction.get(reservationRef),
      transaction.get(accountDeletionClaimRef(params.uid)),
    ]);

    // A provider response can race account deletion. Never recreate or charge
    // a managed account after deletion has claimed the UID; the deletion flow
    // releases every active reservation itself.
    if (deletionClaim.exists) {
      throw createHttpError(409, 'This managed account is being deleted.');
    }

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

    transaction.set(summaryRef, { billingSummary: nextSummary, updatedAt: currentTime }, { merge: true });
    transaction.set(reservationRef, {
      status: 'settled',
      settledAt: currentTime,
      billedCredits: params.billedCredits,
      chargedCredits: settlement.chargedCredits,
      shortfallCredits: settlement.shortfallCredits,
      billedUsd: params.billedUsd,
      purgeAt: timestampFromMillis(currentTime + MANAGED_OPERATIONAL_RETENTION_MS),
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
 * `purchaseToken` is the stable provider-scoped Stripe Checkout idempotency key.
 * It is SHA-256 hashed, so the external identifier never appears in a document
 * path or a client-visible entitlement.
 */
export const grantPurchasedCredits = async (params: {
  uid: string;
  user: AppUser;
  purchaseToken: string;
  productId: string;
  orderId: string | null;
  creditsGranted: number;
  rawPurchase: Record<string, unknown>;
  rawVerification: Record<string, unknown>;
}): Promise<{ alreadyProcessed: boolean; grantedCredits: number; billingSummary: ManagedBillingSummary }> => {
  const platform: PurchasePlatform = 'stripe';
  await ensureManagedUserDocument(params.uid);
  const claimId = purchaseClaimId(platform, params.purchaseToken);
  const purchaseRef = purchaseClaimsCollection().doc(claimId);
  const summaryRef = managedAccountRef(params.uid);
  const entitlementRef = managedEntitlementsCollection(params.uid).doc(claimId);
  const billingLedgerRef = managedBillingEventsCollection(params.uid).doc();
  const currentTime = nowMs();

  const transactionResult = await adminDb.runTransaction(async (transaction) => {
    const [purchaseSnapshot, summarySnapshot, deletionClaim] = await Promise.all([
      transaction.get(purchaseRef),
      transaction.get(summaryRef),
      transaction.get(accountDeletionClaimRef(params.uid)),
    ]);

    if (deletionClaim.exists) {
      throw createHttpError(409, 'This managed account is being deleted.');
    }

    const currentSummary = mergeBillingSummary(summarySnapshot.data()?.billingSummary);
    if (purchaseSnapshot.exists) {
      const existing = purchaseSnapshot.data() as { uid?: string };
      if (existing.uid && existing.uid !== params.uid) {
        throw createHttpError(409, 'This purchase is already linked to another account.');
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
      billingSummary: nextSummary,
      schemaVersion: MANAGED_SCHEMA_VERSION,
      updatedAt: currentTime,
    }, { merge: true });
    transaction.set(purchaseRef, {
      uid: params.uid,
      platform,
      externalIdHash: claimId,
      productId: params.productId,
      orderId: params.orderId,
      creditsGranted: params.creditsGranted,
      createdAt: currentTime,
      rawPurchase: params.rawPurchase,
      rawVerification: params.rawVerification,
    });
    transaction.set(entitlementRef, {
      id: claimId,
      platform,
      productId: params.productId,
      creditsGranted: params.creditsGranted,
      purchaseToken: null,
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
        purchaseClaimId: claimId,
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
