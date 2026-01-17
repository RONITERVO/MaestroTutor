// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { openDB, ASSETS_STORE } from '../storage/db';

const LOADING_GIFS_KEY = 'loadingGifs';
const MAESTRO_PROFILE_KEY = 'maestroProfileImage';

export type MaestroProfileAsset = {
  dataUrl?: string;
  mimeType?: string;
  uri?: string;
  updatedAt?: number;
};

export async function getLoadingGifsDB(): Promise<string[] | null> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(ASSETS_STORE, 'readonly');
    const st = tx.objectStore(ASSETS_STORE);
    const req = st.get(LOADING_GIFS_KEY);
    req.onerror = () => reject(new Error('Error reading loading gifs from DB'));
    req.onsuccess = () => resolve(req.result ? (req.result.value as string[]) : null);
  });
}

export async function setLoadingGifsDB(gifs: string[]): Promise<void> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(ASSETS_STORE, 'readwrite');
    const st = tx.objectStore(ASSETS_STORE);
    const req = st.put({ key: LOADING_GIFS_KEY, value: gifs });
    req.onerror = () => reject(new Error('Error saving loading gifs to DB'));
    req.onsuccess = () => resolve();
  });
}

export async function getMaestroProfileImageDB(): Promise<MaestroProfileAsset | null> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(ASSETS_STORE, 'readonly');
    const st = tx.objectStore(ASSETS_STORE);
    const req = st.get(MAESTRO_PROFILE_KEY);
    req.onerror = () => reject(new Error('Error reading maestro profile from DB'));
    req.onsuccess = () => resolve(req.result ? (req.result.value as MaestroProfileAsset) : null);
  });
}

export async function setMaestroProfileImageDB(asset: MaestroProfileAsset): Promise<void> {
  const db = await openDB();
  const value: MaestroProfileAsset = { ...(asset || {}), updatedAt: Date.now() };
  return new Promise((resolve, reject) => {
    const tx = db.transaction(ASSETS_STORE, 'readwrite');
    const st = tx.objectStore(ASSETS_STORE);
    const req = st.put({ key: MAESTRO_PROFILE_KEY, value });
    req.onerror = () => reject(new Error('Error saving maestro profile to DB'));
    req.onsuccess = () => resolve();
  });
}

export async function clearMaestroProfileImageDB(): Promise<void> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(ASSETS_STORE, 'readwrite');
    const st = tx.objectStore(ASSETS_STORE);
    const req = st.delete(MAESTRO_PROFILE_KEY);
    req.onerror = () => reject(new Error('Error clearing maestro profile from DB'));
    req.onsuccess = () => resolve();
  });
}
