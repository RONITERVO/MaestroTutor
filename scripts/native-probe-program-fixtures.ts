// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Explicit offline fixtures. These sources are never provider output.
import {moduleHash} from '../shared/programModuleIdentity';
import type {BehaviourProgram,ProgramModule,ProgramNode} from '../shared/programSyntax';
const paint = (target:string,id: string, red: boolean): ProgramNode => ({ id, op: 'invoke', capability: 'object.color.set', version: 1,
  arguments: { target, red: red ? 1 : 0, green: 0, blue: red ? 0 : 1 }, bindings: {} });
export function paritySignalFixture(target:string): BehaviourProgram {
  return { version: 3, entry: 'main', resources: [target], state: [{ name: 'handledCount', initial: 0 }],
    events: [{ name: 'user.parityColour', type: 'text' }], functions: [
      { name: 'main', returns: 'void', parameters: [], locals: [{ name: 'received', initial: false }, { name: 'colour', initial: '' }], body: [
        { id: 'twice', op: 'repeat', count: { value: 2 }, body: [
          { id: 'wait', op: 'awaitEvent', event: 'user.parityColour', source: '', timeout: { value: 0 }, received: 'received', value: 'colour' },
          { id: 'react', op: 'call', function: 'colourBall', args: [{ var: 'colour' }] },
          { id: 'count', op: 'setState', variable: 'handledCount', value: { op: 'add', args: [{ state: 'handledCount' }, { value: 1 }] } },
          { id: 'delay', op: 'sleep', seconds: { value: 1 } },
        ] },
      ] },
      { name: 'colourBall', returns: 'void', parameters: [{ name: 'colour', type: 'text' }], locals: [], body: [
        { id: 'choose', op: 'if', test: { op: 'eq', args: [{ var: 'colour' }, { value: 'red' }] }, then: [paint(target,'red', true)], else: [paint(target,'blue', false)] },
      ] },
    ] };
}
export function parityCompositeFixture(module:ProgramModule): BehaviourProgram {
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
