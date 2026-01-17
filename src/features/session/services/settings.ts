// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { openDB, SETTINGS_STORE } from '../storage/db';
import type { AppSettings } from '../types';

const SETTINGS_KEY = 'singleton';

export async function getAppSettingsDB(): Promise<AppSettings | null> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(SETTINGS_STORE, 'readonly');
    const st = tx.objectStore(SETTINGS_STORE);
    const req = st.get(SETTINGS_KEY);
    req.onerror = () => reject(new Error('Error reading app settings from DB'));
    req.onsuccess = () => resolve(req.result ? (req.result.settings as AppSettings) : null);
  });
}

export async function setAppSettingsDB(settings: AppSettings): Promise<void> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(SETTINGS_STORE, 'readwrite');
    const st = tx.objectStore(SETTINGS_STORE);
    const req = st.put({ key: SETTINGS_KEY, settings });
    req.onerror = () => reject(new Error('Error saving app settings to DB'));
    req.onsuccess = () => resolve();
  });
}
