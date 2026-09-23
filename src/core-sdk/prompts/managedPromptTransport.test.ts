// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it, vi } from 'vitest';
import { createManagedGeminiClient } from '../managedGeminiClient';
import { buildLiveSttSystemInstruction, REPLY_SUGGESTIONS_RESPONSE_SCHEMA } from '../../core/config/prompts';
import { createLiveOpenReason, LIVE_OPEN_TRIGGER } from '../../../shared/liveOpenReason';

describe('managed prompt transport', () => {
  for (const method of ['generateContent', 'generateContentStream'] as const) {
    it(`forwards ${method} text, roles, attachments and schema without rewriting`, async () => {
      const send = vi.fn(async (_request: any) => method === 'generateContent' ? { text: 'ok' } : { async *[Symbol.asyncIterator]() { yield { text: 'ok' }; } });
      const backend = { [method]: send };
      const client = createManagedGeminiClient(backend as any);
      const request = {
        model: 'gemini-3.8-flash',
        contents: [{ role: 'user', parts: [{ text: 'Quotes " stay.\n日本語 [FI]' }, { fileData: { fileUri: 'files/fixture', mimeType: 'image/png' } }] },
          { role: 'model', parts: [{ text: 'Earlier assistant turn.' }] }],
        config: { systemInstruction: 'Instruction with\n\nblank lines.', responseJsonSchema: REPLY_SUGGESTIONS_RESPONSE_SCHEMA,
          responseMimeType: 'application/json', tools: [{ googleSearch: {} }], maxOutputTokens: 500 },
      };
      const original = structuredClone(request);
      await client.models[method](request);
      expect(send.mock.calls[0][0]).toEqual(original);
      expect(request).toEqual(original);
    });
  }

  it('stores the exact Live instruction and configuration in the gateway ticket request', async () => {
    const stop = new Error('stop before networking');
    const createLiveGatewayTicket = vi.fn(async (_request: any) => { throw stop; });
    const client = createManagedGeminiClient({ createLiveGatewayTicket } as any, { createGatewaySocket: vi.fn() });
    const request = { model: 'gemini-live-test',
      liveOpenReason: createLiveOpenReason(LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE, { requestId: 'prompt-contract' }),
      config: { systemInstruction: buildLiveSttSystemInstruction({ lastAssistantMessage: 'Hola', replySuggestions: ['Sí'] }),
        responseModalities: ['AUDIO'], inputAudioTranscription: {}, outputAudioTranscription: {} },
    };
    await expect(client.live.connect({ ...request, callbacks: {} })).rejects.toThrow(stop);
    expect(createLiveGatewayTicket.mock.calls[0][0]).toEqual({ purpose: 'live', ...request });
  });
});
