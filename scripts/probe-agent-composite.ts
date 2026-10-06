// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { isDeepStrictEqual } from 'node:util';
import type { HeadlessClient } from '../src/headless/client';
import { runHeadlessRoomTurn } from '../src/headless/roomJourney';
import type { RoomAgentState, RoomCommand } from '../src/core-sdk/room/roomAgent';
import { parseProgram, type BehaviourProgram, type ProgramNode } from '../src/core-sdk/room/programs';
import type { RuleSequence } from '../src/core-sdk/room/rules';
import type { ProgramModule } from '../shared/programSyntax';
import { moduleHash } from '../shared/programModuleIdentity';
import { parseRecipe } from '../src/core-sdk/room/recipe';
import { factReply, placementReply } from './native-probe-contract';
import { assertSameRoomObjects } from './agent-provider-contract';
import { assertAvatarPreferences } from './probe-agent-animation';

export const PARITY_COMPOSITE_REQUEST = `Ask the room agent to save an editable program named ParitySpinners, using the included reusable module named Passive spinner. Discover and pin its exact library definition; do not rewrite its internals. Save only now. When I start it later, run two branches in parallel: one waits indefinitely for the text event user.spawnLeft, then calls the module's create export once at room position x=2, y=1, z=0.3; the other independently waits indefinitely for user.spawnRight, then creates one copy at x=3, y=1, z=0.3. Use identity rotation and scale 1 for both. Each branch finishes after its own construction, and the parent finishes only when both are done. Do not emit these events yourself. Create nothing yet, add no buttons or automatic bindings, and leave existing objects, avatar preferences and physics mode alone.`;
const record = (value: unknown): value is Record<string, unknown> => value !== null && typeof value === 'object' && !Array.isArray(value);
const nodes = (body: ProgramNode[]): ProgramNode[] => body.flatMap(node => [node, ...('body' in node ? nodes(node.body) : []),
  ...(node.op === 'if' ? [...nodes(node.then), ...nodes(node.else)] : []),
  ...(node.op === 'switch' ? [...node.cases.flatMap(arm => nodes(arm.body)), ...nodes(node.default)] : [])]);
/** Compare authored values with native float readback, requiring every expected field. */
export function assertNativeValues(expected: unknown, actual: unknown): void {
  if (typeof expected === 'number') {
    if (typeof actual !== 'number' || !Number.isFinite(actual) || Math.abs(expected - actual) > .00001) throw new Error('Native numeric value differs.');
  } else if (Array.isArray(expected)) {
    if (!Array.isArray(actual) || actual.length !== expected.length) throw new Error('Native array differs.');
    expected.forEach((value, index) => assertNativeValues(value, actual[index]));
  } else if (record(expected)) {
    if (!record(actual)) throw new Error('Native record is unavailable.');
    for (const [key, value] of Object.entries(expected)) {
      if (!Object.prototype.hasOwnProperty.call(actual, key)) throw new Error('Missing native field: ' + key);
      assertNativeValues(value, actual[key]);
    }
  } else if (expected !== actual) throw new Error('Native value differs.');
}
export function assertNativeRecipe(expected: unknown, actual: unknown) {
  const normalize = (value: unknown) => {
    const recipe = parseRecipe(value);
    if (!recipe) throw new Error('Invalid editable native recipe.');
    // JsonUtility serializes a null root parent as an empty string. Both denote
    // the same root; nonempty parent identities must still match exactly.
    return { ...recipe, parts: recipe.parts.map(part => ({ ...part, parent: part.parent || '' })) };
  };
  assertNativeValues(normalize(expected), normalize(actual));
}
export function assertCompositeSource(sequence: RuleSequence, module: ProgramModule) {
  const parsed = parseProgram(sequence.program);
  if (!parsed.program || parsed.error) throw new Error('Invalid editable composite program: ' + parsed.error);
  const source = JSON.parse(sequence.program) as BehaviourProgram;
  const imports = source.imports || [], blocks = source.functions.flatMap(fn => nodes(fn.body));
  if (sequence.name !== 'ParitySpinners' || sequence.repeat || source.version !== 3 || source.parallelVersion !== 1
    || source.moduleVersion !== 1 || source.dataVersion !== 1 || source.resources.length || imports.length < 1 || imports.length > 2
    || imports.some(item => item.hash !== moduleHash(module) || !isDeepStrictEqual(item.module, module) || Object.keys(item.signals).length) || !blocks.some(node => node.op === 'parallel' && node.branches.length === 2)
    || blocks.filter(node => node.op === 'call' && imports.some(item => item.alias === node.module) && node.function === 'create').length !== 2
    || ['user.spawnLeft', 'user.spawnRight'].some(event => !source.events?.some(item => item.name === event && item.type === 'text')
      || !blocks.some(node => node.op === 'awaitEvent' && node.event === event))
    || blocks.some(node => node.op === 'invoke' || node.op === 'emitEvent')) throw new Error('Composite source lost its exact pin, independent events or two parallel module calls.');
  return source;
}
export function assertParallelWaiting(state: RoomAgentState, sequenceId: string, events: string[]) {
  const running = state.rules?.running.filter(run => run.sequenceId === sequenceId) || [];
  const roots = running.filter(run => !run.parentRunId), children = running.filter(run => run.parentRunId);
  if (roots.length !== 1 || events.some(event => !children.some(run => run.parentRunId === roots[0].id && run.waitEvent === event))
    || state.rules?.outcomes?.some(outcome => outcome.sequenceId === sequenceId)) throw new Error('Expected independent native branches under one unfinished parent.');
  return roots[0].id;
}

