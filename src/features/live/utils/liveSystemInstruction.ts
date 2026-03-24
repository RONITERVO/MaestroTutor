// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { ChatMessage } from '../../../core/types';
import { deriveHistoryForApi } from '../../chat';
import { getGlobalProfileDB } from '../../session';
import { buildCompactAssistantHistoryText } from '../../chat/utils/assistantMessageContext';
import { groupAdjacentRoleItems } from '../../../shared/utils/conversationTurns';

export interface BuildLiveSystemInstructionParams {
  basePrompt: string;
  messages: ChatMessage[];
  computeHistorySubsetForMedia: (arr: ChatMessage[]) => ChatMessage[];
  resolveBookmarkContextSummary: () => string | null;
}

/**
 * Build the live system instruction with the same chat-context enrichment used
 * by explicit user-initiated live sessions.
 */
export const buildLiveSystemInstruction = async ({
  basePrompt,
  messages,
  computeHistorySubsetForMedia,
  resolveBookmarkContextSummary,
}: BuildLiveSystemInstructionParams): Promise<string> => {
  let prompt = basePrompt;

  const historySubset = computeHistorySubsetForMedia(messages);
  const apiHistory = deriveHistoryForApi(historySubset, {
    maxMessages: 10,
    contextSummary: resolveBookmarkContextSummary() || undefined,
    globalProfileText: (await getGlobalProfileDB())?.text || undefined,
  });
  const sourceMessagesById = new Map(historySubset.map(message => [message.id, message]));
  const latestAssistantEntryId = [...apiHistory]
    .reverse()
    .find(entry => entry.role === 'assistant')
    ?.messageId;

  const historyContext = groupAdjacentRoleItems(apiHistory)
    .map((group) => {
      const role = group.role === 'user' ? 'User' : 'Maestro';
      const text = group.items
        .map((entry) => {
          const sourceMessage = entry.messageId ? sourceMessagesById.get(entry.messageId) : undefined;
          return entry.role === 'assistant'
            ? (buildCompactAssistantHistoryText(sourceMessage, {
                includeArtifact: entry.messageId === latestAssistantEntryId,
                includeToolRequest: entry.messageId === latestAssistantEntryId,
              }) || entry.rawAssistantResponse || entry.text || '(assistant attachment)')
            : (entry.rawAssistantResponse || entry.text || '(image)');
        })
        .filter((value): value is string => Boolean(value && value.trim()))
        .join('\n\n')
        .trim();

      return text ? `${role}: ${text}` : '';
    })
    .filter(Boolean)
    .join('\n');

  if (historyContext) {
    prompt += `\n\n--- CURRENT CONVERSATION CONTEXT (History) ---\n${historyContext}\n--- END CONTEXT ---`;
  }

  return prompt;
};
