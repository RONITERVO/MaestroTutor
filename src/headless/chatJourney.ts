// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { composeMaestroSystemInstruction } from '../core/config/prompts';
import { getGeminiModels } from '../core/config/models';
import type { ChatMessage, UploadedAttachmentVariant } from '../core/types';
import { assertTutorTurnInvariants } from '../core-sdk/assertions';
import { deriveHistoryForApi, sanitizeHistoryWithVerifiedMedia } from '../core-sdk/chat/history';
import { resolveLanguagePair } from '../core-sdk/chat/language';
import { runTutorTextTurn } from '../core-sdk/chat/tutorTextTurn';
import { runReplySuggestions } from '../core-sdk/chat/suggestions';
import type { HeadlessClient } from './client';

export interface HeadlessChatTurnParams {
  text: string;
  operationId?: string;
  languagePairId?: string;
  useGoogleSearch?: boolean;
  fileParts?: Array<{ fileUri: string; mimeType: string }>;
  uploadedFileVariants?: UploadedAttachmentVariant[];
  requireInvariants?: boolean;
  /** Internal UI-equivalent re-engagements send "..." without persisting a user bubble. */
  persistUserMessage?: boolean;
}

export const selectHeadlessLanguage = async (
  client: HeadlessClient,
  input: { pairId?: string; targetLanguageCode?: string; nativeLanguageCode?: string },
) => {
  const pair = resolveLanguagePair(input);
  client.state.settings.selectedLanguagePairId = pair.id;
  if (!client.state.chats[pair.id]) client.state.chats[pair.id] = [];
  await client.save();
  client.runtime.events.emit({
    operationId: client.runtime.ids.create('language-select'),
    journey: 'persistence',
    phase: 'language.selected',
    data: { pairId: pair.id, targetLanguageCode: pair.targetLanguageCode, nativeLanguageCode: pair.nativeLanguageCode },
  });
  return pair;
};

const createMessage = (
  client: HeadlessClient,
  role: 'user' | 'assistant',
  fields: Omit<ChatMessage, 'id' | 'role' | 'timestamp'>,
): ChatMessage => ({
  id: client.runtime.ids.create(`message-${role}`),
  role,
  timestamp: client.runtime.clock.now(),
  ...fields,
});

export const runHeadlessChatTurn = async (
  client: HeadlessClient,
  params: HeadlessChatTurnParams,
) => {
  const pair = resolveLanguagePair({
    pairId: params.languagePairId || client.state.settings.selectedLanguagePairId,
  });
  const history = client.state.chats[pair.id] || [];
  const userMessage = params.persistUserMessage === false ? null : createMessage(client, 'user', {
    text: params.text,
    ...(params.uploadedFileVariants?.length
      ? { uploadedFileVariants: params.uploadedFileVariants }
      : params.fileParts?.length
        ? {
            uploadedFileVariants: params.fileParts.map((part, index) => ({
              id: `headless-${index}`,
              uri: part.fileUri,
              mimeType: part.mimeType,
              targets: ['chat'],
              source: 'original',
              order: index,
            })),
          }
        : {}),
  });
  const derivedHistory = await sanitizeHistoryWithVerifiedMedia(
    deriveHistoryForApi(history),
    uris => client.files.statuses(uris),
  );
  const turn = await runTutorTextTurn({
    model: getGeminiModels().text.default,
    prompt: params.text,
    history: derivedHistory,
    nativeLanguageCode: pair.nativeLanguageCode,
    systemInstruction: composeMaestroSystemInstruction(pair.baseSystemPrompt),
    currentFileParts: params.fileParts,
    useGoogleSearch: params.useGoogleSearch ?? client.state.settings.enableGoogleSearch ?? true,
  }, {
    runtime: client.runtime,
    operationId: params.operationId,
    aiClient: client.ai,
  });
  const assistantMessage = createMessage(client, 'assistant', {
    translations: turn.parsed.translations.length ? turn.parsed.translations : undefined,
    llmRawResponse: turn.rawResponse,
    rawAssistantResponse: turn.parsed.visibleText || undefined,
    text: turn.parsed.translations.length ? undefined : turn.parsed.visibleText || undefined,
    isLoadingArtifact: turn.parsed.hasSkippedNonLanguageContent,
  });
  if (userMessage) history.push(userMessage);
  history.push(assistantMessage);
  client.state.chats[pair.id] = history;
  client.state.settings.selectedLanguagePairId = pair.id;
  await client.save();
  const assertions = assertTutorTurnInvariants({
    rawResponse: turn.rawResponse,
    translations: turn.parsed.translations,
  });
  if ((params.requireInvariants ?? true) && !assertions.passed) {
    const error = new Error('Tutor turn completed but failed one or more response invariants.') as Error & { assertions?: unknown };
    error.assertions = assertions;
    throw error;
  }
  const streamEvents = client.events.snapshot().filter(event => event.operationId === turn.operationId);
  return {
    operationId: turn.operationId,
    languagePair: {
      id: pair.id,
      targetLanguageCode: pair.targetLanguageCode,
      nativeLanguageCode: pair.nativeLanguageCode,
    },
    userMessage,
    assistantMessage,
    assertions,
    usageMetadata: turn.response.usageMetadata,
    modelUsed: turn.response.modelUsed,
    modelVersion: turn.response.modelVersion,
    searchQueryCount: turn.searchQueryCount,
    streaming: {
      textDeltaCount: streamEvents.filter(event => event.phase === 'response.text-delta').length,
      thoughtDeltaCount: streamEvents.filter(event => event.phase === 'response.thought-delta').length,
      visiblyStreamed: streamEvents.some(event => event.phase === 'response.text-delta'),
    },
  };
};

