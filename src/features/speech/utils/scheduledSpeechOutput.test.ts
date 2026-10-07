// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { ScheduledSpeechOutput } from './scheduledSpeechOutput';

function fixture(limit = 120) {
  const sources: any[] = [];
  const context = { currentTime: 0, state: 'running', destination: {}, baseLatency: 0, outputLatency: 0,
    createBuffer: vi.fn((_channels: number, count: number, rate: number) => {
      const samples = new Float32Array(count);
      return { duration: count / rate, getChannelData: () => samples };
    }),
    createBufferSource: () => {
      const source = { buffer: null, onended: null, start: vi.fn(), stop: vi.fn(), connect: vi.fn(), disconnect: vi.fn() };
      sources.push(source); return source;
    },
  };
  return { context, sources, output: new ScheduledSpeechOutput(context as unknown as AudioContext, 24000, limit) };
}
beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

it('copies mono PCM and schedules contiguous chunks without including later underrun gaps in its sample position', () => {
  const h = fixture(), pcm = new Int16Array([-32768, 32767]); h.output.write(pcm); pcm[0] = 0;
  expect(h.sources[0].buffer.getChannelData(0)).toEqual(new Float32Array([-1, 32767 / 32768]));
  h.output.write(new Int16Array(2400)); expect(h.sources[1].start).toHaveBeenCalledWith(2 / 24000);
  h.context.currentTime = 4; h.output.write(new Int16Array(2400));
  expect(h.sources[2].start).toHaveBeenCalledWith(4);
  expect(h.output.read().playedSamples).toBe(2402);
  h.context.currentTime = 4.17;
  expect(h.output.read().playedSamples).toBeCloseTo(3602, -1);
  h.output.dispose();
});

it('drains the captured fence only after all earlier chunks and the device tail, while a later chunk may remain', async () => {
  const h = fixture(); h.output.write(new Int16Array(2400)); h.output.write(new Int16Array(2400));
  const done = vi.fn(); const first = h.output.drain().then(done);
  h.output.write(new Int16Array(2400)); const later = vi.fn(); const second = h.output.drain().then(later);
  h.sources[1].onended(); await vi.advanceTimersByTimeAsync(120); expect(done).not.toHaveBeenCalled();
  h.sources[0].onended(); await vi.advanceTimersByTimeAsync(119); expect(done).not.toHaveBeenCalled();
  await vi.advanceTimersByTimeAsync(1); await first; expect(done).toHaveBeenCalledWith('drained');
  expect(later).not.toHaveBeenCalled(); h.output.reset(); await second; expect(later).toHaveBeenCalledWith('cancelled');
  expect(vi.getTimerCount()).toBe(0);
});

it('cancels the old generation and ignores callbacks arriving after reset or disposal', async () => {
  const h = fixture(); h.output.write(new Int16Array(2400)); const oldCallback = h.sources[0].onended;
  const oldDrain = h.output.drain(); h.output.reset(); await expect(oldDrain).resolves.toBe('cancelled');
  h.output.write(new Int16Array(2400)); const current = vi.fn(); const waiting = h.output.drain().then(current);
  oldCallback(); await vi.advanceTimersByTimeAsync(1000); expect(current).not.toHaveBeenCalled();
  h.output.dispose(); await waiting; expect(current).toHaveBeenCalledWith('cancelled');
  expect(() => h.output.write(new Int16Array(1))).toThrow('closed');
  await expect(h.output.drain()).resolves.toBe('cancelled'); expect(vi.getTimerCount()).toBe(0);
});

it('bounds queued memory and admits more only after completed playback; rejected writes do not advance state', async () => {
  const h = fixture(.1); h.output.write(new Int16Array(2400));
  expect(() => h.output.write(new Int16Array(1))).toThrow('full');
  expect(h.context.createBuffer).toHaveBeenCalledOnce(); expect(h.output.read().submittedSamples).toBe(2400);
  h.sources[0].onended(); await vi.advanceTimersByTimeAsync(120);
  h.output.write(new Int16Array(2400)); expect(h.output.read().submittedSamples).toBe(4800); h.output.dispose();
});

it('cleans up a failed source schedule without counting it as submitted audio', async () => {
  const h = fixture(); h.context.createBufferSource = () => {
    const source = { buffer: null, onended: null, start: vi.fn(() => { throw new Error('Renderer unavailable'); }),
      stop: vi.fn(), connect: vi.fn(), disconnect: vi.fn() };
    h.sources.push(source); return source;
  };
  expect(() => h.output.write(new Int16Array(2400))).toThrow('Renderer unavailable');
  expect(h.sources[0].disconnect).toHaveBeenCalledOnce();
  expect(h.output.read().submittedSamples).toBe(0); await expect(h.output.drain()).resolves.toBe('drained');
});
