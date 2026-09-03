// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it, vi } from 'vitest';
import { createCoreRuntime } from '../runtime';
import { createSyntheticPcmSource, PcmCaptureHandoff } from './pcmInput';

describe('PCM input timing', () => {
  it('paces synthetic capture to absolute sample deadlines without processing drift', async () => {
    let now = 0;
    const sleeps: number[] = [];
    const runtime = createCoreRuntime({
      clock: {
        now: () => now,
        sleep: async (ms) => {
          sleeps.push(ms);
          now += ms;
        },
        setInterval: () => 0,
        clearInterval: () => undefined,
      },
    });
    const source = createSyntheticPcmSource({
      pcm: new Int16Array(1_600),
      sampleRate: 16_000,
      chunkDurationMs: 20,
      pace: true,
      runtime,
    });
    const arrivals: number[] = [];

    await source.start(async () => {
      arrivals.push(now);
      now += 7; // logging/encoding work that must not accumulate into pacing
    });

    expect(arrivals).toEqual([20, 40, 60, 80, 100]);
    expect(now).toBe(107);
    expect(sleeps.reduce((total, delay) => total + delay, 0)).toBe(72);
  });

  it('drains a capture handoff once and routes future packets directly', () => {
    const handoff = new PcmCaptureHandoff();
    handoff.push(new Int16Array([1, 2]));
    handoff.push(new Int16Array([3]));
    const received: number[] = [];

    expect(handoff.transferTo(packet => received.push(...packet))).toEqual({
      bufferedPackets: 2,
      bufferedSamples: 3,
    });
    handoff.push(new Int16Array([4, 5]));

    expect(received).toEqual([1, 2, 3, 4, 5]);
    expect(() => handoff.transferTo(vi.fn())).toThrow('already been transferred');
  });
});