export const runHeadlessSuggestions = async (
  client: HeadlessClient,
  input: { languagePairId?: string; assistantMessageId?: string; responseSource?: 'chat' | 'live' },
) => {
  const pair = resolveLanguagePair({
    pairId: input.languagePairId || client.state.settings.selectedLanguagePairId,
  });
  const history = client.state.chats[pair.id] || [];
  const assistantMessage = input.assistantMessageId
    ? history.find(message => message.id === input.assistantMessageId && message.role === 'assistant')
    : history.slice().reverse().find(message => message.role === 'assistant' && !message.thinking);
  if (!assistantMessage) throw new Error('No assistant message is available for reply suggestions.');
  const lastTutorMessage = assistantMessage.llmRawResponse
    || assistantMessage.rawAssistantResponse
    || assistantMessage.text
    || assistantMessage.translations?.map(pairValue => pairValue.target).join('\n')
    || '';
  if (!lastTutorMessage.trim()) throw new Error('The selected assistant message has no tutor text.');
  const result = await runReplySuggestions({
    assistantMessageId: assistantMessage.id,
    lastTutorMessage,
    history,
    languagePair: pair,
    existingGlobalProfile: client.state.globalProfile,
    responseSource: input.responseSource,
  }, {
    runtime: client.runtime,
    aiClient: client.ai,
  });
  assistantMessage.replySuggestions = result.suggestions;
  if (result.chatSummary) assistantMessage.chatSummary = result.chatSummary;
  if (result.globalProfile) client.state.globalProfile = result.globalProfile;
  await client.save();
  const streamEvents = client.events.snapshot().filter(event => event.operationId === result.operationId);
  return {
    operationId: result.operationId,
    assistantMessageId: assistantMessage.id,
    suggestions: result.suggestions,
    reengagementSeconds: result.reengagementSeconds,
    chatSummary: result.chatSummary,
    globalProfile: result.globalProfile,
    artifact: result.artifact,
    toolRequest: result.toolRequest,
    modelUsed: result.modelUsed,
    modelVersion: result.modelVersion,
    usageMetadata: result.usageMetadata,
    streaming: {
      textDeltaCount: streamEvents.filter(event => event.phase === 'response.text-delta').length,
      thoughtDeltaCount: streamEvents.filter(event => event.phase === 'response.thought-delta').length,
      visiblyStreamed: streamEvents.some(event => event.phase === 'response.text-delta'),
    },
  };
};
