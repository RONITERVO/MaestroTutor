// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { createHash } from 'node:crypto';
import { getGeminiModels } from '../core/config/models';
import { runCoreManagedMusicGeneration } from '../core-sdk/media/musicGeneration';
import type { HeadlessClient } from './client';

export const runHeadlessMusicGeneration = async (client: HeadlessClient, input: {
  prompt: string;
  durationSeconds?: number;
  model?: string;
  upload?: boolean;
  includeDataUrl?: boolean;
}) => {
  const result = await runCoreManagedMusicGeneration({
    backend: client.backend,
    runtime: client.runtime,
    model: input.model || getGeminiModels().music.generation,
    prompt: input.prompt,
    durationSeconds: input.durationSeconds,
  });
  const uploaded = input.upload
    ? await client.backend.uploadMedia({
      dataUrl: result.dataUrl,
      mimeType: result.mimeType,
      displayName: 'headless-generated-music.wav',
    })
    : null;
  return {
    operationId: result.operationId,
    mimeType: result.mimeType,
    durationSeconds: result.durationSeconds,
    sampleRate: result.sampleRate,
    channels: result.channels,
    sampleCount: result.sampleCount,
    dataUrlLength: result.dataUrl.length,
    dataSha256: createHash('sha256').update(result.dataUrl).digest('hex'),
    uploaded,
    ...(input.includeDataUrl ? { dataUrl: result.dataUrl } : {}),
  };
};
