// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export const DB_NAME = 'GeminiLanguageTutorDB';
export const DB_VERSION = 7;
export const STORE_NAME = 'chatHistories';
export const META_STORE = 'chatMetas';
export const GLOBAL_PROFILE_STORE = 'globalProfile';
export const SETTINGS_STORE = 'appSettings';
export const ASSETS_STORE = 'appAssets';

export const openDB = (): Promise<IDBDatabase> => {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);

    request.onerror = () => reject(new Error('Error opening IndexedDB'));
    request.onsuccess = () => resolve(request.result);

    request.onupgradeneeded = (event) => {
      const db = (event.target as IDBOpenDBRequest).result;
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        db.createObjectStore(STORE_NAME, { keyPath: 'pairId' });
      }
      if (!db.objectStoreNames.contains(META_STORE)) {
        db.createObjectStore(META_STORE, { keyPath: 'pairId' });
      }
      if (!db.objectStoreNames.contains(GLOBAL_PROFILE_STORE)) {
        db.createObjectStore(GLOBAL_PROFILE_STORE, { keyPath: 'key' });
      }
      if (!db.objectStoreNames.contains(SETTINGS_STORE)) {
        db.createObjectStore(SETTINGS_STORE, { keyPath: 'key' });
      }
      if (!db.objectStoreNames.contains(ASSETS_STORE)) {
        db.createObjectStore(ASSETS_STORE, { keyPath: 'key' });
      }
    };
  });
};