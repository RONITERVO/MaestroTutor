// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { createHash } from 'node:crypto';
import { composeMaestroSystemInstruction } from '../core/config/prompts';
import { getGeminiModels } from '../core/config/models';
import type { ChatMessage } from '../core/types';
import { buildCoreLiveSystemInstruction } from '../core-sdk/chat/liveContext';
import { resolveLanguagePair } from '../core-sdk/chat/language';
import { parseStrictTutorResponseText } from '../core-sdk/chat/tutorResponse';
import { mergeInt16Arrays, pcmToWav } from '../core-sdk/media/audioProcessing';
import { createSyntheticPcmSource } from '../core-sdk/media/pcmInput';
import { runSyntheticLiveJourney } from '../core-sdk/media/syntheticLiveJourney';
import { buildLiveSttSystemInstruction } from '../core-sdk/media/liveSessionInstructions';
import type { HeadlessClient } from './client';
import { runHeadlessSuggestionAftersteps } from './suggestionJourney';
import { createSyntheticVisualFrame } from './syntheticVisual';

const decodePcmChunk = (base64: string): Int16Array => {
  const bytes = Uint8Array.from(globalThis.atob(base64), character => character.charCodeAt(0));
  return new Int16Array(bytes.buffer, bytes.byteOffset, Math.floor(bytes.byteLength / 2)).slice();
};

const audioDigest = (pcm: Int16Array): string => createHash('sha256')
  .update(new Uint8Array(pcm.buffer, pcm.byteOffset, pcm.byteLength))
  .digest('hex');

export interface HeadlessLiveTurnInput {
  pcm: Int16Array;
  sampleRate?: number;
  languagePairId?: string;
  mode: 'conversation' | 'observer' | 'stt';
  pace?: boolean;
  timeoutMs?: number;
  includeVisual?: boolean;
  visualLabel?: string;
  runSuggestionAftersteps?: boolean;
  uploadVisual?: boolean;
  instructionSuffix?: string;
}

