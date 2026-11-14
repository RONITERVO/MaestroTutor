// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { beforeEach, expect, it, vi } from 'vitest';
const mocks = vi.hoisted(() => ({ currentUser: null as unknown, ready: vi.fn(async () => {}), token: vi.fn(), native: false, pending: false, recover: vi.fn(async () => {}) }));
vi.mock('@capacitor/core', () => ({ Capacitor: { isNativePlatform: () => false, getPlatform: () => 'web' } }));
vi.mock('../../platform/quest/questIntegrityBridge', () => ({ isNativeQuestBook: () => mocks.native }));
vi.mock('./questFirebaseIdentity', () => ({ recoverQuestIdentity: mocks.recover, isQuestIdentityPending: () => mocks.pending }));
vi.mock('../firebase/maestroFirebaseService', () => ({ maestroFirebaseService: { isConfigured: () => true, getAuth: async () => ({ get currentUser() { return mocks.currentUser; }, authStateReady: mocks.ready }) } }));
import { firebaseAuthBridgeService } from './firebaseAuthBridgeService';
const user = { uid: 'account-a', email: 'a@example.test', displayName: 'A', photoURL: null, refreshToken: 'refresh', getIdToken: mocks.token };
beforeEach(() => { vi.clearAllMocks(); mocks.native = false; mocks.pending = false; mocks.currentUser = user; mocks.token.mockResolvedValue('token-a'); });
it('pairs a refreshed token with the same captured user', async () => {
  await expect(firebaseAuthBridgeService.getCurrentIdentity(true)).resolves.toMatchObject({ user: { id: 'account-a' }, firebaseIdToken: 'token-a' });
  expect(mocks.token).toHaveBeenCalledWith(true);
});
it('rejects a token whose account changed in another tab during refresh', async () => {
  mocks.token.mockImplementationOnce(async () => { mocks.currentUser = { ...user, uid: 'account-b' }; return 'token-a'; });
  await expect(firebaseAuthBridgeService.getCurrentIdentity(true)).rejects.toThrow('account changed');
});
it('does not expose a partially signed-in Quest identity or run crash recovery during its active transaction', async () => {
  mocks.native = true; mocks.pending = true;
  await expect(firebaseAuthBridgeService.getCurrentIdentity()).rejects.toThrow('still in progress');
  expect(mocks.recover).not.toHaveBeenCalled(); expect(mocks.token).not.toHaveBeenCalled();
});
it('checks pending Quest ownership again after token refresh', async () => {
  mocks.native = true; mocks.token.mockImplementationOnce(async () => { mocks.pending = true; return 'token-a'; });
  await expect(firebaseAuthBridgeService.getCurrentIdentity()).rejects.toThrow('still in progress');
  expect(mocks.recover).toHaveBeenCalledOnce();
});
