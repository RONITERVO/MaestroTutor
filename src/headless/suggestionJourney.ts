// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { ChatMessage } from '../core/types';
import { getVisibleAssistantMessageText } from '../core-sdk/chat/assistantMessageContext';
import {
  executeSuggestionToolRequest,
  normalizeSuggestionCreatorArtifact,
  normalizeSuggestionCreatorToolRequest,
  type SuggestionCreatorArtifact,
  type SuggestionCreatorToolRequest,
} from '../core-sdk/chat/suggestionAftersteps';
import { resolveLanguagePair } from '../core-sdk/chat/language';
import { runHeadlessAudioNoteGeneration } from './audioNoteJourney';
import type { HeadlessClient } from './client';
import { runHeadlessImageGeneration } from './mediaJourney';
import { runHeadlessMusicGeneration } from './musicJourney';
import { runHeadlessSuggestions } from './chatJourney';

export interface HeadlessSyntheticAfterstepDecision {
  artifact?: SuggestionCreatorArtifact | null;
  toolRequest?: SuggestionCreatorToolRequest | null;
}

export const runHeadlessSuggestionAftersteps = async (client: HeadlessClient, input: {
  languagePairId?: string;
  assistantMessageId?: string;
  responseSource?: 'chat' | 'live';
  syntheticDecision?: HeadlessSyntheticAfterstepDecision;
  uploadGeneratedMedia?: boolean;
}) => {
  const pair = resolveLanguagePair({
    pairId: input.languagePairId || client.state.settings.selectedLanguagePairId,
  });
  const history = client.state.chats[pair.id] || [];
  const selected = input.assistantMessageId
    ? history.find(message => message.id === input.assistantMessageId && message.role === 'assistant')
    : history.slice().reverse().find(message => message.role === 'assistant' && !message.thinking);
  if (!selected) throw new Error('No assistant message is available for suggestion aftersteps.');

  const operationId = client.runtime.ids.create('suggestion-aftersteps');
  client.runtime.events.emit({
    operationId,
    journey: 'suggestions',
    phase: 'aftersteps.started',
    data: { assistantMessageId: selected.id, responseSource: input.responseSource || 'chat' },
  });
  const suggestionResult = await runHeadlessSuggestions(client, {
    languagePairId: pair.id,
    assistantMessageId: selected.id,
    responseSource: input.responseSource,
  });
  const decisionSource = input.syntheticDecision ? 'synthetic-boundary' : 'model';
  const artifact = normalizeSuggestionCreatorArtifact(
    input.syntheticDecision ? input.syntheticDecision.artifact : suggestionResult.artifact,
  );
  const fallbackText = getVisibleAssistantMessageText(selected)
    || selected.llmRawResponse
    || selected.rawAssistantResponse
    || selected.text
    || '';
  const toolRequest = normalizeSuggestionCreatorToolRequest(
    input.syntheticDecision ? input.syntheticDecision.toolRequest : suggestionResult.toolRequest,
    fallbackText,
  );

  if (artifact) {
    selected.imageUrl = artifact.dataUrl;
    selected.imageMimeType = artifact.mimeType;
    selected.attachmentName = artifact.fileName;
    selected.isLoadingArtifact = false;
  } else if (!toolRequest) {
    selected.isLoadingArtifact = false;
  }

  let toolMessage: ChatMessage = selected;
  if (artifact && toolRequest) {
    toolMessage = {
      id: client.runtime.ids.create('message-assistant'),
      role: 'assistant',
      timestamp: client.runtime.clock.now(),
      rawAssistantResponse: fallbackText || undefined,
    };
    history.push(toolMessage);
  }

  let toolResult: unknown = null;
  if (toolRequest) {
    const upload = input.uploadGeneratedMedia ?? true;
    toolResult = await executeSuggestionToolRequest<unknown>(toolRequest, {
      image: request => runHeadlessImageGeneration(client, {
        contextText: selected.llmRawResponse || request.prompt || fallbackText,
        languagePairId: pair.id,
        assistantMessageId: toolMessage.id,
        upload,
      }),
      audioNote: request => runHeadlessAudioNoteGeneration(client, {
        text: request.text,
        langCode: pair.targetLanguageCode,
        languagePairId: pair.id,
        assistantMessageId: toolMessage.id,
        upload,
      }),
      music: request => runHeadlessMusicGeneration(client, {
        prompt: request.prompt,
        durationSeconds: request.durationSeconds,
        languagePairId: pair.id,
        assistantMessageId: toolMessage.id,
        upload,
      }),
    });
  }
  await client.save();
  client.runtime.events.emit({
    operationId,
    journey: 'suggestions',
    phase: 'aftersteps.completed',
    data: {
      assistantMessageId: selected.id,
      decisionSource,
      hasArtifact: Boolean(artifact),
      tool: toolRequest?.tool || null,
    },
  });
  return {
    operationId,
    assistantMessageId: selected.id,
    suggestionResult,
    decisionSource,
    artifact: artifact
      ? { mimeType: artifact.mimeType, fileName: artifact.fileName, dataUrlLength: artifact.dataUrl.length }
      : null,
    toolRequest,
    toolMessageId: toolRequest ? toolMessage.id : null,
    toolResult,
  };
};
