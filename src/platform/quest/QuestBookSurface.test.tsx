// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, fireEvent, render } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { QuestBookSurface } from './QuestBookSurface';
import { useBookPresentation } from './BookPresentationContext';
import { BOOK_LAYOUT_STORAGE_KEY } from './bookModel';
import { useMaestroStore } from '../../store';
import type { RoomAgentState } from '../../core-sdk/room/roomAgent';
import { sessionActivity } from '../browser/sessionActivity';

vi.mock('../../shared/hooks/useAppTranslations', () => ({ useAppTranslations: () => ({ t: (key: string) => key }) }));
function ChatProbe() {
  const book = useBookPresentation()!;
  return <div data-testid="chat-probe" data-current={JSON.stringify([...book.visibleMessageIds])} data-earlier={JSON.stringify([...book.earlierMessageIds])} />;
}

beforeEach(() => {
  sessionActivity.setSuspended(false); sessionActivity.resume();
  const values = new Map<string, string>();
  vi.stubGlobal('localStorage', { getItem: (key: string) => values.get(key) ?? null, setItem: (key: string, value: string) => values.set(key, value) });
  const messages = Array.from({ length: 20 }, (_, i) => ({ id: `m${i}`, role: 'assistant' as const, text: `Message ${i}`, timestamp: i }));
  useMaestroStore.setState(state => ({ messages, settings: { ...state.settings, selectedLanguagePairId: 'es-en', historyBookmarkMessageId: 'm3' } }));
});
afterEach(() => { cleanup(); vi.useRealTimers(); vi.unstubAllGlobals(); });

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


const nativeRoom = (session = 'a'.repeat(32), ready = true): RoomAgentState => ({
  version: 1, session, revision: 1, sceneRevision: 1, ack: 0, ok: true,
  status: ready ? 'Workspace ready' : 'Opening workspace', visible: false,
  canUndo: false, canRedo: false, physicsRunning: false, created: [], capabilities: ['catalog.v1'],
  objects: ready ? [{ id: 'book', name: 'Book', kind: 'Book', position: { x: 0, y: 1, z: 1 }, scale: 1,
    color: { r: 1, g: 1, b: 1, a: 1 }, animated: false, objectRevision: 1 }] : [],
});
const roomCommand = () => window.maestroBook!.roomSnapshot().request;
const openWorkshop = () => act(() => { window.maestroBook!.command({ version: 1, type: 'workspace.open' }); });
const nativeState = async (state: RoomAgentState) => { await act(async () => { expect(window.maestroBook!.roomState(state)).toBe(true); }); };
const acknowledge = async (state: RoomAgentState, visible: boolean, ok = true) => {
  const sequence = roomCommand()!.sequence;
  await nativeState({ ...state, revision: state.revision + 1, ack: sequence, visible, ok });
};

