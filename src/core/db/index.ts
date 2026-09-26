// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { summarizeRoomTask } from '../../core-sdk/room/roomTaskProjection';

export const DB_NAME = 'GeminiLanguageTutorDB';
export const DB_VERSION = 10;
export const STORE_NAME = 'chatHistories';
export const META_STORE = 'chatMetas';
export const GLOBAL_PROFILE_STORE = 'globalProfile';
export const SETTINGS_STORE = 'appSettings';
export const AGENT_TASK_STORE = 'agentTasks';
export const AGENT_TASK_SUMMARY_STORE = 'agentTaskSummaries';
export const ASSETS_STORE = 'appAssets';
export const BACKUP_STAGE_STORE = 'backupStaging';

export const openDB = (): Promise<IDBDatabase> => {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);

    request.onerror = () => reject(new Error('Error opening IndexedDB'));
    request.onsuccess = () => { request.result.onversionchange = () => request.result.close(); resolve(request.result); };

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
      if (!db.objectStoreNames.contains(AGENT_TASK_STORE)) {
        db.createObjectStore(AGENT_TASK_STORE, { keyPath: 'id' });
      }
      if (!db.objectStoreNames.contains(AGENT_TASK_SUMMARY_STORE)) {
        const summaries = db.createObjectStore(AGENT_TASK_SUMMARY_STORE, { keyPath: 'id' });
        summaries.createIndex('conversationId', 'conversationId');
        // Upgrade existing journals one at a time; do not retain their media in a list.
        const cursorRequest = request.transaction!.objectStore(AGENT_TASK_STORE).openCursor();
        cursorRequest.onsuccess = () => {
          const cursor = cursorRequest.result;
          if (!cursor) return;
          summaries.put(summarizeRoomTask(cursor.value));
          cursor.continue();
        };
      }
      if (!db.objectStoreNames.contains(BACKUP_STAGE_STORE)) {
        const staging = db.createObjectStore(BACKUP_STAGE_STORE, { keyPath: ['batch', 'kind', 'id'] });
        staging.createIndex('batch', 'batch'); staging.createIndex('createdAt', 'createdAt');
      }
      if (!db.objectStoreNames.contains(ASSETS_STORE)) {
        db.createObjectStore(ASSETS_STORE, { keyPath: 'key' });
      }
    };
  });
};