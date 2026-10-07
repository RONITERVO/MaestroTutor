// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SpeechBookClient } from './speechBookBridge';

const host = 'a'.repeat(32);
const clients: SpeechBookClient[] = [];
function setup() {
  vi.useFakeTimers();
  const client = new SpeechBookClient(() => Date.now()); clients.push(client);
  const idle = client.exchange(null)!;
  client.exchange({ ...idle, host, status: 'ready', acceptedSequence: 0, submittedSamples: 0, playedSamples: 0, microphoneSuppressed: false });
  return client;
}
const receipt = (client: SpeechBookClient, acceptedSequence: number, submittedSamples: number, playedSamples: number) => {
  const request = client.exchange(null)!;
  return { ...request, host, status: 'playing', acceptedSequence, submittedSamples, playedSamples, microphoneSuppressed: false };
};
afterEach(() => { for (const client of clients.splice(0)) client.dispose(); vi.useRealTimers(); });

describe('book speech transport', () => {
  it('drains PCM independently of the native tail and keeps reset and stale owners gated', async () => {
    const client = setup(), output = client.create();
    expect(output.isMicrophoneSuppressed?.()).toBe(true);
    output.write(new Int16Array(240));
    const done = output.drain(), quiet = receipt(client, 1, 240, 240);
    client.exchange({ ...quiet, microphoneSuppressed: true });
    await expect(done).resolves.toBe('drained');
    expect(output.isMicrophoneSuppressed?.()).toBe(true);
    client.exchange(quiet); expect(output.isMicrophoneSuppressed?.()).toBe(false);
    output.reset(); client.exchange(quiet);
    expect(output.isMicrophoneSuppressed?.()).toBe(true);
    client.exchange({ ...receipt(client, 0, 0, 0), microphoneSuppressed: true });
    expect(output.isMicrophoneSuppressed?.()).toBe(true);
    client.exchange(receipt(client, 0, 0, 0)); expect(output.isMicrophoneSuppressed?.()).toBe(false);
    client.suspend(); expect(output.isMicrophoneSuppressed?.()).toBe(true);
  });
  it.each([undefined, 0, 'false'])('ignores an invalid microphone receipt (%s) without claiming progress', flag => {
    const client = setup(), output = client.create(); output.write(new Int16Array(240));
    client.exchange({ ...receipt(client, 1, 240, 240), microphoneSuppressed: flag });
    expect(output.read().playedSamples).toBe(0); expect(output.isMicrophoneSuppressed?.()).toBe(true);
  });
  it('does not establish a voice connection with an old native speech protocol', () => {
    const client = new SpeechBookClient(); clients.push(client);
    client.exchange({ ...client.exchange(null), version: 1, host, status: 'ready', microphoneSuppressed: false });
    expect(() => client.create()).toThrow();
  });
  it('copies little-endian PCM, retries unchanged bytes and drains only played samples at its fence', async () => {
    const client = setup(), onEvent = vi.fn(), output = client.create({ onEvent });
    const pcm = new Int16Array(4800); pcm[0] = -32768; pcm[1] = 32767;
    output.write(pcm); pcm[0] = 0;
    const done = vi.fn(); const drain = output.drain().then(done);
    output.write(new Int16Array(9600));
    const offered = client.exchange(null)!;
    expect(atob(offered.chunks[0].pcm).slice(0, 4)).toBe('\x00\x80\xff\x7f');
    expect(client.exchange(null)).toEqual(offered);
    client.exchange(receipt(client, 1, 4800, 4799)); await Promise.resolve(); expect(done).not.toHaveBeenCalled();
    client.exchange(receipt(client, 1, 4800, 4800)); await drain;
    expect(done).toHaveBeenCalledWith('drained'); expect(onEvent).toHaveBeenCalledOnce();
    expect(output.read()).toEqual({ submittedSamples: 14400, playedSamples: 4800, started: true });
    expect(client.exchange(null)!.chunks.map(c => c.sequence)).toEqual([2, 3]);
  });
  it('bounds native credit independently of the provider burst and aggregates tiny writes', () => {
    const client = setup(), output = client.create();
    for (let i = 0; i < 1000; i++) output.write(new Int16Array([i]));
    const small = client.exchange(null)!;
    expect(small.chunks).toHaveLength(1); expect(atob(small.chunks[0].pcm).length).toBe(2000);
    output.write(new Int16Array(24000 * 5));
    expect(client.exchange(null)!.chunks[0]).toEqual(small.chunks[0]);
    expect(client.exchange(null)!.chunks).toHaveLength(8);
    expect(() => output.write(new Int16Array(24000 * 120))).toThrow('full');
    expect(output.read().submittedSamples).toBe(121000);
  });
  it('cancels Stop fences and refuses stale receipts or PCM resurrection after a reset', async () => {
    const client = setup(), output = client.create(); output.write(new Int16Array(4800));
    const old = receipt(client, 1, 4800, 4800), drain = output.drain();
    output.reset(); await expect(drain).resolves.toBe('cancelled');
    output.write(new Int16Array(240)); client.exchange(old);
    expect(output.read().playedSamples).toBe(0);
    client.exchange(receipt(client, 1, 240, 240)); await expect(output.drain()).resolves.toBe('drained');
    output.dispose(); expect(client.exchange(null)?.open).toBe(false);
    client.exchange(old); expect(client.exchange(null)?.open).toBe(false);
  });
  it.each(['timeout', 'host replacement', 'native stop', 'impossible receipt', 'suspend'])('fails closed for %s without replay', async reason => {
    const client = setup(), onError = vi.fn(), output = client.create({ onError }); output.write(new Int16Array(4800));
    const failed = expect(output.drain()).rejects.toThrow('interrupted');
    if (reason === 'timeout') vi.advanceTimersByTime(1750);
    else if (reason === 'host replacement') client.exchange({ ...receipt(client, 0, 0, 0), host: 'b'.repeat(32) });
    else if (reason === 'native stop') client.exchange({ ...receipt(client, 0, 0, 0), status: 'failed' });
    else if (reason === 'impossible receipt') client.exchange(receipt(client, 2, 9600, 9600));
    else client.suspend();
    await failed; expect(onError).toHaveBeenCalledOnce(); expect(client.exchange(null)?.open).toBe(false);
    expect(() => output.write(new Int16Array([1]))).toThrow();
  });
  it('requires a recent native handshake and cancels the old owner when another starts', async () => {
    const unready = new SpeechBookClient(); clients.push(unready); expect(() => unready.create()).toThrow();
    const client = setup(), first = client.create(); first.write(new Int16Array([1])); const drained = first.drain();
    const second = client.create(); await expect(drained).resolves.toBe('cancelled');
    expect(() => first.write(new Int16Array([1]))).toThrow(); second.dispose();
    vi.advanceTimersByTime(1501); expect(() => client.create()).toThrow();
  });
});
