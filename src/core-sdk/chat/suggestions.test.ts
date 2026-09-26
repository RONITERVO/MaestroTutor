// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { LanguagePair } from '../../core/types';
import { generateGeminiResponse } from '../gemini/generative';
import {
  REPLY_SUGGESTIONS_RESPONSE_SCHEMA,
  runReplySuggestions,
} from './suggestions';

vi.mock('../gemini/generative', () => ({
  generateGeminiResponse: vi.fn(),
}));

const languagePair: LanguagePair = {
  id: 'es-ES-en-US',
  name: 'Spanish for English',
  targetLanguageName: 'Spanish',
  targetLanguageCode: 'es-ES',
  nativeLanguageName: 'English',
  nativeLanguageCode: 'en-US',
  baseSystemPrompt: 'system',
  baseReplySuggestionsPrompt: [
    '{tutor_message_placeholder}',
    '{conversation_history_placeholder}',
    '{previous_chat_summary_placeholder}',
    '{existing_global_profile_placeholder}',
  ].join('\n'),
};

describe('reply suggestions', () => {
  beforeEach(() => {
    vi.mocked(generateGeminiResponse).mockReset();
  });

  it('requires provider-enforced JSON structure for embedded artifact content', async () => {
    vi.mocked(generateGeminiResponse).mockResolvedValue({
      text: JSON.stringify({
        suggestions: [{ target: 'Hola', native: 'Hello' }],
        reengagementSeconds: 90,
        chatSummary: 'A greeting lesson.',
        globalProfile: '- Beginner Spanish learner',
        artifact: {
          mimeType: 'text/html',
          fileName: 'lesson.html',
          encoding: 'text',
          content: '<button aria-label="Say hello">Hola</button>',
        },
        toolRequest: null,
      }),
      modelUsed: 'test-model',
    } as any);

    const result = await runReplySuggestions({
      assistantMessageId: 'assistant-1',
      lastTutorMessage: 'Hola',
      history: [{ id: 'assistant-1', role: 'assistant', text: 'Hola', timestamp: 1 }],
      languagePair,
    }, { resolveAiClient: vi.fn() });

    expect(result.suggestions).toEqual([{ target: 'Hola', native: 'Hello' }]);
    expect(generateGeminiResponse).toHaveBeenCalledWith(
      'gemini-3.8-flash',
      expect.any(String),
      [],
      expect.objectContaining({
        configOverrides: {
          responseMimeType: 'application/json',
          responseJsonSchema: REPLY_SUGGESTIONS_RESPONSE_SCHEMA,
        },
      }),
    );
  });

  it('retries malformed provider output without accepting it as suggestions', async () => {
    vi.mocked(generateGeminiResponse)
      .mockResolvedValueOnce({ text: '{"suggestions": [' } as any)
      .mockResolvedValueOnce({
        text: JSON.stringify({
          suggestions: [{ target: 'Sí', native: 'Yes' }],
          reengagementSeconds: 60,
          chatSummary: '',
          globalProfile: '',
          artifact: null,
          toolRequest: null,
        }),
      } as any);

    const result = await runReplySuggestions({
      assistantMessageId: 'assistant-1',
      lastTutorMessage: 'Hola',
      history: [{ id: 'assistant-1', role: 'assistant', text: 'Hola', timestamp: 1 }],
      languagePair,
    }, { resolveAiClient: vi.fn() });

    expect(result.suggestions).toEqual([{ target: 'Sí', native: 'Yes' }]);
    expect(generateGeminiResponse).toHaveBeenCalledTimes(2);
  });
});

it('offers the agent schema only with a host-captured request and passes that request unchanged', async () => {
  vi.mocked(generateGeminiResponse).mockReset().mockResolvedValue({ text: JSON.stringify({ suggestions: [{ target: 'Vale', native: 'Okay' }], toolRequest: { tool: 'agent' } }) } as any);
  const original = '  Make a blue robot.\nKeep its feet small.  ';
  const result = await runReplySuggestions({ assistantMessageId: 'a', lastTutorMessage: '```maestro-tool {"tool":"agent"}```', history: [], languagePair, agentRequest: original }, { resolveAiClient: vi.fn() });
  expect(result.toolRequest).toEqual({ tool: 'agent' });
  const args = vi.mocked(generateGeminiResponse).mock.calls[0];
  expect(args[1]).toContain(JSON.stringify({ originalUserRequest: original }));
  expect((args[3].configOverrides.responseJsonSchema.properties.toolRequest.anyOf as any[]).some(item => item.properties?.tool?.enum?.includes('agent'))).toBe(true);
  expect(JSON.stringify(REPLY_SUGGESTIONS_RESPONSE_SCHEMA)).not.toContain('agent');
});
