// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {validateInlineImages} from '../../../shared/inlineImages';
import {validRoomCaptureImage} from '../../../shared/roomViewCapture';
import type { ChatMessage, ChatMeta } from '../../core/types';
import type { RoomTaskRecord } from '../room/roomTaskHandoff';
import { validateLiveInputMedia } from '../media/liveInputContext';
export const TASK_ARCHIVE_VERSION = 1;
export const TASK_CHUNK_CHARS = 128 * 1024;
export const MAX_TASK_ARCHIVE_CHARS = 64 * 1024 * 1024;
export interface ArchivedRoomTask { version: 1; record: RoomTaskRecord; hidden: boolean }
export type BackupEntry =
  | { kind: 'chat'; id: string; value: ChatMessage[] }
  | { kind: 'meta'; id: string; value: ChatMeta | null }
  | { kind: 'profile'; id: 'singleton'; value: string | null }
  | { kind: 'asset'; id: 'maestroProfileImage'; value: Record<string, unknown> | null }
  | { kind: 'task'; id: string; value: ArchivedRoomTask };
const invalid = (): never => { throw new Error('INVALID_BACKUP_FORMAT'); };
const object = (value: unknown): Record<string, any> => value !== null && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, any> : invalid();
const id = (value: unknown): string => typeof value === 'string' && value.length > 0 && value.length <= 1024
  && !/[\u0000-\u001f]/.test(value) && !['__proto__', 'prototype', 'constructor'].includes(value) ? value : invalid();
const text = (value: unknown): string => typeof value === 'string' ? value : invalid();
const integer = (value: unknown): number => Number.isSafeInteger(value) && Number(value) >= 0 ? Number(value) : invalid();
const hash = async (value: string) => Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(value))), byte => byte.toString(16).padStart(2, '0')).join('');

/** Archive data is passive evidence, never an executable plan or fresh authority.
 * Validate the fields consumed by history/details and retain the full original JSON. */
