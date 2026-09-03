// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { RealtimePcmPacketizer } from './realtimePcmPacketizer';

describe('RealtimePcmPacketizer output pacing', () => {
  it('replays a burst at PCM cadence while preserving packet order', async () => {
    let now = 1_000;
    const sentAt: number[] = [];
    const sentValues: number[] = [];
    const packetizer = new RealtimePcmPacketizer({
      sampleRate: 1_000,
      packetDurationMs: 20,
      paceOutput: true,
      pacingClock: {
        now: () => now,
        sleep: async milliseconds => { now += milliseconds; },
      },
      onPacket: packet => {
        sentAt.push(now);
        sentValues.push(packet[0]);
      },
    });

    for (let value = 1; value <= 5; value += 1) {
      packetizer.push(new Int16Array(20).fill(value));
    }
    await packetizer.flushPending();

    expect(sentValues).toEqual([1, 2, 3, 4, 5]);
    expect(sentAt).toEqual([1_000, 1_020, 1_040, 1_060, 1_080]);
    expect(packetizer.getStats()).toMatchObject({
      totalOutputSamples: 100,
      maxPacketSamples: 20,
      pacedOutput: true,
      outputPacingWaitMs: 80,
      outputPacingElapsedMs: 80,
    });
  });

  it('does not add delay when microphone-paced packets arrive on schedule', async () => {
    let now = 1_000;
    const sentAt: number[] = [];
    const packetizer = new RealtimePcmPacketizer({
      sampleRate: 1_000,
      packetDurationMs: 20,
      paceOutput: true,
      pacingClock: {
        now: () => now,
        sleep: async milliseconds => { now += milliseconds; },
      },
      onPacket: () => { sentAt.push(now); },
    });

    packetizer.push(new Int16Array(20));
    await packetizer.flushPending();
    now += 20;
    packetizer.push(new Int16Array(20));
    await packetizer.flushPending();

    expect(sentAt).toEqual([1_000, 1_020]);
    expect(packetizer.getStats().outputPacingWaitMs).toBe(0);
  });
});
