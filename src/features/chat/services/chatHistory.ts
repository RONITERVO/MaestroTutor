// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { openDB, STORE_NAME, META_STORE, GLOBAL_PROFILE_STORE, AGENT_TASK_STORE, AGENT_TASK_SUMMARY_STORE } from '../../../core/db/index';
import { ChatMessage, ChatMeta } from '../../../core/types';
import { hasRoomTaskSources, projectRoomTaskSummaries } from '../../../core-sdk/room/roomTaskProjection';
import { readRoomTaskSummaries, clearRoomTaskVisibility } from './roomTaskSummaries';
import { sanitizeForPersistence } from '../utils/persistence';
export { deriveHistoryForApi } from '../../../core-sdk/chat/history';

export const getChatHistoryDB = async (pairId: string): Promise<ChatMessage[]> => {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction([STORE_NAME, AGENT_TASK_SUMMARY_STORE], 'readonly');
    let messages: ChatMessage[] = [];
    tx.oncomplete = () => { db.close(); resolve(messages); };
    tx.onabort = tx.onerror = () => { db.close(); reject(tx.error || new Error('Error fetching history from DB')); };
    const request = tx.objectStore(STORE_NAME).get(pairId);
    request.onsuccess = () => {
      readRoomTaskSummaries(tx, pairId, summaries => {
        messages = projectRoomTaskSummaries(request.result?.messages || [], summaries, pairId);
      });
    };
  });
};
export const saveChatHistoryDB = async (pairId: string, messages: ChatMessage[]): Promise<void> => {
  if (!pairId) return;
  const messagesToSave = messages.filter(msg => msg.role !== 'system_selection').map(sanitizeForPersistence);
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction([STORE_NAME, AGENT_TASK_STORE, AGENT_TASK_SUMMARY_STORE], 'readwrite');
    tx.oncomplete = () => { db.close(); resolve(); };
    tx.onabort = tx.onerror = () => { db.close(); reject(tx.error || new Error('History transaction was aborted')); };
    readRoomTaskSummaries(tx, pairId, summaries => {
      for (const summary of summaries) {
        if (!hasRoomTaskSources(messagesToSave, summary)) {
          tx.objectStore(AGENT_TASK_STORE).delete(summary.id);
          tx.objectStore(AGENT_TASK_SUMMARY_STORE).delete(summary.id);
        }
      }
      // An old autosave cannot overwrite a newer durable task result. Source
      // deletion still wins, removing both the journal and its chat projection.
      tx.objectStore(STORE_NAME).put({ pairId,
        messages: projectRoomTaskSummaries(messagesToSave, summaries, pairId).map(sanitizeForPersistence) });
    });
  });
};

export const safeSaveChatHistoryDB = async (pairId: string, messages: ChatMessage[], retries = 1): Promise<boolean> => {
  try {
    await saveChatHistoryDB(pairId, messages);
    return true;
  } catch (e) {
    if (retries > 0) {
      await Promise.resolve();
      return safeSaveChatHistoryDB(pairId, messages, retries - 1);
    }
    console.warn('IndexedDB save failed for pair:', pairId, e);
    return false;
  }
};

export const getAllChatHistoriesDB = async (): Promise<Record<string, ChatMessage[]>> => {
  const histories: Record<string, ChatMessage[]> = {};
  await iterateChatHistoriesDB((pairId, messages) => { histories[pairId] = messages; });
  return histories;
};

export const hasAnyChatHistoriesDB = async (): Promise<boolean> => {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORE_NAME, "readonly");
    const store = transaction.objectStore(STORE_NAME);
    const cursorReq = store.openCursor();
    cursorReq.onerror = () => reject(new Error("Error checking for chat histories"));
    cursorReq.onsuccess = (ev) => {
      const cursor = (ev.target as IDBRequest<IDBCursorWithValue>).result;
      resolve(!!cursor);
    };
  });
};

export const iterateChatHistoriesDB = async (
  onRow: (pairId: string, messages: ChatMessage[]) => Promise<void> | void
): Promise<void> => {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORE_NAME, "readonly");
    const store = transaction.objectStore(STORE_NAME);
    transaction.oncomplete = () => db.close();
    transaction.onabort = transaction.onerror = () => { db.close(); reject(transaction.error || new Error("Error iterating histories")); };
    const cursorReq = store.openKeyCursor();
    let chain: Promise<void> = Promise.resolve();
    let settled = false;
    cursorReq.onerror = () => reject(new Error("Error iterating chat histories"));
    cursorReq.onsuccess = (ev) => {
      const cursor = (ev.target as IDBRequest<IDBCursor>).result;
      if (!cursor) {
        chain.then(() => {
          if (!settled) {
            settled = true;
            resolve();
          }
        }).catch((err) => {
          if (!settled) {
            settled = true;
            reject(err instanceof Error ? err : new Error(String(err)));
          }
        });
        return;
      }
      const pairId = typeof cursor.primaryKey === 'string' ? cursor.primaryKey : '';
      chain = chain.then(async () => { await onRow(pairId, await getChatHistoryDB(pairId)); });
      cursor.continue();
    };
  });
};

