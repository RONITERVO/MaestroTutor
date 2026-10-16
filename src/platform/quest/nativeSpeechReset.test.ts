// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { expect, it, vi } from 'vitest';
import native from '../../../test-fixtures/browser/nativeSpeechReset.json';
import { SpeechBookClient } from './speechBookBridge';

it('consumes actual native reset receipts without replay and completes only a fresh voice', async () => {
  vi.useFakeTimers();
  // Match the synthetic native test session; every receipt below is unmodified.
  vi.spyOn(crypto, 'randomUUID').mockReturnValue('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb');
  const client = new SpeechBookClient(() => 0);
  try {
    client.exchange(native.initial.status);
    const onError = vi.fn(), voice = client.create({ onError });
    voice.write(new Int16Array(19200));
    expect(client.exchange(null)?.chunks).toHaveLength(4);
    const interrupted = expect(voice.finish()).rejects.toThrow('interrupted');
    client.exchange(native.beforeReset.status);
    expect(voice.read().playedSamples).toBeGreaterThan(0);
    expect(voice.read().playedSamples).toBeLessThan(19200);

    const stopped = client.exchange(native.afterReset.status);
    await interrupted;
    expect(onError).toHaveBeenCalledOnce();
    expect(stopped).toMatchObject({ open: false, revision: 3, chunks: [], completion: null });
    expect(voice.isMicrophoneSuppressed?.()).toBe(true);
    expect(() => voice.write(new Int16Array(240))).toThrow('interrupted');
    expect(client.exchange(native.lateRequest.status)?.open).toBe(false);
    expect(onError).toHaveBeenCalledOnce();
    expect(() => client.create()).toThrow('interrupted');

    client.exchange(native.closed.status);
    const fresh = client.create();
    fresh.write(new Int16Array(19200));
    const finished = fresh.finish();
    expect(client.exchange(null)?.chunks).toHaveLength(4);
    expect(client.exchange(native.recoveredAccepted.status)?.completion).toEqual({
      sequence: 4, submittedSamples: 19200,
    });
    client.exchange(native.recoveredDrained.status);
    await expect(finished).resolves.toBe('drained');
    expect(fresh.read().playedSamples).toBe(19200);
    expect(onError).toHaveBeenCalledOnce();

    // The native test measured actual DSP after retaining and then releasing the wall.
    expect(native.closed.geometry).toBe(1);
    expect(native.recoveredDrained.geometry).toBe(1);
    expect(native.clearDrained.geometry).toBe(0);
    expect(native.energy.clear).toBeGreaterThan(.0001);
    expect(native.energy.retained).toBeLessThan(native.energy.clear * .7);
  } finally {
    client.dispose(); vi.restoreAllMocks(); vi.useRealTimers();
  }
});
