// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useRef } from 'react';
import MiniGameViewer from '../components/MiniGameViewer';
import { useEmbedViewport } from './useEmbedViewport';
import { embedActivation } from './embedActivation';
import { DEVICE_BUDGETS } from '../../../store/slices/hardwareSlice';

const translations = { t: (key: string) => key };
vi.mock('../../../shared/hooks/useAppTranslations', () => ({
  useAppTranslations: () => translations,
  default: () => translations,
}));

/**
 * Mount-order fidelity.
 *
 * The other embed tests point the manager at a root *before* rendering, which is
 * the opposite of what the app does: React runs child effects before parent
 * ones, so every embed registers before ChatInterface has set the scroll root.
 * That ordering is where the interesting failures live, so this file reproduces
 * it exactly — a parent that owns the viewport wiring, children that register
 * on their own.
 */

interface Placement { top: number; ratio: number }

/**
 * Behaves like the real thing in the ways that matter: it delivers an initial
 * entry for every newly observed target (asynchronously, as browsers do) rather
 * than waiting to be driven, and it reports against the root it was given.
 */
class FakeIntersectionObserver implements IntersectionObserver {
  static instances: FakeIntersectionObserver[] = [];
  static placement = new Map<Element, Placement>();

  readonly root: Element | Document | null;
  readonly rootMargin = '0px';
  readonly thresholds: ReadonlyArray<number>;
  private targets = new Set<Element>();

  constructor(private callback: IntersectionObserverCallback, options?: IntersectionObserverInit) {
    this.root = (options?.root as Element) ?? null;
    this.thresholds = (options?.threshold as number[]) ?? [0];
    FakeIntersectionObserver.instances.push(this);
  }

  observe(target: Element): void {
    this.targets.add(target);
    // Real observers deliver an initial record for a newly observed target.
    queueMicrotask(() => {
      if (this.targets.has(target)) this.deliver([target]);
    });
  }

  unobserve(target: Element): void { this.targets.delete(target); }
  disconnect(): void { this.targets.clear(); }
  takeRecords(): IntersectionObserverEntry[] { return []; }

  /** Re-report everything currently observed, as a scroll would. */
  reportAll(): void { this.deliver([...this.targets]); }

  private deliver(targets: Element[]): void {
    const entries = targets.map((target) => {
      const { top, ratio } = FakeIntersectionObserver.placement.get(target) ?? { top: 400, ratio: 1 };
      return {
        target,
        isIntersecting: ratio > 0,
        intersectionRatio: ratio,
        boundingClientRect: { top, height: 200, bottom: top + 200 } as DOMRectReadOnly,
        rootBounds: { top: 0, height: 1000, bottom: 1000 } as DOMRectReadOnly,
        intersectionRect: { top, height: 200 * ratio } as DOMRectReadOnly,
        time: 0,
      };
    }) as unknown as IntersectionObserverEntry[];
    if (entries.length) this.callback(entries, this);
  }

  static live(): FakeIntersectionObserver | undefined {
    // The manager rebuilds its observer when the root changes; only the most
    // recent one has any targets.
    return [...FakeIntersectionObserver.instances].reverse().find((o) => o.root !== null);
  }
}

const GAME = '<canvas id="stage" width="800" height="600"></canvas><script>void 0;</script>';

/** Stands in for ChatInterface: owns the scroll root, renders embeds as children. */
const Chat: React.FC<{ ids: string[] }> = ({ ids }) => {
  const scrollRef = useRef<HTMLDivElement>(null);
  useEmbedViewport(scrollRef, DEVICE_BUDGETS.mid);
  return (
    <div ref={scrollRef} style={{ overflowY: 'auto' }}>
      {ids.map((id) => (
        <MiniGameViewer key={id} embedId={id} sourceCode={GAME} variant="assistant" />
      ))}
    </div>
  );
};