export const runHeadlessLiveTurn = async (
  client: HeadlessClient,
  input: HeadlessLiveTurnInput,
): Promise<any> => {
  const sampleRate = input.sampleRate ?? 16_000;
  if (sampleRate !== 16_000) throw new Error('Headless Live journeys require 16 kHz PCM16 mono input.');
  const pair = resolveLanguagePair({
    pairId: input.languagePairId || client.state.settings.selectedLanguagePairId,
  });
  const history = client.state.chats[pair.id] || [];
  const visual = input.includeVisual ? await createSyntheticVisualFrame(input.visualLabel) : null;
  const latestAssistant = history.slice().reverse().find(message => message.role === 'assistant');
  const basePrompt = input.mode === 'stt'
    ? buildLiveSttSystemInstruction({
        lastAssistantMessage: latestAssistant?.rawAssistantResponse || latestAssistant?.text,
        replySuggestions: latestAssistant?.replySuggestions?.map(suggestion => suggestion.target),
      })
    : composeMaestroSystemInstruction(pair.baseSystemPrompt);
  let systemInstruction = input.mode === 'stt'
    ? basePrompt
    : buildCoreLiveSystemInstruction({
        basePrompt,
        messages: history,
        globalProfileText: client.state.globalProfile,
        maxMessages: 10,
      });
  if (input.instructionSuffix?.trim()) {
    systemInstruction += `\n\n${input.instructionSuffix.trim()}`;
  }
  const result = await runSyntheticLiveJourney(client.ai, {
    source: createSyntheticPcmSource({
      pcm: input.pcm,
      sampleRate,
      pace: input.pace ?? true,
      runtime: client.runtime,
    }),
    systemInstruction,
    model: input.mode === 'stt' ? getGeminiModels().audio.stt : getGeminiModels().audio.conversation,
    thinkingMode: input.mode === 'stt' ? 'minimal' : 'conversation',
    voiceName: client.state.settings.tts?.voiceName || 'Kore',
    gateInputOnSpeech: input.mode === 'observer',
    semanticSpeech: true,
    timeoutMs: input.timeoutMs,
    includeModelAudio: true,
    videoFrames: visual ? [{ dataBase64: visual.dataBase64, mimeType: visual.mimeType }] : undefined,
  }, { runtime: client.runtime });
  const modelAudio = mergeInt16Arrays(
    (result.modelAudioChunksBase64 || []).map(decodePcmChunk),
  );

  if (input.mode === 'stt') {
    return {
      ...result,
      mode: input.mode,
      accessMode: client.accessMode,
      capturedInputSamples: input.pcm.length,
      capturedInputSha256: audioDigest(input.pcm),
      capturedModelSamples: modelAudio.length,
      capturedModelSha256: modelAudio.length ? audioDigest(modelAudio) : null,
      modelAudioChunksBase64: undefined,
    };
  }

  const userMessage: ChatMessage = {
    id: client.runtime.ids.create('message-user'),
    role: 'user',
    timestamp: client.runtime.clock.now(),
    text: result.inputTranscript || result.transcript,
    recordedUtterance: {
      dataUrl: pcmToWav(input.pcm, sampleRate, 1),
      provider: 'gemini',
      langCode: pair.targetLanguageCode,
      transcript: result.inputTranscript || result.transcript,
      sampleRate,
    },
    ...(visual ? {
      imageUrl: `data:${visual.mimeType};base64,${visual.dataBase64}`,
      imageMimeType: visual.mimeType,
      attachmentName: 'headless-live-visual.jpg',
    } : {}),
  };
  if (visual && input.uploadVisual) {
    const uploaded = await client.files.upload({
      dataUrl: userMessage.imageUrl!, mimeType: visual.mimeType, displayName: userMessage.attachmentName,
    });
    userMessage.uploadedFileVariants = [{
      id: 'primary', uri: uploaded.uri, mimeType: uploaded.mimeType,
      targets: ['chat', 'image-generation'], source: 'original', order: 10,
    }];
  }
  const parsed = parseStrictTutorResponseText(result.outputTranscript, pair.nativeLanguageCode);
  const assistantMessage: ChatMessage = {
    id: client.runtime.ids.create('message-assistant'),
    role: 'assistant',
    timestamp: client.runtime.clock.now(),
    rawAssistantResponse: result.outputTranscript || result.transcript,
    translations: parsed.translations.length ? parsed.translations : undefined,
    text: parsed.translations.length ? undefined : (parsed.visibleText || result.outputTranscript || result.transcript),
    ...(modelAudio.length ? {
      ttsAudioCache: [{
        key: `headless-live:${audioDigest(modelAudio)}`,
        langCode: pair.targetLanguageCode,
        provider: 'gemini-live',
        audioDataUrl: pcmToWav(modelAudio, 24_000, 1),
        updatedAt: client.runtime.clock.now(),
        voiceName: client.state.settings.tts?.voiceName || 'Kore',
      }],
    } : {}),
  };
  history.push(userMessage, assistantMessage);
  client.state.chats[pair.id] = history;
  client.state.settings.selectedLanguagePairId = pair.id;
  await client.save();

  const aftersteps = input.runSuggestionAftersteps === false
    ? null
    : await runHeadlessSuggestionAftersteps(client, {
        languagePairId: pair.id,
        assistantMessageId: assistantMessage.id,
        responseSource: 'live',
      });
  return {
    ...result,
    mode: input.mode,
    accessMode: client.accessMode,
    userMessage,
    assistantMessage,
    aftersteps,
    visual: visual ? { width: visual.width, height: visual.height, semanticLabel: visual.semanticLabel } : null,
    capturedInputSamples: input.pcm.length,
    capturedInputSha256: audioDigest(input.pcm),
    capturedModelSamples: modelAudio.length,
    capturedModelSha256: modelAudio.length ? audioDigest(modelAudio) : null,
    modelAudioChunksBase64: undefined,
  };
};
