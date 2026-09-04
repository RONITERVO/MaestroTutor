// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it, vi } from 'vitest';
import type { HeadlessClient } from './client';
import {
  dispatchHeadlessMethod,
  HeadlessDispatchError,
  summarizeHeadlessArtifact,
} from './dispatcher';

const unusedClient = { accessMode: 'managed' } as HeadlessClient;

describe('headless dispatcher contract', () => {
  it('publishes a stable JSON-RPC discovery document', async () => {
    const result = await dispatchHeadlessMethod(unusedClient, 'system.describe') as {
      protocolVersion: string;
      transport: string;
      methods: string[];
      methodInfo: Record<string, { params: string[]; accessModes: string[]; parityClass: string }>;
      access: { accessMode: string; availableMethods: string[]; unavailableMethods: string[] };
      configuredModels: { text: { default: string }; music: string };
    };
    expect(result.protocolVersion).toBe('1.7.0');
    expect(result.transport).toBe('json-rpc-2.0-ndjson');
    expect(result.methods).toContain('billing.checkout.completeTest');
    expect(result.methods).toContain('speech.synthetic.live');
    expect(result.methods).toContain('media.music.generate');
    expect(result.methods).toContain('media.audioNote.generate');
    expect(result.methods).toContain('chat.attachment.turn');
    expect(result.methods).toContain('speech.transcribe');
    expect(result.methods).toContain('speech.tts.generate');
    expect(result.methods).toContain('live.conversation.turn');
    expect(result.methods).toContain('live.observer.turn');
    expect(result.methods).toContain('suggestions.process');
    expect(result.methods).toContain('translation.create');
    expect(result.methods).toContain('chat.reengage');
    expect(result.methods).toContain('journey.firstLesson');
    expect(result.methodInfo['chat.attachment.turn'].params.join(' ')).toContain('svg|video|office');
    expect(result.methodInfo['chat.attachment.turn'].params).toEqual(expect.arrayContaining([
      'useGoogleSearch?',
      'requireInvariants?',
    ]));
    expect(result.methodInfo['live.observer.turn'].params.join(' ')).toContain('expectedTranscript');
    expect(result.methodInfo['journey.firstLesson'].params.join(' ')).toContain('required with custom PCM');
    expect(Object.keys(result.methodInfo).sort()).toEqual([...result.methods].sort());
    expect(result.methodInfo['chat.turn']).toMatchObject({
      accessModes: ['managed', 'byok'],
      parityClass: 'provider-parity',
    });
    expect(result.methodInfo['billing.checkout.create']).toMatchObject({
      accessModes: ['managed'],
      parityClass: 'managed-account-only',
    });
    expect(result.access).toMatchObject({ accessMode: 'managed', unavailableMethods: [] });
    expect(result.configuredModels.text.default).toBeTruthy();
    expect(result.configuredModels.music).toBeTruthy();
  });

  it('advertises and enforces managed-only methods while allowing BYOK content reports', async () => {
    const submitAiContentReport = vi.fn(async (request: unknown) => ({ ok: true, reportId: 'report-1', request }));
    const byokClient = {
      accessMode: 'byok',
      account: { submitAiContentReport },
    } as unknown as HeadlessClient;
    const description = await dispatchHeadlessMethod(byokClient, 'system.describe') as any;
    expect(description.access.unavailableMethods).toEqual(expect.arrayContaining([
      'auth.signIn',
      'billing.checkout.create',
    ]));
    expect(description.access.availableMethods).toContain('report.submit');
    await expect(dispatchHeadlessMethod(byokClient, 'auth.signIn')).rejects.toThrow('requires managed access');
    await expect(dispatchHeadlessMethod(byokClient, 'auth.google.verifyHosted')).rejects.toThrow('requires managed access');
    await expect(dispatchHeadlessMethod(byokClient, 'report.submit', {
      accessMode: 'managed', messageId: 'message-1', reason: 'other',
    })).rejects.toMatchObject({ rpcCode: -32602 });
    await expect(dispatchHeadlessMethod(byokClient, 'report.submit', {
      messageId: 'message-1', reason: 'other',
    })).resolves.toMatchObject({ ok: true, reportId: 'report-1' });
    expect(submitAiContentReport).toHaveBeenCalledWith(expect.objectContaining({ accessMode: 'byok' }));
  });

  it('uses the JSON-RPC method-not-found code', async () => {
    await expect(dispatchHeadlessMethod(unusedClient, 'missing.method')).rejects.toMatchObject({
      name: 'HeadlessDispatchError', rpcCode: -32601,
    } satisfies Partial<HeadlessDispatchError>);
  });

  it('uses the JSON-RPC invalid-params code', async () => {
    await expect(dispatchHeadlessMethod(unusedClient, 'chat.turn', {})).rejects.toMatchObject({
      name: 'HeadlessDispatchError', rpcCode: -32602,
    } satisfies Partial<HeadlessDispatchError>);
  });

  it('materializes SDK text getters for JSON-safe BYOK generate responses', async () => {
    const responseWithTextGetter = (text: string) => {
      const response = Object.create({
        get text() { return text; },
      }) as Record<string, unknown>;
      response.candidates = [{ content: { parts: [{ text }] } }];
      return response;
    };
    const client = {
      accessMode: 'byok',
      ai: {
        models: {
          generateContent: vi.fn(async () => responseWithTextGetter('provider-ok')),
          generateContentStream: vi.fn(async function* () {
            yield responseWithTextGetter('stream-ok');
          }),
        },
      },
    } as unknown as HeadlessClient;

    const generated = await dispatchHeadlessMethod(client, 'gemini.generate', {
      model: 'test-model', contents: 'test',
    });
    const streamed = await dispatchHeadlessMethod(client, 'gemini.generateStream', {
      model: 'test-model', contents: 'test',
    }) as { chunks: unknown[] };

    expect(JSON.parse(JSON.stringify(generated))).toMatchObject({ text: 'provider-ok' });
    expect(JSON.parse(JSON.stringify(streamed.chunks[0]))).toMatchObject({ text: 'stream-ok' });
  });

  it.each([
    {},
    { fixture: '' },
    { fixture: 7 },
    { fixture: 'unknown' },
    { dataUrl: 'data:text/plain;base64,QQ==' },
    { mimeType: 'text/plain' },
    { dataUrl: '', mimeType: 'text/plain' },
    { dataUrl: 'data:text/plain;base64,QQ==', mimeType: '' },
  ])('rejects invalid attachment alternatives before starting the journey: %j', async invalid => {
    await expect(dispatchHeadlessMethod(unusedClient, 'chat.attachment.turn', {
      text: 'Inspect this attachment.',
      ...invalid,
    })).rejects.toMatchObject({ rpcCode: -32602 });
  });

  it('bounds and filters language discovery output', async () => {
    const result = await dispatchHeadlessMethod(unusedClient, 'language.list', {
      targetLanguageCode: 'fi-FI',
      nativeLanguageCode: 'en-US',
      limit: 1,
    }) as { total: number; returned: number; truncated: boolean; pairs: Array<Record<string, unknown>> };

    expect(result).toMatchObject({ total: 1, returned: 1, truncated: false });
    expect(result.pairs[0]).toMatchObject({
      id: 'fi-FI-en-US',
      targetLanguageCode: 'fi-FI',
      nativeLanguageCode: 'en-US',
    });
    expect(result.pairs[0]).not.toHaveProperty('baseSystemPrompt');
    expect(result.pairs[0]).not.toHaveProperty('baseReplySuggestionsPrompt');
  });

  it('rejects unbounded language discovery limits', async () => {
    await expect(dispatchHeadlessMethod(unusedClient, 'language.list', { limit: 501 }))
      .rejects.toMatchObject({ rpcCode: -32602 });
  });

  it('summarizes artifact content deterministically without exposing it', () => {
    expect(summarizeHeadlessArtifact({
      mimeType: 'image/svg+xml',
      fileName: 'lesson.svg',
      encoding: 'text',
      content: '<svg/>',
    })).toEqual({
      mimeType: 'image/svg+xml',
      fileName: 'lesson.svg',
      encoding: 'text',
      contentLength: 6,
      contentSha256: 'd4dc56669143034f31aa309635d4113d9ad76a02b1739da22c965ed2049be9e6',
    });
  });
});
