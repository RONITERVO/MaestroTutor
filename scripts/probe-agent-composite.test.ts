// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, it, expect } from 'vitest';
import spinner from '../unity/MaestroQuest/Assets/Maestro/Resources/Programs/Modules/Spinner.json';
import { moduleHash } from '../shared/programModuleIdentity';
import type { BehaviourProgram, ProgramModule, ProgramNode } from '../shared/programSyntax';
import type { RoomAgentState } from '../src/core-sdk/room/roomAgent';
import { assertCompositeSource, assertNativeValues, assertNativeRecipe, assertParallelWaiting } from './probe-agent-composite';
const module = spinner as unknown as ProgramModule;
function source(): BehaviourProgram {
  return { version: 3, moduleVersion: 1, dataVersion: 1, parallelVersion: 1, entry: 'main', resources: [], state: [],
    events: [{ name: 'user.spawnLeft', type: 'text' }, { name: 'user.spawnRight', type: 'text' }],
    imports: [{ alias: 'kit', hash: moduleHash(module), module, signals: {} }],
    functions: [{ name: 'main', returns: 'void', parameters: [], locals: [], body: [{ id: 'fork', op: 'parallel', branches: [{ function: 'left', args: [] }, { function: 'right', args: [] }] }] },
      ...(['left', 'right'] as const).map((side, index) => ({ name: side, returns: 'void' as const, parameters: [],
        locals: [{ name: 'received', initial: false }, { name: 'payload', initial: '' }], body: [
          { id: side + 'Wait', op: 'awaitEvent', event: index ? 'user.spawnRight' : 'user.spawnLeft', source: '', timeout: { value: 0 }, received: 'received', value: 'payload' },
          { id: side + 'Create', op: 'call', module: 'kit', function: 'create', args: [
            { value: { x: 2 + index, y: 1, z: .3 }, type: { record: { x: 'number', y: 'number', z: 'number' } } },
            { value: { x: 0, y: 0, z: 0, w: 1 }, type: { record: { x: 'number', y: 'number', z: 'number', w: 'number' } } }, { value: 1 }] },
        ] as ProgramNode[] }))] };
}
const sequence = (program: BehaviourProgram) => ({ id: 'a'.repeat(32), name: 'ParitySpinners', repeat: false, interruption: 0, program: JSON.stringify(program) });
describe('composite provider evidence gates', () => {
  it('requires an exact immutable native pin and rejects a substituted or altered module', () => {
    expect(() => assertCompositeSource(sequence(source()), module)).not.toThrow();
    const separate = source(); separate.imports!.push({ ...separate.imports![0], alias: 'second' });
    const right = separate.functions[2].body[1]; if (right.op === 'call') right.module = 'second';
    expect(() => assertCompositeSource(sequence(separate), module)).not.toThrow();
    const changed = structuredClone(source()); changed.imports![0].module.name = 'Different';
    expect(() => assertCompositeSource(sequence(changed), module)).toThrow();
    changed.imports![0].hash = moduleHash(changed.imports![0].module);
    expect(() => assertCompositeSource(sequence(changed), module)).toThrow(/exact pin/);
  });
  it('refuses serial authoring and programs that self-trigger the user events', () => {
    const serial = source(); serial.functions[0].body = [{ id: 'serial', op: 'call', function: 'left', args: [] }];
    expect(() => assertCompositeSource(sequence(serial), module)).toThrow(/parallel/);
    const automatic = source(); automatic.functions[0].body.unshift({ id: 'auto', op: 'emitEvent', event: 'user.spawnLeft', value: { value: 'go' } });
    expect(() => assertCompositeSource(sequence(automatic), module)).toThrow(/parallel/);
  });
  it('normalizes only root-parent null/empty serialization and still rejects missing or changed hierarchy', () => {
    const recipe = { version: 1, parts: [{ id: 'Body', parent: null, shape: 'box', position: { x: 0, y: 0, z: 0 }, size: { x: .1, y: .1, z: .1 }, rotation: { x: 0, y: 0, z: 0, w: 1 }, color: { r: 1, g: 1, b: 1, a: 1 } }], tracks: [], duration: 2, playing: false, loop: false };
    expect(() => assertNativeRecipe(recipe, { ...recipe, parts: [{ ...recipe.parts[0], parent: '' }] })).not.toThrow();
    expect(() => assertNativeRecipe(recipe, { ...recipe, parts: [] })).toThrow();
    expect(() => assertNativeRecipe(recipe, { ...recipe, parts: [{ ...recipe.parts[0], parent: 'Unknown' }] })).toThrow();
  });
  it('requires observed child waits belonging to the same unfinished native parent', () => {
    const state = { rules: { running: [{ id: 'parent', sequenceId: 'program' }, { id: 'left', sequenceId: 'program', parentRunId: 'parent', waitEvent: 'user.spawnLeft' },
      { id: 'right', sequenceId: 'program', parentRunId: 'parent', waitEvent: 'user.spawnRight' }], outcomes: [] } } as unknown as RoomAgentState;
    expect(assertParallelWaiting(state, 'program', ['user.spawnLeft', 'user.spawnRight'])).toBe('parent');
    const wrong = structuredClone(state); wrong.rules!.running[2].parentRunId = 'another';
    expect(() => assertParallelWaiting(wrong, 'program', ['user.spawnLeft', 'user.spawnRight'])).toThrow(/branches/);
    state.rules!.outcomes = [{ id: 'parent', sequenceId: 'program', phase: 'completed', status: 'Finished' }];
    expect(() => assertParallelWaiting(state, 'program', ['user.spawnLeft'])).toThrow(/unfinished/);
  });
  it('accepts native float roundoff while rejecting absent geometry, wrong connections and nonfinite values', () => {
    expect(() => assertNativeValues({ position: { x: .3 }, parts: [{ id: 'Arm' }] }, { position: { x: .300000012 }, parts: [{ id: 'Arm', segments: 0 }] })).not.toThrow();
    for (const actual of [{ position: {} }, { position: { x: NaN } }, { position: { x: .31 } }]) expect(() => assertNativeValues({ position: { x: .3 } }, actual)).toThrow();
    expect(() => assertNativeValues({ connected: 'ownMount' }, { connected: 'otherMount' })).toThrow();
    expect(() => assertNativeValues({ parts: [{ id: 'Arm' }] }, { parts: [] })).toThrow();
  });
});
