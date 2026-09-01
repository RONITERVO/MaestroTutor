// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Reconnect pacing for managed live sessions.
 *
 * Every live reconnect mints a fresh access token from the backend, and minting
 * one reserves credits. That makes an unbounded retry loop not merely wasteful
 * but directly expensive: the draft this replaces retried on a flat 150ms timer
 * with no ceiling and no backoff, so a Gemini outage or a quota rejection would
 * spin the client as fast as the network allowed, draining the user's balance
 * and hammering the backend for as long as the app stayed open.
 *
 * The policy is deliberately separate from the socket code so the arithmetic
 * can be tested without a websocket, and so both the conversation and the STT
 * hook are paced by one implementation rather than two similar ones.
 */

export interface ReconnectPolicyOptions {
  /** Delay before the first retry. */
  baseDelayMs?: number;
  /** Ceiling for the exponential growth. */
  maxDelayMs?: number;
  /** Attempts before giving up and surfacing the failure to the user. */
  maxAttempts?: number;
  /** Fraction of the delay applied as random jitter, 0..1. */
  jitterRatio?: number;
  /** Injectable for tests. */
  random?: () => number;
}

export const DEFAULT_RECONNECT_POLICY: Required<Omit<ReconnectPolicyOptions, 'random'>> = {
  baseDelayMs: 500,
  maxDelayMs: 30_000,
  maxAttempts: 6,
  jitterRatio: 0.25,
};

export class ReconnectPolicy {
  private attempt = 0;
  private readonly options: Required<Omit<ReconnectPolicyOptions, 'random'>>;
  private readonly random: () => number;

  constructor(options: ReconnectPolicyOptions = {}) {
    this.options = { ...DEFAULT_RECONNECT_POLICY, ...options };
    this.random = options.random ?? Math.random;
  }

  /** Called on a successful connection: the next failure starts from scratch. */
  reset(): void {
    this.attempt = 0;
  }

  get attemptsUsed(): number {
    return this.attempt;
  }

  get exhausted(): boolean {
    return this.attempt >= this.options.maxAttempts;
  }

  /**
   * Delay before the next attempt, or null when the budget is spent.
   *
   * Returning null is what makes this safe: the caller is expected to stop and
   * report, not to fall back on a default delay.
   */
  nextDelayMs(): number | null {
    if (this.exhausted) return null;

    const exponential = this.options.baseDelayMs * Math.pow(2, this.attempt);
    const capped = Math.min(this.options.maxDelayMs, exponential);
    this.attempt += 1;

    // Jitter spreads reconnects out so a shared outage does not bring every
    // client back simultaneously the moment the service recovers.
    const jitter = capped * this.options.jitterRatio * this.random();
    return Math.round(capped + jitter);
  }
}
