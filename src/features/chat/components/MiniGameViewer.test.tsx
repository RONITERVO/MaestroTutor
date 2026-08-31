// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import MiniGameViewer from './MiniGameViewer';
import { embedActivation } from '../embeds/embedActivation';

vi.mock('../../../shared/hooks/useAppTranslations', () => ({
  useAppTranslations: () => ({ t: (key: string) => key }),
  default: () => ({ t: (key: string) => key }),
}));

/**
 * The guarantee this file protects: scrolling a long chat full of mini-games
 * never puts more than the budgeted number of documents in the WebView, and the
 * space each one occupies is the same whether it is running or not.
 */

type EntryInit = { target: Element; ratio: number; top?: number };

class MockIntersectionObserver implements IntersectionObserver {
  static instances: MockIntersectionObserver[] = [];

  readonly root: Element | Document | null;
  readonly rootMargin = '0px';
  readonly thresholds: ReadonlyArray<number> = [0];
  private targets = new Set<Element>();

  constructor(private callback: IntersectionObserverCallback, options?: IntersectionObserverInit) {
    this.root = (options?.root as Element) ?? null;
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

const makeEntry = ({ target, ratio, top = 450 }: EntryInit): IntersectionObserverEntry => ({
  target,
  isIntersecting: ratio > 0,
  intersectionRatio: ratio,
  boundingClientRect: { top, height: 100, bottom: top + 100, left: 0, right: 100, width: 100, x: 0, y: top } as DOMRectReadOnly,
  rootBounds: { top: 0, height: 1000, bottom: 1000, left: 0, right: 400, width: 400, x: 0, y: 0 } as DOMRectReadOnly,
  intersectionRect: { top, height: 100 * ratio, bottom: top + 100 * ratio, left: 0, right: 100, width: 100, x: 0, y: top } as DOMRectReadOnly,
  time: 0,
});

const GAME_SOURCE = '<canvas id="stage" width="800" height="600"></canvas><script>void 0;</script>';

const renderGames = (count: number) => render(
  <>
    {Array.from({ length: count }, (_, index) => (
      <MiniGameViewer
        key={index}
        embedId={`msg-${index}`}
        sourceCode={GAME_SOURCE}
        variant="assistant"
      />
    ))}
  </>,
);

const settle = () => act(() => {
  vi.advanceTimersByTime(16);
  vi.advanceTimersByTime(300);
  vi.advanceTimersByTime(16);
});

describe('MiniGameViewer embed budget', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    MockIntersectionObserver.instances = [];
    vi.stubGlobal('IntersectionObserver', MockIntersectionObserver);
    embedActivation.reset();
    embedActivation.setRoot(document.body);
  });

  afterEach(() => {
    cleanup();
    embedActivation.reset();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('mounts no iframe at all until an embed wins the live slot', () => {
    const { container } = renderGames(6);
    expect(container.querySelectorAll('iframe')).toHaveLength(0);
  });

  it('mounts exactly one iframe when many mini-games are on screen', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const { container } = renderGames(10);

    const boxes = Array.from(container.querySelectorAll('.embed-box'));
    expect(boxes).toHaveLength(10);

    act(() => {
      MockIntersectionObserver.latest().emit(boxes.map((box) => ({ target: box, ratio: 1 })));
    });
    settle();

    expect(container.querySelectorAll('iframe')).toHaveLength(1);
  });

  it('does not accumulate iframes as the user scrolls past game after game', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const { container } = renderGames(8);
    const boxes = Array.from(container.querySelectorAll('.embed-box'));

    // Walk a viewport down the list: each step, one box is centred and the
    // previous one has left the screen. This is the exact motion that used to
    // leave a live iframe behind at every stop.
    for (let index = 0; index < boxes.length; index += 1) {
      act(() => {
        MockIntersectionObserver.latest().emit(
          boxes.map((box, boxIndex) => ({
            target: box,
            ratio: boxIndex === index ? 1 : 0,
          })),
        );
      });
      settle();
      expect(container.querySelectorAll('iframe').length).toBeLessThanOrEqual(1);
    }

    expect(container.querySelectorAll('iframe')).toHaveLength(1);
  });

  it('reserves the same box whether the embed is live or at rest', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const { container } = renderGames(2);
    const boxes = Array.from(container.querySelectorAll<HTMLElement>('.embed-box'));

    // 800x600 canvas in the source => a 4:3 box, reserved before anything runs.
    const reservedBefore = boxes.map((box) => box.style.getPropertyValue('--embed-ar'));
    expect(reservedBefore.every((ratio) => Math.abs(Number(ratio) - 4 / 3) < 0.01)).toBe(true);

    act(() => {
      MockIntersectionObserver.latest().emit(boxes.map((box) => ({ target: box, ratio: 1 })));
    });
    settle();

    expect(container.querySelectorAll('iframe')).toHaveLength(1);
    // Booting one of them changed nobody's reserved box: no layout shift.
    const reservedAfter = boxes.map((box) => box.style.getPropertyValue('--embed-ar'));
    expect(reservedAfter).toEqual(reservedBefore);
  });

  it('tears the iframe down again once the embed scrolls out of view', () => {
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
    const { container } = renderGames(1);
    const box = container.querySelector('.embed-box')!;

    act(() => {
      MockIntersectionObserver.latest().emit([{ target: box, ratio: 1 }]);
    });
    settle();
    expect(container.querySelectorAll('iframe')).toHaveLength(1);

    act(() => {
      MockIntersectionObserver.latest().emit([{ target: box, ratio: 0 }]);
    });
    settle();

    expect(container.querySelectorAll('iframe')).toHaveLength(0);
    // The box it left behind is still exactly as tall as it was.
    expect(container.querySelectorAll('.embed-box')).toHaveLength(1);
  });
});
