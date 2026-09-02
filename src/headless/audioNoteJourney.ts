// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { createHash } from 'node:crypto';
import { getGeminiModels } from '../core/config/models';
import { runCoreAudioNoteGeneration } from '../core-sdk/media/audioNoteGeneration';
import { TRIGGER_AUDIO_PCM_24K, TRIGGER_SAMPLE_RATE } from '../core-sdk/media/triggerAudioAsset';
import type { HeadlessClient } from './client';
import { buildTriggeredTtsSystemInstruction } from '../core-sdk/media/triggeredTts';
import { LIVE_OPEN_TRIGGER } from '../../shared/liveOpenReason';

export const runHeadlessAudioNoteGeneration = async (client: HeadlessClient, input: {
  text: string;
  langCode?: string;
  voiceName?: string;
  model?: string;
  upload?: boolean;
  includeDataUrl?: boolean;
  languagePairId?: string;
  assistantMessageId?: string;
  exactTts?: boolean;
}) => {
  const result = await runCoreAudioNoteGeneration({
    aiClient: client.ai,
    liveOpenTrigger: input.exactTts
      ? LIVE_OPEN_TRIGGER.VOICE_TTS_CLICK
      : LIVE_OPEN_TRIGGER.TOOL_AUDIO_NOTE,
    runtime: client.runtime,
    model: input.model || getGeminiModels().audio.tts,
    text: input.text,
    langCode: input.langCode,
    voiceName: input.voiceName,
    triggerPcmBase64: TRIGGER_AUDIO_PCM_24K,
    triggerSampleRate: TRIGGER_SAMPLE_RATE,
    systemInstruction: input.exactTts
      ? buildTriggeredTtsSystemInstruction([{ text: input.text, langCode: input.langCode }])
      : undefined,
  });
  const uploaded = input.upload
    ? await client.files.upload({
      dataUrl: result.dataUrl,
      mimeType: result.mimeType,
      displayName: 'headless-generated-audio-note.wav',
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
    message.attachmentName = 'maestro-audio-note.wav';
    message.maestroToolKind = 'audio-note';
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
    sampleCount: result.sampleCount,
    triggerAudioSamplesSent: result.triggerAudioSamplesSent,
    triggerPacketCount: result.triggerPacketCount,
    dataUrlLength: result.dataUrl.length,
    dataSha256: createHash('sha256').update(result.dataUrl).digest('hex'),
    uploaded,
    assistantMessageId: input.assistantMessageId || null,
    purpose: input.exactTts ? 'tts' : 'audio-note',
    ...(input.includeDataUrl ? { dataUrl: result.dataUrl } : {}),
  };
};
