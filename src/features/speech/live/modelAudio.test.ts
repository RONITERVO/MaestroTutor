// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createLiveModelAudio } from './modelAudio';
import { createLiveSessionState } from './state';
import { WorkletSpeechOutput } from '../utils/workletSpeechOutput';

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());
const flush = async () => { for (let i = 0; i < 10; i++) await Promise.resolve(); };
const setup = () => {
  const state = createLiveSessionState({});
  const postMessage = vi.fn();
  const node = { port: { postMessage, onmessage: null as any }, connect: vi.fn(), disconnect: vi.fn() } as any;
  state.speechOutputRef.current = new WorkletSpeechOutput({ state: 'running', baseLatency: 0, outputLatency: 0 } as any, node);
  state.speechOutputRef.current.write(new Int16Array([1, 2, 3]));
  state.currentSessionIdRef.current = 1;
  state.currentModelAudioTurnIdRef.current = 1;
  state.playbackPendingRef.current = true;
  const markLatest = vi.fn();
  state.turnTimingRef.current = { markLatest, markOnce: vi.fn() } as any;
  const createCodecWorker = vi.fn(); const onFailure = vi.fn();
  const audio = createLiveModelAudio(state, { createCodecWorker, onFailure });
  const acknowledge = () => node.port.onmessage({ data: { type: 'drained', generation: 0, renderedSamples: 3, requestId: postMessage.mock.calls[postMessage.mock.calls.length - 1][0].requestId } });
  return { state, audio, acknowledge, markLatest, postMessage, createCodecWorker, onFailure };
};

describe('Live playback completion ownership', () => {
  it('shares the drain through the full hardware tail with concurrent consumers', async () => {
    const h = setup(); const first = h.audio.waitForPlaybackDrain();
    h.acknowledge(); await flush();
    const finished = vi.fn(); const second = h.audio.waitForPlaybackDrain().then(finished);
    await flush(); expect(finished).not.toHaveBeenCalled();
    expect(h.postMessage.mock.calls.filter(([message]) => message.type === 'request-drain')).toHaveLength(1);
    await vi.advanceTimersByTimeAsync(119); expect(finished).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1); await Promise.all([first, second]);
    expect(finished).toHaveBeenCalledOnce();
    expect(h.state.playbackPendingRef.current).toBe(false);
  });

  it('an old hardware tail cannot mark a new session or clear newer audio', async () => {
    const h = setup(); const pending = h.audio.waitForPlaybackDrain();
    h.acknowledge(); await flush();
    const newMark = vi.fn();
    h.state.currentSessionIdRef.current = 2;
    h.state.turnTimingRef.current = { markLatest: newMark } as any;
    h.state.playbackPendingRef.current = true;
    await vi.advanceTimersByTimeAsync(120); await pending;
    expect(h.markLatest).not.toHaveBeenCalled();
    expect(newMark).not.toHaveBeenCalled();
    expect(h.state.playbackPendingRef.current).toBe(true);
  });

  it('commits decoded samples in provider order and lets a new turn bypass cancelled decoding', async () => {
    const h = setup(); const write = vi.fn();
    h.state.speechOutputRef.current = { write, reset: vi.fn() } as any;
    let first!: (buffer: ArrayBuffer) => void; let second!: (buffer: ArrayBuffer) => void;
    const decode = vi.fn().mockReturnValueOnce(new Promise(resolve => { first = resolve; }))
      .mockReturnValueOnce(new Promise(resolve => { second = resolve; }));
    h.createCodecWorker.mockReturnValue({ decodeBase64ToPcmBuffer: decode });
    h.audio.enqueueModelAudio('first', 1, true); h.audio.enqueueModelAudio('second', 1, true);
    second(new Int16Array([2]).buffer); await flush(); expect(write).not.toHaveBeenCalled();
    first(new Int16Array([1]).buffer); await h.audio.waitForModelAudioDecodeCheckpoint(h.audio.getModelAudioDecodeCheckpoint());
    expect(write.mock.calls.map(([samples]) => Array.from(samples))).toEqual([[1], [2]]);
    expect(h.state.currentModelAudioChunksRef.current.map(samples => Array.from(samples))).toEqual([[1], [2]]);
    decode.mockReturnValueOnce(new Promise(() => {})).mockResolvedValueOnce(new Int16Array([4]).buffer);
    h.audio.enqueueModelAudio('stalled', 1, true);
    h.audio.cancelModelAudioDecodeJobs(); h.audio.startNextModelAudioTurn(1);
    h.audio.enqueueModelAudio('new turn', 1, true);
    await h.audio.waitForModelAudioDecodeCheckpoint(h.audio.getModelAudioDecodeCheckpoint());
    expect(write).toHaveBeenLastCalledWith(new Int16Array([4]));
  });
});
