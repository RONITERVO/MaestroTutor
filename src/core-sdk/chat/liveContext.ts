// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { ChatMessage } from '../../core/types';
import { groupAdjacentRoleItems } from '../../shared/utils/conversationTurns';
import { buildCompactAssistantHistoryText } from './assistantMessageContext';
import { deriveHistoryForApi } from './history';

/** Shared live/observer context serialization used before the browser media adapter. */
export const buildCoreLiveSystemInstruction = (input: {
  basePrompt: string;
  messages: ChatMessage[];
  contextSummary?: string;
  globalProfileText?: string;
  maxMessages?: number;
}): string => {
  const apiHistory = deriveHistoryForApi(input.messages, {
    maxMessages: input.maxMessages ?? 10,
    contextSummary: input.contextSummary,
    globalProfileText: input.globalProfileText,
  });
  const sourceMessagesById = new Map(input.messages.map(message => [message.id, message]));
  const latestAssistantEntryId = [...apiHistory].reverse()
    .find(entry => entry.role === 'assistant')?.messageId;
  const historyContext = groupAdjacentRoleItems(apiHistory)
    .map(group => {
      const role = group.role === 'user' ? 'User' : 'Maestro';
      const text = group.items.map(entry => {
        const source = entry.messageId ? sourceMessagesById.get(entry.messageId) : undefined;
        return entry.role === 'assistant'
          ? (buildCompactAssistantHistoryText(source, {
              includeArtifact: entry.messageId === latestAssistantEntryId,
              includeToolRequest: entry.messageId === latestAssistantEntryId,
            }) || entry.rawAssistantResponse || entry.text || '(assistant attachment)')
          : (entry.rawAssistantResponse || entry.text || '(image)');
      }).filter((value): value is string => Boolean(value?.trim())).join('\n\n').trim();
      return text ? `${role}: ${text}` : '';
    })
    .filter(Boolean)
    .join('\n');
  return historyContext
    ? `${input.basePrompt}\n\n--- CURRENT CONVERSATION CONTEXT (History) ---\n${historyContext}\n--- END CONTEXT ---`
    : input.basePrompt;
};
