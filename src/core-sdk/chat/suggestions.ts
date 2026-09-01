// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

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
          .map(message => message.text?.trim() || '(sent an image)')
          .filter(Boolean)
          .join('\n\n')
          .trim();
        return userText ? `User: ${userText}` : '';
      }
      const tutorText = group.items
        .map(message => (
          buildCompactAssistantHistoryText(message)
          || message.translations?.[0]?.target
          || message.rawAssistantResponse
          || message.text
          || '(sent an image)'
        ))
        .filter(Boolean)
        .join('\n\n')
        .trim();
      return tutorText ? `Tutor: ${tutorText}` : '';
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
    .replace('{conversation_history_placeholder}', historyForPrompt || 'No history yet.')
    .replace('{previous_chat_summary_placeholder}', previousChatSummary)
    .replace('{existing_global_profile_placeholder}', input.existingGlobalProfile?.trim() || '(none)');
  if (input.responseSource === 'live') {
    prompt += '\n\nIMPORTANT: This latest tutor message came from the live audio model. Its transcript will not contain fenced artifact blocks or maestro-tool JSON even when an artifact or tool would improve the turn. For this live turn, decide yourself whether to synthesize an "artifact" object and/or a "toolRequest" object from the tutor transcript using the same quality bar as the main chat path. Artifacts, an image tool request, an audio-note tool request, a music tool request, or null are all allowed. Do not default to images or audio-note. Do consider creating different artifact, not repeating same that is already in the ui, if this is likely a followup to already created artifact on previous message. If artifact or tool does not materially improve the response, return null for them.';
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
        configOverrides: { responseMimeType: 'application/json' },
        lifecycleHooks: options.lifecycleHooks,
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
