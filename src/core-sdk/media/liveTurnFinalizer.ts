// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/**
 * Gemini can dispatch transcript, audio, and usage callbacks that were already
 * in flight after the callback carrying turnComplete. Finalize only after the
 * callback stream has stayed quiet for this window and the supplied sink drains.
 */
export const LIVE_TURN_CALLBACK_QUIET_MS = 1_500;

export class LiveTurnFinalizer {
  private timer: ReturnType<typeof setTimeout> | null = null;
  private pending: (() => void) | null = null;
  private revision = 0;

  constructor(private readonly drain?: () => Promise<void>) {}

  schedule(finalize: () => void): void {
    this.revision += 1;
    this.pending = finalize;
    this.arm();
  }

  touch(): void {
    this.revision += 1;
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
    this.revision += 1;
    if (this.timer) clearTimeout(this.timer);
    this.timer = null;
    this.pending = null;
  }

  private arm(): void {
    if (this.timer) clearTimeout(this.timer);
    this.timer = setTimeout(async () => {
      this.timer = null;
      const revision = this.revision;
      if (this.drain) {
        await this.drain();
        // A callback received while decoding/playing belongs to this turn too.
        if (revision !== this.revision || !this.pending) return;
      }
      const finalize = this.pending;
      this.pending = null;
      finalize?.();
    }, LIVE_TURN_CALLBACK_QUIET_MS);
  }
}
