// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { ChatMessage } from '../../core/types';
import type { RoomTaskRecord } from './roomTaskHandoff';

/** Small persisted projection. Never include source media, plans or scene receipts. */
export interface RoomTaskSummary {
  version: 1;
  id: string;
  conversationId: string;
  sourceUserId: string;
  sourceAssistantId: string;
  message: ChatMessage;
  hidden?: boolean;
}
export function summarizeRoomTask(record: RoomTaskRecord): RoomTaskSummary {
  const parsed = record.reply?.parsed;
  return {
    version: 1, id: record.id, conversationId: record.handoff.conversationId,
    sourceUserId: record.handoff.sourceUserId, sourceAssistantId: record.handoff.sourceAssistantId,
    message: {
      id: record.id, timestamp: record.startedAt,
      role: parsed ? 'assistant' : 'status',
      text: parsed ? (parsed.translations.length ? undefined : parsed.visibleText) : record.note,
      translations: parsed?.translations.length ? parsed.translations.map(({ target, native }) => ({ target, native })) : undefined,
      rawAssistantResponse: parsed?.visibleText, llmRawResponse: parsed?.visibleText,
      maestroToolKind: 'agent', agentTask: { id: record.id, sourceUserId: record.handoff.sourceUserId,
        sourceAssistantId: record.handoff.sourceAssistantId, phase: record.phase, note: record.note },
    },
  };
}
export function hasRoomTaskSources(messages: ChatMessage[], summary: RoomTaskSummary): boolean {
  return messages.some(message => message.id === summary.sourceUserId && message.role === 'user')
    && messages.some(message => message.id === summary.sourceAssistantId && message.role === 'assistant');
}
/** Repair stale/missing result messages without executing or announcing anything.
 * Existing positions/cache are preserved; a missing result follows its source reply. */
export function projectRoomTaskSummaries(messages: ChatMessage[], summaries: RoomTaskSummary[], conversationId: string): ChatMessage[] {
  let result = messages.slice();
  for (const summary of summaries) {
    if (summary.conversationId !== conversationId) continue;
    const index = result.findIndex(message => message.id === summary.id && message.agentTask?.id === summary.id);
    if (summary.hidden || !hasRoomTaskSources(result, summary)) {
      if (index >= 0) result.splice(index, 1);
      continue;
    }
    if (index >= 0) {
      const old = result[index];
      result[index] = { ...old, ...summary.message, timestamp: old.timestamp };
    } else if (!result.some(message => message.id === summary.id)) {
      const sourceIndex = result.findIndex(message => message.id === summary.sourceAssistantId);
      result.splice(sourceIndex + 1, 0, { ...summary.message });
    }
  }
  return result;
}
