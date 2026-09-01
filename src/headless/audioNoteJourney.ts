// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { createHash } from 'node:crypto';
import { getGeminiModels } from '../core/config/models';
import { runCoreAudioNoteGeneration } from '../core-sdk/media/audioNoteGeneration';
import { TRIGGER_AUDIO_PCM_24K, TRIGGER_SAMPLE_RATE } from '../core-sdk/media/triggerAudioAsset';
import type { HeadlessClient } from './client';

export const runHeadlessAudioNoteGeneration = async (client: HeadlessClient, input: {
  text: string;
  langCode?: string;
  voiceName?: string;
  model?: string;
  upload?: boolean;
  includeDataUrl?: boolean;
}) => {
  const result = await runCoreAudioNoteGeneration({
    aiClient: client.ai,
    runtime: client.runtime,
    model: input.model || getGeminiModels().audio.tts,
    text: input.text,
    langCode: input.langCode,
    voiceName: input.voiceName,
    triggerPcmBase64: TRIGGER_AUDIO_PCM_24K,
    triggerSampleRate: TRIGGER_SAMPLE_RATE,
  });
  const uploaded = input.upload
    ? await client.backend.uploadMedia({
      dataUrl: result.dataUrl,
      mimeType: result.mimeType,
      displayName: 'headless-generated-audio-note.wav',
    })
    : null;
  return {
    operationId: result.operationId,
    mimeType: result.mimeType,
    durationSeconds: result.durationSeconds,
    sampleCount: result.sampleCount,
    dataUrlLength: result.dataUrl.length,
    dataSha256: createHash('sha256').update(result.dataUrl).digest('hex'),
    uploaded,
    ...(input.includeDataUrl ? { dataUrl: result.dataUrl } : {}),
  };
};
