// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { translateText } from '../api/gemini/generative';
import type { ReplySuggestion } from '../core/types';
import { resolveLanguagePair } from '../core-sdk/chat/language';
import type { HeadlessClient } from './client';

const primary = (value?: string): string => (value || '').trim().toLowerCase().split('-', 1)[0] || '';

export const runHeadlessTranslation = async (client: HeadlessClient, input: {
  text: string;
  languagePairId?: string;
  from?: 'target' | 'native';
  attachToSuggestions?: boolean;
}) => {
  const pair = resolveLanguagePair({
    pairId: input.languagePairId || client.state.settings.selectedLanguagePairId,
  });
  const configuredSttLanguage = client.state.settings.stt?.language;
  const inferredFromTarget = primary(configuredSttLanguage) === primary(pair.targetLanguageCode);
  const fromTarget = input.from ? input.from === 'target' : inferredFromTarget;
  const fromName = fromTarget ? pair.targetLanguageName : pair.nativeLanguageName;
  const toName = fromTarget ? pair.nativeLanguageName : pair.targetLanguageName;
  const operationId = client.runtime.ids.create('translation');
  client.runtime.events.emit({
    operationId,
    journey: 'suggestions',
    phase: 'translation.started',
    data: { from: fromName, to: toName, textLength: input.text.length },
  });
  const result = await translateText(input.text, fromName, toName, {
    aiClient: client.ai,
    lifecycleHooks: {
      onProgress: progress => client.runtime.events.emit({
        operationId,
        journey: 'suggestions',
        phase: `translation.${progress.phase}`,
        data: { model: progress.model, attempt: progress.attempt, elapsedMs: progress.elapsedMs },
      }),
    },
  });
  const suggestion: ReplySuggestion = fromTarget
    ? { target: input.text, native: result.translatedText }
    : { target: result.translatedText, native: input.text };

  let assistantMessageId: string | null = null;
  if (input.attachToSuggestions !== false) {
    const history = client.state.chats[pair.id] || [];
    const assistant = history.slice().reverse().find(message => message.role === 'assistant' && !message.thinking);
    if (assistant) {
      assistantMessageId = assistant.id;
      const existing = assistant.replySuggestions || [];
      if (!existing.some(item => item.target === suggestion.target && item.native === suggestion.native)) {
        assistant.replySuggestions = [suggestion, ...existing];
        await client.save();
      }
    }
  }
  client.runtime.events.emit({
    operationId,
    journey: 'suggestions',
    phase: 'translation.completed',
    data: { assistantMessageId, translatedLength: result.translatedText.length },
  });
  return {
    operationId,
    languagePairId: pair.id,
    from: fromName,
    to: toName,
    originalText: input.text,
    translatedText: result.translatedText,
    suggestion,
    assistantMessageId,
    modelUsed: result.modelUsed,
    modelVersion: result.modelVersion,
    usageMetadata: result.usageMetadata,
  };
};
