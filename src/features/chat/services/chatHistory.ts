
import { openDB, STORE_NAME, META_STORE, GLOBAL_PROFILE_STORE } from '../../../core/db/index';
import { ChatMessage, ChatMeta, UserProfile } from '../../../core/types';
import { sanitizeForPersistence } from '../utils/persistence';
import { MAX_MEDIA_TO_KEEP } from '../../../core/config/app';

export const getChatHistoryDB = async (pairId: string): Promise<ChatMessage[]> => {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORE_NAME, "readonly");
    const store = transaction.objectStore(STORE_NAME);
    const request = store.get(pairId);

    request.onerror = () => reject(new Error("Error fetching history from DB"));
    request.onsuccess = () => {
      resolve(request.result ? request.result.messages : []);
    };
  });
};

export const saveChatHistoryDB = async (pairId: string, messages: ChatMessage[]): Promise<void> => {
  if (!pairId) return;
  const messagesToSave = messages
    .filter(msg => msg.role !== 'system_selection')
    .map(sanitizeForPersistence);
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORE_NAME, "readwrite");
    const store = transaction.objectStore(STORE_NAME);
    const request = store.put({ pairId, messages: messagesToSave });

    request.onerror = () => reject(new Error("Error saving history to DB"));
    request.onsuccess = () => resolve();
  });
};

export const backupKeyForPair = (pairId: string) => `chatBackup:${pairId}`;
export const writeBackupForPair = (pairId: string, messages: ChatMessage[]) => {
  try { window.localStorage.setItem(backupKeyForPair(pairId), JSON.stringify(messages)); } catch {}
};
export const readBackupForPair = (pairId: string): ChatMessage[] | null => {
  try {
    const raw = window.localStorage.getItem(backupKeyForPair(pairId));
    return raw ? (JSON.parse(raw) as ChatMessage[]) : null;
  } catch { return null; }
};
export const clearBackupForPair = (pairId: string) => { try { window.localStorage.removeItem(backupKeyForPair(pairId)); } catch {} };

export const safeSaveChatHistoryDB = async (pairId: string, messages: ChatMessage[], retries = 1): Promise<boolean> => {
  try {
    await saveChatHistoryDB(pairId, messages);
    clearBackupForPair(pairId);
    return true;
  } catch (e) {
    if (retries > 0) {
      await Promise.resolve();
      return safeSaveChatHistoryDB(pairId, messages, retries - 1);
    }
    writeBackupForPair(pairId, messages);
    console.warn('IndexedDB save failed; kept a temporary backup in localStorage for pair:', pairId, e);
    return false;
  }
};

export const getAllChatHistoriesDB = async (): Promise<Record<string, ChatMessage[]>> => {
  const db = await openDB();
  const tryGetAll = (): Promise<any[]> => new Promise((resolve, reject) => {
    const transaction = db.transaction(STORE_NAME, "readonly");
    const store = transaction.objectStore(STORE_NAME) as IDBObjectStore & { getAll?: () => IDBRequest<any[]> };
    if (typeof store.getAll === 'function') {
      const req = store.getAll!();
      req.onsuccess = () => resolve(req.result || []);
      req.onerror = () => reject(req.error || new Error("getAll() failed"));
    } else {
      reject(new Error('getAll not supported'));
    }
  });

  try {
    const rows = await tryGetAll();
    const allChats: Record<string, ChatMessage[]> = {};
    rows.forEach((item: any) => { allChats[item.pairId] = item.messages; });
    return allChats;
  } catch (_) {
    return await new Promise((resolve, reject) => {
      const transaction = db.transaction(STORE_NAME, "readonly");
      const store = transaction.objectStore(STORE_NAME);
      const result: Record<string, ChatMessage[]> = {};
      const cursorReq = store.openCursor();
      cursorReq.onerror = () => reject(new Error("Error fetching all histories from DB"));
      cursorReq.onsuccess = (ev) => {
        const cursor = (ev.target as IDBRequest<IDBCursorWithValue>).result;
        if (cursor) {
          const val: any = cursor.value;
          if (val && typeof val.pairId === 'string') {
            result[val.pairId] = val.messages;
          }
          cursor.continue();
        } else {
          resolve(result);
        }
      };
    });
  }
};

