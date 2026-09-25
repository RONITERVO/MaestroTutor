// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
const ports = vi.hoisted(() => ({ stream: vi.fn() }));
vi.mock('../services/geminiLiveTts', () => ({ streamGeminiLiveTts: ports.stream }));
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
    act(() => hook.result.current.speak([{ text: 'Cached speech', langCode: 'en', cachedAudio: 'data:audio/wav;base64,AAAA' }], 'en', 'voice.tts-click'));
    expect(play).toHaveBeenCalledOnce();
    await act(async () => { sessionActivity.setSuspended(true); });
    expect(pause).toHaveBeenCalledOnce();
    act(() => { sessionActivity.setSuspended(false); sessionActivity.resume(); });
    expect(play).toHaveBeenCalledOnce(); expect(hook.result.current.hasPendingQueueItems()).toBe(false);
  });
});
