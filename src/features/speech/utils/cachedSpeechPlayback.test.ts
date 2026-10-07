// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { SpeechBookClient, registerBookSpeech } from '../../../platform/quest/speechBookBridge';
import { isNativeQuestBook } from '../../../platform/quest/questIntegrityBridge';
import { pcmToWav } from '../../../core-sdk/media/audioProcessing';
import { decodeCachedSpeech, playCachedSpeech } from './cachedSpeechPlayback';
vi.mock('../../../platform/quest/questIntegrityBridge', () => ({ isNativeQuestBook: vi.fn() }));

const wav = pcmToWav(new Int16Array([-32768, -1024, 0, 1024, 32767]));
const decoded = (samples = [-1, -1 / 32, 0, 1 / 32, 32767 / 32768]) => ({
  sampleRate: 24000, length: samples.length, numberOfChannels: 1, getChannelData: () => new Float32Array(samples),
} as unknown as AudioBuffer);
let decode: ReturnType<typeof vi.fn>, unregister: (() => void) | undefined;
beforeEach(() => {
  vi.useFakeTimers(); vi.mocked(isNativeQuestBook).mockReturnValue(true);
  decode = vi.fn(async () => decoded());
  vi.stubGlobal('OfflineAudioContext', class { decodeAudioData = decode; });
});
afterEach(() => { unregister?.(); unregister = undefined; vi.useRealTimers(); vi.unstubAllGlobals(); vi.resetAllMocks(); });
const settle = () => vi.advanceTimersByTimeAsync(0);
function host() {
  const client = new SpeechBookClient(); unregister = registerBookSpeech(client);
  const idle = client.exchange(null)!;
  const receive = (extra = {}) => client.exchange({ ...client.exchange(null), host: 'a'.repeat(32), status: 'ready',
    acceptedSequence: 0, submittedSamples: 0, playedSamples: 0, ...extra });
  receive(); expect(idle.open).toBe(false);
  return { client, receive };
}

it('replays actual decoded PCM through native receipts and waits for the played tail without browser/provider playback', async () => {
  const h = host(), getAudioContext = vi.fn(), done = vi.fn();
  const promise = playCachedSpeech({ audioDataUrl: wav, getAudioContext, signal: new AbortController().signal }).then(done);
  await settle();
  const packet = h.client.exchange(null)!;
  expect(packet.chunks).toHaveLength(1);
  const bytes = Uint8Array.from(atob(packet.chunks[0].pcm), c => c.charCodeAt(0));
  expect(new Int16Array(bytes.buffer)).toEqual(new Int16Array([-32768, -1024, 0, 1024, 32767]));
  expect(getAudioContext).not.toHaveBeenCalled();
  h.receive({ acceptedSequence: 1, submittedSamples: 5, playedSamples: 4 }); await settle();
  expect(done).not.toHaveBeenCalled();
  h.receive({ acceptedSequence: 1, submittedSamples: 5, playedSamples: 5 }); await promise;
  expect(done).toHaveBeenCalledWith('drained'); expect(h.client.exchange(null)?.open).toBe(false);
  expect(vi.getTimerCount()).toBe(0);
});

it('returns immediately on Stop during decode and a late decoder cannot replace the new voice', async () => {
  const h = host(), abort = new AbortController();
  let finish!: (audio: AudioBuffer) => void;
  decode.mockImplementationOnce(() => new Promise<AudioBuffer>(resolve => { finish = resolve; }));
  const promise = playCachedSpeech({ audioDataUrl: wav, getAudioContext: vi.fn(), signal: abort.signal });
  await settle(); abort.abort(); await expect(promise).resolves.toBe('cancelled');
  const fresh = h.client.create(); fresh.write(new Int16Array([73]));
  const packet = h.client.exchange(null)!;
  finish(decoded()); await settle();
  expect(h.client.exchange(null)).toEqual(packet); expect(fresh.read().submittedSamples).toBe(1);
  fresh.dispose(); expect(vi.getTimerCount()).toBe(0);
});

it('cancels while a browser context resumes, cleaning only the late output and never scheduling PCM', async () => {
  vi.mocked(isNativeQuestBook).mockReturnValue(false);
  const abort = new AbortController(); let finish!: (ctx: AudioContext) => void;
  const getAudioContext = vi.fn(() => new Promise<AudioContext>(resolve => { finish = resolve; }));
  const promise = playCachedSpeech({ audioDataUrl: wav, getAudioContext, signal: abort.signal });
  expect(getAudioContext).toHaveBeenCalledOnce(); abort.abort();
  await expect(promise).resolves.toBe('cancelled');
  const createBufferSource = vi.fn(), close = vi.fn();
  finish({ createBufferSource, close } as unknown as AudioContext); await settle();
  expect(createBufferSource).not.toHaveBeenCalled(); expect(close).not.toHaveBeenCalled();
  expect(decode).not.toHaveBeenCalled(); expect(vi.getTimerCount()).toBe(0);
});

it('fails on native loss without starting a browser copy', async () => {
  const h = host(), getAudioContext = vi.fn();
  const promise = playCachedSpeech({ audioDataUrl: wav, getAudioContext, signal: new AbortController().signal });
  const rejected = expect(promise).rejects.toThrow('interrupted');
  await settle(); h.receive({ status: 'failed' }); await rejected;
  expect(getAudioContext).not.toHaveBeenCalled(); expect(h.client.exchange(null)?.open).toBe(false);
  expect(vi.getTimerCount()).toBe(0);
});

it('limits a hung decode even while native heartbeats continue', async () => {
  const h = host(); decode.mockImplementationOnce(() => new Promise(() => {}));
  const heartbeat = setInterval(() => h.receive(), 100);
  const promise = playCachedSpeech({ audioDataUrl: wav, getAudioContext: vi.fn(), signal: new AbortController().signal });
  const rejected = expect(promise).rejects.toThrow('stopped responding');
  await vi.advanceTimersByTimeAsync(10000); await rejected; clearInterval(heartbeat);
  expect(h.client.exchange(null)?.open).toBe(false); expect(vi.getTimerCount()).toBe(0);
});

it('bounds encoded data, rejects remote URLs and malformed samples, and normalizes stereo safely', async () => {
  await expect(decodeCachedSpeech('https://example.invalid/private.wav')).rejects.toThrow('inline');
  await expect(decodeCachedSpeech('x'.repeat(16 * 1024 * 1024 + 1))).rejects.toThrow('large');
  expect(decode).not.toHaveBeenCalled();
  decode.mockResolvedValueOnce({ ...decoded(), length: 24000 * 121 });
  await expect(decodeCachedSpeech(wav)).rejects.toThrow('duration');
  decode.mockResolvedValueOnce(decoded([NaN]));
  await expect(decodeCachedSpeech(wav)).rejects.toThrow('invalid samples');
  decode.mockResolvedValueOnce({ ...decoded([1, -2, .5]), numberOfChannels: 2,
    getChannelData: (index: number) => new Float32Array(index ? [1, -1, -.5] : [1, -2, .5]) });
  await expect(decodeCachedSpeech(wav)).resolves.toEqual(new Int16Array([32767, -32768, 0]));
});