describe('workshop navigation while the native workspace opens', () => {
  it('retains an early open through the startup session handoff, then respects Back to chat', async () => {
    const ui = render(<QuestBookSurface><input aria-label="Chat draft" defaultValue="Keep this" /></QuestBookSurface>);
    const composer = ui.getByLabelText('Chat draft'); openWorkshop(); expect(roomCommand()).toBeNull();
    await nativeState(nativeRoom('a'.repeat(32), false));
    expect(roomCommand()).toMatchObject({ session: 'a'.repeat(32), commands: [{ action: 'workspace', visible: true }] });
    const ready = nativeRoom('b'.repeat(32)); await nativeState(ready);
    expect(roomCommand()).toMatchObject({ session: ready.session, sequence: 1, commands: [{ action: 'workspace', visible: true }] });
    await acknowledge(ready, true); expect(ui.getByRole('button', { name: 'Action catalog' })).toBeTruthy();
    expect(ui.getByLabelText('Chat draft')).toBe(composer); expect(composer.closest('[hidden]')).not.toBeNull();
    fireEvent.click(ui.getByRole('button', { name: 'Back to chat' }));
    expect(roomCommand()?.commands).toEqual([{ action: 'workspace', visible: false }]);
    await acknowledge({ ...ready, revision: 2 }, false);
    await nativeState(nativeRoom('c'.repeat(32))); expect(roomCommand()).toBeNull(); expect(composer.closest('[hidden]')).toBeNull();
  });
  it('keeps an acknowledged startup view open when its content binds, without repeating requests in one session', async () => {
    const ui = render(<QuestBookSurface><ChatProbe /></QuestBookSurface>); const opening = nativeRoom('a'.repeat(32), false);
    await nativeState(opening); openWorkshop(); await acknowledge(opening, true);
    await nativeState({ ...opening, revision: 3, ack: 1, visible: true }); expect(roomCommand()).toBeNull();
    const ready = nativeRoom('b'.repeat(32)); await nativeState(ready);
    expect(roomCommand()?.commands).toEqual([{ action: 'workspace', visible: true }]); await acknowledge(ready, true);
    expect(ui.getByRole('button', { name: 'Back to chat' })).toBeTruthy();
  });
  it('cancels an early open when the user chooses a conversation page', async () => {
    render(<QuestBookSurface><ChatProbe /></QuestBookSurface>); openWorkshop();
    act(() => { window.maestroBook!.command({ version: 1, type: 'history.latest' }); });
    await nativeState(nativeRoom()); expect(roomCommand()).toBeNull();
  });
  it('keeps a late open acknowledgement from hiding chat after navigation away', async () => {
    const ui = render(<QuestBookSurface><ChatProbe /></QuestBookSurface>); const state = nativeRoom(); await nativeState(state); openWorkshop();
    act(() => { window.maestroBook!.command({ version: 1, type: 'history.latest' }); });
    await acknowledge(state, true);
    expect(ui.getByTestId('chat-probe').closest('[hidden]')).toBeNull();
    expect(roomCommand()?.commands).toEqual([{ action: 'workspace', visible: false }]);
    await acknowledge({ ...state, revision: 2 }, false); expect(roomCommand()).toBeNull();
  });
  it('does not replay an object edit after a ready workspace is replaced', async () => {
    const ui = render(<QuestBookSurface><ChatProbe /></QuestBookSurface>); const state = nativeRoom(); await nativeState(state); openWorkshop(); await acknowledge(state, true);
    fireEvent.click(ui.getByRole('button', { name: '+ Box robot' })); expect(roomCommand()?.commands[0].action).toBe('create');
    await nativeState(nativeRoom('b'.repeat(32))); expect(roomCommand()).toBeNull();
    expect(ui.queryByRole('button', { name: '+ Box robot' })).toBeNull();
  });
  it('stops an unacknowledged open on suspend and requires a new user request', async () => {
    render(<QuestBookSurface><ChatProbe /></QuestBookSurface>); await nativeState(nativeRoom('a'.repeat(32), false)); openWorkshop();
    await act(async () => { window.maestroBook!.lifecycle(true); });
    act(() => { window.maestroBook!.lifecycle(false); window.maestroBook!.command({ version: 1, type: 'session.resume' }); });
    await nativeState(nativeRoom('b'.repeat(32))); expect(roomCommand()).toBeNull();
    openWorkshop(); expect(roomCommand()?.commands).toEqual([{ action: 'workspace', visible: true }]);
  });
  it('does not automatically retry a native refusal, but accepts a new explicit open', async () => {
    render(<QuestBookSurface><ChatProbe /></QuestBookSurface>); const state = nativeRoom(); await nativeState(state); openWorkshop();
    await acknowledge(state, false, false); await nativeState({ ...state, revision: 3, ack: 1 }); expect(roomCommand()).toBeNull();
    await nativeState(nativeRoom('b'.repeat(32))); expect(roomCommand()).toBeNull();
    openWorkshop(); expect(roomCommand()?.commands).toEqual([{ action: 'workspace', visible: true }]);
  });
  it('respects Back to chat in the temporary startup maintenance view', async () => {
    const ui = render(<QuestBookSurface><ChatProbe /></QuestBookSurface>); const opening = nativeRoom('a'.repeat(32), false);
    await nativeState(opening); openWorkshop(); await acknowledge(opening, true);
    fireEvent.click(ui.getByRole('button', { name: 'Back to chat' })); await acknowledge({ ...opening, revision: 2 }, false);
    await nativeState(nativeRoom('b'.repeat(32))); expect(roomCommand()).toBeNull();
    expect(ui.getByTestId('chat-probe').closest('[hidden]')).toBeNull();
  });
  it('does not turn a timed-out open into a retry loop on reconnect', async () => {
    vi.useFakeTimers(); render(<QuestBookSurface><ChatProbe /></QuestBookSurface>); await nativeState(nativeRoom()); openWorkshop();
    await act(async () => { await vi.advanceTimersByTimeAsync(15001); }); expect(roomCommand()).toBeNull();
    await nativeState(nativeRoom('b'.repeat(32))); expect(roomCommand()).toBeNull();
  });
  it('lets a later animation-library choice supersede an unacknowledged workshop open', async () => {
    const ui = render(<QuestBookSurface><ChatProbe /></QuestBookSurface>); const state = nativeRoom(); await nativeState(state); openWorkshop();
    await act(async () => { expect(window.maestroBook!.libraryState({ version: 1, session: 'd'.repeat(32), revision: 1, ack: 0,
      visible: true, busy: false, readOnly: false, query: '', offset: 0, total: 0, pageSize: 12, compatibleOnly: true,
      favouritesOnly: false, includeShort: false, entries: [], selected: null, canPreview: false, canWalk: false,
      canAssign: false, ruleId: null, ruleName: null, stepIndex: 0, sourceIndex: 0, sourceCount: 0, sourceName: null,
      attribution: '', termsPage: 0, termsPages: 1, status: 'Choose a motion' })).toBe(true); });
    await acknowledge(state, true);
    expect(ui.getByRole('heading', { name: 'Animations' })).toBeTruthy();
    expect(ui.queryByRole('button', { name: 'Action catalog' })).toBeNull();
    expect(roomCommand()?.commands).toEqual([{ action: 'workspace', visible: false }]);
    await acknowledge({ ...state, revision: 2 }, false);
    await nativeState(nativeRoom('b'.repeat(32))); expect(roomCommand()).toBeNull();
  });
  it('can explicitly reopen a still-visible native workshop after a refused close', async () => {
    const ui = render(<QuestBookSurface><ChatProbe /></QuestBookSurface>); const state = nativeRoom(); await nativeState(state); openWorkshop(); await acknowledge(state, true);
    act(() => { window.maestroBook!.command({ version: 1, type: 'history.latest' }); });
    await acknowledge({ ...state, revision: 2 }, true, false); expect(ui.getByTestId('chat-probe').closest('[hidden]')).toBeNull();
    openWorkshop(); expect(ui.getByRole('button', { name: 'Back to chat' })).toBeTruthy(); expect(roomCommand()).toBeNull();
  });

  it('does not treat earlier chat navigation as a close request for a later native view', async () => {
    const ui = render(<QuestBookSurface><ChatProbe /></QuestBookSurface>);
    act(() => { window.maestroBook!.command({ version: 1, type: 'history.latest' }); });
    await nativeState({ ...nativeRoom(), visible: true });
    expect(roomCommand()).toBeNull(); expect(ui.getByRole('button', { name: 'Back to chat' })).toBeTruthy();
  });

});
