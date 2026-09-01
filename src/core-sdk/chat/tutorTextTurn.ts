// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import {
  generateGeminiResponse,
  type GeminiRequestLifecycleHooks,
  type GenerateGeminiResponseOptions,
} from '../../api/gemini/generative';
import type { CoreGeminiClient } from '../managedGeminiClient';
import { createCoreRuntime, type CoreRuntime } from '../runtime';
import { formatStreamingTutorDraftText, parseStrictTutorResponseText } from './tutorResponse';

export interface TutorTextTurnInput {
  model: string;
  prompt: string;
  history: unknown[];
  nativeLanguageCode: string;
  systemInstruction: string;
  currentFileParts?: Array<{ fileUri: string; mimeType: string }>;
  useGoogleSearch?: boolean;
  configOverrides?: unknown;
  timeoutMs?: number;
}

export interface TutorTextTurnOptions {
  runtime?: CoreRuntime;
  operationId?: string;
  aiClient?: CoreGeminiClient;
  lifecycleHooks?: GeminiRequestLifecycleHooks;
  onGoogleSearchUnavailable?: () => void;
}

export type TutorTextTurnResult = {
  operationId: string;
  rawResponse: string;
  parsed: ReturnType<typeof parseStrictTutorResponseText>;
  response: Awaited<ReturnType<typeof generateGeminiResponse>>;
  searchQueryCount: number;
};

export const runTutorTextTurn = async (
  input: TutorTextTurnInput,
  options: TutorTextTurnOptions = {},
): Promise<TutorTextTurnResult> => {
  const runtime = options.runtime || createCoreRuntime();
  const operationId = options.operationId || runtime.ids.create('chat-turn');
  runtime.events.emit({
    operationId,
    journey: 'chat',
    phase: 'turn.started',
    data: {
      model: input.model,
      promptLength: input.prompt.length,
      historyCount: input.history.length,
      filePartCount: input.currentFileParts?.length || 0,
      useGoogleSearch: Boolean(input.useGoogleSearch),
    },
  });

  try {
    const response = await generateGeminiResponse(
      input.model,
      input.prompt,
      input.history,
      {
        systemInstruction: input.systemInstruction,
        currentFileParts: input.currentFileParts,
        useGoogleSearch: input.useGoogleSearch,
        configOverrides: input.configOverrides,
        timeoutMs: input.timeoutMs,
        aiClient: options.aiClient,
        onGoogleSearchUnavailable: options.onGoogleSearchUnavailable,
        lifecycleHooks: {
          onProgress: event => {
            runtime.events.emit({
              operationId,
              journey: 'chat',
              phase: `model.${event.phase}`,
              data: {
                model: event.model,
                attempt: event.attempt,
                totalAttempts: event.totalAttempts,
                retryInMs: event.retryInMs,
                elapsedMs: event.elapsedMs,
                reason: event.reason,
              },
            });
            options.lifecycleHooks?.onProgress?.(event);
          },
          onTextDelta: (delta, fullText) => {
            runtime.events.emit({
              operationId,
              journey: 'chat',
              phase: 'response.text-delta',
              data: {
                deltaLength: delta.length,
                fullLength: fullText.length,
                visibleDraft: formatStreamingTutorDraftText(fullText, input.nativeLanguageCode),
              },
            });
            options.lifecycleHooks?.onTextDelta?.(delta, fullText);
          },
          onThoughtDelta: (delta, fullThought) => {
            runtime.events.emit({
              operationId,
              journey: 'chat',
              phase: 'response.thought-delta',
              data: { deltaLength: delta.length, fullLength: fullThought.length },
            });
            options.lifecycleHooks?.onThoughtDelta?.(delta, fullThought);
          },
        },
      } satisfies GenerateGeminiResponseOptions,
    );
    const rawResponse = response.text || '';
    const parsed = parseStrictTutorResponseText(rawResponse, input.nativeLanguageCode);
    const searchQueryCount = response.candidates?.reduce((count: number, candidate: any) => (
      count + (Array.isArray(candidate?.groundingMetadata?.webSearchQueries)
        ? candidate.groundingMetadata.webSearchQueries.length
        : 0)
    ), 0) || 0;
    runtime.events.emit({
      operationId,
      journey: 'chat',
      phase: 'turn.completed',
      data: {
        modelUsed: response.modelUsed,
        modelVersion: response.modelVersion,
        rawResponseLength: rawResponse.length,
        translationCount: parsed.translations.length,
        skippedNonLanguageContent: parsed.hasSkippedNonLanguageContent,
        searchQueryCount,
      },
    });
    return { operationId, rawResponse, parsed, response, searchQueryCount };
  } catch (error) {
    runtime.events.emit({
      operationId,
      journey: 'chat',
      phase: 'turn.failed',
      data: { message: error instanceof Error ? error.message : String(error) },
    });
    throw error;
  }
};
