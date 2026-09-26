// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { openDB, STORE_NAME, META_STORE, GLOBAL_PROFILE_STORE, ASSETS_STORE, AGENT_TASK_STORE, AGENT_TASK_SUMMARY_STORE, BACKUP_STAGE_STORE } from '../../../core/db';
import { BackupDecoder, encodeRoomTaskArchive, type BackupEntry, type ArchivedRoomTask } from '../../../core-sdk/backup/archive';
import { hasRoomTaskSources, summarizeRoomTask, projectRoomTaskSummaries, type RoomTaskSummary } from '../../../core-sdk/room/roomTaskProjection';
import { clearRoomTaskVisibility, readRoomTaskSummaries } from '../../chat/services/roomTaskSummaries';
import type { ChatMessage } from '../../../core/types';
export interface StagedBackup { batch: string; chats: number; tasks: number }
type StagedRow = BackupEntry & { batch: string; createdAt: number };
async function transaction<T>(stores: string[], mode: IDBTransactionMode, run: (tx: IDBTransaction, result: (value: T) => void) => void): Promise<T> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(stores, mode); let value: T;
    tx.oncomplete = () => { db.close(); resolve(value); };
    tx.onabort = tx.onerror = () => { db.close(); reject(tx.error || new Error('Backup storage transaction failed. Existing data was not replaced.')); };
    try { run(tx, result => { value = result; }); } catch (error) { tx.abort(); reject(error); }
  });
}
export async function discardBackupStage(batch: string): Promise<void> {
  await transaction<void>([BACKUP_STAGE_STORE], 'readwrite', tx => {
    const request = tx.objectStore(BACKUP_STAGE_STORE).index('batch').openCursor(IDBKeyRange.only(batch));
    request.onsuccess = () => { const cursor = request.result; if (cursor) { cursor.delete(); cursor.continue(); } };
  });
}
export async function stageBackup(readLines: (visit: (line: string) => Promise<void>) => Promise<void>): Promise<StagedBackup> {
  const batch = crypto.randomUUID(), decoder = new BackupDecoder();
  // Only abandoned week-old staging is swept; another current import retains its own batch.
  await transaction<void>([BACKUP_STAGE_STORE], 'readwrite', tx => {
    const request = tx.objectStore(BACKUP_STAGE_STORE).index('createdAt').openCursor(IDBKeyRange.upperBound(Date.now() - 7 * 86400000));
    request.onsuccess = () => { const cursor = request.result; if (cursor) { cursor.delete(); cursor.continue(); } };
  });
  const save = async (entries: BackupEntry[]) => {
    if (!entries.length) return;
    await transaction<void>([BACKUP_STAGE_STORE], 'readwrite', tx => {
      for (const entry of entries) tx.objectStore(BACKUP_STAGE_STORE).add({ ...entry, batch, createdAt: Date.now() });
    });
  };
  try {
    await readLines(async line => { await save(await decoder.push(line)); });
    await save(decoder.finish());
    const staged = { batch, chats: decoder.chats, tasks: decoder.tasks };
    await transaction<void>([BACKUP_STAGE_STORE], 'readwrite', tx => {
      tx.objectStore(BACKUP_STAGE_STORE).add({ batch, kind: 'manifest', id: 'manifest', value: staged, createdAt: Date.now() });
    });
    return staged;
  } catch (error) { await discardBackupStage(batch).catch(() => {}); throw error; }
}
export async function hasStagedBackupPair(staged: StagedBackup, pairId: string): Promise<boolean> {
  return transaction<boolean>([BACKUP_STAGE_STORE], 'readonly', (tx, done) => {
    const request = tx.objectStore(BACKUP_STAGE_STORE).get([staged.batch, 'chat', pairId]);
    request.onsuccess = () => done(!!request.result?.value?.length);
  });
}
function mergeMessages(existing: ChatMessage[], incoming: ChatMessage[]): ChatMessage[] {
  const messages = new Map(existing.map(message => [message.id, message]));
  for (const message of incoming) {
    const old = messages.get(message.id);
    if (!old || (!old.timestamp && message.timestamp) || (message.timestamp && message.timestamp > old.timestamp)) messages.set(message.id, message);
  }
  return [...messages.values()].sort((a, b) => (a.timestamp ?? Number.MAX_SAFE_INTEGER) - (b.timestamp ?? Number.MAX_SAFE_INTEGER) || a.id.localeCompare(b.id));
}
/** Copy verified staging to all live stores in ONE transaction. Unknown imported
 * work remains read-only history. Existing task IDs win in append mode. */