export function validateArchivedRoomTask(value: unknown): ArchivedRoomTask {
  const archive = object(value), record = object(archive.record), handoff = object(record.handoff), input = object(handoff.input);
  if (archive.version !== 1 || record.version !== 1 || handoff.version !== 1 || typeof archive.hidden !== 'boolean') invalid();
  if (id(record.id) !== id(handoff.id)) invalid();
  id(handoff.conversationId); id(handoff.sourceUserId); id(handoff.sourceAssistantId);
  if (new Set([record.id, handoff.sourceUserId, handoff.sourceAssistantId]).size !== 3) invalid();
  text(handoff.nativeSession); text(handoff.accessScope); text(input.prompt); text(input.model); text(input.systemInstruction); text(input.nativeLanguageCode);
  if (!Array.isArray(input.history) || !['working','replying','completed','limited','stopped','interrupted','failed'].includes(record.phase)) invalid();
  text(record.note); integer(record.startedAt); integer(record.updatedAt);
  if(record.snapshots!==undefined&&(!Array.isArray(record.snapshots)||record.snapshots.length>6||!record.snapshots.every(validRoomCaptureImage)))invalid();
  if (!Array.isArray(record.operations) || record.operations.length > 1024) invalid();
  for (const item of record.operations) {
    const operation = object(item); integer(operation.sceneRevision);
    if (!Array.isArray(operation.commands) || operation.commands.length > 1024) invalid();
    for (const command of operation.commands) text(object(command).action);
    if (operation.receipt !== undefined) { const receipt = object(operation.receipt); text(receipt.status); if (typeof receipt.ok !== 'boolean') invalid(); }
  }
  if (record.directive !== undefined) {
    const directive = object(record.directive); id(directive.taskId);
    if (!['stop','revise','continue'].includes(directive.action)) invalid();
  }
  if (record.relatedTask !== undefined) {
    const related = object(record.relatedTask); id(related.id); text(related.note); text(related.reply);
    if (!['stop','revise','continue'].includes(related.action) || !['working','replying','completed','limited','stopped','interrupted','failed'].includes(related.phase)
      || typeof related.wasRunning !== 'boolean' || typeof related.unconfirmed !== 'boolean' || !Array.isArray(related.requests)
      || related.requests.some((request: unknown) => typeof request !== 'string') || related.requests.join('').length > 64000
      || !Array.isArray(related.operations) || related.operations.length > 1024) invalid();
    for (const value of related.operations) {
      const operation = object(value); integer(operation.sceneRevision);
      if (!Array.isArray(operation.commands) || operation.commands.length > 1024) invalid();
      for (const command of operation.commands) text(object(command).action);
      if (operation.receipt !== undefined) { const receipt = object(operation.receipt); text(receipt.status); if (typeof receipt.ok !== 'boolean') invalid(); }
    }
  }
  if (record.reply !== undefined) {
    const reply = object(record.reply), parsed = object(reply.parsed); text(reply.rawResponse); text(parsed.visibleText);
    if (!Array.isArray(parsed.translations)) invalid();
    for (const pair of parsed.translations) { text(object(pair).target); text(object(pair).native); }
  }
  if (input.currentImages !== undefined) { try { validateInlineImages(input.currentImages); } catch { invalid(); } }
  if (input.liveInputMedia !== undefined) {
    const media = object(input.liveInputMedia);
    if (media.complete === true) { try { validateLiveInputMedia(media as any); } catch { invalid(); } }
    else if (media.version !== 1 || media.complete !== false || !['missing','invalid','interrupted','limit'].includes(media.issue)
      || media.audio !== undefined || !Array.isArray(media.frames) || media.frames.length || !Array.isArray(media.packets) || media.packets.length) invalid();
  }
  return value as ArchivedRoomTask;
}
export async function* encodeRoomTaskArchive(archive: ArchivedRoomTask): AsyncGenerator<string> {
  validateArchivedRoomTask(archive);
  const serialized = JSON.stringify(archive);
  if (serialized.length > MAX_TASK_ARCHIVE_CHARS) throw new Error('A task record exceeds the backup size limit; no data was truncated.');
  const checksum = await hash(serialized), total = Math.ceil(serialized.length / TASK_CHUNK_CHARS);
  for (let index = 0; index < total; index++) yield JSON.stringify({ type: 'agentTaskChunk', version: 1,
    taskId: archive.record.id, pairId: archive.record.handoff.conversationId, index, total, checksum,
    data: serialized.slice(index * TASK_CHUNK_CHARS, (index + 1) * TASK_CHUNK_CHARS) }) + '\n';
}

/** Streaming reader retains one conversation and one task at most, then emits
 * validated entries for durable staging. New archives require an end record. */
