// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/**
 * Gemini can dispatch transcript, audio, and usage callbacks that were already
 * in flight after the callback carrying turnComplete. Finalize only after the
 * callback stream has stayed quiet for this window.
 */
export const LIVE_TURN_CALLBACK_QUIET_MS = 250;

export class LiveTurnFinalizer {
  private timer: ReturnType<typeof setTimeout> | null = null;
  private pending: (() => void) | null = null;

  schedule(finalize: () => void): void {
    this.pending = finalize;
    this.arm();
  }

  touch(): void {
    if (!this.pending) return;
    this.arm();
  }

  flush(): void {
    if (!this.pending) return;
    if (this.timer) clearTimeout(this.timer);
    this.timer = null;
    const finalize = this.pending;
    this.pending = null;
    finalize();
  }

  cancel(): void {
    if (this.timer) clearTimeout(this.timer);
    this.timer = null;
    this.pending = null;
  }

  private arm(): void {
    if (this.timer) clearTimeout(this.timer);
    this.timer = setTimeout(() => {
      this.timer = null;
      const finalize = this.pending;
      this.pending = null;
      finalize?.();
    }, LIVE_TURN_CALLBACK_QUIET_MS);
  }
}
