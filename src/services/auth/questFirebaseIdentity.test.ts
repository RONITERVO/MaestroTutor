// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { beforeEach, expect, it, vi } from 'vitest';
const mocks = vi.hoisted(() => ({
  auth: { currentUser: null as unknown, authStateReady: vi.fn(async () => {}) },
  getAuth: vi.fn(), clear: vi.fn(async () => {}), signOut: vi.fn(), signIn: vi.fn(), persistence: vi.fn(async () => {}),
  start: vi.fn(), finish: vi.fn(), cancel: vi.fn(), guard: null as null | ((user: unknown) => void),
  suspended: false, activity: new Set<() => void>(), controller: null as AbortController | null,
}));
vi.mock('../firebase/maestroFirebaseService', () => ({ maestroFirebaseService: { getAuth: mocks.getAuth } }));
vi.mock('../../core/security/managedAccessSessionStorage', () => ({ clearManagedAccessSession: mocks.clear }));
vi.mock('../../platform/browser/sessionActivity', () => ({ sessionActivity: { status: () => ({ suspended: mocks.suspended }),
  subscribe: (callback: () => void) => { mocks.activity.add(callback); return () => mocks.activity.delete(callback); } } }));
vi.mock('./questPairingService', () => ({ questPairingService: { start: mocks.start, cancel: mocks.cancel } }));
vi.mock('firebase/auth', () => ({ signOut: mocks.signOut, signInWithCustomToken: mocks.signIn, setPersistence: mocks.persistence,
  browserLocalPersistence: 'local-persistence', beforeAuthStateChanged: (_auth: unknown, guard: (user: unknown) => void) => {
    mocks.guard = guard; return () => { mocks.guard = null; };
  } }));
