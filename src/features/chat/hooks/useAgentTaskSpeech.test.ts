// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useAgentTaskSpeech, taskSpeechReady } from './useAgentTaskSpeech';
import { publishRoomTaskResult } from '../services/roomTaskResults';
import { useMaestroStore, initialSettings } from '../../../store';
import { sessionActivity } from '../../../platform/browser/sessionActivity';
const result = { id: 'task', conversationId: 'pair', valid: async () => true };
const advance = () => act(async () => { await vi.advanceTimersByTimeAsync(100); });
const patch = (updates: Partial<ReturnType<typeof useMaestroStore.getState>>) => act(() => { useMaestroStore.setState(updates); });
function setup() {
  let playing = false;
  const resume = vi.fn();
  const config = { enabled: true, speakMessage: vi.fn(() => { playing = true; }), stopSpeaking: vi.fn(() => { playing = false; }),
    hasPendingQueueItems: () => playing, pauseLiveForSpeech: vi.fn(async () => resume), pauseObserverForSpeech: vi.fn(async () => true) };
  const hook = renderHook(() => useAgentTaskSpeech(config));
  return { ...hook, config, resume, finish: () => { playing = false; } };
}
beforeEach(() => {
  vi.useFakeTimers(); vi.spyOn(document, 'hidden', 'get').mockReturnValue(false); sessionActivity.resume();
  useMaestroStore.setState({ settings: { ...initialSettings, selectedLanguagePairId: 'pair' }, isLoadingHistory: false,
    activityTokens: new Set(), liveSessionState: 'idle', silentObserverState: 'armed',
    messages: [{ id: 'task', role: 'assistant', timestamp: 1, text: 'Ready', agentTask: { id: 'task', phase: 'completed', note: 'Ready' } }] });
});
afterEach(async () => { cleanup(); sessionActivity.setSuspended(false); await Promise.resolve(); sessionActivity.resume(); vi.restoreAllMocks(); vi.useRealTimers(); });
describe('task results use the existing speech path', () => {
  it('does not speak saved history on mount; a fresh completion speaks once after idle monitoring yields', async () => {
    const h = setup(); await advance(); expect(h.config.speakMessage).not.toHaveBeenCalled();
    publishRoomTaskResult(result); publishRoomTaskResult(result); await advance();
    expect(h.config.pauseLiveForSpeech).toHaveBeenCalledOnce(); expect(h.config.pauseObserverForSpeech).toHaveBeenCalledOnce();
    expect(h.config.speakMessage).toHaveBeenCalledExactlyOnceWith(useMaestroStore.getState().messages[0]);
    expect([...useMaestroStore.getState().activityTokens].some(token => token.startsWith('tts:agent-result-'))).toBe(true);
    await advance(); expect(h.resume).not.toHaveBeenCalled();
    h.finish(); await advance(); expect(h.resume).toHaveBeenCalledOnce();
    expect(useMaestroStore.getState().activityTokens.size).toBe(0);
  });
  it.each(['tts:speak', 'stt:listen', 'live:session', 'live:observer-session', 'vad:active', 'vad:observer-active', 'whisper:checking', 'ui:hold', 'ui:video-play', 'gen:response'])('waits without claiming audio while %s owns activity', async token => {
    const h = setup(); patch({ activityTokens: new Set([token]) }); publishRoomTaskResult(result); await advance();
    expect(h.config.pauseLiveForSpeech).not.toHaveBeenCalled(); expect(h.config.speakMessage).not.toHaveBeenCalled();
    patch({ activityTokens: new Set() }); await advance(); expect(h.config.speakMessage).toHaveBeenCalledOnce();
  });
  it('allows background agent/suggestion work and idle observer monitoring', () => {
    patch({ activityTokens: new Set(['agent:task', 'gen:suggestions', 'vad:observer-listen', 'whisper:observer-loading']) });
    expect(taskSpeechReady()).toBe(true);
  });
  it.each(['conversation', 'source', 'disabled', 'suspend'])('cancels queued speech on %s loss without late playback', async reason => {
    const h = setup(); patch({ activityTokens: new Set(['tts:speak']) }); publishRoomTaskResult(result); await advance();
    if (reason === 'conversation') patch({ settings: { ...initialSettings, selectedLanguagePairId: 'different' } });
    if (reason === 'source') patch({ messages: [] });
    if (reason === 'disabled') { h.config.enabled = false; h.rerender(); }
    if (reason === 'suspend') act(() => { sessionActivity.setSuspended(true); });
    patch({ activityTokens: new Set() }); await advance(); expect(h.config.speakMessage).not.toHaveBeenCalled();
    expect([...useMaestroStore.getState().activityTokens].some(token => token.startsWith('tts:agent-result-'))).toBe(false);
  });
  it('manual Stop cancels current and queued speech and returns its audio reservation', async () => {
    const h = setup(); publishRoomTaskResult(result); await advance(); await act(async () => { await h.result.current(); });
    expect(h.config.stopSpeaking).toHaveBeenCalledOnce(); expect(h.resume).toHaveBeenCalledOnce();
    expect([...useMaestroStore.getState().activityTokens].some(token => token.startsWith('tts:agent-result-'))).toBe(false);
    publishRoomTaskResult(result); await advance(); expect(h.config.speakMessage).toHaveBeenCalledOnce();
  });
});

it('late cleanup from an unmounted delivery cannot remove a new audio reservation', async () => {
  const old = setup(); let finish!: () => void;
  const stopped = new Promise<void>(done => { finish = done; });
  old.config.stopSpeaking.mockImplementation(() => stopped as any);
  publishRoomTaskResult(result); await advance(); old.unmount();
  const current = setup(); publishRoomTaskResult({ ...result, id: 'new-task' });
  patch({ messages: [{ id: 'new-task', role: 'assistant', timestamp: 2, text: 'New result', agentTask: { id: 'new-task', phase: 'completed', note: 'Ready' } }] });
  await advance(); expect(current.config.speakMessage).toHaveBeenCalledOnce();
  const token = [...useMaestroStore.getState().activityTokens].find(item => item.startsWith('tts:agent-result-'));
  expect(token).toBeTruthy(); await act(async () => { finish(); });
  expect(useMaestroStore.getState().activityTokens.has(token!)).toBe(true);
});
