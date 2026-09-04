// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { LIVE_TURN_CALLBACK_QUIET_MS, LiveTurnFinalizer } from './liveTurnFinalizer';

describe('LiveTurnFinalizer', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('waits for a quiet window after late callbacks', async () => {
    const finalize = vi.fn();
    const finalizer = new LiveTurnFinalizer();
    finalizer.schedule(finalize);
    await vi.advanceTimersByTimeAsync(LIVE_TURN_CALLBACK_QUIET_MS - 1);
    finalizer.touch();
    await vi.advanceTimersByTimeAsync(LIVE_TURN_CALLBACK_QUIET_MS - 1);
    expect(finalize).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    expect(finalize).toHaveBeenCalledOnce();
  });

  it('can flush on transport close and cancel on invalidation', () => {
    const flushed = vi.fn();
    const cancelled = vi.fn();
    const first = new LiveTurnFinalizer();
    first.schedule(flushed);
    first.flush();
    expect(flushed).toHaveBeenCalledOnce();

    const second = new LiveTurnFinalizer();
    second.schedule(cancelled);
    second.cancel();
    vi.runAllTimers();
    expect(cancelled).not.toHaveBeenCalled();
  });

  it('retains late content during 120 seconds of playback', async () => {
    let finishPlayback!: () => void;
    const playback = new Promise<void>(resolve => { finishPlayback = resolve; });
    const finalize = vi.fn();
    const finalizer = new LiveTurnFinalizer(() => playback);
    finalizer.schedule(finalize);
    await vi.advanceTimersByTimeAsync(119_000);
    expect(finalize).not.toHaveBeenCalled();
    finalizer.touch(); // late transcript/audio, long after turnComplete
    finishPlayback();
    await vi.advanceTimersByTimeAsync(LIVE_TURN_CALLBACK_QUIET_MS - 1);
    expect(finalize).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    expect(finalize).toHaveBeenCalledOnce();
  });
});
