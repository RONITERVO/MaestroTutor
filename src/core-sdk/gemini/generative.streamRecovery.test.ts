// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { GoogleGenAI } from '@google/genai';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { generateGeminiResponse } from './generative';
import { createManagedGeminiClient } from '../managedGeminiClient';

const logs = vi.hoisted(() => ({ complete: vi.fn(), error: vi.fn() }));
vi.mock('../diagnostics', () => ({ debugLogService: { logRequest: () => logs } }));
const model = 'stream-recovery-fixture';
const interrupted = () => new Error('Incomplete JSON segment at the end');
const stream = (...chunks: any[]) => (async function* () { for (const chunk of chunks) yield chunk; })();
const brokenStream = (...chunks: any[]) => (async function* () {
  for (const chunk of chunks) yield chunk;
  throw interrupted();
})();
const client = (send: (...args: any[]) => any, mode = 'byok') => mode === 'managed'
  ? createManagedGeminiClient({ generateContentStream: send } as any)
  : { models: { generateContentStream: send } } as any;

beforeEach(() => { vi.useFakeTimers(); vi.clearAllMocks(); });
afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

describe.each(['byok', 'managed'])('%s buffered response recovery', mode => {
  it('discards an incomplete private plan and retries once with the same request and model', async () => {
    const send = vi.fn().mockResolvedValueOnce(brokenStream({ text: '{"commands":[' }))
      .mockResolvedValueOnce(stream({ text: '{"commands":[]}', usageMetadata: { totalTokenCount: 12 } }));
    const onProgress = vi.fn();
    const done = generateGeminiResponse(model, 'Put the apple on the floor', [], {
      aiClient: client(send, mode), configOverrides: { responseMimeType: 'application/json' }, lifecycleHooks: { onProgress },
    });
    const result = expect(done).resolves.toMatchObject({ text: '{"commands":[]}', modelUsed: model });
    await vi.advanceTimersByTimeAsync(1000); await result;
    expect(send).toHaveBeenCalledTimes(2);
    const [first, second] = send.mock.calls.map(([request]) => request);
    expect(second.model).toBe(first.model); expect(second.contents).toEqual(first.contents);
    const { abortSignal: _firstSignal, ...firstConfig } = first.config;
    const { abortSignal: _secondSignal, ...secondConfig } = second.config;
    expect(secondConfig).toEqual(firstConfig);
    expect(onProgress.mock.calls.map(([event]) => event.phase)).not.toContain('high-demand');
    expect(onProgress.mock.calls.map(([event]) => event.phase)).not.toContain('fallback-switch');
    expect(onProgress).toHaveBeenCalledWith(expect.objectContaining({ phase: 'retry-scheduled', reason: 'incomplete-stream' }));
    expect(logs.error).toHaveBeenCalledOnce(); expect(logs.complete).toHaveBeenCalledOnce();
    expect(vi.getTimerCount()).toBe(0);
  });

  it('stops after a second incomplete stream instead of spending the high-demand retry budget', async () => {
    const send = vi.fn(async () => brokenStream());
    const done = generateGeminiResponse(model, 'Request', [], { aiClient: client(send, mode) });
    const failed = expect(done).rejects.toThrow('Incomplete JSON segment');
    await vi.advanceTimersByTimeAsync(60_000); await failed;
    expect(send).toHaveBeenCalledTimes(2); expect(logs.complete).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
  });

  it.each(['text', 'thought'])('does not retry after delivering %s to a consumer', async kind => {
    const partial = kind === 'text' ? { text: 'Hola' }
      : { candidates: [{ content: { parts: [{ text: 'Thinking', thought: true }] } }] };
    const send = vi.fn(async () => brokenStream(partial));
    const onTextDelta = vi.fn(), onThoughtDelta = vi.fn();
    await expect(generateGeminiResponse(model, 'Request', [], { aiClient: client(send, mode),
      lifecycleHooks: { onTextDelta, onThoughtDelta } })).rejects.toThrow('Incomplete JSON segment');
    expect(kind === 'text' ? onTextDelta : onThoughtDelta).toHaveBeenCalledOnce();
    expect(send).toHaveBeenCalledOnce(); expect(vi.getTimerCount()).toBe(0);
  });

  it('permits a retry before the first output callback and delivers only the successful response', async () => {
    const send = vi.fn().mockResolvedValueOnce(brokenStream()).mockResolvedValueOnce(stream({ text: 'Hola' }));
    const onTextDelta = vi.fn();
    const done = generateGeminiResponse(model, 'Request', [], { aiClient: client(send, mode), lifecycleHooks: { onTextDelta } });
    const result = expect(done).resolves.toMatchObject({ text: 'Hola' });
    await vi.advanceTimersByTimeAsync(1000); await result;
    expect(onTextDelta.mock.calls).toEqual([['Hola', 'Hola']]); expect(send).toHaveBeenCalledTimes(2);
  });

  it('honors Stop during the recovery delay without another provider request', async () => {
    const controller = new AbortController();
    const send = vi.fn(async () => brokenStream());
    const done = generateGeminiResponse(model, 'Request', [], { aiClient: client(send, mode), signal: controller.signal,
      lifecycleHooks: { onProgress: event => { if (event.phase === 'retry-scheduled') controller.abort(); } } });
    const stopped = expect(done).rejects.toMatchObject({ name: 'AbortError' });
    await vi.advanceTimersByTimeAsync(60_000); await stopped;
    expect(send).toHaveBeenCalledOnce(); expect(vi.getTimerCount()).toBe(0);
  });

  it('does not treat malformed model JSON or an ordinary error as a transport interruption', async () => {
    const send = vi.fn().mockResolvedValueOnce(stream({ text: '{"commands":[' }))
      .mockRejectedValueOnce(new SyntaxError('Unexpected end of JSON input'));
    await expect(generateGeminiResponse(model, 'Request', [], { aiClient: client(send, mode) }))
      .resolves.toMatchObject({ text: '{"commands":[' });
    await expect(generateGeminiResponse(model, 'Request', [], { aiClient: client(send, mode) }))
      .rejects.toThrow('Unexpected end of JSON input');
    expect(send).toHaveBeenCalledTimes(2); expect(vi.getTimerCount()).toBe(0);
  });

  it('does not reset its single recovery allowance when falling back without Search', async () => {
    const send = vi.fn().mockResolvedValueOnce(brokenStream())
      .mockRejectedValueOnce(Object.assign(new Error('Search unavailable'), { status: 429 }))
      .mockResolvedValueOnce(brokenStream()).mockResolvedValueOnce(stream({ text: 'Must not run' }));
    const done = generateGeminiResponse(model, 'Request', [], { aiClient: client(send, mode), useGoogleSearch: true });
    const failed = expect(done).rejects.toThrow('Incomplete JSON segment');
    await vi.advanceTimersByTimeAsync(60_000); await failed;
    expect(send).toHaveBeenCalledTimes(3);
    expect(send.mock.calls[1][0].config.tools).toEqual([{ googleSearch: {} }]);
    expect(send.mock.calls[2][0].config.tools).toBeUndefined();
    expect(vi.getTimerCount()).toBe(0);
  });

  it('does not reinterpret a coded service error just because its message matches', async () => {
    const send = vi.fn().mockRejectedValue(Object.assign(interrupted(), { status: 400, code: 'INVALID_ARGUMENT' }));
    await expect(generateGeminiResponse(model, 'Request', [], { aiClient: client(send, mode) }))
      .rejects.toMatchObject({ code: 'INVALID_ARGUMENT' });
    expect(send).toHaveBeenCalledOnce();
  });
});

it('recovers the installed Google SDK EOF framing failure with no real network request', async () => {
  const event = (text: string) => `data: ${JSON.stringify({ candidates: [{ content: { parts: [{ text }] } }] })}\n\n`;
  const fetch = vi.fn().mockResolvedValueOnce(new Response(event('{"commands":[') + 'data: {"candidates":'))
    .mockResolvedValueOnce(new Response(event('{"commands":[]}')));
  vi.stubGlobal('fetch', fetch);
  const sdk = new GoogleGenAI({ apiKey: 'test-only' });
  const done = generateGeminiResponse(model, 'Request', [], { aiClient: client(request => sdk.models.generateContentStream(request)) });
  const result = expect(done).resolves.toMatchObject({ text: '{"commands":[]}' });
  await vi.advanceTimersByTimeAsync(1000); await result;
  expect(fetch).toHaveBeenCalledTimes(2);
  expect(logs.error).toHaveBeenCalledWith(expect.objectContaining({ message: 'Incomplete JSON segment at the end' }));
});
