// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it, vi } from 'vitest';
import {
  getAudioOutputTailDelayMs,
  ScheduledPlaybackDrain,
  WorkletPlaybackDrainCoordinator,
} from './playbackDrain';

describe('playback drain coordination', () => {
  it('does not equate a worklet drain request with audible completion', async () => {
    const coordinator = new WorkletPlaybackDrainCoordinator();
    const postMessage = vi.fn();
    let result: string | undefined;

    const pending = coordinator.request({ postMessage }).then(value => {
      result = value;
    });
    const request = postMessage.mock.calls[0][0];

    await Promise.resolve();
    expect(result).toBeUndefined();
    expect(request).toEqual({ type: 'request-drain', requestId: 1 });
    expect(coordinator.handleMessage({ type: 'drained', requestId: 1 })).toBe(true);
    await pending;
    expect(result).toBe('drained');
  });

  it('cancels pending worklet waits during an explicit user teardown', async () => {
    const coordinator = new WorkletPlaybackDrainCoordinator();
    const pending = coordinator.request({ postMessage: vi.fn() });

    coordinator.cancelAll();

    await expect(pending).resolves.toBe('cancelled');
  });

  it('keeps scheduled TTS pending until every audio source ends', async () => {
    const drain = new ScheduledPlaybackDrain();
    const firstEnded = drain.trackSource();
    const secondEnded = drain.trackSource();
    let result: string | undefined;
    const pending = drain.wait().then(value => {
      result = value;
    });

    firstEnded();
    await Promise.resolve();
    expect(result).toBeUndefined();
    secondEnded();
    await pending;
    expect(result).toBe('drained');
  });

  it('uses device latency with a bounded Android output-tail margin', () => {
    expect(getAudioOutputTailDelayMs({ baseLatency: 0.01, outputLatency: 0.02 })).toBe(120);
    expect(getAudioOutputTailDelayMs({ baseLatency: 0.2, outputLatency: 0.1 })).toBe(351);
    expect(getAudioOutputTailDelayMs({ baseLatency: 2, outputLatency: 2 })).toBe(1_000);
  });
});
