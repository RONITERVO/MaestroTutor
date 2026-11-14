// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
const ports = vi.hoisted(() => ({ stream: vi.fn(), replay: vi.fn() }));
vi.mock('../services/geminiLiveTts', () => ({ streamGeminiLiveTts: ports.stream }));
vi.mock('../utils/cachedSpeechPlayback', () => ({ playCachedSpeech: ports.replay }));
import { useTtsEngine } from './useTtsEngine';
import { sessionActivity } from '../../../platform/browser/sessionActivity';

afterEach(async () => {
  cleanup(); sessionActivity.setSuspended(false); sessionActivity.resume();
  vi.unstubAllGlobals(); vi.clearAllMocks();
});

describe('speech playback interruption', () => {
  it('does not open a paid transport when suspended while the audio context resumes', async () => {
    let finishResume!: () => void;
    const close = vi.fn(async () => {});
    vi.stubGlobal('AudioContext', class {
      state = 'suspended';
      resume = () => new Promise<void>(resolve => { finishResume = resolve; });
      close = close;
    });
    const hook = renderHook(() => useTtsEngine());
    act(() => hook.result.current.speak('Interrupted speech', 'en', 'voice.tts-click'));
    await act(async () => { sessionActivity.setSuspended(true); });
    act(() => { sessionActivity.setSuspended(false); sessionActivity.resume(); });
    await act(async () => { finishResume(); });
    expect(ports.stream).not.toHaveBeenCalled(); expect(close).toHaveBeenCalled();
    expect(hook.result.current.isSpeaking).toBe(false);
  });

  it('stops cached speech and leaves the old queue empty after explicit resume', async () => {
    const play = vi.fn(async () => {}); const pause = vi.fn();
    vi.stubGlobal('Audio', class { play = play; pause = pause; });
    const hook = renderHook(() => useTtsEngine());
    act(() => hook.result.current.speak([{ text: 'Cached speech', langCode: 'en', speaker: 'learner', cachedAudio: 'data:audio/wav;base64,AAAA' }], 'en', 'voice.tts-click'));
    expect(play).toHaveBeenCalledOnce();
    await act(async () => { sessionActivity.setSuspended(true); });
    expect(pause).toHaveBeenCalledOnce();
    act(() => { sessionActivity.setSuspended(false); sessionActivity.resume(); });
    expect(play).toHaveBeenCalledOnce(); expect(hook.result.current.hasPendingQueueItems()).toBe(false);
    expect(ports.replay).not.toHaveBeenCalled();
  });

  it('queues Maestro replay once, waits for native completion, then advances to the next item', async () => {
    const completions: ((result: string) => void)[] = [];
    ports.replay.mockImplementation(() => new Promise(resolve => completions.push(resolve)));
    const complete = vi.fn(), hook = renderHook(() => useTtsEngine({ onQueueComplete: complete }));
    act(() => hook.result.current.speak([{ text: 'One', langCode: 'en', cachedAudio: 'one' }], 'en', 'voice.tts-click'));
    act(() => hook.result.current.speak([{ text: 'Two', langCode: 'en', cachedAudio: 'two' }], 'en', 'voice.tts-click'));
    expect(ports.replay).toHaveBeenCalledTimes(1); expect(ports.stream).not.toHaveBeenCalled();
    expect(hook.result.current.speakingUtteranceText).toBe('One');
    await act(async () => completions[0]('drained'));
    expect(ports.replay).toHaveBeenCalledTimes(2); expect(complete).not.toHaveBeenCalled();
    expect(hook.result.current.speakingUtteranceText).toBe('Two');
    await act(async () => completions[1]('drained'));
    expect(complete).toHaveBeenCalledOnce(); expect(hook.result.current.hasPendingQueueItems()).toBe(false);
  });

  it.each(['stop', 'suspend', 'unmount'])('fences a late replay after %s and does not advance an old queue', async reason => {
    const completions: ((result: string) => void)[] = [];
    ports.replay.mockImplementation(() => new Promise(resolve => completions.push(resolve)));
    const hook = renderHook(() => useTtsEngine());
    act(() => hook.result.current.speak([{ text: 'Old', langCode: 'en', cachedAudio: 'old' },
      { text: 'Old second', langCode: 'en', cachedAudio: 'old second' }], 'en', 'voice.tts-click'));
    const oldSignal = ports.replay.mock.calls[0][0].signal as AbortSignal;
    await act(async () => {
      if (reason === 'stop') await hook.result.current.stopSpeaking();
      else if (reason === 'suspend') sessionActivity.setSuspended(true);
      else hook.unmount();
    });
    expect(oldSignal.aborted).toBe(true);
    if (reason !== 'unmount') {
      act(() => { sessionActivity.setSuspended(false); sessionActivity.resume();
        hook.result.current.speak([{ text: 'New', langCode: 'en', cachedAudio: 'new' }], 'en', 'voice.tts-click'); });
    }
    await act(async () => completions[0]('drained'));
    expect(ports.replay).toHaveBeenCalledTimes(reason === 'unmount' ? 1 : 2);
    if (reason !== 'unmount') expect(hook.result.current.speakingUtteranceText).toBe('New');
  });

  it('does not auto-replay the remaining queue after a renderer failure', async () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});
    ports.replay.mockRejectedValueOnce(new Error('Native voice lost'));
    const hook = renderHook(() => useTtsEngine());
    await act(async () => hook.result.current.speak([{ text: 'One', langCode: 'en', cachedAudio: 'one' },
      { text: 'Two', langCode: 'en', cachedAudio: 'two' }], 'en', 'voice.tts-click'));
    expect(ports.replay).toHaveBeenCalledOnce(); expect(hook.result.current.hasPendingQueueItems()).toBe(false);
    expect(hook.result.current.isSpeaking).toBe(false); error.mockRestore();
  });

  it('ignores an old learner recording callback after a new replay starts', async () => {
    const recordings: any[] = [];
    vi.stubGlobal('Audio', class { onended = null; onerror = null; play = vi.fn(async () => {}); pause = vi.fn();
      constructor() { recordings.push(this); } });
    ports.replay.mockImplementation(() => new Promise(() => {}));
    const hook = renderHook(() => useTtsEngine());
    act(() => hook.result.current.speak([{ text: 'Learner', langCode: 'en', speaker: 'learner', cachedAudio: 'recording' }], 'en', 'voice.tts-click'));
    const oldEnd = recordings[0].onended;
    await act(async () => hook.result.current.stopSpeaking());
    act(() => hook.result.current.speak([{ text: 'Maestro', langCode: 'en', cachedAudio: 'cached' }], 'en', 'voice.tts-click'));
    act(() => oldEnd());
    expect(hook.result.current.speakingUtteranceText).toBe('Maestro'); expect(ports.replay).toHaveBeenCalledOnce();
  });
});
