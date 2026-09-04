import { describe, expect, it, vi } from 'vitest';

describe('turn timing evidence', () => {
  it('sorts latest-event timestamps and clears persisted diagnostics', async () => {
    vi.resetModules();
    const timing = await import('./turnTiming');
    timing.clearTurnTimings();
    let now = 0;
    const turn = timing.beginTurnTiming('ordered', () => now);
    turn.markLatest('speech.last');
    now = 10;
    turn.mark('input.first');
    now = 20;
    turn.markLatest('speech.last');
    const events = JSON.parse(timing.exportTurnTimings()).reports[0].events;
    expect(events.map((event: {elapsedMs: number}) => event.elapsedMs)).toEqual([0, 10, 20]);
    timing.clearTurnTimings();
    expect(JSON.parse(timing.exportTurnTimings()).reports).toEqual([]);
  });
  it('uses elapsed monotonic time, deduplicates first-output events, and persists across reloads', async () => {
    vi.resetModules();
    const values = new Map<string, string>();
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => values.get(key) ?? null,
      setItem: (key: string, value: string) => values.set(key, value),
    });
    try {
      const timing = await import('./turnTiming');
      let now = 100;
      const turn = timing.beginTurnTiming('test-turn', () => now);
      now = 175;
      turn.markOnce('audio.first', { queueMs: 42, invalid: Infinity });
      now = 500;
      turn.markOnce('audio.first');
      timing.flushTurnTimings();
      vi.resetModules();
      const restored = await import('./turnTiming');
      const report = JSON.parse(restored.exportTurnTimings()).reports[0];
      expect(report.events).toEqual([
        { name: 'turn.started', elapsedMs: 0 },
        { name: 'audio.first', elapsedMs: 75, metrics: { queueMs: 42 } },
      ]);
    } finally { vi.unstubAllGlobals(); }
  });

  it('keeps diagnostics bounded and tolerates unavailable storage', async () => {
    vi.resetModules();
    vi.stubGlobal('localStorage', {
      getItem: () => { throw new Error('denied'); },
      setItem: () => { throw new Error('quota'); },
    });
    try {
      const timing = await import('./turnTiming');
      for (let i = 0; i < 40; i++) {
        const turn = timing.beginTurnTiming(`turn-${i}`, () => 0);
        for (let j = 0; j < 200; j++) turn.mark('speech.activity');
      }
      expect(() => timing.flushTurnTimings()).not.toThrow();
      const reports = JSON.parse(timing.exportTurnTimings()).reports;
      expect(reports).toHaveLength(30);
      expect(reports.every((report: { events: unknown[] }) => report.events.length === 120)).toBe(true);
    } finally { vi.unstubAllGlobals(); }
  });
});