export const clearAndSaveAllHistoriesDB = async (
  allChats: Record<string, ChatMessage[]>,
  allMetas?: Record<string, ChatMeta> | null,
  globalProfileText?: string | null
): Promise<void> => {
    const db = await openDB();
    return new Promise((resolve, reject) => {
  const transaction = db.transaction([STORE_NAME, META_STORE, GLOBAL_PROFILE_STORE, AGENT_TASK_STORE, AGENT_TASK_SUMMARY_STORE], "readwrite");
  const store = transaction.objectStore(STORE_NAME);
  const metaStore = transaction.objectStore(META_STORE);
  const profileStore = transaction.objectStore(GLOBAL_PROFILE_STORE);

        transaction.oncomplete = () => { clearRoomTaskVisibility(); db.close(); resolve(); };
        transaction.onabort = transaction.onerror = () => { db.close(); reject(new Error("Transaction error during bulk save")); };
        
        const clearRequest = store.clear();
        const clearMetaReq = metaStore.clear();
  const clearProfileReq = profileStore.clear();
        transaction.objectStore(AGENT_TASK_STORE).clear();
        transaction.objectStore(AGENT_TASK_SUMMARY_STORE).clear();
        clearRequest.onerror = () => reject(new Error("Error clearing store before bulk save"));
        clearMetaReq.onerror = () => reject(new Error("Error clearing meta store before bulk save"));
        clearProfileReq.onerror = () => reject(new Error("Error clearing profile store before bulk save"));
        clearMetaReq.onsuccess = () => {};
        clearProfileReq.onsuccess = () => {};
        clearRequest.onsuccess = () => {
          for (const pairId in allChats) {
              if (Object.prototype.hasOwnProperty.call(allChats, pairId)) {
                  const messagesToSave = allChats[pairId].filter(msg => msg.role !== 'system_selection');
                  store.add({ pairId, messages: messagesToSave });
              }
          }
          if (allMetas) {
            for (const pairId in allMetas) {
              if (Object.prototype.hasOwnProperty.call(allMetas, pairId)) {
                metaStore.add({ pairId, meta: allMetas[pairId] });
              }
            }
          }
          const text = (globalProfileText || '').trim();
          if (text) {
            try { profileStore.put({ key: 'singleton', text, updatedAt: Date.now(), fingerprint: '' }); } catch {}
          }
        };
    });
};

export const getChatMetaDB = async (pairId: string): Promise<ChatMeta | null> => {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(META_STORE, 'readonly');
    const st = tx.objectStore(META_STORE);
    const req = st.get(pairId);
    req.onerror = () => reject(new Error('Error fetching chat meta from DB'));
    req.onsuccess = () => resolve(req.result ? (req.result.meta as ChatMeta) : null);
  });
};

export const getAllChatMetasDB = async (): Promise<Record<string, ChatMeta>> => {
  const db = await openDB();
  const tryGetAll = (): Promise<any[]> => new Promise((resolve, reject) => {
    const tx = db.transaction(META_STORE, 'readonly');
    const st = tx.objectStore(META_STORE) as IDBObjectStore & { getAll?: () => IDBRequest<any[]> };
    if (typeof st.getAll === 'function') {
      const req = st.getAll!();
      req.onerror = () => reject(req.error || new Error('getAll() failed for metas'));
      req.onsuccess = () => resolve(req.result || []);
    } else {
      reject(new Error('getAll not supported'));
    }
  });

  try {
    const rows = await tryGetAll();
    const metas: Record<string, ChatMeta> = {};
    (rows || []).forEach((row: any) => { metas[row.pairId] = row.meta as ChatMeta; });
    return metas;
  } catch {
    return await new Promise((resolve, reject) => {
      const tx = db.transaction(META_STORE, 'readonly');
      const st = tx.objectStore(META_STORE);
      const out: Record<string, ChatMeta> = {};
      const cursorReq = st.openCursor();
      cursorReq.onerror = () => reject(new Error('Error fetching all chat metas from DB'));
      cursorReq.onsuccess = (ev) => {
        const cursor = (ev.target as IDBRequest<IDBCursorWithValue>).result;
        if (cursor) {
          const val: any = cursor.value;
          if (val && typeof val.pairId === 'string') {
            out[val.pairId] = val.meta as ChatMeta;
          }
          cursor.continue();
        } else {
          resolve(out);
        }
      };
    });
  }
};

export const setChatMetaDB = async (pairId: string, meta: ChatMeta | null): Promise<void> => {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(META_STORE, 'readwrite');
    const st = tx.objectStore(META_STORE);
    if (meta) {
      const getReq = st.get(pairId);
      getReq.onerror = () => reject(new Error('Error reading chat meta to merge'));
      getReq.onsuccess = () => {
        const existing = getReq.result?.meta || {};
        const merged = { ...existing, ...meta } as ChatMeta;
        const req = st.put({ pairId, meta: merged });
        req.onerror = () => reject(new Error('Error saving chat meta to DB'));
        req.onsuccess = () => resolve();
      };
    } else {
      const req = st.delete(pairId);
      req.onerror = () => reject(new Error('Error deleting chat meta from DB'));
      req.onsuccess = () => resolve();
    }
  });
};
