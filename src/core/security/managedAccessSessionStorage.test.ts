// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import type { ManagedAccessSession } from '../contracts/backend';
vi.mock('@capacitor/core', () => ({ Capacitor: { isNativePlatform: () => false } }));
vi.mock('@aparajita/capacitor-secure-storage', () => ({ SecureStorage: {} }));
const storage = { getItem: vi.fn(), setItem: vi.fn(), removeItem: vi.fn() };
const session = { user: { id: 'u' }, firebaseIdToken: 'private' } as ManagedAccessSession;
beforeEach(() => { vi.resetModules(); vi.resetAllMocks(); vi.stubGlobal('window', { localStorage: storage, dispatchEvent: vi.fn() }); });
afterEach(() => vi.unstubAllGlobals());
it('strict transaction persistence surfaces a quota failure without publishing a cached account', async () => {
  storage.setItem.mockImplementation(() => { throw new Error('quota'); });
  const module = await import('./managedAccessSessionStorage');
  await expect(module.saveManagedAccessSession(session, { requirePersistence: true })).rejects.toThrow('could not be saved or cleared');
  expect(module.getCachedManagedAccessSession()).toBeUndefined();
});
it('strict cleanup cannot report success while session removal fails', async () => {
  const module = await import('./managedAccessSessionStorage'); await module.saveManagedAccessSession(session, { requirePersistence: true });
  storage.removeItem.mockImplementation(() => { throw new Error('unavailable'); });
  await expect(module.clearManagedAccessSession({ requirePersistence: true })).rejects.toThrow('could not be saved or cleared');
  expect(module.getCachedManagedAccessSession()).toBe(session);
  storage.removeItem.mockImplementation(() => {}); await module.clearManagedAccessSession({ requirePersistence: true });
  expect(module.getCachedManagedAccessSession()).toBeNull();
});
it('ordinary best-effort storage retains its previous non-throwing contract', async () => {
  storage.setItem.mockImplementation(() => { throw new Error('quota'); }); storage.removeItem.mockImplementation(() => { throw new Error('unavailable'); });
  const module = await import('./managedAccessSessionStorage');
  await expect(module.saveManagedAccessSession(session)).resolves.toBeUndefined();
  await expect(module.clearManagedAccessSession()).resolves.toBeUndefined();
});
