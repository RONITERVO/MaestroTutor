// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it, vi } from 'vitest';
import type { ChatMessage } from '../../core/types';
import { deriveHistoryForApi, sanitizeHistoryWithVerifiedMedia } from './history';
import { buildCoreLiveSystemInstruction } from './liveContext';
import { generateGeminiResponse } from '../gemini/generative';
import { createManagedGeminiClient } from '../managedGeminiClient';
import { GENERATED_IMAGE_CONTEXT } from '../../../shared/prompts/context';

const image = (id: string, generated: boolean): ChatMessage => ({
  id, role: 'user', timestamp: 1, text: 'Help me set up the room.',
  ...(generated ? { imageOrigin: 'generated' } : {}),
  uploadedFileVariants: [{ id: 'primary', uri: `files/${id}`, mimeType: 'image/png', targets: ['chat'], source: 'original' }],
});

describe('generated image provenance', () => {
  it.each(['byok', 'managed'])('labels only generated images at the %s provider boundary without changing user words', async mode => {
    const generated = image('artwork', true), camera = image('photo', false);
    const history = await sanitizeHistoryWithVerifiedMedia(deriveHistoryForApi([generated, camera]), async uris =>
      Object.fromEntries(uris.map(uri => [uri, { active: true, deleted: false }])));
    const send = vi.fn(async (_request: any) => (async function* () { yield { text: 'Ready' }; })());
    const aiClient = mode === 'managed' ? createManagedGeminiClient({ generateContentStream: send } as any) : { models: { generateContentStream: send } } as any;
    await generateGeminiResponse('origin-test', 'Please help.', history, { aiClient, currentFileParts: [
      { fileUri: 'files/current-art', mimeType: 'image/webp', origin: 'generated' },
      { fileUri: 'files/real-photo', mimeType: 'image/jpeg' },
    ] });
    const parts = send.mock.calls[0][0].contents.flatMap((content: any) => content.parts);
    expect(parts.filter((part: any) => part.text === GENERATED_IMAGE_CONTEXT)).toHaveLength(2);
    for (const uri of ['files/artwork', 'files/current-art']) {
      const at = parts.findIndex((part: any) => part.fileData?.fileUri === uri);
      expect(parts[at - 1]).toEqual({ text: GENERATED_IMAGE_CONTEXT });
      expect(parts[at].fileData).not.toHaveProperty('origin');
    }
    expect(parts.some((part: any) => part.text === 'Please help.')).toBe(true);
    expect(generated.text).toBe('Help me set up the room.');
    expect(camera).not.toHaveProperty('imageOrigin');
  });

  it('keeps origin in Live after saving and removing expired image uploads, including generated assistant images', async () => {
    const messages = JSON.parse(JSON.stringify([image('art', true), { ...image('assistant', false), role: 'assistant', maestroToolKind: 'image' }]));
    messages.forEach((message: ChatMessage) => { delete message.uploadedFileVariants; });
    const context = buildCoreLiveSystemInstruction({ basePrompt: 'Tutor', messages });
    expect(context.split(GENERATED_IMAGE_CONTEXT)).toHaveLength(3);
    expect(context).toContain('Help me set up the room.');
  });

  it('keeps variant provenance in headless history and does not guess from filenames', () => {
    const generated = image('from-headless', false);
    generated.uploadedFileVariants![0].origin = 'generated';
    const unknown = { ...image('unknown', false), attachmentName: 'assistant-generated.jpg' };
    const history = deriveHistoryForApi([generated, unknown]);
    expect(history[0].fileParts![0].origin).toBe('generated');
    expect(history[1].fileParts![0]).not.toHaveProperty('origin');
    expect(buildCoreLiveSystemInstruction({ basePrompt: 'Tutor', messages: [generated, unknown] }).split(GENERATED_IMAGE_CONTEXT)).toHaveLength(2);
  });
});
