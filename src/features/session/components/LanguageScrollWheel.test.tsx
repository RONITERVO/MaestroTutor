// Copyright 2026 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { useCallback, useState } from 'react';
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ALL_LANGUAGES, type LanguageDefinition } from '../../../core/config/languages';
import LanguageScrollWheel from './LanguageScrollWheel';

const languages = ['en-US', 'es-ES', 'fr-FR'].map(code =>
  ALL_LANGUAGES.find(language => language.langCode === code)!,
);

interface HarnessProps {
  onSelection: (language: LanguageDefinition) => void;
  freshProps?: boolean;
  disabled?: boolean;
}
function ControlledWheel({ onSelection, freshProps = false, disabled = false }: HarnessProps) {
  const [selected, setSelected] = useState<LanguageDefinition | null>(null);
  const [interactions, setInteractions] = useState(0);
  const select = useCallback((language: LanguageDefinition) => {
    onSelection(language);
    setSelected(language);
  }, [onSelection]);
  // Globe interaction/animation state can rerender the parent independently
  // of the controlled language value. Keep callback identities stable here.
  const interact = useCallback(() => setInteractions(value => value + 1), []);
  return <>
    <output aria-label="Parent interaction count">{interactions}</output>
    <button onClick={interact}>Redraw parent</button>
    <button onClick={() => setSelected(languages[1])}>Choose Spanish externally</button>
    <LanguageScrollWheel languages={freshProps ? languages.map(language => ({ ...language })) : languages} selectedValue={selected}
      onSelect={freshProps ? language => select(language) : select}
      onInteract={freshProps ? () => interact() : interact}
      disabled={disabled} title="Target language" variant="target" />
  </>;
}

function renderWheel(props: Omit<HarnessProps, 'onSelection'> = {}) {
  const onSelection = vi.fn();
  const view = render(<ControlledWheel onSelection={onSelection} {...props} />);
  const wheel = screen.getByRole('listbox', { name: 'Target language' });
  const options = screen.getAllByRole('option');
  // jsdom does not lay out elements. Model a 160px viewport and three 40px
  // rows whose centered scroll positions are 0, 40 and 80. All rows are visible
  // at scrollTop 80, so clicking Spanish while French is centered is valid.
  Object.defineProperties(wheel, {
    clientHeight: { configurable: true, value: 160 },
    offsetHeight: { configurable: true, value: 160 },
  });
  options.forEach((option, index) => Object.defineProperties(option, {
    offsetTop: { configurable: true, value: 60 + index * 40 },
    offsetHeight: { configurable: true, value: 40 },
  }));
  return { wheel, options, onSelection, ...view };
}

const resizeObservers: MockResizeObserver[] = [];
class MockResizeObserver implements ResizeObserver {
  readonly observed = new Set<Element>();
  constructor(private readonly callback: ResizeObserverCallback) { resizeObservers.push(this); }
  observe(target: Element) { this.observed.add(target); }
  unobserve(target: Element) { this.observed.delete(target); }
  disconnect() { this.observed.clear(); }
  notify(target: Element) {
    if (this.observed.has(target)) this.callback([{ target } as ResizeObserverEntry], this);
  }
}
function resized(target: Element) {
  act(() => resizeObservers.forEach(observer => observer.notify(target)));
}
function tallerViewport(wheel: HTMLElement) {
  Object.defineProperties(wheel, {
    clientHeight: { configurable: true, value: 200 },
    offsetHeight: { configurable: true, value: 200 },
  });
}

let scrollEndDescriptor: PropertyDescriptor | undefined;
beforeEach(() => {
  vi.useFakeTimers();
  resizeObservers.length = 0;
  vi.stubGlobal('ResizeObserver', MockResizeObserver);
  scrollEndDescriptor = Object.getOwnPropertyDescriptor(window, 'onscrollend');
  Object.defineProperty(window, 'onscrollend', { configurable: true, writable: true, value: null });
});
afterEach(() => {
  cleanup();
  vi.clearAllTimers();
  vi.useRealTimers();
  vi.unstubAllGlobals();
  if (scrollEndDescriptor) Object.defineProperty(window, 'onscrollend', scrollEndDescriptor);
  else Reflect.deleteProperty(window, 'onscrollend');
});

