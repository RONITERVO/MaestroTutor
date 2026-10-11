// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { RoomTaskPhase, RoomTaskRecord } from './roomTaskHandoff';
export class RoomTaskSteeringError extends Error {}
export interface RoomTaskDirective { action: 'stop' | 'revise' | 'continue'; taskId: string }
/** Small host-issued catalogue, captured with the user's turn. Previews are labels, not instructions. */
export interface RoomTaskTarget { id: string; phase: RoomTaskPhase; requestPreview: string; replyPreview: string; running: boolean }
export interface RelatedRoomTask {
  id: string; action: RoomTaskDirective['action']; phase: RoomTaskPhase; note: string;
  requests: string[]; operations: RoomTaskRecord['operations']; reply: string;
  wasRunning: boolean; unconfirmed: boolean;
}
export function parseRoomTaskDirective(value: unknown, targets: readonly RoomTaskTarget[]): RoomTaskDirective | null {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return null;
  const item = value as Record<string, unknown>;
  if (Object.keys(item).some(key => !['action', 'taskId'].includes(key))
    || !['stop', 'revise', 'continue'].includes(String(item.action)) || typeof item.taskId !== 'string') return null;
  const target = targets.find(candidate => candidate.id === item.taskId);
  if (!target || (item.action === 'stop' && !target.running) || (item.action === 'continue' && target.running)) return null;
  return { action: item.action as RoomTaskDirective['action'], taskId: item.taskId };
}
export function relatedRoomTask(record: RoomTaskRecord, directive: RoomTaskDirective, wasRunning: boolean): RelatedRoomTask {
  const requests = directive.action === 'stop' ? [] : [...(record.relatedTask?.requests || []), record.handoff.input.prompt];
  if (requests.join('').length > 64000) throw new RoomTaskSteeringError('This task has too much prior context. Please make a new, self-contained request.');
  return { id: record.id, action: directive.action, phase: record.phase, note: record.note,
    requests, operations: structuredClone(record.operations), reply: record.reply?.parsed.visibleText || '', wasRunning,
    unconfirmed: !!record.relatedTask?.unconfirmed || record.operations.some(operation => !operation.receipt) };
}
