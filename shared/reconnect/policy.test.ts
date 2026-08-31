// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { DEFAULT_RECONNECT_POLICY, ReconnectPolicy } from './policy';

/**
 * The property under test is a spending guarantee, not a timing preference:
 * each reconnect mints a paid token, so the number of attempts must be finite
 * and the interval between them must grow.
 */

const noJitter = () => 0;

describe('ReconnectPolicy', () => {
  it('gives up rather than retrying forever', () => {
    const policy = new ReconnectPolicy({ random: noJitter });
    const delays: Array<number | null> = [];
    for (let i = 0; i < DEFAULT_RECONNECT_POLICY.maxAttempts + 3; i += 1) {
      delays.push(policy.nextDelayMs());
    }
    const attempted = delays.filter((d) => d !== null);
    expect(attempted).toHaveLength(DEFAULT_RECONNECT_POLICY.maxAttempts);
    expect(policy.nextDelayMs()).toBeNull();
    expect(policy.exhausted).toBe(true);
  });

  it('backs off exponentially instead of hammering a flat interval', () => {
    const policy = new ReconnectPolicy({ baseDelayMs: 500, random: noJitter });
    expect(policy.nextDelayMs()).toBe(500);
    expect(policy.nextDelayMs()).toBe(1000);
    expect(policy.nextDelayMs()).toBe(2000);
    expect(policy.nextDelayMs()).toBe(4000);
  });

  it('caps the delay so a long outage does not push retries hours out', () => {
    const policy = new ReconnectPolicy({
      baseDelayMs: 500, maxDelayMs: 3000, maxAttempts: 10, random: noJitter,
    });
    const delays = Array.from({ length: 10 }, () => policy.nextDelayMs());
    expect(Math.max(...delays.map((d) => d ?? 0))).toBe(3000);
  });

  it('spends a bounded amount of wall clock before surrendering', () => {
    // The number that matters in money terms: with the defaults, a hard outage
    // costs at most this many token mints, not one every 150ms indefinitely.
    const policy = new ReconnectPolicy({ random: noJitter });
    let total = 0;
    for (;;) {
      const delay = policy.nextDelayMs();
      if (delay === null) break;
      total += delay;
    }
    expect(policy.attemptsUsed).toBe(DEFAULT_RECONNECT_POLICY.maxAttempts);
    expect(total).toBeGreaterThan(30_000);
  });

  it('adds jitter so clients do not stampede back together', () => {
    const low = new ReconnectPolicy({ baseDelayMs: 1000, random: () => 0 });
    const high = new ReconnectPolicy({ baseDelayMs: 1000, random: () => 1 });
    expect(high.nextDelayMs()).toBeGreaterThan(low.nextDelayMs()!);
  });

  it('starts over after a connection succeeds', () => {
    const policy = new ReconnectPolicy({ baseDelayMs: 500, random: noJitter });
    policy.nextDelayMs();
    policy.nextDelayMs();
    policy.reset();
    expect(policy.attemptsUsed).toBe(0);
    expect(policy.nextDelayMs()).toBe(500);
  });
});
