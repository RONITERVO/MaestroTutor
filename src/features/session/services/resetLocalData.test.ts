// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {afterEach, expect, it, vi} from 'vitest';
const mock = vi.hoisted(() => ({open: vi.fn()}));
vi.mock('../../../core/db', () => ({openDB: mock.open}));
import {resetLocalData} from './resetLocalData';
afterEach(() => vi.resetAllMocks());

function database() {
  const clear = vi.fn();
  const tx = {oncomplete: undefined as undefined | (() => void), onabort: undefined as undefined | (() => void),
    abort: vi.fn(), objectStore: vi.fn(() => ({clear}))};
  const close = vi.fn();
  mock.open.mockResolvedValue({objectStoreNames: ['first', 'second'], transaction: () => tx, close});
  return {tx, clear, close};
}
it('waits for abort after a synchronous failure rather than reporting while writes are pending', async () => {
  const {tx, clear, close} = database();
  clear.mockImplementationOnce(() => {}).mockImplementationOnce(() => {throw new Error('Clear failed');});
  let settled = false;
  const pending = resetLocalData().finally(() => {settled = true;});
  const result = expect(pending).rejects.toThrow('Clear failed');
  await vi.waitFor(() => expect(tx.abort).toHaveBeenCalledOnce());
  expect(settled).toBe(false); expect(close).not.toHaveBeenCalled();
  tx.onabort!(); await result; expect(close).toHaveBeenCalledOnce();
});
it('does not claim rollback if a guard changes after the transaction committed', async () => {
  const {tx, close} = database(); let changed!: () => void; let unchanged = true;
  const unsubscribe = vi.fn();
  const pending = resetLocalData({unchanged: () => unchanged, subscribe: callback => {changed = callback; return unsubscribe;}});
  const result = expect(pending).rejects.toThrow('The reset finished before it could be stopped.');
  await vi.waitFor(() => expect(tx.objectStore).toHaveBeenCalledTimes(2));
  tx.abort.mockImplementation(() => {throw new DOMException('Finished', 'InvalidStateError');});
  unchanged = false; expect(() => changed()).not.toThrow(); tx.oncomplete!(); await result;
  expect(unsubscribe).toHaveBeenCalledOnce(); expect(close).toHaveBeenCalledOnce();
});
it('rolls back a thrown guard even when its thrown value is undefined', async () => {
  const {tx, clear} = database();
  const pending = resetLocalData({unchanged: () => {throw undefined;}, subscribe: () => () => {}});
  const result = expect(pending).rejects.toThrow('Reset failed.');
  await vi.waitFor(() => expect(tx.abort).toHaveBeenCalledOnce());
  expect(clear).not.toHaveBeenCalled(); tx.onabort!(); await result;
});
