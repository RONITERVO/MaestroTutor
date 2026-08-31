// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import {
  type BillingSummary,
  EMPTY_BILLING_SUMMARY,
  applyGrant,
  applyRelease,
  applyReservation,
  applySettlement,
  normalizeBillingSummary,
} from './ledger';

const NOW = 1_700_000_000_000;

const withCredits = (available: number, reserved = 0): BillingSummary => ({
  ...EMPTY_BILLING_SUMMARY,
  availableCredits: available,
  reservedCredits: reserved,
});

/** Credits are neither created nor destroyed except by grants and charges. */
const totalHeld = (summary: BillingSummary): number => (
  summary.availableCredits + summary.reservedCredits
);

describe('reserving', () => {
  it('moves credits from available into reserved', () => {
    const result = applyReservation(withCredits(1000), 250, NOW);
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.summary.availableCredits).toBe(750);
    expect(result.summary.reservedCredits).toBe(250);
    expect(totalHeld(result.summary)).toBe(1000);
  });

  it('refuses rather than overdrawing', () => {
    const result = applyReservation(withCredits(100), 250, NOW);
    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.reason).toBe('insufficient-credits');
    expect(result.shortfallCredits).toBe(150);
  });

  it('treats an exact balance as affordable', () => {
    const result = applyReservation(withCredits(250), 250, NOW);
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.summary.availableCredits).toBe(0);
  });
});

describe('releasing', () => {
  it('returns exactly what was held', () => {
    const reserved = applyReservation(withCredits(1000), 300, NOW);
    if (!reserved.ok) throw new Error('reservation should have succeeded');
    const released = applyRelease(reserved.summary, 300, NOW);
    expect(released.availableCredits).toBe(1000);
    expect(released.reservedCredits).toBe(0);
  });

  it('cannot drive reserved below zero when records disagree', () => {
    // A partial write could leave the summary and the reservation inconsistent;
    // releasing must still land on a legal balance.
    const released = applyRelease(withCredits(0, 10), 999, NOW);
    expect(released.reservedCredits).toBe(0);
    expect(released.availableCredits).toBe(999);
  });
});

describe('settling', () => {
  it('refunds the unused part of the reservation', () => {
    const reserved = applyReservation(withCredits(1000), 400, NOW);
    if (!reserved.ok) throw new Error('reservation should have succeeded');

    const settled = applySettlement(
      reserved.summary,
      { reservedCredits: 400, billedCredits: 150, billedUsd: 0.15 },
      NOW,
    );

    expect(settled.chargedCredits).toBe(150);
    expect(settled.shortfallCredits).toBe(0);
    expect(settled.summary.availableCredits).toBe(850);
    expect(settled.summary.reservedCredits).toBe(0);
    expect(settled.summary.lifetimeSpentCredits).toBe(150);
  });

  it('never leaves a negative balance when the cost overruns the reservation', () => {
    // The bug this guards: subtracting the overrun freely drove availableCredits
    // negative, which then silently swallowed the user's next purchase.
    const reserved = applyReservation(withCredits(500), 500, NOW);
    if (!reserved.ok) throw new Error('reservation should have succeeded');

    const settled = applySettlement(
      reserved.summary,
      { reservedCredits: 500, billedCredits: 900, billedUsd: 0.9 },
      NOW,
    );

    expect(settled.summary.availableCredits).toBe(0);
    expect(settled.summary.reservedCredits).toBe(0);
    expect(settled.chargedCredits).toBe(500);
    expect(settled.shortfallCredits).toBe(400);
  });

  it('draws an overrun from the remaining balance before reporting a shortfall', () => {
    const reserved = applyReservation(withCredits(1000), 200, NOW);
    if (!reserved.ok) throw new Error('reservation should have succeeded');

    const settled = applySettlement(
      reserved.summary,
      { reservedCredits: 200, billedCredits: 300, billedUsd: 0.3 },
      NOW,
    );

    expect(settled.chargedCredits).toBe(300);
    expect(settled.shortfallCredits).toBe(0);
    expect(settled.summary.availableCredits).toBe(700);
  });

  it('charges nothing for a request that cost nothing', () => {
    const reserved = applyReservation(withCredits(1000), 100, NOW);
    if (!reserved.ok) throw new Error('reservation should have succeeded');
    const settled = applySettlement(
      reserved.summary,
      { reservedCredits: 100, billedCredits: 0, billedUsd: 0 },
      NOW,
    );
    expect(settled.summary.availableCredits).toBe(1000);
    expect(settled.chargedCredits).toBe(0);
  });
});

describe('granting', () => {
  it('adds to available and to the lifetime total', () => {
    const granted = applyGrant(withCredits(50), { credits: 1000, productId: 'pack' }, NOW);
    expect(granted.availableCredits).toBe(1050);
    expect(granted.lifetimePurchasedCredits).toBe(1000);
    expect(granted.lastProductId).toBe('pack');
  });

  it('lands intact on a balance that had a shortfall', () => {
    // Following the overrun case above: the purchase must arrive in full rather
    // than being eaten by a negative balance.
    const overrun = applySettlement(
      withCredits(0, 500),
      { reservedCredits: 500, billedCredits: 900, billedUsd: 0.9 },
      NOW,
    );
    const granted = applyGrant(overrun.summary, { credits: 1000, productId: 'pack' }, NOW);
    expect(granted.availableCredits).toBe(1000);
  });
});

describe('normalizing stored state', () => {
  it('repairs impossible values rather than propagating them', () => {
    const summary = normalizeBillingSummary({
      availableCredits: -50,
      reservedCredits: Number.NaN,
      lifetimeSpentUsd: -1,
    });
    expect(summary.availableCredits).toBe(0);
    expect(summary.reservedCredits).toBe(0);
    expect(summary.lifetimeSpentUsd).toBe(0);
  });

  it('accepts a missing document as an empty balance', () => {
    expect(normalizeBillingSummary(undefined)).toEqual(EMPTY_BILLING_SUMMARY);
  });
});

describe('a full request lifecycle conserves credits', () => {
  it('leaves the balance reduced by exactly what was charged', () => {
    const start = withCredits(5000);
    const reserved = applyReservation(start, 800, NOW);
    if (!reserved.ok) throw new Error('reservation should have succeeded');
    const settled = applySettlement(
      reserved.summary,
      { reservedCredits: 800, billedCredits: 321, billedUsd: 0.321 },
      NOW,
    );
    expect(totalHeld(settled.summary)).toBe(totalHeld(start) - settled.chargedCredits);
    expect(settled.summary.reservedCredits).toBe(0);
  });

  it('leaves nothing reserved when a request is abandoned', () => {
    const start = withCredits(5000);
    const reserved = applyReservation(start, 800, NOW);
    if (!reserved.ok) throw new Error('reservation should have succeeded');
    const released = applyRelease(reserved.summary, 800, NOW);
    expect(released).toMatchObject({ availableCredits: 5000, reservedCredits: 0 });
  });
});