export class BackupDecoder {
  private header = false;
  private enhanced = false;
  private ended = false;
  private seen = new Set<string>();
  private chat?: { id: string; next: number; messages: ChatMessage[]; ids: Set<string> };
  private task?: { id: string; pairId: string; next: number; total: number; checksum: string; data: string[]; size: number };
  chats = 0;
  tasks = 0;
  private unique(kind: string, value: string): void { const key = JSON.stringify([kind, value]); if (this.seen.has(key)) invalid(); this.seen.add(key); }
  private flushChat(): BackupEntry[] {
    if (!this.chat) return [];
    const result: BackupEntry = { kind: 'chat', id: this.chat.id, value: this.chat.messages };
    this.unique('chat', this.chat.id); this.chats++; this.chat = undefined; return [result];
  }
  async push(line: string): Promise<BackupEntry[]> {
    let row: Record<string, any>;
    try { row = object(JSON.parse(line)); } catch { return invalid(); }
    if (this.ended) invalid();
    if (!this.header) {
      if (row.type !== 'header' || row.format !== 'ndjson-v1') invalid();
      if (row.taskArchiveVersion !== undefined && row.taskArchiveVersion !== TASK_ARCHIVE_VERSION) invalid();
      this.enhanced = row.taskArchiveVersion === TASK_ARCHIVE_VERSION; this.header = true; return [];
    }
    if (row.type === 'header' || (this.task && row.type !== 'agentTaskChunk')) invalid();
    if (this.enhanced && this.chat && row.type !== 'chatChunk') invalid();
    const emitted = row.type !== 'chatChunk' ? this.flushChat() : [];
    if (row.type === 'chatChunk') {
      const pairId = id(row.pairId), index = integer(row.chunkIndex);
      if (this.chat && this.chat.id !== pairId) { if (this.enhanced) invalid(); emitted.push(...this.flushChat()); }
      if (!this.chat) { if (index !== 0 || this.seen.has(JSON.stringify(['chat', pairId]))) invalid(); this.chat = { id: pairId, next: 0, messages: [], ids: new Set() }; }
      if (index !== this.chat.next++ || !Array.isArray(row.messages)) invalid();
      for (const value of row.messages) {
        const message = object(value); id(message.id);
        if (!['user','assistant','system','error','status','system_selection'].includes(message.role)
          || (message.timestamp !== undefined && !Number.isFinite(message.timestamp)) || this.chat.ids.has(message.id)) invalid();
        if (message.agentTask !== undefined) { const task = object(message.agentTask); id(task.id); text(task.note); if (!['working','replying','completed','limited','stopped','interrupted','failed'].includes(task.phase)) invalid(); }
        if (message.translations !== undefined) {
          if (!Array.isArray(message.translations)) invalid();
          for (const pair of message.translations) { text(object(pair).target); text(object(pair).native); }
        }
        this.chat.ids.add(message.id); this.chat.messages.push(value as ChatMessage);
      }
      if (row.isLast === true) emitted.push(...this.flushChat());
    } else if (row.type === 'agentTaskChunk') {
      if (!this.enhanced || row.version !== 1) invalid();
      const taskId = id(row.taskId), pairId = id(row.pairId), index = integer(row.index), total = integer(row.total), data = text(row.data);
      if (!total || total > Math.ceil(MAX_TASK_ARCHIVE_CHARS / TASK_CHUNK_CHARS) || !/^[0-9a-f]{64}$/.test(row.checksum) || data.length > TASK_CHUNK_CHARS) invalid();
      this.task ??= { id: taskId, pairId, next: 0, total, checksum: row.checksum, data: [], size: 0 };
      const pending = this.task;
      if (taskId !== pending.id || pairId !== pending.pairId || index !== pending.next++ || total !== pending.total || row.checksum !== pending.checksum || index >= total) invalid();
      pending.size += data.length; if (pending.size > MAX_TASK_ARCHIVE_CHARS) invalid(); pending.data.push(data);
      if (index === total - 1) {
        const serialized = pending.data.join(''); if (await hash(serialized) !== pending.checksum) invalid();
        let archive: ArchivedRoomTask; try { archive = validateArchivedRoomTask(JSON.parse(serialized)); } catch { return invalid(); }
        if (archive.record.id !== taskId || archive.record.handoff.conversationId !== pairId) invalid();
        this.unique('task', taskId); this.tasks++; this.task = undefined;
        emitted.push({ kind: 'task', id: taskId, value: archive });
      }
    } else if (row.type === 'meta') {
      const pairId = id(row.pairId); this.unique('meta', pairId);
      if (row.meta !== null) object(row.meta);
      emitted.push({ kind: 'meta', id: pairId, value: row.meta });
    } else if (row.type === 'globalProfile') {
      this.unique('profile', 'singleton'); if (row.text !== null) text(row.text);
      emitted.push({ kind: 'profile', id: 'singleton', value: row.text });
    } else if (row.type === 'assets') {
      this.unique('asset', 'maestroProfileImage');
      if (row.maestroProfile !== null) {
        const asset = object(row.maestroProfile);
        // Validate every field consumed by avatar hydration before the archive
        // can replace live data. Keep optional legacy fields and unknown metadata.
        for (const key of ['dataUrl', 'mimeType', 'uri', 'accessScope']) {
          if (key in asset) text(asset[key]);
        }
        if ('updatedAt' in asset) integer(asset.updatedAt);
      }
      emitted.push({ kind: 'asset', id: 'maestroProfileImage', value: row.maestroProfile });
    } else if (row.type === 'end') {
      if (!this.enhanced || row.chats !== this.chats || row.tasks !== this.tasks) invalid(); this.ended = true;
    } else invalid();
    return emitted;
  }
  finish(): BackupEntry[] {
    if (!this.header || this.task || (this.enhanced && !this.ended)) return invalid();
    const result = this.flushChat(); if (!this.enhanced && !this.chats) invalid(); return result;
  }
}
