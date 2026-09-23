// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { LIVE_REPLY_SUGGESTIONS_SUFFIX, REPLY_SUGGESTIONS_RESPONSE_SCHEMA, PROMPT_CONTEXT_TEXT } from '../../core/config/prompts';

import { generateGeminiResponse, type GeminiRequestLifecycleHooks } from '../../api/gemini/generative';
import { getGeminiModels } from '../../core/config/models';
import type { ChatMessage, LanguagePair, ReplySuggestion } from '../../core/types';
import { groupAdjacentRoleItems } from '../../shared/utils/conversationTurns';
import { createCoreRuntime, type CoreRuntime } from '../runtime';
import type { CoreGeminiClient } from '../managedGeminiClient';
import { buildCompactAssistantHistoryText } from './assistantMessageContext';

export interface ReplySuggestionsInput {
  assistantMessageId: string;
  lastTutorMessage: string;
  history: ChatMessage[];
  languagePair: LanguagePair;
  existingGlobalProfile?: string;
  responseSource?: 'chat' | 'live';
}

export interface ReplySuggestionsOptions {
  runtime?: CoreRuntime;
  aiClient?: CoreGeminiClient;
  lifecycleHooks?: GeminiRequestLifecycleHooks;
  retries?: number;
}

export interface ReplySuggestionsResult {
  operationId: string;
  suggestions: ReplySuggestion[];
  reengagementSeconds?: number;
  chatSummary?: string;
  globalProfile?: string;
  artifact: unknown;
  toolRequest: unknown;
  rawResponse: string;
  usageMetadata?: any;
  modelVersion?: string;
  modelUsed?: string;
}

/**
 * Keep the provider responsible for producing valid structured output. A MIME
 * type alone still permits malformed JSON (most often when an artifact embeds
 * HTML or JavaScript containing quotes), which used to make both the UI and
 * headless first-lesson journey abandon suggestion processing after retries.
 */
export { REPLY_SUGGESTIONS_RESPONSE_SCHEMA } from '../../core/config/prompts';

const extractJsonObject = (responseText: string): Record<string, unknown> => {
  let json = responseText.trim();
  const fenceMatch = json.match(/^```(?:\w*)?\s*\n?(.*?)\n?\s*```$/s);
  if (fenceMatch?.[1]) json = fenceMatch[1].trim();
  else {
    const firstBrace = json.indexOf('{');
    const lastBrace = json.lastIndexOf('}');
    if (firstBrace >= 0 && lastBrace >= firstBrace) json = json.slice(firstBrace, lastBrace + 1);
  }
  const parsed = JSON.parse(json) as unknown;
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    throw new Error('Suggestion response must be a JSON object.');
  }
  return parsed as Record<string, unknown>;
};

const normalizeSuggestions = (value: unknown): ReplySuggestion[] => {
  if (!Array.isArray(value)) throw new Error('Suggestion response is missing its suggestions array.');
  const suggestions = value.map((candidate, index) => {
    if (!candidate || typeof candidate !== 'object' || Array.isArray(candidate)) {
      throw new Error(`Suggestion ${index} must be an object.`);
    }
    const record = candidate as Record<string, unknown>;
    if (typeof record.target !== 'string' || typeof record.native !== 'string') {
      throw new Error(`Suggestion ${index} must include target and native strings.`);
    }
    return { target: record.target.trim(), native: record.native.trim() };
  }).filter(suggestion => suggestion.target && suggestion.native);
  if (!suggestions.length) throw new Error('Suggestion response did not contain a complete suggestion.');
  return suggestions;
};