const user = { uid: 'original-uid', email: 'owner@example.test', displayName: 'Owner', photoURL: null, refreshToken: 'refresh', getIdToken: async () => 'id-token' };
const flush = async () => { for (let i = 0; i < 20; i++) await Promise.resolve(); };
beforeEach(() => {
  vi.resetModules(); vi.clearAllMocks();
  const storage = new Map<string, string>();
  Object.defineProperty(window, 'localStorage', { configurable: true, value: { getItem: (key: string) => storage.get(key) ?? null, setItem: (key: string, value: string) => storage.set(key, value), removeItem: (key: string) => storage.delete(key) } }); mocks.auth.currentUser = null; mocks.suspended = false; mocks.activity.clear(); mocks.guard = null;
  mocks.getAuth.mockResolvedValue(mocks.auth);
  mocks.signOut.mockImplementation(async () => { mocks.guard?.(null); mocks.auth.currentUser = null; });
  mocks.signIn.mockImplementation(async () => { mocks.guard?.(user); mocks.auth.currentUser = user; return { user }; });
  mocks.start.mockImplementation(async (signal: AbortSignal) => {
    const controller = new AbortController(); mocks.controller = controller;
    signal.addEventListener('abort', () => controller.abort(signal.reason), { once: true });
    const check = () => { if (controller.signal.aborted) throw controller.signal.reason; };
    return { token: 'custom.token.private', signal: controller.signal, check, finish: mocks.finish };
  });
  mocks.cancel.mockImplementation((reason = new DOMException('Cancelled', 'AbortError')) => mocks.controller?.abort(reason));
});
it('holds identity unpublished until the shared backend handshake commits', async () => {
  const module = await import('./questFirebaseIdentity');
  const identity = await module.beginQuestIdentity(new AbortController().signal);
  expect(identity.user.id).toBe('original-uid'); expect(identity.firebaseIdToken).toBe('id-token');
  expect(module.isQuestIdentityPending()).toBe(true); expect(window.localStorage.getItem(module.QUEST_PENDING_IDENTITY_KEY)).toBe('pending');
  expect(mocks.signIn).toHaveBeenCalledWith(mocks.auth, 'custom.token.private');
  identity.commit?.(); expect(module.isQuestIdentityPending()).toBe(false);
  expect(window.localStorage.getItem(module.QUEST_PENDING_IDENTITY_KEY)).toBeNull(); expect(mocks.finish).toHaveBeenCalledOnce();
});
it('a late Firebase result after cancellation is rejected and signed out before releasing ownership', async () => {
  let finish!: () => void;
  mocks.signIn.mockImplementation(() => new Promise(resolve => { finish = () => { mocks.auth.currentUser = user; resolve({ user }); }; }));
  const module = await import('./questFirebaseIdentity'), controller = new AbortController();
  const pending = module.beginQuestIdentity(controller.signal); const rejected = expect(pending).rejects.toMatchObject({ name: 'AbortError' });
  await vi.waitFor(() => expect(mocks.signIn).toHaveBeenCalledOnce());
  controller.abort(); await expect(module.beginQuestIdentity(new AbortController().signal)).rejects.toMatchObject({ code: 'busy' });
  finish(); await rejected; expect(mocks.auth.currentUser).toBeNull(); expect(mocks.signOut).toHaveBeenCalledOnce();
  expect(window.localStorage.getItem(module.QUEST_PENDING_IDENTITY_KEY)).toBeNull(); expect(module.isQuestIdentityPending()).toBe(false);
});
it('Firebase blocking hook refuses a cancelled user and preserves cancellation through the SDK error wrapper', async () => {
  const controller = new AbortController();
  mocks.signIn.mockImplementation(async () => {
    controller.abort();
    try { mocks.guard?.(user); }
    catch { throw Object.assign(new Error('Login blocked by user-provided method'), { code: 'auth/login-blocked' }); }
    mocks.auth.currentUser = user; return { user };
  });
  const module = await import('./questFirebaseIdentity');
  await expect(module.beginQuestIdentity(controller.signal)).rejects.toMatchObject({ name: 'AbortError' });
  expect(mocks.auth.currentUser).toBeNull();
});
it('cancellation while Firebase initialization is pending cannot later start pairing', async () => {
  let finish!: (value: unknown) => void;
  mocks.getAuth.mockImplementation(() => new Promise(resolve => { finish = resolve; }));
  const module = await import('./questFirebaseIdentity'); const pending = module.beginQuestIdentity(new AbortController().signal);
  const rejected = expect(pending).rejects.toMatchObject({ name: 'AbortError' }); await flush();
  const cancelled = module.cancelQuestIdentity(); finish(mocks.auth); await rejected; await cancelled;
  expect(mocks.start).not.toHaveBeenCalled();
});
it('backgrounding during final sign-in invalidates the transaction', async () => {
  const module = await import('./questFirebaseIdentity'); const identity = await module.beginQuestIdentity(new AbortController().signal);
  mocks.suspended = true; for (const listener of mocks.activity) listener();
  expect(() => identity.commit?.()).toThrow('interrupted'); await identity.rollback?.(); expect(mocks.auth.currentUser).toBeNull();
});
it('a crash marker clears both SDK identity and cached managed session before restoration', async () => {
  const module = await import('./questFirebaseIdentity'); mocks.auth.currentUser = user;
  window.localStorage.setItem(module.QUEST_PENDING_IDENTITY_KEY, 'pending');
  await module.recoverQuestIdentity(mocks.auth as never); await module.recoverQuestIdentity(mocks.auth as never);
  expect(mocks.signOut).toHaveBeenCalledOnce(); expect(mocks.clear).toHaveBeenCalledOnce(); expect(mocks.auth.currentUser).toBeNull();
});
it('failed cleanup keeps the marker and cannot be mistaken for successful sign-out', async () => {
  const module = await import('./questFirebaseIdentity'); const identity = await module.beginQuestIdentity(new AbortController().signal);
  mocks.signOut.mockRejectedValueOnce(new Error('private failure'));
  await expect(identity.rollback?.()).rejects.toMatchObject({ code: 'cleanup' });
  expect(window.localStorage.getItem(module.QUEST_PENDING_IDENTITY_KEY)).toBe('pending');
  await module.recoverQuestIdentity(mocks.auth as never); expect(mocks.auth.currentUser).toBeNull();
});
it('does not replace an existing signed-in user implicitly', async () => {
  mocks.auth.currentUser = user; const module = await import('./questFirebaseIdentity');
  await expect(module.beginQuestIdentity(new AbortController().signal)).rejects.toMatchObject({ code: 'already-signed-in' });
  expect(mocks.signOut).not.toHaveBeenCalled(); expect(mocks.start).not.toHaveBeenCalled();
});
it('never forwards raw Firebase errors or credentials to chat-visible errors', async () => {
  mocks.signIn.mockRejectedValue(new Error('private custom.token.secret'));
  const module = await import('./questFirebaseIdentity');
  await expect(module.beginQuestIdentity(new AbortController().signal)).rejects.toMatchObject({ code: 'sign-in', message: 'Could not finish sign-in. Return to the book and try again.' });
});
it('rollback is idempotent and a rolled-back identity cannot be committed', async () => {
  const module = await import('./questFirebaseIdentity'); const identity = await module.beginQuestIdentity(new AbortController().signal);
  await identity.rollback?.(); await identity.rollback?.(); expect(mocks.signOut).toHaveBeenCalledOnce();
  expect(() => identity.commit?.()).toThrow('cancelled');
});
