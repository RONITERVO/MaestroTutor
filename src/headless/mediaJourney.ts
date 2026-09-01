// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE } from '../core/config/prompts';
import { deriveHistoryForApi, sanitizeHistoryWithVerifiedMedia } from '../core-sdk/chat/history';
import { runMaestroImageGeneration } from '../core-sdk/chat/imageGeneration';
import { resolveLanguagePair } from '../core-sdk/chat/language';
import type { HeadlessClient } from './client';

export const runHeadlessImageGeneration = async (
  client: HeadlessClient,
  input: {
    contextText: string;
    languagePairId?: string;
    assistantMessageId?: string;
    maxAttempts?: number;
    upload?: boolean;
    includeDataUrl?: boolean;
  },
) => {
  const pair = resolveLanguagePair({ pairId: input.languagePairId || client.state.settings.selectedLanguagePairId });
  const messages = client.state.chats[pair.id] || [];
  const history = await sanitizeHistoryWithVerifiedMedia(
    deriveHistoryForApi(messages.filter(message => message.id !== input.assistantMessageId), {
      globalProfileText: client.state.globalProfile,
      placeholderLatestUserMessage: DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE,
    }),
    async uris => (await client.backend.checkFileStatuses({ uris })).statuses,
  );
  const result = await runMaestroImageGeneration({
    contextText: input.contextText,
    history,
    maxAttempts: input.maxAttempts,
  }, {
    runtime: client.runtime,
    aiClient: client.ai,
  });
  if (!('base64Image' in result)) return result;

  const shouldUpload = input.upload ?? true;
  const uploaded = shouldUpload
    ? await client.backend.uploadMedia({
      dataUrl: result.base64Image,
      mimeType: result.mimeType,
      displayName: 'headless-generated',
    })
    : null;
  if (input.assistantMessageId) {
    const message = messages.find(candidate => candidate.id === input.assistantMessageId && candidate.role === 'assistant');
    if (!message) throw new Error('The requested assistant message does not exist in the selected chat.');
    message.imageUrl = result.base64Image;
    message.imageMimeType = result.mimeType;
    message.attachmentName = 'assistant-generated.jpg';
    message.maestroToolKind = 'image';
    if (uploaded) {
      message.uploadedFileVariants = [{
        id: 'primary',
        uri: uploaded.uri,
        mimeType: uploaded.mimeType,
        targets: ['chat', 'image-generation'],
        source: 'original',
        order: 10,
      }];
    }
    await client.save();
  }
  return {
    operationId: result.operationId,
    attempts: result.attempts,
    mimeType: result.mimeType,
    dataUrlLength: result.base64Image.length,
    ...(input.includeDataUrl ? { dataUrl: result.base64Image } : {}),
    uploaded,
  };
};