export const clearAndSaveAllHistoriesDB = async (
  allChats: Record<string, ChatMessage[]>,
  allMetas?: Record<string, ChatMeta> | null,
  userProfile?: UserProfile | null,
  globalProfileText?: string | null
): Promise<void> => {
    const db = await openDB();
    return new Promise((resolve, reject) => {
  const transaction = db.transaction([STORE_NAME, META_STORE, GLOBAL_PROFILE_STORE], "readwrite");
  const store = transaction.objectStore(STORE_NAME);
  const metaStore = transaction.objectStore(META_STORE);
  const profileStore = transaction.objectStore(GLOBAL_PROFILE_STORE);

        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(new Error("Transaction error during bulk save"));
        
        const clearRequest = store.clear();
        const clearMetaReq = metaStore.clear();
  const clearProfileReq = profileStore.clear();
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
  } catch (_) {
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

type DerivedHistoryItem = { role: 'user' | 'assistant'; text?: string; rawAssistantResponse?: string; imageFileUri?: string; imageMimeType?: string };

export const deriveHistoryForApi = (fullHistory: ChatMessage[], opts?: { roles?: Array<'user' | 'assistant' | 'system'>; maxMessages?: number; maxMediaToKeep?: number; contextSummary?: string; globalProfileText?: string; placeholderLatestUserMessage?: string; }) => {
    const { roles = ['user','assistant'], maxMessages, maxMediaToKeep = MAX_MEDIA_TO_KEEP, contextSummary, globalProfileText, placeholderLatestUserMessage } = opts || {};
    const roleSet = new Set(roles);
    
    // 1. Filter relevant messages
    let filtered = fullHistory.filter(m => {
      if (!roleSet.has(m.role as any)) return false;
      if (m.role === 'user' || m.role === 'assistant') return !m.thinking;
      return true;
    });

    // 2. Trim to maxMessages
    if (typeof maxMessages === 'number' && maxMessages >= 0) {
      if (filtered.length > maxMessages) filtered = maxMessages > 0 ? filtered.slice(-maxMessages) : [];
    }

    // 3. Map to simple API objects
    const history: DerivedHistoryItem[] = filtered.map(m => {
      const uri = (m as any).llmFileUri || m.imageFileUri || undefined;
      const mime = uri ? ((m as any).llmFileMimeType || m.imageMimeType || undefined) : undefined;
      return {
        role: (m.role === 'user' || m.role === 'assistant') ? m.role : 'user',
        text: m.text,
        rawAssistantResponse: m.rawAssistantResponse,
        imageFileUri: uri,
        imageMimeType: mime,
      };
    });

    // 4. Enforce media limits
    if (history.length > 0 && Number.isFinite(maxMediaToKeep) && (maxMediaToKeep as number) >= 0) {
      const mediaIdx: number[] = [];
      for (let i = 0; i < history.length; i++) if (history[i].imageFileUri) mediaIdx.push(i);
      const toKeep = new Set<number>(mediaIdx.slice(-(maxMediaToKeep as number)));
      for (let i = 0; i < history.length; i++) {
          if (history[i].imageFileUri && !toKeep.has(i)) {
              history[i].imageFileUri = undefined; 
              history[i].imageMimeType = undefined; 
          }
      }
    }

    // 5. Build context preface (Global Profile + Chat Summary)
    const contextParts: string[] = [];
    if (globalProfileText && globalProfileText.trim()) {
      const txt = globalProfileText.trim().slice(0, 10000);
      contextParts.push(`Learner Profile (global):\n${txt}\nEND OF GLOBAL PROFILE MEMORY.`);
    }
    if (contextSummary && contextSummary.trim()) {
      contextParts.push(`Conversation Summary:\n${contextSummary.trim().slice(0, 10000)}`);
    }

    // 6. Insert context. 
    // Optimization: If the first history message is 'user', prepend to it to avoid double-user turn.
    // Otherwise, insert as a separate system/user turn at the start.
    if (contextParts.length > 0) {
      const prefaceText = contextParts.join('\n\n');
      if (history.length > 0 && history[0].role === 'user') {
          history[0].text = `${prefaceText}\n\n${history[0].text || ''}`;
      } else {
          history.unshift({ role: 'user', text: prefaceText });
      }
    }

    // 7. Append placeholder latest message if provided (e.g. for Image Gen context)
    if (placeholderLatestUserMessage && placeholderLatestUserMessage.trim()) {
         history.push({ role: 'user', text: placeholderLatestUserMessage.trim().slice(0, 10000) });
    }
    
    return history;
  };
