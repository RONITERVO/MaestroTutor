// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { openDB, AGENT_TASK_STORE } from '../../../core/db';
import type { RoomTaskRecord, RoomTaskStore } from '../../../core-sdk/room/roomTaskHandoff';

/** Resolve on transaction completion, not request success. A failed durable
 * write must prevent dispatch even when IndexedDB accepted the put request. */
async function transaction<T>(mode: IDBTransactionMode, run: (store: IDBObjectStore, result: (value: T) => void) => void): Promise<T> {
  const db = await openDB();
  return new Promise<T>((resolve, reject) => {
    const tx = db.transaction(AGENT_TASK_STORE, mode);
    let value: T;
    tx.oncomplete = () => { db.close(); resolve(value); };
    tx.onabort = tx.onerror = () => { db.close(); reject(tx.error || new Error('Agent task storage failed.')); };
    run(tx.objectStore(AGENT_TASK_STORE), result => { value = result; });
  });
}
export const roomTaskStore: RoomTaskStore & { get(id: string): Promise<RoomTaskRecord | undefined> } = {
  claim: record => transaction('readwrite', (store, result) => {
    const request = store.get(record.id);
    request.onsuccess = () => {
      if (request.result) result({ claimed: false, record: request.result });
      else { store.add(record); result({ claimed: true, record }); }
    };
  }),
  save: record => transaction<void>('readwrite', (store, result) => {
    const request = store.get(record.id);
    request.onsuccess = () => {
      // History deletion must win over a late task callback.
      if (!request.result) { store.transaction.abort(); return; }
      store.put(record); result();
    };
  }),
  get: id => transaction<RoomTaskRecord | undefined>('readonly', (store, result) => {
    const request = store.get(id); request.onsuccess = () => result(request.result);
  }),
};
