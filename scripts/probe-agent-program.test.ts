// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import {paritySignalFixture} from './native-probe-program-fixtures';
const source=()=>paritySignalFixture(target);
import { assertParitySignalSource, assertProgramBall, isProgramDelay } from './probe-agent-program';
import type { BehaviourProgram, ProgramNode } from '../src/core-sdk/room/programs';
import type { RoomAgentState } from '../src/core-sdk/room/roomAgent';
const target = 'a'.repeat(32);
const sequence = (program: BehaviourProgram) => ({ id: 'b'.repeat(32), name: 'ParitySignal', interruption: 0, repeat: false, program: JSON.stringify(program) });
const ball = { id: target, name: 'ParityBall', kind: 'Ball', position: { x: 0, y: 1, z: 0 }, scale: .5, color: { r: 0, g: 0, b: 1, a: 1 }, animated: false, physics:{mode:'solid' as const,shape:'sphere' as const,mass:2} };
const scene = (objects: RoomAgentState['objects']): RoomAgentState => ({ version: 1, session: 'native', revision: 1, sceneRevision: 1, ack: 0, ok: true,
  status: 'Ready', created: [], objects, canUndo: false, canRedo: false, physicsRunning: false });
describe('event-program provider proof contracts', () => {
  it('requires an editable event/function/branch/state/timer source', () => {
    expect(assertParitySignalSource(sequence(source()), target).version).toBe(3);
    const missingTimer = source(); (missingTimer.functions[0].body[0] as Extract<ProgramNode, { op: 'repeat' }>).body.pop();
    expect(() => assertParitySignalSource(sequence(missingTimer), target)).toThrow(/delay/);
    expect(() => assertParitySignalSource({ ...sequence(source()), repeat: true }, target)).toThrow();
    expect(() => assertParitySignalSource({ ...sequence(source()), program: '{}' }, target)).toThrow(/valid source/);
    expect(() => assertParitySignalSource(sequence(source()), 'c'.repeat(32))).toThrow();
  });
  it('recognizes both native Wait and interpreter Sleep while refusing an event wait', () => {
    const program = source();
    const run = { id: 'run', sequenceId: 'sequence', preparing: false, nodeId: 'delay', status: 'Running' };
    expect(isProgramDelay({ ...run, waiting: true, waitSeconds: .5 }, program)).toBe(true);
    expect(isProgramDelay({ ...run, waiting: true, waitEvent: 'user.parityColour', waitSeconds: 2 }, program)).toBe(false);
    expect(isProgramDelay(run, program)).toBe(false);
    (program.functions[0].body[0] as Extract<ProgramNode, { op: 'repeat' }>).body[3] = { id: 'delay', op: 'invoke', capability: 'time.wait', version: 1, arguments: { seconds: 1 }, bindings: {} };
    expect(isProgramDelay(run, program)).toBe(true);
  });
  it('checks the actual branch colour and preserves identity, size and other objects', () => {
    const before = scene([ball, { ...ball, id: 'c'.repeat(32), name: 'Other' }]);
    const red = scene([{ ...ball, color: { r: 1, g: 0, b: 0, a: 1 } }, before.objects[1]]);
    expect(() => assertProgramBall(before, red, target, true)).not.toThrow();
    for(const i of [0,1]){const changed=structuredClone(red);changed.objects[i].physics!.mass=3;expect(()=>assertProgramBall(before,changed,target,true)).toThrow(/unrelated/);}
    expect(() => assertProgramBall(before, red, target, false)).toThrow(/colour/);
    expect(() => assertProgramBall(before, scene([{ ...red.objects[0], scale: 1 }, before.objects[1]]), target, true)).toThrow();
    expect(() => assertProgramBall(before, scene([red.objects[0]]), target, true)).toThrow();
    expect(() => assertProgramBall(before, scene([{ ...red.objects[0], id: 'd'.repeat(32) }, before.objects[1]]), target, true)).toThrow();
  });
});
