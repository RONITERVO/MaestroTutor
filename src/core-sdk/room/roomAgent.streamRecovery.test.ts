// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { runRoomActionTask, type RoomAgentState } from './roomAgent';
import { createManagedGeminiClient } from '../managedGeminiClient';

beforeEach(() => vi.useFakeTimers());
afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks(); });

it.each(['byok', 'managed'])('retains a completed %s room action when the next planning stream breaks', async mode => {
  let current: RoomAgentState = { version: 1, session: 'a'.repeat(32), revision: 1, sceneRevision: 4,
    ack: 0, ok: true, status: 'Ready', objects: [], created: [], canUndo: false, canRedo: false, physicsRunning: false };
  const create = { action: 'create', reference: 'robot', name: 'Robot', kind: 'boxRobot' };
  let attempt = 0;
  const send = vi.fn(async () => {
    const index = attempt++;
    return (async function* () {
      // Even complete-looking plan text must not execute before a successful EOF.
      yield { text: JSON.stringify({ commands: index < 2 ? [create] : [] }) };
      if (index === 1) throw new Error('Incomplete JSON segment at the end');
    })();
  });
  const aiClient = mode === 'managed' ? createManagedGeminiClient({ generateContentStream: send } as any)
    : { models: { generateContentStream: send } } as any;
  const execute = vi.fn(async () => {
    current = { ...current, ack: 1, sceneRevision: 5, status: 'Created robot', created: ['b'.repeat(32)] };
    return current;
  });
  const beforeDispatch = vi.fn(), onReceipt = vi.fn();
  const done = runRoomActionTask({ model: 'recovery-fixture', prompt: 'Make a robot', history: [] }, { aiClient },
    { state: () => current, valid: () => true, execute }, () => {}, { beforeDispatch, onReceipt });
  const completed = expect(done).resolves.toMatchObject({ receipts: [expect.objectContaining({ ack: 1 })], budgetExhausted: false });
  await vi.advanceTimersByTimeAsync(1000); await completed;
  expect(execute).toHaveBeenCalledOnce(); expect(beforeDispatch).toHaveBeenCalledOnce(); expect(onReceipt).toHaveBeenCalledOnce();
  expect(execute.mock.calls[0]).toMatchObject([[create], 4, []]);
  expect(send).toHaveBeenCalledTimes(3);
  const requests = send.mock.calls as unknown as [any][];
  expect(requests[2][0].contents).toEqual(requests[1][0].contents);
  const retryContext = JSON.parse(requests[2][0].contents[0].parts[0].text);
  expect(retryContext.receipts).toEqual([current]);
  expect(vi.getTimerCount()).toBe(0);
});
