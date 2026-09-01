// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { ChatMessage } from '../../../core/types';
import { getGlobalProfileDB } from '../../session';
import { buildCoreLiveSystemInstruction } from '../../../core-sdk/chat/liveContext';

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
  const historySubset = computeHistorySubsetForMedia(messages);
  return buildCoreLiveSystemInstruction({
    basePrompt,
    messages: historySubset,
    contextSummary: resolveBookmarkContextSummary() || undefined,
    globalProfileText: (await getGlobalProfileDB())?.text || undefined,
    maxMessages: 10,
  });
};
