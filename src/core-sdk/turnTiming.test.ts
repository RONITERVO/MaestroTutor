// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createTurnTimingRecorder } from './turnTiming';

afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); });

describe('Core turn timing recorder', () => {
  it('isolates clients without consulting browser storage', () => {
    vi.useFakeTimers();
    const getItem = vi.fn(() => { throw new Error('browser storage must not be read'); });
    vi.stubGlobal('localStorage', { getItem });
    const first = createTurnTimingRecorder();
    const second = createTurnTimingRecorder();
    first.beginTurnTiming('first', () => 0).linkGateway('gateway');
    expect(JSON.parse(second.exportTurnTimings()).reports).toEqual([]);
    expect(JSON.parse(first.exportTurnTimings()).reports[0]).toMatchObject({ turnId: 'first', gatewaySessionId: 'gateway' });
    first.flushTurnTimings();
    expect(getItem).not.toHaveBeenCalled();
  });

  it('reads the legacy schema and debounces writes using the original storage key', () => {
    vi.useFakeTimers();
    const saved = [{ turnId: 'old', startedAt: '2026-01-01', clock: 'browser-monotonic',
      events: [{ name: 'audio.first', elapsedMs: 1 }, { name: 'bad event!', elapsedMs: 2 }] }];
    const storage = { getItem: vi.fn((_key: string) => JSON.stringify(saved)), setItem: vi.fn((_key: string, _value: string) => {}) };
    const timing = createTurnTimingRecorder({ storage: () => storage });
    expect(JSON.parse(timing.exportTurnTimings()).reports[0]).toMatchObject({ clock: 'client-monotonic', events: [{ name: 'audio.first', elapsedMs: 1 }] });
    const turn = timing.beginTurnTiming('new', () => 0);
    for (let i = 0; i < 50; i++) turn.markLatest('speech.last');
    vi.advanceTimersByTime(999);
    expect(storage.setItem).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(storage.getItem).toHaveBeenCalledOnce();
    expect(storage.getItem).toHaveBeenCalledWith('maestro.turn-timings.v1');
    expect(storage.setItem).toHaveBeenCalledOnce();
    expect(storage.setItem.mock.calls[0][0]).toBe('maestro.turn-timings.v1');
    timing.clearTurnTimings();
    expect(storage.setItem).toHaveBeenLastCalledWith('maestro.turn-timings.v1', '[]');
  });
});
