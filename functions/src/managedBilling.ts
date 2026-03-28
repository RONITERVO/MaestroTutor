// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import type { AppUser } from './auth';
import { appConfig, getReservationTtlMs } from './config';
import { adminDb } from './firebase';
import { createHttpError } from './http';
import { creditsToUsd } from './pricing';

export interface ManagedBillingSummary {
  availableCredits: number;
  reservedCredits: number;
  lifetimePurchasedCredits: number;
  lifetimeSpentCredits: number;
  lifetimeSpentUsd: number;
  updatedAt: number | null;
  lastPurchaseAt: number | null;
  lastChargeAt: number | null;
  lastProductId: string | null;
}

export interface EntitlementRecord {
  id: string;
  platform: 'google-play';
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

export const EMPTY_BILLING_SUMMARY: ManagedBillingSummary = {
  availableCredits: 0,
  reservedCredits: 0,
  lifetimePurchasedCredits: 0,
  lifetimeSpentCredits: 0,
  lifetimeSpentUsd: 0,
  updatedAt: null,
  lastPurchaseAt: null,
  lastChargeAt: null,
  lastProductId: null,
};

const mergeBillingSummary = (value: unknown): ManagedBillingSummary => ({
  ...EMPTY_BILLING_SUMMARY,
  ...(typeof value === 'object' && value !== null ? value as Partial<ManagedBillingSummary> : {}),
});

const clampLimit = (limit: number | undefined, fallback = 50): number => {
  const parsed = Number(limit);
  if (!Number.isFinite(parsed)) return fallback;
  return Math.max(1, Math.min(200, Math.floor(parsed)));
};

const ensureAccountSummary = async (uid: string, user: AppUser): Promise<ManagedBillingSummary> => {
  const ref = accountSummaryRef(uid);
  const snapshot = await ref.get();
  if (snapshot.exists) {
    const data = snapshot.data() || {};
    const billingSummary = mergeBillingSummary(data.billingSummary);
    await ref.set(
      {
        user,
        billingSummary,
      },
      { merge: true }
    );
    return billingSummary;
  }

  await ref.set({
    user,
    billingSummary: {
      ...EMPTY_BILLING_SUMMARY,
      updatedAt: nowMs(),
    },
  });
  return {
    ...EMPTY_BILLING_SUMMARY,
    updatedAt: nowMs(),
  };
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

  const billingSummary = await adminDb.runTransaction(async (transaction: any) => {
    const summarySnapshot = await transaction.get(summaryRef);
    const currentSummary = mergeBillingSummary(summarySnapshot.data()?.billingSummary);

    if (currentSummary.availableCredits < params.estimatedCredits) {
      throw createHttpError(402, 'Not enough Maestro credits to start this request.');
    }

    const nextSummary: ManagedBillingSummary = {
      ...currentSummary,
      availableCredits: currentSummary.availableCredits - params.estimatedCredits,
      reservedCredits: currentSummary.reservedCredits + params.estimatedCredits,
      updatedAt: currentTime,
    };

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

  return adminDb.runTransaction(async (transaction: any) => {
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

    const nextSummary: ManagedBillingSummary = {
      ...currentSummary,
      availableCredits: currentSummary.availableCredits + reservation.reservedCredits,
      reservedCredits: Math.max(0, currentSummary.reservedCredits - reservation.reservedCredits),
      updatedAt: currentTime,
    };

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

  return adminDb.runTransaction(async (transaction: any) => {
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

    const reservedCredits = Math.max(0, reservation.reservedCredits);
    const creditDelta = reservedCredits - params.billedCredits;
    const nextSummary: ManagedBillingSummary = {
      ...currentSummary,
      availableCredits: currentSummary.availableCredits + creditDelta,
      reservedCredits: Math.max(0, currentSummary.reservedCredits - reservedCredits),
      lifetimeSpentCredits: currentSummary.lifetimeSpentCredits + params.billedCredits,
      lifetimeSpentUsd: Math.max(
        0,
        Math.round((currentSummary.lifetimeSpentUsd + params.billedUsd) * 1_000_000) / 1_000_000
      ),
      updatedAt: currentTime,
      lastChargeAt: currentTime,
    };

    transaction.set(summaryRef, { billingSummary: nextSummary }, { merge: true });
    transaction.set(reservationRef, {
      status: 'settled',
      settledAt: currentTime,
      billedCredits: params.billedCredits,
      billedUsd: params.billedUsd,
      metadata: {
        ...(reservation.metadata || {}),
        ...(params.metadata || {}),
      },
    }, { merge: true });
    transaction.set(usageLedgerRef, {
      operation: params.operation,
      model: params.model,
      billedCredits: params.billedCredits,
      billedUsd: params.billedUsd,
      createdAt: currentTime,
      metadata: {
        reservationId: params.reservationId,
        ...(params.metadata || {}),
      },
    });
    transaction.set(billingLedgerRef, {
      kind: 'charge',
      credits: params.billedCredits,
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

export const chargeManagedCredits = async (params: {
  uid: string;
  user: AppUser;
  operation: string;
  model: string;
  billedCredits: number;
  billedUsd?: number;
  metadata?: Record<string, unknown>;
}): Promise<ManagedBillingSummary> => {
  if (params.billedCredits <= 0) {
    return ensureAccountSummary(params.uid, params.user);
  }

  const billedUsd = typeof params.billedUsd === 'number'
    ? params.billedUsd
    : creditsToUsd(params.billedCredits);
  const summaryRef = accountSummaryRef(params.uid);
  const usageLedgerRef = usageLedgerCollection(params.uid).doc();
  const billingLedgerRef = billingLedgerCollection(params.uid).doc();
  const currentTime = nowMs();

  return adminDb.runTransaction(async (transaction: any) => {
    const summarySnapshot = await transaction.get(summaryRef);
    const currentSummary = mergeBillingSummary(summarySnapshot.data()?.billingSummary);

    if (currentSummary.availableCredits < params.billedCredits) {
      throw createHttpError(402, 'Not enough Maestro credits to continue.');
    }

    const nextSummary: ManagedBillingSummary = {
      ...currentSummary,
      availableCredits: currentSummary.availableCredits - params.billedCredits,
      lifetimeSpentCredits: currentSummary.lifetimeSpentCredits + params.billedCredits,
      lifetimeSpentUsd: Math.round((currentSummary.lifetimeSpentUsd + billedUsd) * 1_000_000) / 1_000_000,
      updatedAt: currentTime,
      lastChargeAt: currentTime,
    };

    transaction.set(summaryRef, {
      user: params.user,
      billingSummary: nextSummary,
    }, { merge: true });
    transaction.set(usageLedgerRef, {
      operation: params.operation,
      model: params.model,
      billedCredits: params.billedCredits,
      billedUsd,
      createdAt: currentTime,
      metadata: params.metadata || {},
    });
    transaction.set(billingLedgerRef, {
      kind: 'charge',
      credits: params.billedCredits,
      usd: billedUsd,
      productId: null,
      createdAt: currentTime,
      metadata: {
        operation: params.operation,
        model: params.model,
        ...(params.metadata || {}),
      },
    });

    return nextSummary;
  });
};

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
  const purchaseRef = adminDb.collection('googlePlayPurchases').doc(params.purchaseToken);
  const summaryRef = accountSummaryRef(params.uid);
  const entitlementRef = entitlementsCollection(params.uid).doc(params.purchaseToken);
  const billingLedgerRef = billingLedgerCollection(params.uid).doc();
  const currentTime = nowMs();

  const transactionResult = await adminDb.runTransaction(async (transaction: any) => {
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

    const nextSummary: ManagedBillingSummary = {
      ...currentSummary,
      availableCredits: currentSummary.availableCredits + params.creditsGranted,
      lifetimePurchasedCredits: currentSummary.lifetimePurchasedCredits + params.creditsGranted,
      updatedAt: currentTime,
      lastPurchaseAt: currentTime,
      lastProductId: params.productId,
    };

    transaction.set(summaryRef, {
      user: params.user,
      billingSummary: nextSummary,
    }, { merge: true });
    transaction.set(purchaseRef, {
      uid: params.uid,
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
      platform: 'google-play',
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
