// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import type { RoomAgentState } from '../src/core-sdk/room/roomAgent';
import { assertCreatedParityBall, assertPaintedParityBall, assertSameRoomObjects } from './agent-provider-contract';
const object = (id: string) => ({ id, kind: 'Ball', name: 'ParityBall', position: { x: 0, y: 1, z: 0 },
  scale: 0.5, color: { r: 0, g: 0.3, b: 1, a: 1 }, animated: false });
const state = (objects: RoomAgentState['objects']): RoomAgentState => ({ version: 1, revision: 1,
  sceneRevision: 1, ack: 1, session: 'native', ok: true, status: 'Ready', objects, created: [],
  physicsRunning: false, canUndo: false, canRedo: false });
describe('conversational provider semantic proof', () => {
  it('requires the requested name, shape, size and colour, not merely a successful receipt', () => {
    const before = state([object('old')]), after = state([...before.objects, object('new')]);
    expect(assertCreatedParityBall(before, after).id).toBe('new');
    for (const patch of [{ name: 'Other' }, { kind: 'Block' }, { scale: 2 }, { scale: NaN },
      { color: { r: 1, g: 0, b: 0, a: 1 } }]) {
      const wrong = state([...before.objects, { ...object('new'), ...patch }]);
      expect(() => assertCreatedParityBall(before, wrong)).toThrow();
    }
  });
  it('rejects replacement, extra objects and unrelated edits', () => {
    const before = state([object('old')]), after = state([...before.objects, object('new')]);
    expect(() => assertCreatedParityBall(before, state([object('new')]))).toThrow();
    expect(() => assertCreatedParityBall(before, state([...after.objects, object('extra')]))).toThrow();
    after.objects[0] = { ...object('old'), scale: 2 };
    expect(() => assertCreatedParityBall(before, after)).toThrow(/unrelated/);
  });
  it('requires editing the same object and checks Undo/Redo state by value', () => {
    const before = state([object('ball')]);
    const after = state([{ ...object('ball'), color: { r: 1, g: 0, b: 0, a: 1 } }]);
    expect(assertPaintedParityBall(before, after, 'ball').color.r).toBe(1);
    expect(() => assertPaintedParityBall(before, state([{ ...after.objects[0], id: 'replacement' }]), 'ball')).toThrow();
    expect(() => assertPaintedParityBall(before, state([{ ...after.objects[0], scale: 1 }]), 'ball')).toThrow();
    expect(() => assertSameRoomObjects(before, after)).toThrow();
    expect(() => assertSameRoomObjects(before, structuredClone(before))).not.toThrow();
  });
});
