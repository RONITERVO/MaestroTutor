// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { getAi } from '../../../api/gemini/client';
import { getGeminiModels } from '../../../core/config/models';
import {
  runCoreAudioNoteGeneration,
  type CoreAudioNoteResult,
} from '../../../core-sdk/media/audioNoteGeneration';
import { createLiveUsageTracker } from '../../../shared/utils/costTracker';
import { debugLogService } from '../../diagnostics';
import { TRIGGER_AUDIO_PCM_24K, TRIGGER_SAMPLE_RATE } from '../../../core-sdk/media/triggerAudioAsset';

export type GeminiAudioNoteResult = Omit<CoreAudioNoteResult, 'operationId' | 'sampleCount'>;

/** Browser adapter around the shared Core SDK Gemini Live audio-note journey. */
export const synthesizeGeminiAudioNote = async (params: {
  text: string;
  langCode?: string;
  voiceName?: string;
  abortSignal?: AbortSignal;
}): Promise<GeminiAudioNoteResult> => {
  const model = getGeminiModels().audio.tts;
  const usageTracker = createLiveUsageTracker({ feature: 'audioNote', configuredModel: model });
  const log = debugLogService.logRequest('synthesizeGeminiAudioNote', model, {
    textLength: params.text.trim().length,
    voiceName: params.voiceName || 'Kore',
    langCode: params.langCode,
  });
  try {
    const result = await runCoreAudioNoteGeneration({
      aiClient: await getAi(),
      model,
      text: params.text,
      triggerPcmBase64: TRIGGER_AUDIO_PCM_24K,
      triggerSampleRate: TRIGGER_SAMPLE_RATE,
      langCode: params.langCode,
      voiceName: params.voiceName,
      abortSignal: params.abortSignal,
      onUsageMetadata: metadata => usageTracker.trackSnapshot(metadata as any),
    });
    log.complete({ durationSeconds: result.durationSeconds, sampleCount: result.sampleCount });
    return {
      dataUrl: result.dataUrl,
      mimeType: result.mimeType,
      durationSeconds: result.durationSeconds,
    };
  } catch (error) {
    log.error(error);
    throw error;
  }
};