export async function runAgentCompositeProof(input: { client: HeadlessClient; before: RoomAgentState;
  execute: (commands: RoomCommand[]) => Promise<RoomAgentState>; directory: string }) {
  const { client, before, execute, directory } = input;
  const module = JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Programs/Modules/Spinner.json', 'utf8')) as ProgramModule;
  const hash = moduleHash(module), evidence: Record<string, unknown> = { phase: 'saving', before, hash, samples: [] };
  const samples = evidence.samples as { label: string; state: RoomAgentState }[];
  const save = () => writeFile(join(directory, 'provider-composite.json'), JSON.stringify(evidence, null, 2));
  const capture = async (label: string, state: RoomAgentState) => { samples.push({ label, state: structuredClone(state) }); await save(); return state; };
  const unchanged = (state: RoomAgentState) => {
    assertSameRoomObjects(before, { ...state, objects: state.objects.filter(object => before.objects.some(old => old.id === object.id)) });
    assertAvatarPreferences(before, state);
    if (state.physicsRunning !== before.physicsRunning) throw new Error('Composite task changed physics mode.');
  };
  const saveJourney = await runHeadlessRoomTurn(client, { text: PARITY_COMPOSITE_REQUEST }); evidence.saveJourney = saveJourney; await save();
  const saved = await capture('saved', await execute([{ action: 'rules', rule: { action: 'inspect' } }]));
  assertSameRoomObjects(before, saved); unchanged(saved);
  const added = saved.rules!.sequences.filter(sequence => !before.rules!.sequences.some(old => old.id === sequence.id));
  if (added.length !== 1 || saved.rules!.running.length || saved.rules!.buttons.length !== before.rules!.buttons.length
    || saved.rules!.bindingCount !== before.rules!.bindingCount || (saved.rules!.outcomes?.length || 0) !== (before.rules!.outcomes?.length || 0)) throw new Error('Save-only composite request started effects or changed unrelated authoring.');
  const sequenceId = added[0].id;
  const inspect = () => execute([{ action: 'rules', rule: { action: 'inspect', target: sequenceId } }]);
  const inspected = await capture('source', await inspect()), source = inspected.rules!.selected!;
  evidence.source = source; assertCompositeSource(source, module);
  const journal = await client.roomAgent!.store.get(saveJourney.task!.id);
  const saveIndex = journal?.operations.findIndex(op => op.commands.some(c => c.action === 'rules' && c.rule?.edits?.some(edit => edit.kind === 'save'))) ?? -1;
  const discovered = saveIndex > 0 && journal!.operations.slice(0, saveIndex).some(op => {
    const catalog = op.receipt?.catalog;
    return op.commands.some(c => c.catalog?.operation === 'inspect' && c.catalog.category === 'modules' && c.catalog.capability === hash)
      && catalog?.operation === 'inspect' && catalog.category === 'modules' && catalog.included && isDeepStrictEqual(catalog.definition, module);
  });
  if (!discovered) throw new Error('Agent did not inspect the exact included module before saving its pin.');
  const wait = async (label: string, accepts: (state: RoomAgentState) => boolean) => {
    const deadline = Date.now() + 15_000; let last: RoomAgentState;
    do { last = await inspect(); if (accepts(last)) return capture(label, last); await new Promise(resolve => setTimeout(resolve, 100)); } while (Date.now() < deadline);
    await capture('failed-' + label, last!); throw new Error('Composite program did not reach ' + label);
  };
  evidence.phase = 'starting'; await save();
  evidence.startJourney = await runHeadlessRoomTurn(client, { text: 'Ask the room agent to start saved ParitySpinners now exactly as saved. Do not emit either event, edit the source or start anything else.' }); await save();
  const armed = await wait('both-branches-waiting', state => state.rules?.running.filter(run => run.sequenceId === sequenceId && run.parentRunId && ['user.spawnLeft', 'user.spawnRight'].includes(run.waitEvent || '')).length === 2);
  const runId = assertParallelWaiting(armed, sequenceId, ['user.spawnLeft', 'user.spawnRight']);
  assertSameRoomObjects(before, armed); unchanged(armed);
  const signal = async (eventName: string) => { const current = await inspect(); return execute([{ action: 'rules', rule: { action: 'signal', revision: current.rules!.revision, eventName, value: 'go' } }]); };
  await capture('left-signal', await signal('user.spawnLeft'));
  const left = await wait('left-built-right-waiting', state => state.objects.length === before.objects.length + 2 && state.rules?.running.some(run => run.parentRunId === runId && run.waitEvent === 'user.spawnRight') === true);
  assertParallelWaiting(left, sequenceId, ['user.spawnRight']); unchanged(left);
  await capture('right-signal', await signal('user.spawnRight'));
  const completed = await wait('completed', state => !state.rules?.running.some(run => run.sequenceId === sequenceId)
    && state.rules?.outcomes?.some(outcome => outcome.id === runId && outcome.phase === 'completed') === true);
  if (completed.objects.length !== before.objects.length + 4 || completed.rules!.selected?.program !== source.program) throw new Error('Composite count or saved source changed.');
  unchanged(completed);
  // Native persisted definitions, editable geometry, physics and internal connections.
  const create = module.program.functions.find(fn => fn.name === 'create')!.body.find(node => node.op === 'invoke')!;
  if (create.op !== 'invoke') throw new Error('Included constructor missing.');
  const blueprint = create.arguments.blueprint as { pieces: { slot: string; name: string; position: { x: number; y: number; z: number }; rotation: unknown; scale: number; source: { recipe: unknown; physics: unknown; collision: { shapes: unknown[] } } }[]; connections: { definition: Record<string, unknown> }[] };
  const fact = async (capability: string, target: string, extras: Record<string, unknown> = {}) => {
    const arguments_ = { target, ...extras };
    const state = await execute([{ action: 'catalog', catalog: { operation: 'inspect', category: 'facts', capability, version: 1, arguments: arguments_ } }]);
    await capture(capability, state); const reply = factReply(state);
    if (!reply.available || reply.capability !== capability || !isDeepStrictEqual(reply.arguments, arguments_) || !record(reply.value)) throw new Error('Missing matching native construction fact.');
    return { state, value: reply.value };
  };
  const additions = completed.objects.filter(object => !before.objects.some(old => old.id === object.id));
  const verifyConstruction = async (state: RoomAgentState, x: number) => {
    const members = additions.filter(object => Math.abs(object.position.x - x) < .00001);
    if (members.length !== 2) throw new Error('Wrong independent construction placement.');
    for (const piece of blueprint.pieces) {
      const object = state.objects.find(object => members.some(member => member.id === object.id) && object.name === piece.name);
      if (!object) throw new Error('Construction member lost identity or name.');
      assertNativeValues(piece.source.physics, object.physics);
      const placement = await fact('object.placement', object.id); assertNativeValues({ target: object.id, scale: piece.scale, rotation: piece.rotation,
        position: { x: x + piece.position.x, y: 1 + piece.position.y, z: .3 + piece.position.z } }, placementReply(placement.state));
      const recipe = await capture('editable-recipe', await execute([{ action: 'inspect', target: object.id }]));
      if (recipe.inspection?.id !== object.id) throw new Error('Recipe readback selected another object.');
      assertNativeRecipe(piece.source.recipe, recipe.inspection.recipe);
      for (const [index, shape] of piece.source.collision.shapes.entries()) {
        const collision = await fact('object.collision.shape', object.id, { revision: recipe.inspection.objectRevision, index });
        if (collision.value.total !== piece.source.collision.shapes.length) throw new Error('Construction collision count differs.');
        assertNativeValues(shape, collision.value.shape);
      }
      const connection = (await fact('object.connection', object.id)).value;
      if (piece.slot === 'mount') { if (connection.configured !== false) throw new Error('Mount acquired an unexpected connection.'); }
      else {
        const mount = members.find(member => member.name === 'Spinner mount')!, definition = blueprint.connections[0].definition;
        assertNativeValues({ configured: true, connected: mount.id, definition: { enabled: true, kind: 'hinge', breakForce: 0, breakTorque: 0 } }, connection);
        assertNativeValues({ limits: definition.limits, drive: definition.drive }, (await fact('object.connection.hinge', object.id)).value);
        for (const side of ['owner', 'connected']) assertNativeValues({ configured: true, frame: definition[side + 'Frame'] }, (await fact('object.connection.frame', object.id, { side })).value);
      }
    }
    return members.map(member => member.id);
  };
  const leftIds = await verifyConstruction(completed, 2), rightIds = await verifyConstruction(completed, 3);
  const undoRight = await capture('undo-right-construction', await execute([{ action: 'undo' }]));
  assertSameRoomObjects(left, undoRight);
  const undoLeft = await capture('undo-left-construction', await execute([{ action: 'undo' }]));
  assertSameRoomObjects(before, undoLeft);
  const redoLeft = await capture('redo-left-construction', await execute([{ action: 'redo' }])); assertSameRoomObjects(left, redoLeft);
  const redoRight = await capture('redo-right-construction', await execute([{ action: 'redo' }])); assertSameRoomObjects(completed, redoRight);
  if (!isDeepStrictEqual(leftIds, await verifyConstruction(redoRight, 2)) || !isDeepStrictEqual(rightIds, await verifyConstruction(redoRight, 3))) throw new Error('Redo lost construction member identities.');
  await signal('user.spawnLeft'); await signal('user.spawnRight');
  await new Promise(resolve => setTimeout(resolve, 300)); const final = await capture('signals-after-completion', await inspect());
  assertSameRoomObjects(completed, final); unchanged(final);
  if (final.rules?.running.length || final.rules?.selected?.program !== source.program || final.rules?.outcomes?.filter(outcome => outcome.sequenceId === sequenceId).length !== 1) throw new Error('Finished composite program replayed or changed its source.');
  evidence.phase = 'passed'; evidence.semantics = { exactIncludedPin: true, discoveredBeforeSave: true, saveOnly: true, editableSource: true,
    parallelNativeBranches: true, independentEvents: true, completeParent: true, editableGeometry: true, exactPlacement: true,
    nativePhysicsSettings: true, internalHinges: true, singleConstructionUndo: true, redoIdentitiesAndConnections: true, unrelatedObjectsPreserved: true, noPostCompletionEffects: true };
  await save(); return evidence;
}
