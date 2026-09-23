// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, expect, it, vi } from 'vitest';
import { createLiveModelAudio } from './modelAudio';
import { createLiveProviderCallbacks } from './providerCallbacks';
import { createLiveSessionState } from './state';
import { createLiveTranscripts } from './transcripts';

afterEach(() => vi.useRealTimers());
it('delivers the pending transcript before turn persistence can remove its temporary bubble', async () => {
  vi.useFakeTimers();
  const events: string[] = [];
  const state = createLiveSessionState({
    onTurnTranscriptUpdate: update => { events.push(`transcript:${update.modelText}`); },
    onTurnComplete: () => { events.push('complete'); },
  });
  state.currentSessionIdRef.current = 1;
  state.currentModelAudioTurnIdRef.current = 1;
  const timers = { setTimeout: (fn: () => void, ms: number) => setTimeout(fn, ms) as unknown as number, clearTimeout: (id: number) => clearTimeout(id) };
  const transcripts = createLiveTranscripts(state, timers);
  const callbacks = createLiveProviderCallbacks(state, {
    activity: { setVadActivity: vi.fn(), updateState: vi.fn() },
    audio: createLiveModelAudio(state, { ...timers, createCodecWorker: vi.fn() }),
    transcripts, cleanup: async () => {}, getAudioTelemetrySnapshot: vi.fn(),
    debugLogService: { logRequest: () => ({ complete: vi.fn() }) } as any,
  }, { sessionId: 1, playModelAudio: false, emitTurns: true, observerActivity: false, usageTracker: { flush: vi.fn(), trackSnapshot: vi.fn(), completeTurn: vi.fn() } });
  callbacks.onmessage({ serverContent: { outputTranscription: { text: 'Answer' }, turnComplete: true } } as any);
  await vi.advanceTimersByTimeAsync(1490);
  // A pending local thinking/transcript update can race the finalizer's quiet timer.
  state.currentOutputTranscriptionRef.current = 'Final answer';
  transcripts.emitTurnTranscriptUpdate('output');
  await vi.advanceTimersByTimeAsync(10);
  expect(events).toEqual(['transcript:Answer', 'transcript:Final answer', 'complete']);
  await vi.advanceTimersByTimeAsync(60);
  expect(events).toEqual(['transcript:Answer', 'transcript:Final answer', 'complete']);
});
