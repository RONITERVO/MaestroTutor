// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { useCallback, useRef, useState } from 'react';
import { act, cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import LanguageSelectorGlobe from './LanguageSelectorGlobe';

function LanguageForm({ sharedBoundary = true, onCancel, onConfirm }: {
  sharedBoundary?: boolean;
  onCancel: () => void;
  onConfirm: (target: string | null) => void;
}) {
  const boundary = useRef<HTMLDivElement>(null);
  const [target, setTarget] = useState<string | null>('es-ES');
  const cancel = useCallback(() => { onCancel(); setTarget(null); }, [onCancel]);
  return <>
    <div ref={boundary}>
      <button onClick={() => onConfirm(target)}>Confirm language selection</button>
      <LanguageSelectorGlobe nativeLangCode="en-US" targetLangCode={target}
        onSelectNative={() => {}} onSelectTarget={setTarget} onCancel={cancel}
        onInteract={() => {}} interactionBoundaryRef={sharedBoundary ? boundary : undefined} />
    </div>
    <button>Outside language form</button>
  </>;
}

function pointerClick(element: HTMLElement) {
  fireEvent.pointerDown(element, { pointerId: 1, pointerType: 'mouse' });
  fireEvent.pointerUp(element, { pointerId: 1, pointerType: 'mouse' });
  fireEvent.click(element);
}

function renderForm(sharedBoundary = true) {
  const onCancel = vi.fn(), onConfirm = vi.fn();
  render(<LanguageForm sharedBoundary={sharedBoundary} onCancel={onCancel} onConfirm={onConfirm} />);
  // The globe installs its real outside-pointer listener after the opening
  // gesture. Exercise the active listener, not a click before its delay ends.
  act(() => vi.advanceTimersByTime(150));
  return { onCancel, onConfirm };
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(null);
  vi.spyOn(console, 'warn').mockImplementation(() => {});
});
afterEach(() => { cleanup(); vi.clearAllTimers(); vi.useRealTimers(); vi.restoreAllMocks(); });

describe('Language selector interaction boundary', () => {
  it('preserves the chosen target through a sibling confirmation pointer gesture', () => {
    const { onCancel, onConfirm } = renderForm();
    const target = within(screen.getByRole('listbox', { name: 'Target language' }))
      .getByRole('option', { name: 'Spanish (ES)' });
    expect(target.getAttribute('aria-selected')).toBe('true');

    pointerClick(screen.getByRole('button', { name: 'Confirm language selection' }));

    expect(onCancel).not.toHaveBeenCalled();
    expect(onConfirm).toHaveBeenCalledExactlyOnceWith('es-ES');
    expect(target.getAttribute('aria-selected')).toBe('true');
  });

  it('still clears the selection when a pointer lands outside the complete form', () => {
    const { onCancel, onConfirm } = renderForm();
    pointerClick(screen.getByRole('button', { name: 'Outside language form' }));

    expect(onCancel).toHaveBeenCalledOnce();
    expect(within(screen.getByRole('listbox', { name: 'Target language' }))
      .getByRole('option', { name: 'Spanish (ES)' }).getAttribute('aria-selected')).toBe('false');
    pointerClick(screen.getByRole('button', { name: 'Confirm language selection' }));
    expect(onConfirm).toHaveBeenCalledExactlyOnceWith(null);
  });

  it('keeps the globe itself as the default boundary for other callers', () => {
    const { onCancel, onConfirm } = renderForm(false);
    const native = within(screen.getByRole('listbox', { name: 'Native language' }))
      .getByRole('option', { name: 'English (US)' });
    fireEvent.pointerDown(native, { pointerId: 1, pointerType: 'mouse' });
    expect(onCancel).not.toHaveBeenCalled();

    pointerClick(screen.getByRole('button', { name: 'Confirm language selection' }));
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onConfirm).toHaveBeenCalledExactlyOnceWith(null);
  });
});
