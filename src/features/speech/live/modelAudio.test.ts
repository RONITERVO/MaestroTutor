// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createLiveModelAudio } from './modelAudio';
import { createLiveSessionState } from './state';

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());
const flush = async () => { for (let i = 0; i < 10; i++) await Promise.resolve(); };
const setup = () => {
  const state = createLiveSessionState({});
  const postMessage = vi.fn();
  state.playbackNodeRef.current = { port: { postMessage } } as any;
  state.outputAudioContextRef.current = { state: 'running', baseLatency: 0, outputLatency: 0 } as any;
  state.currentSessionIdRef.current = 1;
  state.currentModelAudioTurnIdRef.current = 1;
  state.playbackPendingRef.current = true;
  const markLatest = vi.fn();
  state.turnTimingRef.current = { markLatest } as any;
  const audio = createLiveModelAudio(state, { setTimeout: (fn, ms) => setTimeout(fn, ms) as unknown as number, createCodecWorker: vi.fn() });
  const acknowledge = () => state.playbackDrainCoordinatorRef.current.handleMessage({ type: 'drained', requestId: postMessage.mock.calls[postMessage.mock.calls.length - 1][0].requestId });
  return { state, audio, acknowledge, markLatest, postMessage };
};

describe('Live playback completion ownership', () => {
  it('shares the drain through the full hardware tail with concurrent consumers', async () => {
    const h = setup(); const first = h.audio.waitForPlaybackDrain();
    h.acknowledge(); await flush();
    const finished = vi.fn(); const second = h.audio.waitForPlaybackDrain().then(finished);
    await flush(); expect(finished).not.toHaveBeenCalled();
    expect(h.postMessage).toHaveBeenCalledOnce();
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
});
