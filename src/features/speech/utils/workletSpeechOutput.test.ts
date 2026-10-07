// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { WorkletSpeechOutput } from './workletSpeechOutput';

beforeEach(() => vi.useFakeTimers());
afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); vi.resetModules(); });

async function fixture(outputRate = 24000, limit = 120) {
  let Processor: any;
  let processor: any;
  const inbound: unknown[] = [];
  const received: any[] = [];
  const node = {
    connect: vi.fn(), disconnect: vi.fn(), onprocessorerror: null as null | (() => void),
    port: { onmessage: null as null | ((event: any) => void), postMessage: vi.fn((message: any, transfer: Transferable[] = []) => {
      inbound.push(structuredClone(message, { transfer }));
    }) },
  };
  vi.stubGlobal('AudioWorkletProcessor', class {
    port = { onmessage: null, postMessage: (message: unknown) => {
      received.push(message); node.port.onmessage?.({ data: structuredClone(message) });
    } };
  });
  vi.stubGlobal('sampleRate', outputRate);
  vi.stubGlobal('registerProcessor', (_name: string, ctor: unknown) => { Processor = ctor; });
  await import('../worklets/pcmPlaybackProcessor.worklet');
  processor = new Processor();
  const context = { state: 'running', destination: {}, baseLatency: 0, outputLatency: 0, close: vi.fn() };
  const onError = vi.fn(), onEvent = vi.fn();
  const output = new WorkletSpeechOutput(context as any, node as any, { onError, onEvent }, limit);
  const render = (count = 1) => {
    const result: number[] = [];
    for (let i = 0; i < count; i++) {
      while (inbound.length) processor.port.onmessage({ data: inbound.shift() });
      const samples = new Float32Array(128); processor.process([], [[samples]], {}); result.push(...samples);
    }
    return result;
  };
  return { output, node, context, onError, onEvent, render, received, inbound };
}

it('renders copied mono PCM and acknowledges each fence while later speech stays queued', async () => {
  const h = await fixture(); const first = new Int16Array(2880).fill(16384);
  h.output.write(first); first.fill(0);
  const drained = vi.fn(); const pending = h.output.drain().then(drained);
  h.output.write(new Int16Array(2880).fill(8192));
  const samples = h.render(23);
  expect(samples.slice(0, 2880).every(value => value === .5)).toBe(true);
  expect(samples.slice(2880).every(value => value === .25)).toBe(true);
  expect(h.received).toContainEqual({ type: 'drained', generation: 0, requestId: 1, renderedSamples: 2944 });
  expect(h.output.read().playedSamples).toBe(0);
  await vi.advanceTimersByTimeAsync(119); expect(drained).not.toHaveBeenCalled();
  await vi.advanceTimersByTimeAsync(1); await pending;
  expect(drained).toHaveBeenCalledWith('drained');
  expect(h.output.read()).toEqual({ submittedSamples: 5760, playedSamples: 2944, started: true });
  const last = h.output.drain(); h.render(23); await vi.advanceTimersByTimeAsync(120);
  await expect(last).resolves.toBe('drained'); expect(h.output.read().playedSamples).toBe(5760);
  expect(h.onEvent).toHaveBeenCalledWith('started'); h.output.dispose(); expect(h.context.close).not.toHaveBeenCalled();
});

it('drains short responses at a 48 kHz device rate without losing or duplicating samples', async () => {
  const h = await fixture(48000); h.output.write(new Int16Array(240).fill(-16384));
  const pending = h.output.drain(); const samples = h.render(4);
  expect(samples.slice(0, 480).every(value => value === -.5)).toBe(true);
  expect(samples.slice(480).every(value => value === 0)).toBe(true);
  await vi.advanceTimersByTimeAsync(120); await expect(pending).resolves.toBe('drained');
  expect(h.output.read().playedSamples).toBe(240); h.output.dispose();
});

it('cancels the hardware tail and rejects old-generation packets after reset', async () => {
  const h = await fixture(); h.output.write(new Int16Array(240).fill(16384));
  const pending = h.output.drain(); h.render(2); h.output.reset();
  await expect(pending).resolves.toBe('cancelled');
  h.inbound.push({ type: 'push', generation: 0, pcm: new Int16Array(120).fill(32767), inputSampleRate: 24000 });
  h.output.write(new Int16Array(240).fill(8192)); const finished = vi.fn(); const next = h.output.drain().then(finished);
  h.node.port.onmessage?.({ data: { type: 'drained', generation: 0, requestId: 2, renderedSamples: 240 } });
  await vi.advanceTimersByTimeAsync(120); expect(finished).not.toHaveBeenCalled();
  expect(h.output.read().playedSamples).toBe(0);
  const samples = h.render(2); expect(samples.slice(0, 240).every(value => value === .25)).toBe(true);
  await vi.advanceTimersByTimeAsync(120); await next; expect(finished).toHaveBeenCalledExactlyOnceWith('drained');
  h.output.dispose();
});

it('bounds the queue before copying and terminates processor errors without a false completion', async () => {
  const h = await fixture(24000, 1); h.output.write(new Int16Array(24000));
  const calls = h.node.port.postMessage.mock.calls.length;
  expect(() => h.output.write(new Int16Array(1))).toThrow('buffer is full');
  expect(h.node.port.postMessage).toHaveBeenCalledTimes(calls);
  const pending = h.output.drain(); const rejected = expect(pending).rejects.toThrow('processor failed');
  h.node.onprocessorerror?.(); await rejected;
  expect(h.node.disconnect).toHaveBeenCalledOnce(); expect(h.onError).toHaveBeenCalledOnce();
  await expect(h.output.drain()).rejects.toThrow('processor failed');
  expect(() => h.output.write(new Int16Array(1))).toThrow('processor failed');
  expect(h.context.close).not.toHaveBeenCalled();
});

it('reports render-side format rejection instead of silently dropping a chunk', async () => {
  const h = await fixture(); h.output.write(new Int16Array(1));
  const pending = h.output.drain(); const rejected = expect(pending).rejects.toThrow('rejected audio');
  h.inbound.unshift({ type: 'push', generation: 0, pcm: new Int16Array(1), inputSampleRate: 8000 });
  h.render(); await rejected;
  expect(h.onError).toHaveBeenCalledOnce(); expect(h.node.disconnect).toHaveBeenCalledOnce();
});
