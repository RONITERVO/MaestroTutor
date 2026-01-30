// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * API Key Storage
 *
 * Stores the user's Gemini API key locally.
 * - Native (Capacitor): Secure Storage (Keychain/Keystore)
 * - Web: localStorage fallback
 *
 * NOTE: This does NOT send the key to any backend.
 */

import { Capacitor } from '@capacitor/core';
import { SecureStorage } from '@aparajita/capacitor-secure-storage';
import { Preferences } from '@capacitor/preferences';

const API_KEY_STORAGE_KEY = 'maestro.geminiApiKey.v1';

let cachedKey: string | null | undefined = undefined;

const isNative = Capacitor.isNativePlatform();
const safeWindow = () => (typeof window !== 'undefined' ? window : undefined);

const readLocalStorage = (): string | null => {
  try {
    const win = safeWindow();
    if (!win?.localStorage) return null;
    const value = win.localStorage.getItem(API_KEY_STORAGE_KEY);
    return value ? value : null;
  } catch {
    return null;
  }
};

const writeLocalStorage = (value: string) => {
  try {
    const win = safeWindow();
    if (!win?.localStorage) return;
    win.localStorage.setItem(API_KEY_STORAGE_KEY, value);
  } catch {
    // ignore
  }
};

const removeLocalStorage = () => {
  try {
    const win = safeWindow();
    if (!win?.localStorage) return;
    win.localStorage.removeItem(API_KEY_STORAGE_KEY);
  } catch {
    // ignore
  }
};

export const normalizeApiKey = (value: string) => value.trim();

export const isLikelyApiKey = (value: string) => {
  const trimmed = normalizeApiKey(value);
  if (trimmed.length < 20) return false;
  return true;
};

// Legacy migration: previously stored in Preferences (unencrypted).
const loadFromPreferences = async (): Promise<string | null> => {
  try {
    const { value } = await Preferences.get({ key: API_KEY_STORAGE_KEY });
    return value ? value : null;
  } catch {
    return null;
  }
};

const removeFromPreferences = async (): Promise<boolean> => {
  try {
    await Preferences.remove({ key: API_KEY_STORAGE_KEY });
    return true;
  } catch {
    return false;
  }
};

const loadFromSecureStorage = async (): Promise<string | null> => {
  try {
    const value = await SecureStorage.getItem(API_KEY_STORAGE_KEY);
    return value ?? null;
  } catch {
    return null;
  }
};

const saveToSecureStorage = async (value: string): Promise<boolean> => {
  try {
    await SecureStorage.setItem(API_KEY_STORAGE_KEY, value);
    return true;
  } catch {
    return false;
  }
};

const removeFromSecureStorage = async (): Promise<boolean> => {
  try {
    await SecureStorage.removeItem(API_KEY_STORAGE_KEY);
    return true;
  } catch {
    return false;
  }
};

export const loadApiKey = async (): Promise<string | null> => {
  if (cachedKey !== undefined) return cachedKey;

  let value: string | null = null;

  if (isNative) {
    value = await loadFromSecureStorage();
    if (!value) {
      const legacy = await loadFromPreferences();
      if (legacy) {
        const migrated = await saveToSecureStorage(legacy);
        if (migrated) {
          await removeFromPreferences();
          value = legacy;
        }
      }
    }
  } else {
    value = readLocalStorage();
  }

  if (!value && typeof import.meta !== 'undefined' && (import.meta as any).env?.DEV) {
    const devKey = (import.meta as any).env?.VITE_API_KEY;
    value = typeof devKey === 'string' && devKey.trim() ? devKey.trim() : null;
  }

  cachedKey = value || null;
  return cachedKey;
};

export const setApiKey = async (rawValue: string): Promise<void> => {
  const value = normalizeApiKey(rawValue);
  cachedKey = value || null;

  if (!value) {
    if (isNative) {
      await removeFromSecureStorage();
      await removeFromPreferences();
    }
    removeLocalStorage();
    return;
  }

  if (isNative) {
    const saved = await saveToSecureStorage(value);
    if (!saved) {
      throw new Error('Secure storage unavailable');
    }
    await removeFromPreferences();
  } else {
    writeLocalStorage(value);
  }
};

export const clearApiKey = async (): Promise<void> => {
  cachedKey = null;
  if (isNative) {
    await removeFromSecureStorage();
    await removeFromPreferences();
  }
  removeLocalStorage();
};

export const getCachedApiKey = (): string | null | undefined => cachedKey;

export const getApiKeyOrThrow = async (): Promise<string> => {
  const key = await loadApiKey();
  if (!key) {
    const err = new Error('Missing API key. Tap "API Key" in the top-right and paste your Gemini key.');
    (err as any).code = 'MISSING_API_KEY';
    throw err;
  }
  return key;
};
