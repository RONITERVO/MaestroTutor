// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
const ports = vi.hoisted(() => ({ live: vi.fn(), start: vi.fn(), stop: vi.fn(), video: vi.fn(), instruction: vi.fn() }));
vi.mock('../../speech', () => ({ useGeminiLiveConversation: (callbacks: any) => {
  ports.live(callbacks); return { start: ports.start, stop: ports.stop, updateVideoInput: ports.video };
} }));
vi.mock('../../chat', () => ({ prepareLiveRoomAgentContext: vi.fn() }));
vi.mock('../utils/liveSystemInstruction', () => ({ buildLiveSystemInstruction: ports.instruction }));
import { useSilentObserverController } from './useSilentObserverController';
import { useAgentTaskSpeech } from '../../chat/hooks/useAgentTaskSpeech';
import { publishRoomTaskResult } from '../../chat/services/roomTaskResults';
import { initialSettings, useMaestroStore } from '../../../store';
import { selectBlocksSilentObserver } from '../../../store/slices/uiSlice';
import { sessionActivity } from '../../../platform/browser/sessionActivity';
const callbacks = () => ports.live.mock.calls[ports.live.mock.calls.length - 1][0];
const advance = () => act(async () => { await vi.advanceTimersByTimeAsync(100); });
const result = { id: 'task', conversationId: 'pair', valid: async () => true };
beforeEach(() => {
  vi.useFakeTimers(); vi.resetAllMocks();
  vi.spyOn(document, 'hasFocus').mockReturnValue(true); vi.spyOn(document, 'hidden', 'get').mockReturnValue(false);
  sessionActivity.resume();
  ports.instruction.mockResolvedValue('Original instruction');
  ports.start.mockImplementation(async () => callbacks().onStateChange('armed'));
  ports.stop.mockImplementation(async () => callbacks().onStateChange('idle'));
  useMaestroStore.setState({ activityTokens: new Set(), silentObserverState: 'idle', silentObserverError: null,
    liveSessionState: 'idle', isLoadingHistory: false, settings: { ...initialSettings, selectedLanguagePairId: 'pair' },
    messages: [{ id: 'task', role: 'assistant', timestamp: 1, text: 'Ready', agentTask: { id: 'task', phase: 'completed', note: 'Ready' } }] });
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.useRealTimers(); });
function setup() {
  let playing = false;
  const speak = vi.fn(() => { playing = true; });
  const pauseLive = async () => () => {};
  const config = { enabled: true, liveSessionState: 'idle' as const, liveVideoStream: null,
    visualContextVideoRef: { current: null }, currentSystemPromptText: '', resolveBookmarkContextSummary: () => null,
    computeHistorySubsetForMedia: (messages: any[]) => messages };
  const hook = renderHook(() => {
    const blocks = useMaestroStore(selectBlocksSilentObserver);
    const observer = useSilentObserverController({ ...config, isBlockingActivity: blocks });
    useAgentTaskSpeech({ enabled: true, speakMessage: speak, stopSpeaking: () => { playing = false; },
      hasPendingQueueItems: () => playing, pauseLiveForSpeech: pauseLive, pauseObserverForSpeech: observer.pauseObserverForSpeech });
    return observer;
  });
  return { ...hook, speak, finish: () => { playing = false; } };
}
it('pauses an armed observer until result playback drains, then re-arms without a manual-stop hold', async () => {
  const h = setup(); await advance();
  expect(ports.start).toHaveBeenCalledOnce(); expect(useMaestroStore.getState().silentObserverState).toBe('armed');
  publishRoomTaskResult(result); await advance();
  expect(h.speak).toHaveBeenCalledOnce(); expect(useMaestroStore.getState().silentObserverState).toBe('idle');
  await advance(); expect(ports.start).toHaveBeenCalledOnce();
  h.finish(); await advance();
  expect(ports.start).toHaveBeenCalledTimes(2); expect(useMaestroStore.getState().silentObserverState).toBe('armed');
});
it('waits for the active observer turn to finish instead of stopping it', async () => {
  const h = setup(); await advance();
  act(() => callbacks().onStateChange('active')); const stops = ports.stop.mock.calls.length;
  publishRoomTaskResult(result); await advance();
  expect(h.speak).not.toHaveBeenCalled(); expect(ports.stop).toHaveBeenCalledTimes(stops);
  act(() => callbacks().onStateChange('armed')); await advance();
  expect(h.speak).toHaveBeenCalledOnce();
});
it('does not speak when the observer fails to release input', async () => {
  const h = setup(); await advance(); ports.stop.mockRejectedValue(new Error('Input cleanup failed'));
  publishRoomTaskResult(result); await advance();
  expect(h.speak).not.toHaveBeenCalled();
  expect([...useMaestroStore.getState().activityTokens].some(token => token.startsWith('tts:agent-result-'))).toBe(false);
  ports.stop.mockImplementation(async () => callbacks().onStateChange('idle')); await advance();
  expect(h.speak).toHaveBeenCalledOnce();
});
it('cancels an in-flight observer instruction build before audio ownership is granted', async () => {
  let resolve!: (value: string) => void;
  ports.instruction.mockReturnValue(new Promise<string>(done => { resolve = done; }));
  const h = setup(); publishRoomTaskResult(result); await advance();
  expect(h.speak).toHaveBeenCalledOnce();
  await act(async () => { resolve('Late instruction'); });
  expect(ports.start).not.toHaveBeenCalled();
  h.finish(); await advance(); expect(ports.start).toHaveBeenCalledOnce();
});
