// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export interface GlobalUserProfile {
  key: 'singleton';
  text: string;
  updatedAt: number;
  fingerprint: string;
}

import { openDB, GLOBAL_PROFILE_STORE } from '../storage/db';

export async function getGlobalProfileDB(): Promise<GlobalUserProfile | null> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(GLOBAL_PROFILE_STORE, 'readonly');
    const st = tx.objectStore(GLOBAL_PROFILE_STORE);
    const req = st.get('singleton');
    req.onerror = () => reject(new Error('Error reading global profile'));
    req.onsuccess = () => resolve(req.result || null);
  });
}

export async function setGlobalProfileDB(text: string): Promise<void> {
  const db = await openDB();
  const trimmed = (text || '').trim();
  const fp = Math.random().toString(36);
  const obj: GlobalUserProfile = { key: 'singleton', text: trimmed, updatedAt: Date.now(), fingerprint: fp };
  return new Promise((resolve, reject) => {
    const tx = db.transaction(GLOBAL_PROFILE_STORE, 'readwrite');
    const st = tx.objectStore(GLOBAL_PROFILE_STORE);
    const req = st.put(obj);
    req.onerror = () => reject(new Error('Error saving global profile'));
    req.onsuccess = () => resolve();
  });
}
