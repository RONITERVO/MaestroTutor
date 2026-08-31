// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { Capacitor } from '@capacitor/core';
import { SecureStorage } from '@aparajita/capacitor-secure-storage';
import type { ManagedAccessSession } from '../contracts/backend';

const STORAGE_KEY = 'maestro.managedAccessSession.v1';

export const MANAGED_ACCESS_CHANGED_EVENT = 'maestro-managed-access-changed';

let cachedSession: ManagedAccessSession | null | undefined;

const isNative = Capacitor.isNativePlatform();

const safeWindow = () => (typeof window !== 'undefined' ? window : undefined);

const dispatchManagedAccessChanged = (session: ManagedAccessSession | null) => {
  try {
    const win = safeWindow();
    if (!win) return;
    win.dispatchEvent(new CustomEvent(MANAGED_ACCESS_CHANGED_EVENT, { detail: { session } }));
  } catch {
    // Ignore dispatch failures.
  }
};

const readLocalStorage = (): ManagedAccessSession | null => {
  try {
    const win = safeWindow();
    const raw = win?.localStorage?.getItem(STORAGE_KEY);
    if (!raw) return null;
    return JSON.parse(raw) as ManagedAccessSession;
  } catch {
    return null;
  }
};

const writeLocalStorage = (session: ManagedAccessSession) => {
  try {
    const win = safeWindow();
    win?.localStorage?.setItem(STORAGE_KEY, JSON.stringify(session));
  } catch {
    // Ignore persistence failures.
  }
};

const removeLocalStorage = () => {
  try {
    const win = safeWindow();
    win?.localStorage?.removeItem(STORAGE_KEY);
  } catch {
    // Ignore persistence failures.
  }
};

const loadFromSecureStorage = async (): Promise<ManagedAccessSession | null> => {
  try {
    const raw = await SecureStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) as ManagedAccessSession : null;
  } catch {
    return null;
  }
};

const saveToSecureStorage = async (session: ManagedAccessSession): Promise<void> => {
  await SecureStorage.setItem(STORAGE_KEY, JSON.stringify(session));
};

const removeFromSecureStorage = async (): Promise<void> => {
  try {
    await SecureStorage.removeItem(STORAGE_KEY);
  } catch {
    // Ignore persistence failures.
  }
};

export const loadManagedAccessSession = async (): Promise<ManagedAccessSession | null> => {
  if (cachedSession !== undefined) return cachedSession;
  cachedSession = isNative ? await loadFromSecureStorage() : readLocalStorage();
  return cachedSession;
};

export const saveManagedAccessSession = async (session: ManagedAccessSession): Promise<void> => {
  cachedSession = session;
  if (isNative) {
    await saveToSecureStorage(session);
  } else {
    writeLocalStorage(session);
  }
  dispatchManagedAccessChanged(session);
};

export const clearManagedAccessSession = async (): Promise<void> => {
  cachedSession = null;
  if (isNative) {
    await removeFromSecureStorage();
  } else {
    removeLocalStorage();
  }
  dispatchManagedAccessChanged(null);
};

export const getCachedManagedAccessSession = (): ManagedAccessSession | null | undefined => cachedSession;

export const hasManagedSession = (session: ManagedAccessSession | null | undefined): boolean => (
  Boolean(session?.firebaseIdToken && session?.user?.id)
);

const isManagedAccessSession = (
  session: ManagedAccessSession | null | undefined
): session is ManagedAccessSession => hasManagedSession(session);

export const hasManagedCredits = (session: ManagedAccessSession | null | undefined): boolean => (
  hasManagedSession(session) && (session?.billingSummary?.availableCredits || 0) > 0
);

export const getManagedAccessSessionOrThrow = async (): Promise<ManagedAccessSession> => {
  const session = await loadManagedAccessSession();
  if (!isManagedAccessSession(session)) {
    throw new Error('Managed access session is missing.');
  }
  return session;
};
