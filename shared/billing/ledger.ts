// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * The arithmetic of a prepaid credit balance.
 *
 * Pure on purpose. Firestore transactions are where this gets *applied*, but
 * the rules about what a balance may do belong somewhere they can be exhausted
 * by tests: a balance that can go negative, or a reservation that can be
 * settled twice, is money quietly created or destroyed, and neither is visible
 * from reading a transaction body.
 *
 * The invariants, in one place:
 *   - available and reserved are never negative
 *   - reserving moves credits from available to reserved, never below zero
 *   - releasing returns exactly what was reserved
 *   - settling charges at most what the account can actually pay, and reports
 *     any shortfall rather than silently overdrawing
 */

export interface BillingSummary {
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

export const EMPTY_BILLING_SUMMARY: BillingSummary = {
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

const nonNegative = (value: number): number => (
  Number.isFinite(value) && value > 0 ? value : 0
);

const roundUsd = (value: number): number => (
  Number.isFinite(value) ? Math.max(0, Math.round(value * 1_000_000) / 1_000_000) : 0
);

/** Coerce whatever is in storage into a summary that satisfies the invariants. */
export const normalizeBillingSummary = (value: unknown): BillingSummary => {
  const raw = (typeof value === 'object' && value !== null ? value : {}) as Partial<BillingSummary>;
  return {
    ...EMPTY_BILLING_SUMMARY,
    ...raw,
    availableCredits: nonNegative(Number(raw.availableCredits)),
    reservedCredits: nonNegative(Number(raw.reservedCredits)),
    lifetimePurchasedCredits: nonNegative(Number(raw.lifetimePurchasedCredits)),
    lifetimeSpentCredits: nonNegative(Number(raw.lifetimeSpentCredits)),
    lifetimeSpentUsd: roundUsd(Number(raw.lifetimeSpentUsd)),
  };
};

export type ReservationOutcome =
  | { ok: true; summary: BillingSummary }
  | { ok: false; reason: 'insufficient-credits'; shortfallCredits: number };

/** Move credits from available into reserved, or refuse. */
export const applyReservation = (
  summary: BillingSummary,
  credits: number,
  now: number,
): ReservationOutcome => {
  const wanted = Math.max(0, Math.ceil(credits));
  if (wanted === 0) return { ok: true, summary };

  if (summary.availableCredits < wanted) {
    return {
      ok: false,
      reason: 'insufficient-credits',
      shortfallCredits: wanted - summary.availableCredits,
    };
  }

  return {
    ok: true,
    summary: {
      ...summary,
      availableCredits: summary.availableCredits - wanted,
      reservedCredits: summary.reservedCredits + wanted,
      updatedAt: now,
    },
  };
};

/** Hand an untouched reservation back. */
export const applyRelease = (
  summary: BillingSummary,
  reservedCredits: number,
  now: number,
): BillingSummary => {
  const held = Math.max(0, reservedCredits);
  return {
    ...summary,
    availableCredits: summary.availableCredits + held,
    // Clamped because a release must never drive reserved below zero, even if
    // the stored reservation disagrees with the summary after a partial write.
    reservedCredits: Math.max(0, summary.reservedCredits - held),
    updatedAt: now,
  };
};

export interface SettlementResult {
  summary: BillingSummary;
  /** Credits the request cost beyond what the account could cover. */
  shortfallCredits: number;
  /** What was actually charged. */
  chargedCredits: number;
}

/**
 * Close out a reservation against what the request really cost.
 *
 * Under-spend returns the difference. Over-spend takes what it can from the
 * remaining balance and reports the rest as a shortfall: the alternative,
 * subtracting freely, lets `availableCredits` go negative, which then silently
 * swallows the user's next purchase. Over-spend should be rare — reservations
 * are deliberately generous — but "rare" is not "never", and the balance is the
 * wrong place to absorb an estimate that was too small.
 */
export const applySettlement = (
  summary: BillingSummary,
  params: { reservedCredits: number; billedCredits: number; billedUsd: number },
  now: number,
): SettlementResult => {
  const held = Math.max(0, params.reservedCredits);
  const billed = Math.max(0, Math.ceil(params.billedCredits));

  const releasedToAvailable = summary.availableCredits + held;
  const chargedCredits = Math.min(billed, releasedToAvailable);
  const shortfallCredits = billed - chargedCredits;

  return {
    summary: {
      ...summary,
      availableCredits: releasedToAvailable - chargedCredits,
      reservedCredits: Math.max(0, summary.reservedCredits - held),
      lifetimeSpentCredits: summary.lifetimeSpentCredits + chargedCredits,
      lifetimeSpentUsd: roundUsd(summary.lifetimeSpentUsd + Math.max(0, params.billedUsd)),
      updatedAt: now,
      lastChargeAt: now,
    },
    shortfallCredits,
    chargedCredits,
  };
};

/** Add purchased credits. Callers must have established the grant is not a replay. */
export const applyGrant = (
  summary: BillingSummary,
  params: { credits: number; productId: string },
  now: number,
): BillingSummary => {
  const granted = Math.max(0, Math.floor(params.credits));
  return {
    ...summary,
    availableCredits: summary.availableCredits + granted,
    lifetimePurchasedCredits: summary.lifetimePurchasedCredits + granted,
    updatedAt: now,
    lastPurchaseAt: now,
    lastProductId: params.productId,
  };
};
