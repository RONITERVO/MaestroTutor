// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

/** Ariadne's proven semantic-speech boundary, split into idle and post-roll. */
export const LIVE_SPEECH_IDLE_MS = 2_500;
export const LIVE_SPEECH_POST_ROLL_MS = 1_500;

export type ContinuousLiveTurnBoundaryState = 'closed' | 'open' | 'closing';

/**
 * Places turn boundaries around a continuous microphone stream.
 *
 * VAD/Whisper decide when a turn exists and when its tail is complete; they do
 * not decide which packets inside an open turn reach Live. Once opened, every
 * packet is forwarded until the full idle plus post-roll deadline has elapsed.
 */
export class ContinuousLiveTurnBoundary {
  private readonly tailMs: number;
  private state: ContinuousLiveTurnBoundaryState = 'closed';
  private closeAt: number | null = null;

  constructor(options: { idleMs?: number; postRollMs?: number } = {}) {
    const idleMs = Math.max(0, options.idleMs ?? LIVE_SPEECH_IDLE_MS);
    const postRollMs = Math.max(0, options.postRollMs ?? LIVE_SPEECH_POST_ROLL_MS);
    this.tailMs = idleMs + postRollMs;
  }

  get currentState(): ContinuousLiveTurnBoundaryState {
    return this.state;
  }

  get isOpen(): boolean {
    return this.state === 'open';
  }

  get isClosing(): boolean {
    return this.state === 'closing';
  }

  get closeDeadline(): number | null {
    return this.closeAt;
  }

  openFromConfirmedSpeech(now: number): boolean {
    if (this.state !== 'closed') return false;
    this.state = 'open';
    this.refreshConfirmedSpeech(now);
    return true;
  }

  /** A later semantic speech result extends the same continuous turn. */
  refreshConfirmedSpeech(now: number): boolean {
    if (this.state !== 'open') return false;
    this.closeAt = now + this.tailMs;
    return true;
  }

  shouldBeginClosing(now: number): boolean {
    return this.state === 'open' && this.closeAt !== null && now >= this.closeAt;
  }

  beginClosing(now: number): boolean {
    if (!this.shouldBeginClosing(now)) return false;
    this.state = 'closing';
    this.closeAt = null;
    return true;
  }

  finishClosing(): void {
    if (this.state !== 'closing') return;
    this.state = 'closed';
  }

  reset(): void {
    this.state = 'closed';
    this.closeAt = null;
  }
}

export default ContinuousLiveTurnBoundary;