export async function commitBackupStage(staged: StagedBackup, appendPair?: string): Promise<{ chats: number; tasks: number }> {
  let copiedChats = 0, copiedTasks = 0;
  const result = await transaction<{ chats: number; tasks: number }>([
    BACKUP_STAGE_STORE, STORE_NAME, META_STORE, GLOBAL_PROFILE_STORE, ASSETS_STORE, AGENT_TASK_STORE, AGENT_TASK_SUMMARY_STORE,
  ], 'readwrite', (tx, done) => {
    const staging = tx.objectStore(BACKUP_STAGE_STORE), manifest = staging.get([staged.batch, 'manifest', 'manifest']);
    manifest.onsuccess = () => {
      if (JSON.stringify(manifest.result?.value) !== JSON.stringify(staged)) { tx.abort(); return; }
      if (!appendPair) {
        for (const name of [STORE_NAME, META_STORE, GLOBAL_PROFILE_STORE, AGENT_TASK_STORE, AGENT_TASK_SUMMARY_STORE]) tx.objectStore(name).clear();
        tx.objectStore(ASSETS_STORE).delete('maestroProfileImage');
      }
      let visitedChats = 0, visitedTasks = 0;
      const request = staging.index('batch').openCursor(IDBKeyRange.only(staged.batch));
      request.onsuccess = () => {
        const cursor = request.result;
        if (!cursor) {
          if (visitedChats !== staged.chats || visitedTasks !== staged.tasks || !copiedChats) { tx.abort(); return; }
          if (appendPair) {
            readRoomTaskSummaries(tx, appendPair, summaries => {
              const chats = tx.objectStore(STORE_NAME), history = chats.get(appendPair);
              history.onsuccess = () => chats.put({ pairId: appendPair, messages: projectRoomTaskSummaries(history.result?.messages || [], summaries, appendPair) });
            });
          }
          done({ chats: copiedChats, tasks: copiedTasks }); return;
        }
        const row = cursor.value as StagedRow;
        if (row.kind === 'chat') {
          visitedChats++;
          if (!appendPair || row.id === appendPair) {
            copiedChats++;
            const chats = tx.objectStore(STORE_NAME);
            if (appendPair) {
              const old = chats.get(row.id); old.onsuccess = () => chats.put({ pairId: row.id, messages: mergeMessages(old.result?.messages || [], row.value) });
            } else chats.put({ pairId: row.id, messages: row.value });
          }
        } else if (row.kind === 'task') {
          visitedTasks++;
          const archive = row.value, record = { ...archive.record, readOnly: true as const }, pairId = record.handoff.conversationId;
          if (!appendPair || pairId === appendPair) {
            const journals = tx.objectStore(AGENT_TASK_STORE), existing = journals.get(record.id);
            existing.onsuccess = () => {
              if (appendPair && existing.result) { cursor.delete(); cursor.continue(); return; }
              const chats = tx.objectStore(STORE_NAME), history = chats.get(pairId);
              history.onsuccess = () => {
                const summary = { ...summarizeRoomTask(record), hidden: archive.hidden };
                const messages: ChatMessage[] = history.result?.messages || [];
                if (!hasRoomTaskSources(messages, summary) || messages.some(message => message.id === record.id && message.agentTask?.id !== record.id)) { tx.abort(); return; }
                journals.add(record); tx.objectStore(AGENT_TASK_SUMMARY_STORE).add(summary); copiedTasks++;
                chats.put({ pairId, messages: projectRoomTaskSummaries(messages, [summary], pairId) });
                cursor.delete(); cursor.continue();
              };
            };
            return;
          }
        } else if (!appendPair && row.kind === 'meta') {
          if (row.value) tx.objectStore(META_STORE).put({ pairId: row.id, meta: row.value });
        } else if (!appendPair && row.kind === 'profile') {
          if (row.value) tx.objectStore(GLOBAL_PROFILE_STORE).put({ key: 'singleton', text: row.value, updatedAt: Date.now(), fingerprint: crypto.randomUUID() });
        } else if (!appendPair && row.kind === 'asset') {
          if (row.value) tx.objectStore(ASSETS_STORE).put({ key: row.id, value: { ...row.value, uri: undefined, updatedAt: Date.now() } });
        }
        cursor.delete(); cursor.continue();
      };
    };
  });
  if (!appendPair) clearRoomTaskVisibility();
  return { ...result, tasks: copiedTasks };
}
/** Read one journal at a time. The exported chat source snapshot bounds which
 * records may join that archive; private input is never copied into chat. */
export async function writeTaskBackup(pairId: string, messages: ChatMessage[], write: (line: string) => Promise<void>): Promise<number> {
  const summaries = await transaction<RoomTaskSummary[]>([AGENT_TASK_SUMMARY_STORE], 'readonly', (tx, done) => readRoomTaskSummaries(tx, pairId, done));
  let count = 0;
  for (const summary of summaries) {
    if (!hasRoomTaskSources(messages, summary)) continue;
    const archive = await transaction<ArchivedRoomTask | undefined>([AGENT_TASK_STORE, AGENT_TASK_SUMMARY_STORE], 'readonly', (tx, done) => {
      const record = tx.objectStore(AGENT_TASK_STORE).get(summary.id), projection = tx.objectStore(AGENT_TASK_SUMMARY_STORE).get(summary.id);
      projection.onsuccess = () => done(record.result && projection.result ? { version: 1, record: record.result, hidden: !!projection.result.hidden } : undefined);
    });
    if (!archive) continue;
    for await (const line of encodeRoomTaskArchive(archive)) await write(line);
    count++;
  }
  return count;
}
