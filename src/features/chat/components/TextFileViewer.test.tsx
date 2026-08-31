// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import TextFileViewer from './TextFileViewer';
import { embedActivation } from '../embeds/embedActivation';
import { EMBED_BOX_VERSION } from '../utils/embedIntrinsics';

const translations = { t: (key: string) => key };
vi.mock('../../../shared/hooks/useAppTranslations', () => ({
  useAppTranslations: () => translations,
  default: () => translations,
}));

/**
 * Covers the wiring rather than the internals: a runnable attachment has to
 * reach MiniGameViewer with an activation identity and its stored box, and an
 * attachment without an identity must not become an iframe that escapes the
 * budget.
 */

class StubIntersectionObserver {
  static instances: StubIntersectionObserver[] = [];
  private targets = new Set<Element>();
  constructor(private callback: IntersectionObserverCallback) {
    StubIntersectionObserver.instances.push(this);
  }
  observe(target: Element) { this.targets.add(target); }
  unobserve(target: Element) { this.targets.delete(target); }
  disconnect() { this.targets.clear(); }
  takeRecords() { return []; }

  showAll() {
    const entries = [...this.targets].map((target) => ({
      target,
      isIntersecting: true,
      intersectionRatio: 1,
      boundingClientRect: { top: 450, height: 100 } as DOMRectReadOnly,
      rootBounds: { top: 0, height: 1000 } as DOMRectReadOnly,
      intersectionRect: { top: 450, height: 100 } as DOMRectReadOnly,
      time: 0,
    })) as unknown as IntersectionObserverEntry[];
    if (entries.length) this.callback(entries, this as unknown as IntersectionObserver);
  }

  static latest() {
    return StubIntersectionObserver.instances[StubIntersectionObserver.instances.length - 1];
  }
}

const toDataUrl = (source: string) => `data:text/html;base64,${btoa(source)}`;

const GAME = '<canvas id="stage" width="600" height="400"></canvas><script>requestAnimationFrame(()=>{});</script>';
const PLAIN_TEXT = 'Just some notes about verb conjugation.';

const settle = () => act(() => {
  vi.advanceTimersByTime(16);
  vi.advanceTimersByTime(300);
  vi.advanceTimersByTime(16);
});

describe('TextFileViewer embed wiring', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    StubIntersectionObserver.instances = [];
    vi.stubGlobal('IntersectionObserver', StubIntersectionObserver);
    embedActivation.reset();
    embedActivation.setRoot(document.body);
    embedActivation.setBudgets({ maxLiveEmbeds: 1 });
  });

  afterEach(() => {
    cleanup();
    embedActivation.reset();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('runs a runnable attachment through the activation slot', () => {
    const { container } = render(
      <TextFileViewer
        src={toDataUrl(GAME)}
        variant="assistant"
        fileName="game.html"
        mimeType="text/html"
        embedId="msg-1"
      />,
    );

    const box = container.querySelector<HTMLElement>('.embed-box');
    expect(box).not.toBeNull();
    // 600x400 canvas => a 3:2 box reserved before anything is mounted.
    expect(Number(box!.style.getPropertyValue('--embed-ar'))).toBeCloseTo(1.5, 3);
    expect(container.querySelectorAll('iframe')).toHaveLength(0);

    act(() => { StubIntersectionObserver.latest().showAll(); });
    settle();

    expect(container.querySelectorAll('iframe')).toHaveLength(1);
  });

  it('prefers a stored box over re-deriving one from the source', () => {
    const { container } = render(
      <TextFileViewer
        src={toDataUrl(GAME)}
        variant="assistant"
        fileName="game.html"
        mimeType="text/html"
        embedId="msg-1"
        embedBox={{ aspectRatio: 0.8, source: 'measured', v: EMBED_BOX_VERSION }}
      />,
    );

    const box = container.querySelector<HTMLElement>('.embed-box')!;
    expect(Number(box.style.getPropertyValue('--embed-ar'))).toBeCloseTo(0.8, 3);
  });

  it('will not run an attachment that has no activation identity', () => {
    const { container } = render(
      <TextFileViewer
        src={toDataUrl(GAME)}
        variant="assistant"
        fileName="game.html"
        mimeType="text/html"
      />,
    );

    act(() => { StubIntersectionObserver.latest()?.showAll(); });
    settle();

    // Falls back to the source preview rather than becoming an unarbitrated
    // iframe — the one thing that could escape the budget.
    expect(container.querySelectorAll('iframe')).toHaveLength(0);
    expect(container.querySelectorAll('.embed-box')).toHaveLength(0);
  });

  it('leaves ordinary text attachments alone', () => {
    const { container } = render(
      <TextFileViewer
        src={`data:text/plain;base64,${btoa(PLAIN_TEXT)}`}
        variant="assistant"
        fileName="notes.txt"
        mimeType="text/plain"
        embedId="msg-2"
      />,
    );

    expect(container.querySelectorAll('iframe')).toHaveLength(0);
    expect(container.textContent).toContain('verb conjugation');
  });
});
