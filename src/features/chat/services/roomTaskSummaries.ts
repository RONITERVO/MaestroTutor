// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { openDB, AGENT_TASK_SUMMARY_STORE, STORE_NAME } from '../../../core/db';
import { projectRoomTaskSummaries, type RoomTaskSummary } from '../../../core-sdk/room/roomTaskProjection';
const hidden = new Set<string>();
export const isRoomTaskHidden = (id: string) => hidden.has(id);
export const clearRoomTaskVisibility = () => hidden.clear();

/** Queued inside the caller's transaction, so it sees the same committed task/chat state. */
export function readRoomTaskSummaries(tx: IDBTransaction, pairId: string, done: (summaries: RoomTaskSummary[]) => void): void {
  const summaries: RoomTaskSummary[] = [];
  const request = tx.objectStore(AGENT_TASK_SUMMARY_STORE).index('conversationId').openCursor(IDBKeyRange.only(pairId));
  request.onsuccess = () => {
    const cursor = request.result;
    if (!cursor) { done(summaries); return; }
    const summary = cursor.value as RoomTaskSummary;
    if (summary.hidden) hidden.add(summary.id);
    summaries.push(summary); cursor.continue();
  };
}
/** A result deletion hides its chat projection, retaining the existing no-replay
 * claim and receipts until its source history is deleted. */
export async function hideRoomTaskMessage(id: string): Promise<void> {
  hidden.add(id);
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction([AGENT_TASK_SUMMARY_STORE, STORE_NAME], 'readwrite');
    tx.oncomplete = () => { db.close(); resolve(); };
    tx.onabort = tx.onerror = () => { db.close(); reject(tx.error || new Error('Could not save task visibility.')); };
    const store = tx.objectStore(AGENT_TASK_SUMMARY_STORE), request = store.get(id);
    request.onsuccess = () => {
      const summary = request.result as RoomTaskSummary | undefined;
      if (!summary) return;
      summary.hidden = true; store.put(summary);
      const chats = tx.objectStore(STORE_NAME), chat = chats.get(summary.conversationId);
      chat.onsuccess = () => {
        if (chat.result) chats.put({ ...chat.result, messages: projectRoomTaskSummaries(chat.result.messages, [summary], summary.conversationId) });
      };
    };
  });
}
