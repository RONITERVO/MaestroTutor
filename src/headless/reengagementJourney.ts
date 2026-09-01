// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { HeadlessClient } from './client';
import { runHeadlessChatTurn } from './chatJourney';
import { runHeadlessSuggestionAftersteps } from './suggestionJourney';

/** Mirrors App.tsx's empty-input conversational re-engagement (`...`). */
export const runHeadlessReengagement = async (client: HeadlessClient, input: {
  languagePairId?: string;
  runSuggestionAftersteps?: boolean;
}) => {
  const turn = await runHeadlessChatTurn(client, {
    text: '...',
    languagePairId: input.languagePairId,
    persistUserMessage: false,
    requireInvariants: true,
  });
  const aftersteps = input.runSuggestionAftersteps === false
    ? null
    : await runHeadlessSuggestionAftersteps(client, {
        languagePairId: turn.languagePair.id,
        assistantMessageId: turn.assistantMessage.id,
        responseSource: 'chat',
      });
  return {
    operationId: turn.operationId,
    emptyUserRequest: true,
    providerPrompt: '...',
    userMessagePersisted: false,
    autoTriggered: true,
    turn,
    aftersteps,
  };
};
