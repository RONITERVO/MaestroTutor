// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { QuestBookSurface } from './QuestBookSurface';
import { useBookPresentation } from './BookPresentationContext';
import { BOOK_LAYOUT_STORAGE_KEY } from './bookModel';
import { useMaestroStore } from '../../store';
import { sessionActivity } from '../browser/sessionActivity';

vi.mock('../../shared/hooks/useAppTranslations', () => ({ useAppTranslations: () => ({ t: (key: string) => key }) }));
function ChatProbe() {
  const book = useBookPresentation()!;
  return <div data-testid="chat-probe" data-current={JSON.stringify([...book.visibleMessageIds])} data-earlier={JSON.stringify([...book.earlierMessageIds])} />;
}

beforeEach(() => {
  const values = new Map<string, string>();
  vi.stubGlobal('localStorage', { getItem: (key: string) => values.get(key) ?? null, setItem: (key: string, value: string) => values.set(key, value) });
  const messages = Array.from({ length: 20 }, (_, i) => ({ id: `m${i}`, role: 'assistant' as const, text: `Message ${i}`, timestamp: i }));
  useMaestroStore.setState(state => ({ messages, settings: { ...state.settings, selectedLanguagePairId: 'es-en', historyBookmarkMessageId: 'm3' } }));
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe('full-page book layouts', () => {
  it('removes an active artifact before acknowledging suspend and waits for the resume command', async () => {
    const source = btoa('<html><body>Practice</body></html>');
    useMaestroStore.setState({ messages: [{ id: 'game', role: 'assistant', timestamp: 1, imageUrl: `data:text/html;base64,${source}`, imageMimeType: 'text/html' }] });
    const { container } = render(<QuestBookSurface><ChatProbe /></QuestBookSurface>);
    act(() => { window.maestroBook!.command({ version: 1, type: 'layout.set', layout: 'practice' }); });
    expect(container.querySelector('iframe')).not.toBeNull();
    await act(async () => { window.maestroBook!.lifecycle(true); });
    expect(container.querySelector('iframe')).toBeNull();
    expect(window.maestroBook!.lifecycleState()).toMatchObject({ suspended: true, settled: true, active: false });
    act(() => { window.maestroBook!.lifecycle(false); });
    expect(container.querySelector('iframe')).toBeNull();
    act(() => { window.maestroBook!.command({ version: 1, type: 'session.resume' }); });
    expect(container.querySelector('iframe')).not.toBeNull();
    expect(sessionActivity.isActive()).toBe(true);
  });
  it('defaults to familiar conversation, with distinct earlier/current pages and no added page UI', () => {
    const { container, getByTestId } = render(<QuestBookSurface><ChatProbe /></QuestBookSurface>);
    expect(container.querySelector('.quest-layout-conversation')).not.toBeNull();
    expect(JSON.parse(getByTestId('chat-probe').dataset.current!)).toEqual(Array.from({ length: 8 }, (_, i) => `m${i + 12}`));
    expect(JSON.parse(getByTestId('chat-probe').dataset.earlier!)).toEqual(Array.from({ length: 8 }, (_, i) => `m${i + 4}`));
    expect(container.querySelectorAll('header, footer, nav, select, button')).toHaveLength(0);
  });

  it('switches through the native command, remembers the choice and keeps bookmark identity', () => {
    const { container, unmount } = render(<QuestBookSurface><ChatProbe /></QuestBookSurface>);
    act(() => { window.maestroBook!.command({ version: 1, type: 'layout.set', layout: 'practice' }); });
    expect(container.querySelector('.quest-layout-practice')).not.toBeNull();
    expect(localStorage.getItem(BOOK_LAYOUT_STORAGE_KEY)).toBe('practice');
    expect(window.maestroBook!.snapshot().bookmarkMessageId).toBe('m3');
    act(() => { window.maestroBook!.command({ version: 1, type: 'bookmark.jump' }); });
    expect(window.maestroBook!.snapshot().historyStart).toBe(3);
    expect(container.querySelectorAll('header, footer, nav, select, button')).toHaveLength(0);
    unmount();
    const again = render(<QuestBookSurface><ChatProbe /></QuestBookSurface>);
    expect(again.container.querySelector('.quest-layout-practice')).not.toBeNull();
  });
});
