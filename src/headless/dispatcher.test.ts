// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import type { HeadlessClient } from './client';
import {
  dispatchHeadlessMethod,
  HeadlessDispatchError,
  summarizeHeadlessArtifact,
} from './dispatcher';

const unusedClient = {} as HeadlessClient;

describe('headless dispatcher contract', () => {
  it('publishes a stable JSON-RPC discovery document', async () => {
    const result = await dispatchHeadlessMethod(unusedClient, 'system.describe') as {
      protocolVersion: string;
      transport: string;
      methods: string[];
    };
    expect(result.protocolVersion).toBe('1.0.0');
    expect(result.transport).toBe('json-rpc-2.0-ndjson');
    expect(result.methods).toContain('billing.checkout.completeTest');
    expect(result.methods).toContain('speech.synthetic.live');
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
