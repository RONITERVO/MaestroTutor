// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { ChatMessage } from '../../../core/types';
import { deriveHistoryForApi } from '../../chat';
import { getGlobalProfileDB } from '../../session';

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

  let historyContext = '';
  apiHistory.forEach((entry) => {
    const role = entry.role === 'user' ? 'User' : 'Maestro';
    const text = entry.rawAssistantResponse || entry.text || '(image)';
    historyContext += `${role}: ${text}\n`;
  });

  if (historyContext) {
    prompt += `\n\n--- CURRENT CONVERSATION CONTEXT (History) ---\n${historyContext}\n--- END CONTEXT ---`;
  }

  return prompt;
};

