// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

export type PlaybackDrainResult = 'drained' | 'cancelled';

interface PlaybackMessagePort {
  postMessage(message: { type: 'request-drain'; requestId: number }): void;
}

interface PlaybackDrainedMessage {
  type: 'drained';
  requestId: number;
}

const isPlaybackDrainedMessage = (value: unknown): value is PlaybackDrainedMessage => {
  if (!value || typeof value !== 'object') return false;
  const candidate = value as Partial<PlaybackDrainedMessage>;
  return candidate.type === 'drained'
    && Number.isSafeInteger(candidate.requestId)
    && Number(candidate.requestId) > 0;
};

/**
 * Resolves worklet drain requests only after the audio thread confirms that its
 * PCM queue has been rendered. A transport close must not be treated as this
 * signal: Live providers commonly deliver audio faster than users can hear it.
 */
export class WorkletPlaybackDrainCoordinator {
  private nextRequestId = 1;
  private readonly pending = new Map<number, (result: PlaybackDrainResult) => void>();

  request(port: PlaybackMessagePort): Promise<PlaybackDrainResult> {
    const requestId = this.nextRequestId++;
    return new Promise((resolve) => {
      this.pending.set(requestId, resolve);
      try {
        port.postMessage({ type: 'request-drain', requestId });
      } catch {
        this.pending.delete(requestId);
        resolve('cancelled');
      }
    });
  }

  handleMessage(value: unknown): boolean {
    if (!isPlaybackDrainedMessage(value)) return false;
    const resolve = this.pending.get(value.requestId);
    if (!resolve) return true;
    this.pending.delete(value.requestId);
    resolve('drained');
    return true;
  }

  cancelAll(): void {
    for (const resolve of this.pending.values()) resolve('cancelled');
    this.pending.clear();
  }
}

/** Tracks scheduled AudioBufferSourceNodes until every source has ended. */
export class ScheduledPlaybackDrain {
  private pendingSources = 0;
  private sealed = false;
  private settled = false;
  private readonly promise: Promise<PlaybackDrainResult>;
  private resolvePromise!: (result: PlaybackDrainResult) => void;

  constructor() {
    this.promise = new Promise((resolve) => {
      this.resolvePromise = resolve;
    });
  }

  trackSource(): () => void {
    if (this.sealed) {
      throw new Error('Cannot schedule audio after playback drain has begun');
    }
    this.pendingSources += 1;
    let completed = false;
    return () => {
      if (completed) return;
      completed = true;
      this.pendingSources = Math.max(0, this.pendingSources - 1);
      this.resolveIfDrained();
    };
  }

  wait(): Promise<PlaybackDrainResult> {
    this.sealed = true;
    this.resolveIfDrained();
    return this.promise;
  }

  cancel(): void {
    if (this.settled) return;
    this.settled = true;
    this.resolvePromise('cancelled');
  }

  private resolveIfDrained(): void {
    if (!this.sealed || this.pendingSources > 0 || this.settled) return;
    this.settled = true;
    this.resolvePromise('drained');
  }
}

/**
 * The worklet acknowledgement means its last render quantum was submitted, not
 * necessarily that Android's hardware buffer has emitted it. Retain the graph
 * for the reported device latency plus a small render margin.
 */
export const getAudioOutputTailDelayMs = (
  context: Pick<AudioContext, 'baseLatency'> & Partial<Pick<AudioContext, 'outputLatency'>>,
): number => {
  const baseLatency = Number.isFinite(context.baseLatency) ? Math.max(0, context.baseLatency) : 0;
  const outputLatency = Number.isFinite(context.outputLatency)
    ? Math.max(0, Number(context.outputLatency))
    : 0;
  return Math.min(1_000, Math.max(120, Math.ceil((baseLatency + outputLatency) * 1_000) + 50));
};
