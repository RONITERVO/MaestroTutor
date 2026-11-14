// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { expect, it } from 'vitest';
import { createHeadlessRequestQueue } from './jsonRpc';
it('allows Stop while a task runs and keeps later mutations ordered', async () => {
  const calls: string[] = []; let stop!: () => void;
  const waiting = new Promise<void>(resolve => { stop = resolve; });
  const request = createHeadlessRequestQueue(async method => {
    calls.push(method);
    if (method === 'journey.room') await waiting;
    if (method === 'room.stop') stop();
    return method;
  });
  const running = request('journey.room');
  const queued = request('chat.turn');
  await Promise.resolve();
  expect(calls).toEqual(['journey.room']);
  expect(await request('room.tasks')).toBe('room.tasks');
  expect(await request('room.stop')).toBe('room.stop');
  await Promise.all([running, queued]);
  expect(calls).toEqual(['journey.room', 'room.tasks', 'room.stop', 'chat.turn']);
});
it('a failed request does not poison the queue', async () => {
  const request = createHeadlessRequestQueue(async method => { if (method === 'bad') throw new Error('bad'); return method; });
  await expect(request('bad')).rejects.toThrow('bad'); expect(await request('next')).toBe('next');
});
