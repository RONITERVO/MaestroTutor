// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { useEffect } from 'react';
import { act, cleanup, fireEvent, render } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { sessionActivity } from '../browser/sessionActivity';
import { QuestAudienceGate } from './QuestAudienceGate';

afterEach(() => { cleanup(); sessionActivity.setSuspended(false); sessionActivity.resume(); });

it('does not mount account/chat/agent effects until a deliberate adult confirmation', () => {
  const start = vi.fn();
  function Session() { useEffect(start, []); return <div>Chat is ready</div>; }
  const ui = render(<QuestAudienceGate><Session /></QuestAudienceGate>);
  const enter = ui.getByRole('button', { name: 'Open my Maestro book' }) as HTMLButtonElement;
  expect(start).not.toHaveBeenCalled();
  expect(enter.disabled).toBe(true);
  fireEvent.click(enter);
  expect(start).not.toHaveBeenCalled();
  fireEvent.click(ui.getByRole('checkbox'));
  expect(start).not.toHaveBeenCalled();
  fireEvent.click(ui.getByRole('checkbox'));
  expect(enter.disabled).toBe(true);
  fireEvent.click(ui.getByRole('checkbox'));
  fireEvent.click(enter);
  expect(start).toHaveBeenCalledTimes(1);
  expect(ui.queryByText('Chat is ready')).not.toBeNull();
});

it('keeps chat inaccessible after the user declares they are under 18', () => {
  const ui = render(<QuestAudienceGate><div>Private chat</div></QuestAudienceGate>);
  fireEvent.click(ui.getByRole('checkbox'));
  fireEvent.click(ui.getByRole('button', { name: 'I am under 18' }));
  expect(ui.getByRole('status').textContent).toContain('No AI session has started');
  expect(ui.queryByRole('checkbox')).toBeNull();
  expect(ui.queryByRole('button', { name: 'Open my Maestro book' })).toBeNull();
  expect(ui.queryByText('Private chat')).toBeNull();
});

it('requires a new confirmation when the book document is replaced', () => {
  const first = render(<QuestAudienceGate><div>Private chat</div></QuestAudienceGate>);
  fireEvent.click(first.getByRole('checkbox'));
  fireEvent.click(first.getByRole('button', { name: 'Open my Maestro book' }));
  first.unmount();
  const next = render(<QuestAudienceGate><div>Private chat</div></QuestAudienceGate>);
  expect((next.getByRole('checkbox') as HTMLInputElement).checked).toBe(false);
  expect(next.queryByText('Private chat')).toBeNull();
});

it('removes the startup splash and offers exact, same-frame public policy links before entry', () => {
  const splash = document.createElement('div'); splash.id = 'splash-screen'; document.body.append(splash);
  const ui = render(<QuestAudienceGate><div>Chat</div></QuestAudienceGate>);
  expect(document.getElementById('splash-screen')).toBeNull();
  const links = ui.getAllByRole('link') as HTMLAnchorElement[];
  expect(links.map(link => link.href)).toEqual(['https://chatwithmaestro.com/privacy.html', 'https://ai.google.dev/gemini-api/terms']);
  expect(links.every(link => !link.target)).toBe(true);
});

it('acknowledges native pause and return without granting actions or bypassing confirmation', async () => {
  const ui = render(<QuestAudienceGate><div>Private chat</div></QuestAudienceGate>);
  const bridge = window.maestroBook!;
  expect(bridge.snapshot()).toMatchObject({ audioPaused: true, historyTotal: 0 });
  expect(bridge.command({ version: 1, type: 'session.resume' })).toBe(false);
  expect(bridge.roomState({ visible: true })).toBe(false);
  expect(bridge.libraryState({ visible: true })).toBe(false);
  expect(bridge.takeFileSelection()).toBe(false);
  expect(bridge.fileExportPoll()).toBeNull();
  expect(bridge.roomSnapshot().request).toBeNull();
  expect(bridge.integrityResult({ token: 'untrusted' })).toBe(false);
  await act(async () => { bridge.lifecycle(true); });
  expect(bridge.lifecycleState()).toEqual({ suspended: true, settled: true, active: false });
  act(() => { bridge.lifecycle(false); });
  expect(bridge.lifecycleState()).toEqual({ suspended: false, settled: true, active: false });
  expect(ui.queryByText('Private chat')).toBeNull();
  expect((ui.getByRole('checkbox') as HTMLInputElement).checked).toBe(false);
  fireEvent.click(ui.getByRole('checkbox'));
  fireEvent.click(ui.getByRole('button', { name: 'Open my Maestro book' }));
  expect(window.maestroBook).toBeUndefined();
});