const settle = async () => {
  await act(async () => { await Promise.resolve(); });
  act(() => { vi.advanceTimersByTime(400); });
  await act(async () => { await Promise.resolve(); });
};

describe('embed activation under real mount order', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    FakeIntersectionObserver.instances = [];
    FakeIntersectionObserver.placement = new Map();
    vi.stubGlobal('IntersectionObserver', FakeIntersectionObserver);
    embedActivation.reset();
  });

  afterEach(() => {
    cleanup();
    embedActivation.reset();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('auto-runs a visible embed without the user having to ask', async () => {
    const { container } = render(<Chat ids={['a']} />);

    const box = container.querySelector('.embed-box')!;
    FakeIntersectionObserver.placement.set(box, { top: 400, ratio: 1 });

    await settle();

    // Nothing was tapped. A visible artifact has to start on its own.
    expect(container.querySelectorAll('iframe')).toHaveLength(1);
  });

  it('still runs something when the observer never delivers an entry', async () => {
    // The failure this guards against is total inertness: nothing auto-runs,
    // artifacts only start when tapped, and an engaged one is never reported
    // off screen so it holds the slot for the session.
    class SilentObserver {
      observe() {}
      unobserve() {}
      disconnect() {}
      takeRecords() { return []; }
    }
    vi.stubGlobal('IntersectionObserver', SilentObserver);

    const { container } = render(<Chat ids={['a', 'b']} />);
    await settle();
    act(() => { vi.advanceTimersByTime(1500); });
    await settle();

    expect(container.querySelectorAll('iframe')).toHaveLength(1);
    expect(embedActivation.debugSnapshot().receivedEntries).toBe(false);
  });

  it('hands the slot to whichever embed the reader scrolls to', async () => {
    const { container } = render(<Chat ids={['a', 'b']} />);
    const boxes = Array.from(container.querySelectorAll('.embed-box'));

    FakeIntersectionObserver.placement.set(boxes[0], { top: 400, ratio: 1 });
    FakeIntersectionObserver.placement.set(boxes[1], { top: 1400, ratio: 0 });
    await settle();
    expect(boxes[0].querySelector('iframe')).not.toBeNull();

    // Scroll: the first leaves, the second arrives.
    FakeIntersectionObserver.placement.set(boxes[0], { top: -900, ratio: 0 });
    FakeIntersectionObserver.placement.set(boxes[1], { top: 400, ratio: 1 });
    act(() => { FakeIntersectionObserver.live()!.reportAll(); });
    await settle();

    expect(boxes[1].querySelector('iframe')).not.toBeNull();
    expect(boxes[0].querySelector('iframe')).toBeNull();
  });

  it('releases an engaged embed once it has been off screen long enough', async () => {
    const { container } = render(<Chat ids={['a', 'b']} />);
    const boxes = Array.from(container.querySelectorAll('.embed-box'));

    FakeIntersectionObserver.placement.set(boxes[0], { top: 400, ratio: 1 });
    FakeIntersectionObserver.placement.set(boxes[1], { top: 1400, ratio: 0 });
    await settle();

    // It is already the live one; engage it the way playing would.
    act(() => { embedActivation.setPinned('a', true); });
    expect(boxes[0].querySelector('iframe')).not.toBeNull();

    // Scroll away and leave it. It must not hold the slot for the session.
    FakeIntersectionObserver.placement.set(boxes[0], { top: -900, ratio: 0 });
    FakeIntersectionObserver.placement.set(boxes[1], { top: 400, ratio: 1 });
    act(() => { FakeIntersectionObserver.live()!.reportAll(); });
    await settle();
    act(() => { vi.advanceTimersByTime(25_000); });
    act(() => { FakeIntersectionObserver.live()!.reportAll(); });
    await settle();

    expect(boxes[1].querySelector('iframe')).not.toBeNull();
    expect(boxes[0].querySelector('iframe')).toBeNull();
  });
});
