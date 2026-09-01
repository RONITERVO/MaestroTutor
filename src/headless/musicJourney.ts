// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { createHash } from 'node:crypto';
import { getGeminiModels } from '../core/config/models';
import {
  runCoreManagedMusicGeneration,
  runCoreMusicGeneration,
} from '../core-sdk/media/musicGeneration';
import type { HeadlessClient } from './client';

export const runHeadlessMusicGeneration = async (client: HeadlessClient, input: {
  prompt: string;
  durationSeconds?: number;
  model?: string;
  upload?: boolean;
  includeDataUrl?: boolean;
  languagePairId?: string;
  assistantMessageId?: string;
}) => {
  const common = {
    runtime: client.runtime,
    model: input.model || getGeminiModels().music.generation,
    prompt: input.prompt,
    durationSeconds: input.durationSeconds,
  };
  const result = client.accessMode === 'managed'
    ? await runCoreManagedMusicGeneration({ ...common, backend: client.backend })
    : await runCoreMusicGeneration({ ...common, aiClient: client.ai });
  const uploaded = input.upload
    ? await client.files.upload({
      dataUrl: result.dataUrl,
      mimeType: result.mimeType,
      displayName: 'headless-generated-music.wav',
    })
    : null;
  if (input.assistantMessageId) {
    const languagePairId = input.languagePairId || client.state.settings.selectedLanguagePairId || '';
    const message = (client.state.chats[languagePairId] || []).find(candidate => (
      candidate.id === input.assistantMessageId && candidate.role === 'assistant'
    ));
    if (!message) throw new Error('The requested assistant message does not exist in the selected chat.');
    message.imageUrl = result.dataUrl;
    message.imageMimeType = result.mimeType;
    message.attachmentName = 'maestro-music.wav';
    message.maestroToolKind = 'music';
    if (uploaded) {
      message.uploadedFileVariants = [{
        id: 'primary', uri: uploaded.uri, mimeType: uploaded.mimeType,
        targets: ['chat'], source: 'original', order: 10,
      }];
    }
    await client.save();
  }
  return {
    operationId: result.operationId,
    mimeType: result.mimeType,
    durationSeconds: result.durationSeconds,
    sampleRate: result.sampleRate,
    channels: result.channels,
    sampleCount: result.sampleCount,
    dataUrlLength: result.dataUrl.length,
    dataSha256: createHash('sha256').update(result.dataUrl).digest('hex'),
    accessMode: client.accessMode,
    assistantMessageId: input.assistantMessageId || null,
    uploaded,
    ...(input.includeDataUrl ? { dataUrl: result.dataUrl } : {}),
  };
};
