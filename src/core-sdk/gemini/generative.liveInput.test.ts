// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it, vi } from 'vitest';
import { generateGeminiResponse } from './generative';
import { createManagedGeminiClient } from '../managedGeminiClient';
import { LiveInputContext, missingLiveInput } from '../media/liveInputContext';
import { debugLogService } from '../diagnostics';

describe('shared provider original Live media', () => {
  it.each(['byok', 'managed'])('sends the same validated audio and frames via %s and redacts diagnostics', async mode => {
    const input = new LiveInputContext(() => 123);
    input.recordAudio('AAD/fwCA//8='); input.recordFrame('/9gKFP/Z');
    const media = input.finish();
    const log = vi.spyOn(debugLogService, 'logRequest');
    const send = vi.fn(async (_request: any) => (async function* () { yield { text: 'Ready' }; })());
    const aiClient = mode === 'managed' ? createManagedGeminiClient({ generateContentStream: send } as any) : { models: { generateContentStream: send } } as any;
    await generateGeminiResponse('media-test', 'Original request', [{ role: 'user', text: 'Original history' }], { aiClient, systemInstruction: 'Original system', liveInputMedia: media });
    const request = send.mock.calls[0][0] as any;
    expect(request.config.systemInstruction).toBe('Original system');
    const parts = request.contents.flatMap((content: any) => content.parts);
    expect(parts.filter((part: any) => part.inlineData)).toEqual([
      { inlineData: { mimeType: 'audio/wav', data: media.audio!.data } },
      { inlineData: { mimeType: 'image/jpeg', data: media.frames[0].data } },
    ]);
    expect(JSON.stringify(request)).toContain('after 4 WAV samples');
    expect(JSON.stringify(log.mock.calls)).not.toContain(media.audio!.data);
    expect(JSON.stringify(log.mock.calls)).not.toContain(media.frames[0].data);
    expect(JSON.stringify(log.mock.calls)).toContain('[REDACTED]'); log.mockRestore();
  });
  it('rejects incomplete media before credentials or network use', async () => {
    const resolveAiClient = vi.fn();
    await expect(generateGeminiResponse('media-test', 'Request', [], { resolveAiClient, liveInputMedia: missingLiveInput() })).rejects.toThrow('original audio');
    expect(resolveAiClient).not.toHaveBeenCalled();
  });
});

it('pins validated media across asynchronous credential resolution', async () => {
  const input = new LiveInputContext(() => 0); input.recordAudio('AAA=');
  const media = input.finish(), original = media.audio!.data;
  let resolve!: (client: any) => void;
  const pending = new Promise<any>(done => { resolve = done; });
  const send = vi.fn(async (_request: any) => (async function* () { yield { text: 'Ready' }; })());
  const result = generateGeminiResponse('media-test', 'Request', [], { resolveAiClient: () => pending, liveInputMedia: media });
  media.audio!.data = 'REPLACED'; media.packets[0].samples = 999;
  resolve({ models: { generateContentStream: send } }); await result;
  expect(JSON.stringify(send.mock.calls[0][0])).toContain(original);
  expect(JSON.stringify(send.mock.calls[0][0])).not.toContain('REPLACED');
});
