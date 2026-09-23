// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { appendLiveHistoryContext, PROMPT_CONTEXT_TEXT } from '../../core/config/prompts';

import type { ChatMessage } from '../../core/types';
import { groupAdjacentRoleItems } from '../../shared/utils/conversationTurns';
import { buildCompactAssistantHistoryText } from './assistantMessageContext';
import type { AssistantArtifactOptions } from './artifactOptions';
import { deriveHistoryForApi } from './history';

/** Shared live/observer context serialization used before the browser media adapter. */
export const buildCoreLiveSystemInstruction = (input: AssistantArtifactOptions & {
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
      const role = group.role === 'user' ? PROMPT_CONTEXT_TEXT.user : PROMPT_CONTEXT_TEXT.maestro;
      const text = group.items.map(entry => {
        const source = entry.messageId ? sourceMessagesById.get(entry.messageId) : undefined;
        return entry.role === 'assistant'
          ? (buildCompactAssistantHistoryText(source, {
              sanitizeSvg: input.sanitizeSvg,
              includeArtifact: entry.messageId === latestAssistantEntryId,
              includeToolRequest: entry.messageId === latestAssistantEntryId,
            }) || entry.rawAssistantResponse || entry.text || PROMPT_CONTEXT_TEXT.assistantAttachment)
          : (entry.rawAssistantResponse || entry.text || PROMPT_CONTEXT_TEXT.image);
      }).filter((value): value is string => Boolean(value?.trim())).join('\n\n').trim();
      return text ? `${role}: ${text}` : '';
    })
    .filter(Boolean)
    .join('\n');
  return appendLiveHistoryContext(input.basePrompt, historyContext);
};
