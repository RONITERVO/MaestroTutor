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
  it('requires the visual reference colour when the colour was absent from the spoken request', () => {
    const before = state([]);
    const red = state([{ ...object('new'), color: { r: 1, g: 0, b: 0, a: 1 } }]);
    expect(assertCreatedParityBall(before, red, 'red', 0.5).id).toBe('new');
    expect(() => assertCreatedParityBall(before, state([object('new')]), 'red', 0.5)).toThrow();
    expect(() => assertCreatedParityBall(before, state([{ ...red.objects[0], scale: 0.8 }]), 'red', 0.5)).toThrow();
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
  it('accepts only float32 transform roundoff between saved and live positions', () => {
    const before = state([{ ...object('old'), position: { x: 0.69, y: 1.45, z: 0.8 } }]);
    const live = state([{ ...before.objects[0], position: { x: 0.69, y: 1.45, z: 0.799999952 } }]);
    expect(() => assertSameRoomObjects(before, live)).not.toThrow();
    expect(() => assertCreatedParityBall(before, state([...live.objects, object('new')]))).not.toThrow();
    expect(() => assertPaintedParityBall(before, state([{ ...live.objects[0], color: { r: 1, g: 0, b: 0, a: 1 } }]), 'old')).not.toThrow();
    for (const z of [0.801, NaN, Infinity]) {
      expect(() => assertSameRoomObjects(before, state([{ ...live.objects[0], position: { ...live.objects[0].position, z } }]))).toThrow();
    }
    // The allowance is a distance, not an independent allowance on each axis.
    expect(() => assertSameRoomObjects(state([object('old')]), state([{ ...object('old'), position: { x: 0.000008, y: 1.000008, z: 0.000008 } }]))).toThrow();
  });
  it('keeps non-position values exact while allowing transform roundoff', () => {
    const before = state([object('old')]);
    for (const patch of [{ scale: 0.50000001 }, { name: 'Renamed' }, { kind: 'Block' },
      { color: { ...before.objects[0].color, b: 0.99999999 } },
      { physics: { mode: 'fixed' as const, shape: 'automatic' as const, mass: 0.5 } }]) {
      expect(() => assertSameRoomObjects(before, state([{ ...object('old'), ...patch }]))).toThrow();
    }
  });

});
