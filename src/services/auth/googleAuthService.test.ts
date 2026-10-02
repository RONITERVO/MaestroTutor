// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { beforeEach, expect, it, vi } from 'vitest';
const mocks = vi.hoisted(() => ({ begin: vi.fn(), current: vi.fn(), load: vi.fn(), save: vi.fn(), clear: vi.fn(), backend: vi.fn(), handshake: vi.fn() }));
vi.mock('./firebaseAuthBridgeService', () => ({ firebaseAuthBridgeService: { beginGoogleSignIn: mocks.begin, getCurrentIdentity: mocks.current } }));
vi.mock('../../core/security/managedAccessSessionStorage', () => ({ loadManagedAccessSession: mocks.load, saveManagedAccessSession: mocks.save, clearManagedAccessSession: mocks.clear }));
vi.mock('../backend/maestroBackendService', () => ({ getManagedSessionForIdentity: mocks.handshake, maestroBackendService: { getManagedSession: mocks.backend } }));
import { googleAuthService } from './googleAuthService';
const identity = { firebaseIdToken: 'new-token', refreshToken: null, expiresAt: null, user: { id: 'new-account', email: null, displayName: null, photoUrl: null } };
const balance = { availableCredits: 123 };
const backendSession = { session: { user: identity.user, entitlements: ['approved'], billingSummary: balance } };
beforeEach(() => { vi.resetAllMocks(); mocks.save.mockResolvedValue(undefined); mocks.load.mockResolvedValue(null); mocks.backend.mockResolvedValue(backendSession); mocks.handshake.mockResolvedValue(backendSession); });
it('does not publish a Quest identity before the backend confirms the account; commits after saving', async () => {
  let respond!: (value: typeof backendSession) => void;
  mocks.handshake.mockImplementation(() => new Promise(resolve => { respond = resolve; }));
  const commit = vi.fn(() => { expect(mocks.save).toHaveBeenCalledOnce(); });
  const transaction = { ...identity, commit, rollback: vi.fn(), assertCurrent: vi.fn() };
  mocks.begin.mockResolvedValue(transaction);
  const abort = new AbortController(); const pending = googleAuthService.beginSignIn(abort.signal);
  await vi.waitFor(() => expect(mocks.handshake).toHaveBeenCalledWith(transaction));
  expect(mocks.save).not.toHaveBeenCalled(); expect(mocks.backend).not.toHaveBeenCalled();
  respond(backendSession); const session = await pending;
  expect(mocks.begin).toHaveBeenCalledWith(abort.signal); expect(session.billingSummary).toBe(balance);
  expect(commit).toHaveBeenCalledOnce(); expect(transaction.rollback).not.toHaveBeenCalled();
  expect(mocks.save.mock.calls[0][0]).not.toHaveProperty('commit');
});
it('rolls back when cancelled during the backend handshake without publishing the result', async () => {
  let current = true;
  const transaction = { ...identity, commit: vi.fn(), rollback: vi.fn(async () => {}), assertCurrent: () => { if (!current) throw new DOMException('Cancelled', 'AbortError'); } };
  mocks.begin.mockResolvedValue(transaction); mocks.handshake.mockImplementation(async () => { current = false; return backendSession; });
  await expect(googleAuthService.beginSignIn()).rejects.toMatchObject({ name: 'AbortError' });
  expect(transaction.rollback).toHaveBeenCalledOnce(); expect(transaction.commit).not.toHaveBeenCalled(); expect(mocks.save).not.toHaveBeenCalled();
});
it('rolls back a failed persistence write rather than committing an incomplete login', async () => {
  const transaction = { ...identity, commit: vi.fn(), rollback: vi.fn(async () => {}), assertCurrent: vi.fn() };
  mocks.begin.mockResolvedValue(transaction); mocks.save.mockRejectedValue(new Error('storage failed'));
  await expect(googleAuthService.beginSignIn()).rejects.toThrow('storage failed');
  expect(transaction.rollback).toHaveBeenCalledOnce(); expect(transaction.commit).not.toHaveBeenCalled();
});
it('keeps ordinary sign-in on its shared backend path without copying another account balance', async () => {
  mocks.begin.mockResolvedValue(identity); mocks.load.mockResolvedValue({ user: { id: 'old-account' }, billingSummary: { availableCredits: 9999 }, entitlements: ['old-access'] });
  await googleAuthService.beginSignIn();
  expect(mocks.handshake).not.toHaveBeenCalled(); expect(mocks.backend).toHaveBeenCalledOnce();
  expect(mocks.save.mock.calls[0][0]).toMatchObject({ user: identity.user, billingSummary: { availableCredits: 0 }, entitlements: [] });
  expect(mocks.save.mock.calls[1][0]).toMatchObject({ billingSummary: balance });
});
