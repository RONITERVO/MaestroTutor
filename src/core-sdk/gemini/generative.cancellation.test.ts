// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { generateGeminiResponse } from './generative';
import { createManagedGeminiClient } from '../managedGeminiClient';

vi.mock('../diagnostics', () => ({ debugLogService: { logRequest: () => ({ complete: vi.fn(), error: vi.fn() }) } }));
function deferred<T>() {
  let resolve!: (value: T) => void, reject!: (error: unknown) => void;
  const promise = new Promise<T>((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
}
const chunk = (text: string) => ({ text, candidates: [{ content: { parts: [{ text: 'thought', thought: true }] } }] });
const stream = (text = 'Ready') => (async function* () { yield chunk(text); })();
const client = (generateContentStream: (...args: any[]) => any) => ({ models: { generateContentStream } }) as any;
const model = 'cancellation-fixture';

beforeEach(() => vi.useFakeTimers());
afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks(); });

describe('owned model request cancellation', () => {
  it('does not resolve credentials or send a request when already stopped', async () => {
    const controller = new AbortController(); controller.abort();
    const resolveAiClient = vi.fn();
    await expect(generateGeminiResponse(model, 'Request', [], { resolveAiClient, signal: controller.signal }))
      .rejects.toMatchObject({ name: 'AbortError' });
    expect(resolveAiClient).not.toHaveBeenCalled();
  });

  it('releases a pending client lookup and never sends after a late credential resolution', async () => {
    const controller = new AbortController(), access = deferred<any>(), started = deferred<void>();
    const send = vi.fn();
    const resolveAiClient = vi.fn(() => { started.resolve(); return access.promise; });
    const done = generateGeminiResponse(model, 'Request', [], { resolveAiClient, signal: controller.signal });
    const stopped = expect(done).rejects.toMatchObject({ name: 'AbortError' });
    await started.promise; controller.abort(); await stopped;
    access.resolve(client(send)); await vi.advanceTimersByTimeAsync(0);
    expect(send).not.toHaveBeenCalled(); expect(vi.getTimerCount()).toBe(0);
  });

  it.each(['byok', 'managed'])('aborts the %s transport and releases the caller even when the transport ignores Stop', async mode => {
    const controller = new AbortController(), pending = deferred<any>(), started = deferred<void>();
    let transportSignal!: AbortSignal;
    const send = vi.fn((request: any, signal?: AbortSignal) => {
      transportSignal = mode === 'managed' ? signal! : request.config.abortSignal;
      started.resolve(); return pending.promise;
    });
    const aiClient = mode === 'managed' ? createManagedGeminiClient({ generateContentStream: send } as any) : client(send);
    const progress = vi.fn(), onTextDelta = vi.fn();
    const done = generateGeminiResponse(model, 'Request', [], { aiClient, signal: controller.signal,
      useGoogleSearch: true, lifecycleHooks: { onProgress: progress, onTextDelta } });
    const stopped = expect(done).rejects.toMatchObject({ name: 'AbortError' });
    await started.promise; controller.abort(); await stopped;
    expect(transportSignal.aborted).toBe(true);
    if (mode === 'managed') expect(send.mock.calls[0][0].config).not.toHaveProperty('abortSignal');
    await vi.advanceTimersByTimeAsync(60_000);
    expect(send).toHaveBeenCalledOnce(); expect(vi.getTimerCount()).toBe(0);
    pending.resolve(stream('Late output')); await vi.advanceTimersByTimeAsync(0);
    expect(onTextDelta).not.toHaveBeenCalled();
    expect(progress.mock.calls.map(([event]) => event.phase)).not.toContain('success');
    expect(progress.mock.calls.map(([event]) => event.phase)).not.toContain('fallback-switch');
  });

  it('ignores late text and thoughts after stopping a stream that has already produced output', async () => {
    const controller = new AbortController(), pending = deferred<any>(), waiting = deferred<void>();
    const send = vi.fn(async () => (async function* () {
      yield chunk('First'); waiting.resolve(); yield await pending.promise;
    })());
    const onTextDelta = vi.fn(), onThoughtDelta = vi.fn();
    const done = generateGeminiResponse(model, 'Request', [], { aiClient: client(send), signal: controller.signal,
      lifecycleHooks: { onTextDelta, onThoughtDelta } });
    const stopped = expect(done).rejects.toMatchObject({ name: 'AbortError' });
    await waiting.promise; controller.abort(); await stopped;
    pending.resolve(chunk('Late')); await vi.advanceTimersByTimeAsync(0);
    expect(onTextDelta.mock.calls).toEqual([['First', 'First']]);
    expect(onThoughtDelta).toHaveBeenCalledOnce(); expect(send).toHaveBeenCalledOnce();
    expect(vi.getTimerCount()).toBe(0);
  });

  it('does not deliver another callback from the same chunk after a callback stops the task', async () => {
    const controller = new AbortController(), onThoughtDelta = vi.fn();
    await expect(generateGeminiResponse(model, 'Request', [], { aiClient: client(async () => stream()), signal: controller.signal,
      lifecycleHooks: { onTextDelta: () => controller.abort(), onThoughtDelta } }))
      .rejects.toMatchObject({ name: 'AbortError' });
    expect(onThoughtDelta).not.toHaveBeenCalled(); expect(vi.getTimerCount()).toBe(0);
  });

  it('cancels retry backoff without another provider attempt', async () => {
    const controller = new AbortController(), waiting = deferred<void>();
    const highDemand = Object.assign(new Error('High demand'), { status: 503, code: 'UNAVAILABLE' });
    const send = vi.fn(async () => { throw highDemand; });
    const done = generateGeminiResponse(model, 'Request', [], { aiClient: client(send), signal: controller.signal,
      lifecycleHooks: { onProgress: event => { if (event.phase === 'retry-scheduled') waiting.resolve(); } } });
    const stopped = expect(done).rejects.toMatchObject({ name: 'AbortError' });
    await waiting.promise;
    const attempts = send.mock.calls.length; controller.abort(); await stopped;
    await vi.advanceTimersByTimeAsync(60_000);
    expect(send).toHaveBeenCalledTimes(attempts); expect(vi.getTimerCount()).toBe(0);
  });

  it('consumes a late provider failure without retry or an unhandled rejection', async () => {
    const controller = new AbortController(), pending = deferred<any>(), started = deferred<void>();
    const send = vi.fn(() => { started.resolve(); return pending.promise; });
    const done = generateGeminiResponse(model, 'Request', [], { aiClient: client(send), signal: controller.signal });
    const stopped = expect(done).rejects.toMatchObject({ name: 'AbortError' });
    await started.promise; controller.abort(); await stopped;
    pending.reject(Object.assign(new Error('Late high demand'), { status: 503 }));
    await vi.advanceTimersByTimeAsync(60_000);
    expect(send).toHaveBeenCalledOnce(); expect(vi.getTimerCount()).toBe(0);
  });

  it('cleans up cancellation listeners after success without affecting another request', async () => {
    const controller = new AbortController(), signals: AbortSignal[] = [];
    const send = vi.fn(async (request: any) => { signals.push(request.config.abortSignal); return stream(); });
    const aiClient = client(send);
    await expect(generateGeminiResponse(model, 'First', [], { aiClient, signal: controller.signal })).resolves.toMatchObject({ text: 'Ready' });
    controller.abort();
    expect(signals[0].aborted).toBe(false);
    await expect(generateGeminiResponse(model, 'Second', [], { aiClient })).resolves.toMatchObject({ text: 'Ready' });
    expect(signals[1].aborted).toBe(false); expect(vi.getTimerCount()).toBe(0);
  });
});