describe('LanguageScrollWheel controlled interaction', () => {
  it('keeps the positioned viewport when pointerdown rerenders an unselected parent', () => {
    const { wheel, options, onSelection } = renderWheel();
    // Browser viewport positioning can precede its queued scroll event. A
    // pointerdown must not move the option away before pointerup/click.
    wheel.scrollTop = 40;
    fireEvent.pointerDown(options[1], { pointerType: 'mouse', pointerId: 1 });

    expect(screen.getByLabelText('Parent interaction count').textContent).toBe('1');
    expect(onSelection).not.toHaveBeenCalled();
    expect(wheel.scrollTop).toBe(40);
  });

  it('keeps an explicit click selected and centered after pending scroll settlement', () => {
    const { wheel, options, onSelection } = renderWheel();
    wheel.scrollTop = 80;
    fireEvent.scroll(wheel);
    fireEvent.pointerDown(options[1], { pointerType: 'mouse', pointerId: 1 });
    fireEvent.pointerUp(options[1], { pointerType: 'mouse', pointerId: 1 });
    fireEvent.click(options[1]);
    expect(options[1].getAttribute('aria-selected')).toBe('true');
    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['es-ES']);

    fireEvent(wheel, new Event('scrollend'));
    act(() => vi.advanceTimersByTime(1000));

    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['es-ES']);
    expect(options[1].getAttribute('aria-selected')).toBe('true');
    expect(wheel.scrollTop).toBe(40);
  });

  it('selects the actual centered option when scrolling settles without a click', () => {
    const { wheel, options, onSelection } = renderWheel();
    wheel.scrollTop = 80;
    fireEvent.scroll(wheel);
    expect(onSelection).not.toHaveBeenCalled();
    fireEvent(wheel, new Event('scrollend'));
    act(() => vi.advanceTimersByTime(1000));

    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['fr-FR']);
    expect(options[2].getAttribute('aria-selected')).toBe('true');
    expect(wheel.scrollTop).toBe(80);
  });

  it('keeps fallback settlement through parent redraws with fresh language and callback props', () => {
    const { wheel, options, onSelection } = renderWheel({ freshProps: true });
    wheel.scrollTop = 80;
    fireEvent.scroll(wheel);
    act(() => vi.advanceTimersByTime(200));
    fireEvent.click(screen.getByRole('button', { name: 'Redraw parent' }));
    expect(wheel.scrollTop).toBe(80);
    expect(onSelection).not.toHaveBeenCalled();
    // No native scrollend: the original 250ms fallback must still settle.
    act(() => vi.advanceTimersByTime(60));
    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['fr-FR']);
    expect(options[2].getAttribute('aria-selected')).toBe('true');
  });

  it('centers keyboard button activation and rejects the previous scroll settlement', () => {
    const { wheel, options, onSelection } = renderWheel();
    wheel.scrollTop = 80;
    fireEvent.scroll(wheel);
    options[1].focus();
    fireEvent.keyDown(options[1], { key: 'Enter' });
    fireEvent.keyUp(options[1], { key: 'Enter' });
    // jsdom does not synthesize the browser's keyboard activation click.
    fireEvent.click(options[1], { detail: 0 });
    expect(wheel.scrollTop).toBe(40);
    fireEvent(wheel, new Event('scrollend'));
    act(() => vi.advanceTimersByTime(1000));
    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['es-ES']);
    expect(options[1].getAttribute('aria-selected')).toBe('true');
  });

  it.each(['wheel', 'keyboard'] as const)('accepts a new %s scroll immediately after centering an explicit choice', gesture => {
    const { wheel, options, onSelection } = renderWheel({ freshProps: true });
    fireEvent.click(options[1]);
    expect(wheel.scrollTop).toBe(40);
    if (gesture === 'wheel') fireEvent.wheel(wheel, { deltaY: 40 });
    else {
      fireEvent.keyDown(options[1], { key: 'ArrowDown' });
      wheel.scrollTop = 80;
    }
    expect(wheel.scrollTop).toBe(80);
    fireEvent.scroll(wheel);
    fireEvent(wheel, new Event('scrollend'));
    act(() => vi.advanceTimersByTime(1000));
    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['es-ES', 'fr-FR']);
    expect(options[2].getAttribute('aria-selected')).toBe('true');
  });

  it('waits for a touch gesture to finish before choosing the centered language', () => {
    const { wheel, options, onSelection } = renderWheel({ freshProps: true });
    fireEvent.touchStart(wheel);
    wheel.scrollTop = 80;
    fireEvent.scroll(wheel);
    act(() => vi.advanceTimersByTime(300));
    fireEvent(wheel, new Event('scrollend'));
    expect(onSelection).not.toHaveBeenCalled();
    fireEvent.touchEnd(wheel);
    act(() => vi.advanceTimersByTime(250));
    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['fr-FR']);
    expect(options[2].getAttribute('aria-selected')).toBe('true');
  });

  it('gives a changed controlled selection priority over pending manual scrolling', () => {
    const { wheel, options, onSelection } = renderWheel({ freshProps: true });
    wheel.scrollTop = 80;
    fireEvent.scroll(wheel);
    fireEvent.click(screen.getByRole('button', { name: 'Choose Spanish externally' }));
    expect(wheel.scrollTop).toBe(40);
    fireEvent(wheel, new Event('scrollend'));
    act(() => vi.advanceTimersByTime(1000));
    expect(options[1].getAttribute('aria-selected')).toBe('true');
    expect(onSelection).not.toHaveBeenCalled();
  });

  it('does not select from scroll or activation events while disabled', () => {
    const { wheel, options, onSelection } = renderWheel({ disabled: true });
    fireEvent.click(options[1]);
    fireEvent.wheel(wheel, { deltaY: 80 });
    expect(wheel.scrollTop).toBe(0);
    wheel.scrollTop = 80;
    fireEvent.scroll(wheel);
    fireEvent(wheel, new Event('scrollend'));
    act(() => vi.advanceTimersByTime(1000));
    expect(onSelection).not.toHaveBeenCalled();
    expect(options.every(option => option.getAttribute('aria-selected') === 'false')).toBe(true);
  });

  it('cancels pending scroll settlement on unmount', () => {
    const { wheel, onSelection, unmount } = renderWheel();
    wheel.scrollTop = 80;
    fireEvent.scroll(wheel);
    expect(vi.getTimerCount()).toBeGreaterThan(0);
    unmount();
    expect(vi.getTimerCount()).toBe(0);
    act(() => vi.advanceTimersByTime(1000));
    expect(onSelection).not.toHaveBeenCalled();
  });


  it('recenters an idle selection when the viewport or cqw-sized content reflows', () => {
    const { wheel, options, onSelection } = renderWheel();
    fireEvent.click(options[1]);
    expect(wheel.scrollTop).toBe(40);
    const content = options[1].parentElement!;
    expect(resizeObservers).toHaveLength(1);
    expect(resizeObservers[0].observed).toEqual(new Set([wheel, content]));

    tallerViewport(wheel);
    resized(wheel);
    expect(wheel.scrollTop).toBe(20);
    options.forEach((option, index) => Object.defineProperties(option, {
      offsetTop: { configurable: true, value: 75 + index * 50 },
      offsetHeight: { configurable: true, value: 50 },
    }));
    resized(content);
    expect(wheel.scrollTop).toBe(50);
    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['es-ES']);
  });

  it('does not let resizing replace an active manual scroll with the previous selection', () => {
    const { wheel, options, onSelection } = renderWheel();
    fireEvent.click(options[1]);
    fireEvent.wheel(wheel, { deltaY: 40 });
    fireEvent.scroll(wheel);
    tallerViewport(wheel);
    resized(wheel);
    expect(wheel.scrollTop).toBe(80);
    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['es-ES']);

    fireEvent(wheel, new Event('scrollend'));
    expect(wheel.scrollTop).toBe(60);
    expect(options[2].getAttribute('aria-selected')).toBe('true');
    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['es-ES', 'fr-FR']);
  });

  it('defers resize centering across pointerdown and pointerup until the click chooses its row', () => {
    const { wheel, options, onSelection } = renderWheel();
    fireEvent.click(options[1]);
    fireEvent.pointerDown(options[2], { pointerId: 42, pointerType: 'mouse' });
    tallerViewport(wheel);
    resized(wheel);
    expect(wheel.scrollTop).toBe(40);
    fireEvent.pointerUp(options[2], { pointerId: 42, pointerType: 'mouse' });
    expect(wheel.scrollTop).toBe(40);
    fireEvent.click(options[2]);
    act(() => vi.advanceTimersByTime(20));
    expect(wheel.scrollTop).toBe(60);
    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['es-ES', 'fr-FR']);
  });

  it('restores idle centering after a pointer is released outside the wheel', () => {
    const { wheel, options, onSelection } = renderWheel();
    fireEvent.click(options[1]);
    fireEvent.pointerDown(wheel, { pointerId: 42, pointerType: 'mouse' });
    tallerViewport(wheel);
    resized(wheel);
    expect(wheel.scrollTop).toBe(40);
    fireEvent.pointerUp(document.body, { pointerId: 42, pointerType: 'mouse' });
    act(() => vi.advanceTimersByTime(20));
    expect(wheel.scrollTop).toBe(20);
    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['es-ES']);
  });

  it('defers reflow centering during touch even when no scroll has started', () => {
    const { wheel, options, onSelection } = renderWheel();
    fireEvent.click(options[1]);
    fireEvent.touchStart(wheel);
    tallerViewport(wheel);
    resized(wheel);
    expect(wheel.scrollTop).toBe(40);
    fireEvent.touchEnd(wheel);
    act(() => vi.advanceTimersByTime(20));
    expect(wheel.scrollTop).toBe(20);
    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['es-ES']);
  });

  it('lets a changed controlled selection supersede a resize deferred by an active pointer', () => {
    const { wheel, options, onSelection } = renderWheel();
    fireEvent.click(options[2]);
    fireEvent.pointerDown(wheel, { pointerId: 42, pointerType: 'mouse' });
    tallerViewport(wheel);
    resized(wheel);
    expect(wheel.scrollTop).toBe(80);
    fireEvent.click(screen.getByRole('button', { name: 'Choose Spanish externally' }));
    expect(wheel.scrollTop).toBe(20);
    fireEvent.pointerUp(document.body, { pointerId: 42, pointerType: 'mouse' });
    act(() => vi.advanceTimersByTime(20));
    expect(options[1].getAttribute('aria-selected')).toBe('true');
    expect(wheel.scrollTop).toBe(20);
    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['fr-FR']);
  });

  it('disconnects resize observations and cancels deferred pointer-release work on unmount', () => {
    const { wheel, options, onSelection, unmount } = renderWheel();
    fireEvent.click(options[1]);
    fireEvent.pointerDown(wheel, { pointerId: 42, pointerType: 'mouse' });
    tallerViewport(wheel);
    resized(wheel);
    fireEvent.pointerUp(document.body, { pointerId: 42, pointerType: 'mouse' });
    expect(vi.getTimerCount()).toBeGreaterThan(0);
    unmount();
    expect(resizeObservers.every(observer => observer.observed.size === 0)).toBe(true);
    expect(vi.getTimerCount()).toBe(0);
    fireEvent.pointerCancel(document.body, { pointerId: 42, pointerType: 'mouse' });
    act(() => vi.advanceTimersByTime(1000));
    expect(onSelection.mock.calls.map(([language]) => language.langCode)).toEqual(['es-ES']);
  });

});
