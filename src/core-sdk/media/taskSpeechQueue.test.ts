// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TaskSpeechQueue, type TaskSpeechLease } from './taskSpeechQueue';
const deferred = <T>() => { let resolve!: (value: T) => void; const promise = new Promise<T>(done => { resolve = done; }); return { promise, resolve }; };
const task = (id = 'one') => ({ id, conversationId: 'pair', valid: vi.fn(async () => true) });
function setup() {
  const lease = { current: vi.fn(() => true), ready: vi.fn(() => true), release: vi.fn() };
  const ports = { available: vi.fn(() => true), ready: vi.fn(() => true), claim: vi.fn(async (): Promise<TaskSpeechLease | null> => lease),
    speak: vi.fn(), playing: vi.fn(() => true), stop: vi.fn() };
  return { ports, lease, queue: new TaskSpeechQueue(ports) };
}
beforeEach(() => vi.useFakeTimers()); afterEach(() => vi.useRealTimers());
describe('completed task speech ownership', () => {
  it('waits for a quiet boundary, speaks once and restores listening only after playback drains', async () => {
    const h = setup(), result = task(); h.queue.enqueue(result); h.queue.enqueue(result);
    h.ports.ready.mockReturnValue(false); await h.queue.tick(); expect(h.ports.claim).not.toHaveBeenCalled();
    h.ports.ready.mockReturnValue(true); await h.queue.tick(); expect(h.ports.speak).toHaveBeenCalledExactlyOnceWith(result);
    await h.queue.tick(); expect(h.lease.release).not.toHaveBeenCalled();
    h.ports.playing.mockReturnValue(false); await h.queue.tick(); expect(h.lease.release).toHaveBeenCalledExactlyOnceWith(true);
    h.queue.enqueue(result); await h.queue.tick(); expect(h.ports.speak).toHaveBeenCalledOnce(); expect(h.queue.hasWork()).toBe(false);
  });
  it('serializes independent results through one audio owner', async () => {
    const h = setup(); h.queue.enqueue(task('one')); h.queue.enqueue(task('two')); await h.queue.tick(); await h.queue.tick();
    expect(h.ports.speak).toHaveBeenCalledOnce(); h.ports.playing.mockReturnValue(false); await h.queue.tick(); await h.queue.tick();
    expect(h.ports.speak.mock.calls.map(([item]) => item.id)).toEqual(['one', 'two']);
  });
  it.each(['before', 'after-claim'])('drops an unauthorized result %s without speaking', async when => {
    const h = setup(), result = task(); h.queue.enqueue(result);
    result.valid.mockResolvedValueOnce(when !== 'before').mockResolvedValue(false);
    await h.queue.tick(); expect(h.ports.speak).not.toHaveBeenCalled(); expect(h.queue.hasWork()).toBe(false);
    if (when === 'after-claim') expect(h.lease.release).toHaveBeenCalledWith(true);
  });
  it('rechecks audio admission after an asynchronous claim', async () => {
    const h = setup(); h.queue.enqueue(task()); h.lease.ready.mockReturnValue(false);
    await h.queue.tick(); expect(h.ports.speak).not.toHaveBeenCalled(); expect(h.lease.release).toHaveBeenCalledWith(true);
    h.lease.ready.mockReturnValue(true); await h.queue.tick(); expect(h.ports.speak).toHaveBeenCalledOnce();
  });
  it.each([true, false])('cancels a pending claim without late speech; resume=%s', async resume => {
    const h = setup(), pending = deferred<TaskSpeechLease | null>(); h.ports.claim.mockReturnValue(pending.promise);
    h.queue.enqueue(task()); const running = h.queue.tick(); await Promise.resolve();
    h.queue.cancel(resume); pending.resolve(h.lease); await running;
    expect(h.ports.speak).not.toHaveBeenCalled(); expect(h.lease.release).toHaveBeenCalledWith(resume);
  });
  it('a cancelled credential read cannot stall a later result or consume its queue slot', async () => {
    const h = setup(), old = task('old'), pending = deferred<boolean>(); old.valid.mockReturnValue(pending.promise);
    h.queue.enqueue(old); const running = h.queue.tick(); h.queue.cancel(); h.queue.enqueue(task('new')); await h.queue.tick();
    pending.resolve(true); await running;
    expect(h.ports.speak.mock.calls.map(([item]) => item.id)).toEqual(['new']);
  });
  it('stops active speech on source loss and leaves the next result intact', async () => {
    const h = setup(); h.queue.enqueue(task('one')); await h.queue.tick(); h.queue.enqueue(task('two'));
    h.ports.available.mockReturnValue(false); await h.queue.tick(); expect(h.ports.stop).toHaveBeenCalledOnce();
    h.ports.available.mockReturnValue(true); await h.queue.tick(); expect(h.ports.speak).toHaveBeenCalledTimes(2);
  });
  it('manual Stop clears pending results and never repeats a potentially spoken result', async () => {
    const h = setup(); const result = task(); h.queue.enqueue(result); await h.queue.tick(); h.queue.enqueue(task('two')); h.queue.cancel();
    expect(h.ports.stop).toHaveBeenCalledOnce(); expect(h.lease.release).toHaveBeenCalledWith(true);
    h.queue.enqueue(result); await h.queue.tick(); expect(h.ports.speak).toHaveBeenCalledOnce();
  });
  it('does not retry playback failures', async () => {
    const h = setup(); h.ports.speak.mockImplementation(() => { throw new Error('Audio unavailable'); });
    h.queue.enqueue(task()); await h.queue.tick(); await h.queue.tick(); expect(h.ports.speak).toHaveBeenCalledOnce();
    expect(h.lease.release).toHaveBeenCalledWith(true); expect(h.queue.hasWork()).toBe(false);
  });
});

it('waits for output cancellation to settle before restoring the microphone', async () => {
  const h = setup(), stopped = deferred<void>(); h.ports.stop.mockReturnValue(stopped.promise as any);
  h.queue.enqueue(task()); await h.queue.tick(); const cancellation = h.queue.cancel();
  expect(cancellation).toBeInstanceOf(Promise);
  expect(h.lease.release).not.toHaveBeenCalled(); stopped.resolve(); await cancellation;
  expect(h.lease.release).toHaveBeenCalledWith(true);
});
