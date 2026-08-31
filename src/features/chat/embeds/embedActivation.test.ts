// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { embedActivation } from './embedActivation';
import type { EmbedPhase } from './embedTypes';

/**
 * These tests are the regression net for the memory guarantee: no matter how
 * the user scrolls, the number of live embeds stays inside the budget. That is
 * the property Play's memory thresholds actually depend on.
 */

type EntryInit = { target: Element; ratio: number; top?: number };

class MockIntersectionObserver implements IntersectionObserver {
  static instances: MockIntersectionObserver[] = [];

  readonly root: Element | Document | null;
  readonly rootMargin = '0px';
  readonly thresholds: ReadonlyArray<number>;
  private targets = new Set<Element>();

  constructor(private callback: IntersectionObserverCallback, options?: IntersectionObserverInit) {
    this.root = (options?.root as Element) ?? null;
    this.thresholds = (options?.threshold as number[]) ?? [0];
    MockIntersectionObserver.instances.push(this);
  }

  observe(target: Element): void { this.targets.add(target); }
  unobserve(target: Element): void { this.targets.delete(target); }
  disconnect(): void { this.targets.clear(); }
  takeRecords(): IntersectionObserverEntry[] { return []; }

  emit(entries: EntryInit[]): void {
    const observed = entries.filter((entry) => this.targets.has(entry.target));
    if (observed.length === 0) return;
    this.callback(observed.map(makeEntry), this);
  }

  static latest(): MockIntersectionObserver {
    const instance = MockIntersectionObserver.instances[MockIntersectionObserver.instances.length - 1];
    if (!instance) throw new Error('no IntersectionObserver was created');
    return instance;
  }
}

/** Root is a 1000px-tall viewport; each embed is 100px tall at `top`. */
const makeEntry = ({ target, ratio, top = 450 }: EntryInit): IntersectionObserverEntry => ({
  target,
  isIntersecting: ratio > 0,
  intersectionRatio: ratio,
  boundingClientRect: { top, height: 100, bottom: top + 100, left: 0, right: 100, width: 100, x: 0, y: top } as DOMRectReadOnly,
  rootBounds: { top: 0, height: 1000, bottom: 1000, left: 0, right: 400, width: 400, x: 0, y: 0 } as DOMRectReadOnly,
  intersectionRect: { top, height: 100 * ratio, bottom: top + 100 * ratio, left: 0, right: 100, width: 100, x: 0, y: top } as DOMRectReadOnly,
  time: 0,
});

interface Harness {
  id: string;
  element: HTMLDivElement;
  phases: EmbedPhase[];
  phase: () => EmbedPhase;
  unregister: () => void;
}

const registerEmbed = (id: string): Harness => {
  const element = document.createElement('div');
  document.body.appendChild(element);
  const phases: EmbedPhase[] = [];
  const unregister = embedActivation.register({
    id,
    kind: 'mini-game',
    element,
    onPhase: (phase) => phases.push(phase),
  });
  return { id, element, phases, phase: () => phases[phases.length - 1] ?? 'placeholder', unregister };
};

/** Flush the manager's rAF coalescing plus its promotion dwell. */
const settle = () => {
  vi.advanceTimersByTime(16);
  vi.advanceTimersByTime(300);
  vi.advanceTimersByTime(16);
};

