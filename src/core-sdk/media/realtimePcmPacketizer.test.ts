// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { RealtimePcmPacketizer } from './realtimePcmPacketizer';

describe('RealtimePcmPacketizer output pacing', () => {
  it('reports queued audio until the asynchronous send actually finishes', async () => {
    let finish!: () => void;
    const packetizer = new RealtimePcmPacketizer({
      sampleRate: 1000, packetDurationMs: 20,
      onPacket: () => new Promise<void>(resolve => { finish = resolve; }),
    });
    packetizer.push(new Int16Array(20));
    const drain = packetizer.flushPending();
    await Promise.resolve();
    await Promise.resolve();
    expect(packetizer.getQueuedAudioMs()).toBe(20);
    finish();
    await drain;
    expect(packetizer.getQueuedAudioMs()).toBe(0);
    packetizer.dispose();
  });
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

  it('starts a fresh pacing epoch after an ordered turn boundary', async () => {
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
    packetizer.push(new Int16Array(20));
    await packetizer.flushPending();
    packetizer.resetPacingEpoch();

    now += 10_000;
    packetizer.push(new Int16Array(20));
    packetizer.push(new Int16Array(20));
    await packetizer.flushPending();

    expect(sentAt).toEqual([1_000, 1_020, 11_020, 11_040]);
    expect(packetizer.getStats().outputPacingElapsedMs).toBe(40);
  });
});
