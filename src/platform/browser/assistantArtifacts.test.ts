// @vitest-environment jsdom
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it, vi } from 'vitest';
import { parseAssistantResponseForAttachment, normalizeSuggestionCreatorArtifact, buildCoreLiveSystemInstruction } from './assistantArtifacts';
import { runReplySuggestions } from '../../api/gemini/journeys';
import { decodeTextFromDataUrl } from '../../core-sdk/chat/fileAttachments';
import type { CoreGeminiClient } from '../../core-sdk/managedGeminiClient';
import type { LanguagePair } from '../../core/types';

const svg = '<svg xmlns="http://www.w3.org/2000/svg"><style>@media (min-width:1px){.spin {animation: spin 1s infinite}}</style><g transform="translate(10 20)" class="spin"><circle r="3"/></g><rect transform="scale(2)"><animateTransform attributeName="transform" type="rotate"/></rect></svg>';
const response = `Hola\n[en] Hello\n\`\`\`svg\n${svg}\n\`\`\``;
const history = [{ id: 'a1', role: 'assistant' as const, text: 'Hola', llmRawResponse: response, timestamp: 1 }];
const pair: LanguagePair = { id: 'es-en', name: 'Spanish', targetLanguageName: 'Spanish', targetLanguageCode: 'es',
  nativeLanguageName: 'English', nativeLanguageCode: 'en', baseSystemPrompt: 'system',
  baseReplySuggestionsPrompt: '{conversation_history_placeholder}' };

describe('browser SVG artifact contract before adapter extraction', () => {
  it('preserves serialized SVG attachments from fenced, inline and suggestion output', () => {
    const fromFence = parseAssistantResponseForAttachment(response);
    const fromInline = parseAssistantResponseForAttachment(`Hola\n${svg}`);
    const fromSuggestion = normalizeSuggestionCreatorArtifact({ mimeType: 'image/svg+xml', fileName: 'lesson.svg', content: svg });
    const fromDataUrl = normalizeSuggestionCreatorArtifact({ mimeType: 'image/svg+xml', fileName: 'lesson.svg', encoding: 'data-url',
      content: `data:image/svg+xml,${encodeURIComponent(svg)}` });
    expect({ cleaned: fromFence.cleanedText, fence: decodeTextFromDataUrl(fromFence.attachment!.dataUrl),
      inline: decodeTextFromDataUrl(fromInline.attachment!.dataUrl),
      suggestion: decodeTextFromDataUrl(fromSuggestion!.dataUrl), dataUrl: decodeTextFromDataUrl(fromDataUrl!.dataUrl) }).toMatchSnapshot();
  });
  it('preserves SVG content in the actual suggestion request and Live context', async () => {
    const requests: unknown[] = [];
    const aiClient = { models: { generateContentStream: vi.fn(async function* (request: unknown) {
      const value = request as { contents: unknown };
      requests.push(value.contents);
      yield { text: JSON.stringify({ suggestions: [{ target: 'Sí', native: 'Yes' }] }) };
    }) } } as unknown as CoreGeminiClient;
    await runReplySuggestions({ assistantMessageId: 'a1', lastTutorMessage: 'Hola', history, languagePair: pair }, { aiClient });
    const live = buildCoreLiveSystemInstruction({ basePrompt: 'system', messages: history });
    expect({ requests, live }).toMatchSnapshot();
  });
  it('retains malformed SVG text and handles unavailable DOM support', () => {
    const artifact = (content: string) => normalizeSuggestionCreatorArtifact({ mimeType: 'image/svg+xml', content });
    expect(decodeTextFromDataUrl(artifact('<svg><broken></svg>')!.dataUrl)).toBe('<svg><broken></svg>');
    vi.stubGlobal('DOMParser', undefined);
    try { expect(decodeTextFromDataUrl(artifact(svg)!.dataUrl)).toBe(svg); }
    finally { vi.unstubAllGlobals(); }
  });
});
