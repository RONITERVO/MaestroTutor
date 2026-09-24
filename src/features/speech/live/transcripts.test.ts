// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, expect, it, vi } from 'vitest';
import { createLiveTranscripts } from './transcripts';
import { createLiveSessionState } from './state';

afterEach(() => vi.useRealTimers());
it('retains distinct thinking entries even when their joined text is identical', async () => {
  vi.useFakeTimers();
  const update = vi.fn();
  const state = createLiveSessionState({ onTurnTranscriptUpdate: update });
  const transcripts = createLiveTranscripts(state, {
    setTimeout: (fn, ms) => setTimeout(fn, ms) as unknown as number,
    clearTimeout: id => clearTimeout(id),
  });
  state.currentModelThinkingTraceRef.current = ['a\nb'];
  transcripts.emitTurnTranscriptUpdate('output'); await vi.advanceTimersByTimeAsync(60);
  state.currentModelThinkingTraceRef.current = ['a', 'b'];
  transcripts.emitTurnTranscriptUpdate('output'); await vi.advanceTimersByTimeAsync(60);
  expect(update).toHaveBeenCalledTimes(2);
  expect(update.mock.calls[1][0].thinkingTrace).toEqual(['a', 'b']);
});
