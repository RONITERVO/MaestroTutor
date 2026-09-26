// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { openDB, AGENT_TASK_STORE, AGENT_TASK_SUMMARY_STORE, STORE_NAME } from '../../../core/db';
import { summarizeRoomTask, projectRoomTaskSummaries, hasRoomTaskSources } from '../../../core-sdk/room/roomTaskProjection';
import { isRoomTaskHidden } from './roomTaskSummaries';
import type { RoomTaskRecord, RoomTaskStore } from '../../../core-sdk/room/roomTaskHandoff';

/** Resolve on transaction completion, not request success. A failed durable
 * write must prevent dispatch even when IndexedDB accepted the put request. */
async function transaction<T>(mode: IDBTransactionMode, run: (store: IDBObjectStore, result: (value: T) => void) => void): Promise<T> {
  const db = await openDB();
  return new Promise<T>((resolve, reject) => {
    const tx = db.transaction(mode === 'readwrite' ? [AGENT_TASK_STORE, AGENT_TASK_SUMMARY_STORE, STORE_NAME] : AGENT_TASK_STORE, mode);
    let value: T;
    tx.oncomplete = () => { db.close(); resolve(value); };
    tx.onabort = tx.onerror = () => { db.close(); reject(tx.error || new Error('Agent task storage failed.')); };
    run(tx.objectStore(AGENT_TASK_STORE), result => { value = result; });
  });
}
/** Journal, compact summary and persisted chat update commit together. */
function writeProjection(store: IDBObjectStore, record: RoomTaskRecord): void {
  const summaries = store.transaction.objectStore(AGENT_TASK_SUMMARY_STORE);
  const previous = summaries.get(record.id);
  previous.onsuccess = () => {
    const summary = summarizeRoomTask(record);
    if (previous.result?.hidden || isRoomTaskHidden(record.id)) summary.hidden = true;
    summaries.put(summary);
    const chats = store.transaction.objectStore(STORE_NAME), request = chats.get(summary.conversationId);
    request.onsuccess = () => {
      if (request.result) chats.put({ ...request.result,
        messages: projectRoomTaskSummaries(request.result.messages, [summary], summary.conversationId) });
    };
  };
}
export const roomTaskStore: RoomTaskStore & { get(id: string): Promise<RoomTaskRecord | undefined> } = {
  claim: record => transaction('readwrite', (store, result) => {
    const request = store.get(record.id);
    request.onsuccess = () => {
      if (request.result) result({ claimed: false, record: request.result });
      else {
        const source = store.transaction.objectStore(STORE_NAME).get(record.handoff.conversationId);
        source.onsuccess = () => {
          // A deletion that committed while authorization was being checked wins.
          if (!hasRoomTaskSources(source.result?.messages || [], summarizeRoomTask(record))) { store.transaction.abort(); return; }
          store.add(record); writeProjection(store, record); result({ claimed: true, record });
        };
      }
    };
  }),
  save: record => transaction<void>('readwrite', (store, result) => {
    const request = store.get(record.id);
    request.onsuccess = () => {
      // History deletion must win over a late task callback.
      if (!request.result) { store.transaction.abort(); return; }
      store.put(record); writeProjection(store, record); result();
    };
  }),
  get: id => transaction<RoomTaskRecord | undefined>('readonly', (store, result) => {
    const request = store.get(id); request.onsuccess = () => result(request.result);
  }),
};
