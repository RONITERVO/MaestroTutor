// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { BackupDecoder, encodeRoomTaskArchive, validateArchivedRoomTask, TASK_CHUNK_CHARS, type ArchivedRoomTask, type BackupEntry } from './archive';
import { LiveInputContext } from '../media/liveInputContext';
const header = { type: 'header', format: 'ndjson-v1', taskArchiveVersion: 1 };
const chat = { type: 'chatChunk', pairId: 'pair', chunkIndex: 0, isLast: true,
  messages: [{ id: 'u', role: 'user', text: 'Make a robot.', timestamp: 1 }, { id: 'a', role: 'assistant', text: 'Delegating.', timestamp: 2 }] };
function archive(): ArchivedRoomTask {
  const capture = new LiveInputContext(() => 0); capture.recordAudio('AAA='); capture.recordFrame('/9j/2Q==');
  return { version: 1, hidden: true, record: { version: 1, id: 'task', phase: 'completed', startedAt: 3, updatedAt: 4, note: 'Ready.',
    handoff: { version: 1, id: 'task', conversationId: 'pair', sourceUserId: 'u', sourceAssistantId: 'a', nativeSession: 'original-room', accessScope: 'byok',
      input: { prompt: 'Make a robot. 🤖', model: 'fixture', systemInstruction: 'Original context', history: [{ role: 'user', text: 'Earlier speech' }], nativeLanguageCode: 'en', liveInputMedia: capture.finish() } },
    operations: [{ sceneRevision: 1, commands: [{ action: 'workspace', visible: true }], receipt: { version: 1, session: 'original-room', revision: 2, sceneRevision: 2, ack: 1, ok: true, status: 'Opened', objects: [], created: [], canUndo: false, canRedo: false, physicsRunning: false } }],
    reply: { rawResponse: 'Ready.', parsed: { visibleText: 'Ready.', translations: [], hasSkippedNonLanguageContent: false } } } };
}
async function taskLines(value = archive()) { const lines = []; for await (const line of encodeRoomTaskArchive(value)) lines.push(JSON.parse(line)); return lines; }
async function decode(rows: unknown[]) { const decoder = new BackupDecoder(), entries: BackupEntry[] = []; for (const row of rows) entries.push(...await decoder.push(typeof row === 'string' ? row : JSON.stringify(row))); entries.push(...decoder.finish()); return entries; }
const end = { type: 'end', chats: 1, tasks: 1 };
describe('passive task backup codec', () => {
  it('roundtrips exact original media, context, receipts, visibility and Unicode across chunks', async () => {
    const value = archive(); value.record.handoff.input.systemInstruction += '🤖'.repeat(TASK_CHUNK_CHARS);
    const lines = await taskLines(value); expect(lines.length).toBeGreaterThan(1);
    const entries = await decode([header, chat, ...lines, end]);
    expect(entries.find(entry => entry.kind === 'task')?.value).toEqual(value);
    expect(JSON.stringify(entries[0])).not.toContain('liveInputMedia');
  });
  it('accepts legacy chat-only archives without a final marker', async () => {
    const { isLast: _last, ...legacyChat } = chat;
    expect(await decode([{ type: 'header', format: 'ndjson-v1', version: 8 }, legacyChat])).toEqual([{ kind: 'chat', id: 'pair', value: chat.messages }]);
  });
  it.each(['footer', 'chatEnd', 'taskEnd', 'hash', 'order', 'duplicateTask', 'duplicateChat', 'afterEnd', 'malformed', 'futureVersion', 'sourceCollision', 'pairMismatch'])('rejects %s without a partial successful import', async fault => {
    const value = archive(); value.record.handoff.input.systemInstruction = 'x'.repeat(TASK_CHUNK_CHARS * 2);
    const lines = await taskLines(value); let rows: unknown[] = [header, chat, ...lines, end];
    if (fault === 'footer') rows.pop();
    if (fault === 'chatEnd') rows[1] = { ...chat, isLast: false };
    if (fault === 'taskEnd') rows.splice(rows.length - 2, 1);
    if (fault === 'hash') lines[0].data = '!' + lines[0].data.slice(1);
    if (fault === 'order') [rows[2], rows[3]] = [rows[3], rows[2]];
    if (fault === 'duplicateTask') rows.splice(rows.length - 1, 0, ...lines);
    if (fault === 'duplicateChat') rows.splice(2, 0, chat);
    if (fault === 'afterEnd') rows.push(chat);
    if (fault === 'malformed') rows.splice(2, 0, '{');
    if (fault === 'futureVersion') rows[0] = { ...header, taskArchiveVersion: 2 };
    if (fault === 'sourceCollision') { value.record.handoff.sourceUserId = value.record.id; expect(() => validateArchivedRoomTask(value)).toThrow(); return; }
    if (fault === 'pairMismatch') for (const line of lines) line.pairId = 'other';
    await expect(decode(rows)).rejects.toThrow('INVALID_BACKUP_FORMAT');
  });
  it('rejects unsafe identifiers and malformed display fields', async () => {
    await expect(decode([header, { ...chat, pairId: '__proto__' }, { ...end, tasks: 0 }])).rejects.toThrow();
    const value = archive(); (value.record.reply!.parsed.translations as any) = { target: 'broken' };
    expect(() => validateArchivedRoomTask(value)).toThrow();
  });
  it('preserves incomplete-input evidence without accepting partial original media', () => {
    const value = archive(); value.record.handoff.input.liveInputMedia = { version: 1, complete: false, issue: 'limit', frames: [], packets: [] };
    expect(validateArchivedRoomTask(value)).toEqual(value);
    (value.record.handoff.input.liveInputMedia as any).frames = [{ data: 'partial' }];
    expect(() => validateArchivedRoomTask(value)).toThrow();
  });
});