describe('embedActivation', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    MockIntersectionObserver.instances = [];
    vi.stubGlobal('IntersectionObserver', MockIntersectionObserver);
    embedActivation.reset();
    embedActivation.setRoot(document.body);
  });

  afterEach(() => {
    embedActivation.reset();
    vi.unstubAllGlobals();
    vi.useRealTimers();
    document.body.innerHTML = '';
  });

  it('keeps at most one embed live however many are visible', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const embeds = ['a', 'b', 'c', 'd'].map(registerEmbed);

    MockIntersectionObserver.latest().emit(embeds.map((embed) => ({ target: embed.element, ratio: 1 })));
    settle();

    expect(embeds.filter((embed) => embed.phase() === 'live')).toHaveLength(1);
    expect(embedActivation.debugSnapshot().live).toHaveLength(1);
  });

  it('honours a raised budget on a stronger device', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 2 });
    const embeds = ['a', 'b', 'c'].map(registerEmbed);

    MockIntersectionObserver.latest().emit(embeds.map((embed) => ({ target: embed.element, ratio: 1 })));
    settle();

    expect(embedActivation.debugSnapshot().live).toHaveLength(2);
  });

  it('gives the slot to the embed nearest the centre of the viewport', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const edge = registerEmbed('edge');
    const centre = registerEmbed('centre');

    MockIntersectionObserver.latest().emit([
      { target: edge.element, ratio: 1, top: 20 },
      { target: centre.element, ratio: 1, top: 450 },
    ]);
    settle();

    expect(centre.phase()).toBe('live');
    expect(edge.phase()).toBe('placeholder');
  });

  it('frees an embed the moment it leaves the viewport, without waiting for a dwell', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const embed = registerEmbed('a');

    MockIntersectionObserver.latest().emit([{ target: embed.element, ratio: 1 }]);
    settle();
    expect(embed.phase()).toBe('live');

    MockIntersectionObserver.latest().emit([{ target: embed.element, ratio: 0 }]);
    // No timer advance: demotion is immediate because nobody can see it happen.
    expect(embed.phase()).toBe('placeholder');
  });

  it('lets an engaged embed outrank a better-placed one', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const playing = registerEmbed('playing');
    const centre = registerEmbed('centre');

    MockIntersectionObserver.latest().emit([
      { target: playing.element, ratio: 0.5, top: 20 },
      { target: centre.element, ratio: 1, top: 450 },
    ]);
    settle();
    expect(centre.phase()).toBe('live');

    embedActivation.setPinned('playing', true);
    expect(playing.phase()).toBe('live');
    expect(centre.phase()).toBe('placeholder');
  });

  it('keeps an engaged embed alive across a glance away when nothing else wants the slot', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const playing = registerEmbed('playing');

    MockIntersectionObserver.latest().emit([{ target: playing.element, ratio: 1 }]);
    settle();
    embedActivation.setPinned('playing', true);
    expect(playing.phase()).toBe('live');

    // Scrolled up to read the previous message. Coming back must not restart it.
    MockIntersectionObserver.latest().emit([{ target: playing.element, ratio: 0 }]);
    settle();
    expect(playing.phase()).toBe('live');

    MockIntersectionObserver.latest().emit([{ target: playing.element, ratio: 1 }]);
    settle();
    expect(playing.phase()).toBe('live');
  });

  it('treats engagement as exclusive, so the last thing asked for wins', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const first = registerEmbed('first');
    const second = registerEmbed('second');

    MockIntersectionObserver.latest().emit([
      { target: first.element, ratio: 1, top: 450 },
      { target: second.element, ratio: 1, top: 780 },
    ]);
    settle();
    expect(first.phase()).toBe('live');

    embedActivation.setPinned('first', true);
    embedActivation.setPinned('second', true);

    // Two pins competing for one slot would otherwise resolve on an arbitrary
    // tie-break rather than on what the user last asked for.
    expect(second.phase()).toBe('live');
    expect(first.phase()).toBe('placeholder');
  });

  it('holds an explicitly engaged embed against a higher-scoring neighbour', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const centre = registerEmbed('centre');
    const edge = registerEmbed('edge');

    MockIntersectionObserver.latest().emit([
      { target: centre.element, ratio: 1, top: 450 },
      { target: edge.element, ratio: 1, top: 780 },
    ]);
    settle();
    expect(centre.phase()).toBe('live');

    embedActivation.setPinned('edge', true);
    expect(edge.phase()).toBe('live');

    // Further arbitration passes must not quietly hand the slot back.
    MockIntersectionObserver.latest().emit([
      { target: centre.element, ratio: 1, top: 450 },
      { target: edge.element, ratio: 1, top: 780 },
    ]);
    settle();

    expect(edge.phase()).toBe('live');
    expect(centre.phase()).toBe('placeholder');
  });

  it('eventually releases an engaged embed that was left off screen', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const playing = registerEmbed('playing');

    MockIntersectionObserver.latest().emit([{ target: playing.element, ratio: 1 }]);
    settle();
    embedActivation.setPinned('playing', true);

    MockIntersectionObserver.latest().emit([{ target: playing.element, ratio: 0 }]);
    settle();
    expect(playing.phase()).toBe('live');

    // Forgotten rather than glanced away from: the grace period lapses and the
    // document is released instead of being held for the rest of the session.
    vi.advanceTimersByTime(25_000);
    MockIntersectionObserver.latest().emit([{ target: playing.element, ratio: 0 }]);
    settle();

    expect(playing.phase()).toBe('placeholder');
  });

  it('yields an engaged embed to a visible one, so engagement never costs a needed slot', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const playing = registerEmbed('playing');
    const other = registerEmbed('other');

    MockIntersectionObserver.latest().emit([{ target: playing.element, ratio: 1 }]);
    settle();
    embedActivation.setPinned('playing', true);
    expect(playing.phase()).toBe('live');

    MockIntersectionObserver.latest().emit([
      { target: playing.element, ratio: 0 },
      { target: other.element, ratio: 1 },
    ]);
    settle();

    expect(playing.phase()).toBe('placeholder');
    expect(other.phase()).toBe('live');
  });

  it('shows a poster instead of nothing once an embed has been captured', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1, posterBudget: 4 });
    const embed = registerEmbed('a');

    MockIntersectionObserver.latest().emit([{ target: embed.element, ratio: 1 }]);
    settle();

    embedActivation.setPoster('a', 'blob:poster-a');
    MockIntersectionObserver.latest().emit([{ target: embed.element, ratio: 0 }]);

    expect(embed.phase()).toBe('frozen');
    expect(embedActivation.getPoster('a')).toBe('blob:poster-a');
  });

  it('evicts and revokes posters beyond the budget', () => {
    const revoke = vi.fn();
    vi.stubGlobal('URL', { ...URL, revokeObjectURL: revoke });
    embedActivation.setBudgets({ posterBudget: 2 });

    embedActivation.setPoster('a', 'blob:a');
    embedActivation.setPoster('b', 'blob:b');
    embedActivation.setPoster('c', 'blob:c');

    expect(revoke).toHaveBeenCalledWith('blob:a');
    expect(embedActivation.getPoster('a')).toBeUndefined();
    expect(embedActivation.debugSnapshot().posters).toBe(2);
  });

  it('refuses posters outright when the budget is zero, as on the low tier', () => {
    const revoke = vi.fn();
    vi.stubGlobal('URL', { ...URL, revokeObjectURL: revoke });
    embedActivation.setBudgets({ posterBudget: 0 });

    embedActivation.setPoster('a', 'blob:a');

    expect(embedActivation.postersEnabled()).toBe(false);
    expect(embedActivation.getPoster('a')).toBeUndefined();
    expect(revoke).toHaveBeenCalledWith('blob:a');
  });

  it('releases the slot when an embed unmounts', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const first = registerEmbed('a');
    const second = registerEmbed('b');

    MockIntersectionObserver.latest().emit([
      { target: first.element, ratio: 1, top: 450 },
      { target: second.element, ratio: 1, top: 20 },
    ]);
    settle();
    expect(first.phase()).toBe('live');

    first.unregister();
    MockIntersectionObserver.latest().emit([{ target: second.element, ratio: 1, top: 450 }]);
    settle();

    expect(second.phase()).toBe('live');
    expect(embedActivation.debugSnapshot().live).toEqual(['b']);
  });

  it('survives a re-register of the same id without tearing down the live embed', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const embed = registerEmbed('a');

    MockIntersectionObserver.latest().emit([{ target: embed.element, ratio: 1 }]);
    settle();
    expect(embed.phase()).toBe('live');

    // React StrictMode double-invokes effects; a re-register must not reboot.
    const phases: EmbedPhase[] = [];
    embedActivation.register({
      id: 'a',
      kind: 'mini-game',
      element: embed.element,
      onPhase: (phase) => phases.push(phase),
    });

    expect(phases).toEqual(['live']);
  });

  it('still functions where IntersectionObserver is unavailable', () => {
    embedActivation.reset();
    vi.stubGlobal('IntersectionObserver', undefined);
    embedActivation.setRoot(document.body);
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });

    const first = registerEmbed('a');
    const second = registerEmbed('b');
    settle();

    // Falls back to registration order rather than rendering nothing at all.
    expect(first.phase()).toBe('live');
    expect(second.phase()).toBe('placeholder');
    expect(embedActivation.debugSnapshot().observing).toBe(false);
  });
});
