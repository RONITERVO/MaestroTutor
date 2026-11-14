// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import QuestLinkPage from './QuestLinkPage';
import type { QuestApprovalAdapter } from './questApprovalService';
import type { ManagedAuthIdentity } from '../services/auth/firebaseAuthBridgeService';
import { QuestLinkError } from '../services/auth/questLinkProtocol';
const identity: ManagedAuthIdentity = { user: { id: 'original-uid', email: 'owner@example.test', displayName: 'Owner', photoUrl: null }, firebaseIdToken: 'private', refreshToken: null, expiresAt: null };
const adapter = (): QuestApprovalAdapter => ({ available: () => true, identity: async () => identity,
  signIn: vi.fn(async () => identity), signOut: vi.fn(async () => {}), approve: vi.fn(async () => {}) });
afterEach(() => { cleanup(); window.history.replaceState({}, '', '/'); });
async function enterCode() {
  await screen.findByText('owner@example.test');
  fireEvent.change(screen.getByLabelText('Code from your book'), { target: { value: 'abcde-fghjk' } });
}
it('requires manual code entry and explicit confirmation, including when the URL contains a code', async () => {
  window.history.replaceState({}, '', '/?code=ABCDE-FGHJK'); const api = adapter(); render(<QuestLinkPage adapter={api} />);
  await screen.findByText('owner@example.test');
  expect((screen.getByLabelText('Code from your book') as HTMLInputElement).value).toBe('');
  expect((screen.getByRole('button', { name: 'Link my Quest book' }) as HTMLButtonElement).disabled).toBe(true);
  await enterCode(); fireEvent.click(screen.getByRole('button', { name: 'Link my Quest book' })); expect(api.approve).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole('checkbox')); fireEvent.click(screen.getByRole('button', { name: 'Link my Quest book' }));
  await screen.findByText('Your book is approved'); expect(api.approve).toHaveBeenCalledWith('ABCDEFGHJK', 'original-uid', expect.any(AbortSignal));
});
it('changing a code clears the previous confirmation', async () => {
  render(<QuestLinkPage adapter={adapter()} />); await enterCode(); fireEvent.click(screen.getByRole('checkbox'));
  fireEvent.change(screen.getByLabelText('Code from your book'), { target: { value: 'BCDEF-GHJKL' } });
  expect((screen.getByRole('checkbox') as HTMLInputElement).checked).toBe(false);
});
it('changing accounts clears confirmation and does not submit approval', async () => {
  const api = adapter(); render(<QuestLinkPage adapter={api} />); await enterCode(); fireEvent.click(screen.getByRole('checkbox'));
  fireEvent.click(screen.getByRole('button', { name: 'Use another account' }));
  await screen.findByRole('button', { name: 'Sign in with Google' }); expect(api.approve).not.toHaveBeenCalled();
});
it('recent-login rejection is actionable and requires a new confirmation', async () => {
  const api = adapter(); vi.mocked(api.approve).mockRejectedValue(new QuestLinkError('quest-link/recent-login-required', 'Sign in with Google again before linking the headset.'));
  render(<QuestLinkPage adapter={api} />); await enterCode(); fireEvent.click(screen.getByRole('checkbox')); fireEvent.click(screen.getByRole('button', { name: 'Link my Quest book' }));
  expect((await screen.findByRole('alert')).textContent).toContain('Sign in with Google again');
  expect((screen.getByRole('checkbox') as HTMLInputElement).checked).toBe(false);
});
it('unconfigured pages never start authentication or request approval', async () => {
  const api = adapter(); api.available = () => false; api.identity = vi.fn();
  render(<QuestLinkPage adapter={api} />); expect(screen.getByText('Account linking is not configured on this page yet.')).toBeTruthy();
  expect(api.identity).not.toHaveBeenCalled(); expect(api.signIn).not.toHaveBeenCalled();
});
it('unmount aborts a pending approval and ignores its late response', async () => {
  const api = adapter(); let finish!: () => void;
  vi.mocked(api.approve).mockImplementation(() => new Promise<void>(resolve => { finish = resolve; }));
  const view = render(<QuestLinkPage adapter={api} />); await enterCode(); fireEvent.click(screen.getByRole('checkbox')); fireEvent.click(screen.getByRole('button', { name: 'Link my Quest book' }));
  await waitFor(() => expect(api.approve).toHaveBeenCalledOnce()); const signal = vi.mocked(api.approve).mock.calls[0][2];
  view.unmount(); expect(signal.aborted).toBe(true); await act(async () => { finish(); });
});

it('a stalled account restore releases the page so the user can sign in', async () => {
  vi.useFakeTimers();
  try {
    const api = adapter(); api.identity = () => new Promise(() => {}); render(<QuestLinkPage adapter={api} />);
    await act(async () => { await vi.advanceTimersByTimeAsync(30000); });
    expect(screen.getByRole('alert').textContent).toContain('Sign in with Google');
    expect((screen.getByRole('button', { name: 'Sign in with Google' }) as HTMLButtonElement).disabled).toBe(false);
  } finally { vi.useRealTimers(); }
});
