// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { runTutorTextTurn } from './journeys';
import { createManagedGeminiClient } from '../../core-sdk/managedGeminiClient';
vi.mock('../../core-sdk/diagnostics', () => ({ debugLogService: { logRequest: () => ({ complete: vi.fn(), error: vi.fn() }) } }));
const input = { model: 'cancellation-fixture', prompt: 'Hello', history: [], nativeLanguageCode: 'en', systemInstruction: 'Tutor' };
const stream = () => (async function* () { yield { text: 'Hello' }; })();
const client = (send: (...args: any[]) => any) => ({ models: { generateContentStream: send } }) as any;
beforeEach(() => vi.useFakeTimers());
afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks(); });

it('does not send a text request that is already stale', async () => {
  const send = vi.fn(async () => stream());
  await expect(runTutorTextTurn(input, { aiClient: client(send), isCurrent: () => false })).rejects.toMatchObject({ name: 'AbortError' });
  expect(send).not.toHaveBeenCalled(); expect(vi.getTimerCount()).toBe(0);
});

it.each(['byok', 'managed'])('cancels a stalled %s request when its conversation or thinking message is no longer current', async mode => {
  let current = true, signal!: AbortSignal;
  let resolve!: (value: any) => void;
  const pending = new Promise<any>(done => { resolve = done; });
  const send = vi.fn((request: any, transportSignal?: AbortSignal) => {
    signal = mode === 'managed' ? transportSignal! : request.config.abortSignal;
    return pending;
  });
  const aiClient = mode === 'managed' ? createManagedGeminiClient({ generateContentStream: send } as any) : client(send);
  const delta = vi.fn();
  const done = runTutorTextTurn(input, { aiClient, isCurrent: () => current, lifecycleHooks: { onTextDelta: delta } });
  const outcome = done.then(() => 'completed', error => error.name);
  await vi.advanceTimersByTimeAsync(0); current = false;
  await vi.advanceTimersByTimeAsync(100);
  expect(signal.aborted).toBe(true);
  expect(await outcome).toBe('AbortError');
  resolve(stream()); await vi.advanceTimersByTimeAsync(60_000);
  expect(send).toHaveBeenCalledOnce(); expect(delta).not.toHaveBeenCalled(); expect(vi.getTimerCount()).toBe(0);
});

it('stops retry backoff as soon as a turn becomes stale', async () => {
  let current = true;
  const send = vi.fn(async () => { throw Object.assign(new Error('High demand'), { status: 503 }); });
  const done = runTutorTextTurn(input, { aiClient: client(send), isCurrent: () => current,
    lifecycleHooks: { onProgress: event => { if (event.phase === 'retry-scheduled') current = false; } } });
  const outcome = done.then(() => 'completed', error => error.name);
  await vi.advanceTimersByTimeAsync(100); const attempts = send.mock.calls.length;
  await vi.advanceTimersByTimeAsync(60_000);
  expect(await outcome).toBe('AbortError'); expect(send).toHaveBeenCalledTimes(attempts); expect(vi.getTimerCount()).toBe(0);
});

it('removes the watcher and caller signal listener after successful completion', async () => {
  const controller = new AbortController(), remove = vi.spyOn(controller.signal, 'removeEventListener');
  const send = vi.fn(async () => stream());
  await expect(runTutorTextTurn(input, { aiClient: client(send), isCurrent: () => true, signal: controller.signal }))
    .resolves.toMatchObject({ rawResponse: 'Hello' });
  expect(vi.getTimerCount()).toBe(0); expect(remove).toHaveBeenCalled();
});


it('forwards caller cancellation while the conversation remains current', async () => {
  const controller = new AbortController(); let signal!: AbortSignal;
  const send = vi.fn((request: any) => { signal = request.config.abortSignal; return new Promise(() => {}); });
  const outcome = runTutorTextTurn(input, { aiClient: client(send), isCurrent: () => true, signal: controller.signal })
    .then(() => 'completed', error => error.name);
  await vi.advanceTimersByTimeAsync(0); controller.abort();
  expect(await outcome).toBe('AbortError'); expect(signal.aborted).toBe(true); expect(vi.getTimerCount()).toBe(0);
});

it('suppresses callbacks immediately when a streaming callback invalidates the turn', async () => {
  let current = true; const thought = vi.fn();
  const send = vi.fn(async () => (async function* () { yield { text: 'First', candidates: [{ content: { parts: [{ text: 'Late thought', thought: true }] } }] }; })());
  await expect(runTutorTextTurn(input, { aiClient: client(send), isCurrent: () => current,
    lifecycleHooks: { onTextDelta: () => { current = false; }, onThoughtDelta: thought } }))
    .rejects.toMatchObject({ name: 'AbortError' });
  expect(thought).not.toHaveBeenCalled(); expect(vi.getTimerCount()).toBe(0);
});
