// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { HeadlessClient } from './client';
import type { ChatMessage } from '../core/types';
const ports = vi.hoisted(() => ({ suggestions: vi.fn(), image: vi.fn(), audio: vi.fn(), music: vi.fn() }));
vi.mock('./chatJourney', () => ({ runHeadlessSuggestions: ports.suggestions }));
vi.mock('./mediaJourney', () => ({ runHeadlessImageGeneration: ports.image }));
vi.mock('./audioNoteJourney', () => ({ runHeadlessAudioNoteGeneration: ports.audio }));
vi.mock('./musicJourney', () => ({ runHeadlessMusicGeneration: ports.music }));
import { runHeadlessSuggestionAftersteps } from './suggestionJourney';
import { listLanguagePairs } from '../core-sdk/chat/language';
const pairId = listLanguagePairs()[0].id;

beforeEach(() => { vi.resetAllMocks(); ports.suggestions.mockResolvedValue({ suggestions: [{ target: 'Hei', native: 'Hello' }] }); });

describe('headless afterstep application contract before sharing decisions', () => {
  it.each([
    { artifact: false, tool: false }, { artifact: true, tool: false },
    { artifact: false, tool: true }, { artifact: true, tool: true },
  ])('preserves raw context and legacy attachment fields: %j', async ({ artifact, tool }) => {
    const history: ChatMessage[] = [{ id: 'a', role: 'assistant', timestamp: 1, text: 'Visible', llmRawResponse: 'Original model context',
      isLoadingArtifact: true, artifactLoadStartTime: 123, storageOptimizedImageUrl: 'old', storageOptimizedImageMimeType: 'image/png',
      uploadedFileVariants: [{ id: 'primary', uri: 'old-uri', mimeType: 'image/png', targets: ['text'], source: 'original', order: 10 }] }];
    const events: unknown[] = [];
    const client = {
      state: { settings: { selectedLanguagePairId: pairId }, chats: { [pairId]: history } },
      runtime: { ids: { create: (prefix: string) => `${prefix}-id` }, clock: { now: () => 100 }, events: { emit: (event: unknown) => events.push(event) } },
      save: vi.fn(async () => { events.push('save'); }),
    } as unknown as HeadlessClient;
    ports.image.mockImplementation(async (_client, input) => { events.push(input); return 'image-result'; });
    const result = await runHeadlessSuggestionAftersteps(client, {
      responseSource: 'live', uploadGeneratedMedia: false,
      syntheticDecision: {
        artifact: artifact ? { mimeType: 'text/markdown', fileName: 'lesson.md', content: '# Lesson' } : null,
        toolRequest: tool ? { tool: 'image', prompt: 'Picture request' } : null,
      },
    });
    expect({ history, events, toolMessageId: result.toolMessageId, toolResult: result.toolResult }).toMatchSnapshot();
    expect(ports.suggestions).toHaveBeenCalledWith(client, { languagePairId: pairId, assistantMessageId: 'a', responseSource: 'live' });
    // This differs deliberately from the browser, which clears these fields.
    expect(history[0].artifactLoadStartTime).toBe(123);
    expect(history[0].uploadedFileVariants?.[0].uri).toBe('old-uri');
  });
});