export const buildReplySuggestionsPrompt = (input: ReplySuggestionsInput): string => {
  const historyForPrompt = groupAdjacentRoleItems(
    input.history.filter(message => message.role === 'user' || message.role === 'assistant'),
  )
    .slice(-6)
    .map(group => {
      if (group.role === 'user') {
        const userText = group.items
          .map(message => message.text?.trim() || PROMPT_CONTEXT_TEXT.sentImage)
          .filter(Boolean)
          .join('\n\n')
          .trim();
        return userText ? `${PROMPT_CONTEXT_TEXT.user}: ${userText}` : '';
      }
      const tutorText = group.items
        .map(message => (
          buildCompactAssistantHistoryText(message)
          || message.translations?.[0]?.target
          || message.rawAssistantResponse
          || message.text
          || PROMPT_CONTEXT_TEXT.sentImage
        ))
        .filter(Boolean)
        .join('\n\n')
        .trim();
      return tutorText ? `${PROMPT_CONTEXT_TEXT.tutor}: ${tutorText}` : '';
    })
    .filter(Boolean)
    .join('\n');

  const targetIndex = input.history.findIndex(message => message.id === input.assistantMessageId);
  let previousChatSummary = '';
  for (let index = targetIndex < 0 ? input.history.length - 1 : targetIndex - 1; index >= 0; index--) {
    const candidate = input.history[index];
    if (candidate.role === 'assistant' && candidate.chatSummary?.trim()) {
      previousChatSummary = candidate.chatSummary.trim();
      break;
    }
  }

  let prompt = input.languagePair.baseReplySuggestionsPrompt
    .replace('{tutor_message_placeholder}', input.lastTutorMessage)
    .replace('{conversation_history_placeholder}', historyForPrompt || PROMPT_CONTEXT_TEXT.noHistory)
    .replace('{previous_chat_summary_placeholder}', previousChatSummary)
    .replace('{existing_global_profile_placeholder}', input.existingGlobalProfile?.trim() || PROMPT_CONTEXT_TEXT.noProfile);
  if (input.responseSource === 'live') {
    prompt += LIVE_REPLY_SUGGESTIONS_SUFFIX;
  }
  return prompt;
};

export const runReplySuggestions = async (
  input: ReplySuggestionsInput,
  options: ReplySuggestionsOptions = {},
): Promise<ReplySuggestionsResult> => {
  const runtime = options.runtime || createCoreRuntime();
  const operationId = runtime.ids.create('suggestions');
  const prompt = buildReplySuggestionsPrompt(input);
  const retries = Math.max(0, Math.min(5, Math.floor(options.retries ?? 2)));
  runtime.events.emit({
    operationId,
    journey: 'suggestions',
    phase: 'request.started',
    data: { historyCount: input.history.length, promptLength: prompt.length, responseSource: input.responseSource || 'chat' },
  });

  let lastError: unknown;
  for (let attempt = 0; attempt <= retries; attempt++) {
    try {
      const response = await generateGeminiResponse(getGeminiModels().text.aux, prompt, [], {
        aiClient: options.aiClient,
        configOverrides: {
          responseMimeType: 'application/json',
          responseJsonSchema: REPLY_SUGGESTIONS_RESPONSE_SCHEMA,
        },
        lifecycleHooks: {
          onProgress: event => options.lifecycleHooks?.onProgress?.(event),
          onTextDelta: (delta, fullText) => {
            runtime.events.emit({
              operationId,
              journey: 'suggestions',
              phase: 'response.text-delta',
              data: { deltaLength: delta.length, fullLength: fullText.length },
            });
            options.lifecycleHooks?.onTextDelta?.(delta, fullText);
          },
          onThoughtDelta: (delta, fullThought) => {
            runtime.events.emit({
              operationId,
              journey: 'suggestions',
              phase: 'response.thought-delta',
              data: { deltaLength: delta.length, fullLength: fullThought.length },
            });
            options.lifecycleHooks?.onThoughtDelta?.(delta, fullThought);
          },
        },
      });
      const rawResponse = response.text || '';
      const parsed = extractJsonObject(rawResponse);
      const suggestions = normalizeSuggestions(parsed.suggestions);
      const result: ReplySuggestionsResult = {
        operationId,
        suggestions,
        reengagementSeconds: typeof parsed.reengagementSeconds === 'number' && parsed.reengagementSeconds >= 5
          ? parsed.reengagementSeconds
          : undefined,
        chatSummary: typeof parsed.chatSummary === 'string' && parsed.chatSummary.trim()
          ? parsed.chatSummary.trim()
          : undefined,
        globalProfile: typeof parsed.globalProfile === 'string' && parsed.globalProfile.trim()
          ? parsed.globalProfile.trim().slice(0, 10_000)
          : undefined,
        artifact: parsed.artifact ?? null,
        toolRequest: parsed.toolRequest ?? null,
        rawResponse,
        usageMetadata: response.usageMetadata,
        modelVersion: response.modelVersion,
        modelUsed: response.modelUsed,
      };
      runtime.events.emit({
        operationId,
        journey: 'suggestions',
        phase: 'request.completed',
        data: { attempt: attempt + 1, suggestionCount: suggestions.length, hasArtifact: Boolean(result.artifact), hasToolRequest: Boolean(result.toolRequest) },
      });
      return result;
    } catch (error) {
      lastError = error;
      runtime.events.emit({
        operationId,
        journey: 'suggestions',
        phase: attempt < retries ? 'request.retrying' : 'request.failed',
        data: { attempt: attempt + 1, message: error instanceof Error ? error.message : String(error) },
      });
      if (attempt < retries) await runtime.clock.sleep(500 * (attempt + 1));
    }
  }
  throw lastError;
};
