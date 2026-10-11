// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { isDeepStrictEqual } from 'node:util';
import { factReply } from './native-probe-contract';
import { validVector, validRotation } from '../src/core-sdk/room/recipe';
import type { HeadlessClient } from '../src/headless/client';
import { runHeadlessRoomTurn } from '../src/headless/roomJourney';
import type { RoomAgentState, RoomCommand } from '../src/core-sdk/room/roomAgent';
import { parseProgram, type ProgramNode } from '../src/core-sdk/room/programs';
import type { RuleSequence } from '../src/core-sdk/room/rules';
import { assertSameRoomObjects } from './agent-provider-contract';

export const PARITY_MOTION_REQUEST = `Ask the room agent to save an editable program named ParityMotion for Maestro. Find the compatible downloaded library animation named Agree_Gesture and use that exact saved motion ID. The program must play that full clip once, in place, with no loop, then finish. Do not play it yet. Do not move or change objects, change the avatar, create buttons, set automatic bindings or change tutor activity or walking preferences. Save only; I will ask to start it later.`;
function nodes(body: ProgramNode[]): ProgramNode[] {
  return body.flatMap(node => [node, ...('body' in node ? nodes(node.body) : []),
    ...(node.op === 'if' ? [...nodes(node.then), ...nodes(node.else)] : []),
    ...(node.op === 'switch' ? [...node.cases.flatMap(arm => nodes(arm.body)), ...nodes(node.default)] : [])]);
}
export function assertMotionProgram(source: RuleSequence, motionId: string, duration: number) {
  const { program, error } = parseProgram(source.program);
  if (!program || error || source.name !== 'ParityMotion' || source.repeat || !program.resources.includes('maestro')) throw new Error('Invalid editable motion program.');
  const invokes = program.functions.flatMap(fn => nodes(fn.body)).filter(node => node.op === 'invoke');
  const action = invokes[0]; const args = action?.arguments;
  if (invokes.length !== 1 || action.capability !== 'animation.play' || action.version !== 1 || !args
    || args.target !== 'maestro' || args.channel !== 'wholeTarget' || args.loop !== false
    || !(args.seconds === 0 || args.seconds === duration) || !(args.movement === undefined || args.movement === 'inPlace')
    || Object.keys(action.bindings).length || !isDeepStrictEqual(args.source, { kind: 'library', motionId }) || args.prop) {
    throw new Error('Program does not preserve the exact library motion and full in-place playback.');
  }
  return program;
}
interface Bone { path: string; position: number[]; rotation: number[] }
interface Frame { time: number; frame: number; ready: boolean; playing: boolean; motionId: string; modelHash: string; rigHash: string;
  error: string | null; truncatedBones: boolean; joints: Bone[]; runs: string[] }
interface Trace { version: number; id: string; discarded: number; frames: Frame[] }
const record = (value: unknown): value is Record<string, unknown> => value !== null && typeof value === 'object' && !Array.isArray(value);
function readTrace(value: unknown, id: string): Trace {
  if (!record(value) || value.version !== 1 || value.id !== id || value.discarded !== 0 || !Array.isArray(value.frames) || value.frames.length > 512) throw new Error('Invalid or truncated native animation evidence.');
  let previous = -1;
  for (const frame of value.frames) {
    if (!record(frame) || typeof frame.time !== 'number' || !Number.isFinite(frame.time) || frame.time <= previous
      || typeof frame.frame !== 'number' || !Number.isInteger(frame.frame) || frame.frame < 0
      || typeof frame.ready !== 'boolean' || typeof frame.playing !== 'boolean'
      || typeof frame.motionId !== 'string' || typeof frame.modelHash !== 'string' || typeof frame.rigHash !== 'string'
      || frame.error !== null || frame.truncatedBones !== false || !Array.isArray(frame.joints) || frame.joints.length > 512
      || !Array.isArray(frame.runs) || frame.runs.some(run => typeof run !== 'string')) throw new Error('Invalid native animation sample.');
    previous = frame.time;
    const paths = new Set<string>();
    for (const joint of frame.joints) {
      if (!record(joint) || typeof joint.path !== 'string' || !joint.path || paths.has(joint.path)
        || !Array.isArray(joint.position) || joint.position.length !== 3 || !Array.isArray(joint.rotation) || joint.rotation.length !== 4
        || [...joint.position, ...joint.rotation].some(n => typeof n !== 'number' || !Number.isFinite(n))
        || Math.abs(Math.hypot(...joint.rotation) - 1) > .001) throw new Error('Invalid native skin joint.');
      paths.add(joint.path);
    }
  }
  return value as unknown as Trace;
}
export function assertAvatarPlayback(value: unknown, expected: { id: string; motionId: string; modelHash: string; rigHash: string; duration: number; runId: string }) {
  const trace = readTrace(value, expected.id), active = trace.frames.filter(frame => frame.playing);
  if (active.length < 3 || new Set(active.map(frame => frame.frame)).size < 3
    || active.some(frame => !frame.ready || frame.motionId !== expected.motionId || frame.modelHash !== expected.modelHash
      || frame.rigHash !== expected.rigHash || !frame.runs.includes(expected.runId) || frame.joints.length < 15)) throw new Error('No matching native motion/run/rig with at least 15 displayed bones.');
  const first = active[0], last = active.at(-1)!;
  const ended = trace.frames.find(frame => frame.time > last.time && !frame.playing && frame.motionId === '');
  if (!ended || ended.modelHash !== expected.modelHash || ended.time - first.time < expected.duration - .4
    || ended.time - first.time > expected.duration + 1 || trace.frames.some(frame => !frame.playing && frame.time > first.time && frame.time < last.time)) throw new Error('Motion did not play one contiguous full-duration run and stop.');
  const base = new Map(first.joints.map(joint => [joint.path, joint.rotation])); const changed = new Set<string>();
  for (const frame of active) {
    if (frame.joints.length !== base.size || frame.joints.some(joint => !base.has(joint.path))) throw new Error('Displayed skeleton changed during playback.');
    for (const joint of frame.joints) {
      const q = base.get(joint.path)!;
      if (Math.abs(q.reduce((sum, value, index) => sum + value * joint.rotation[index], 0)) < Math.cos(Math.PI / 360)) changed.add(joint.path);
    }
  }
  if (changed.size < 5) throw new Error('Native skin did not show the requested joint animation.');
  return { samples: active.length, displayedBones: base.size, changedBones: changed.size, observedSeconds: ended.time - first.time,
    exactMotion: true, exactModel: true, compatibleRig: true, actualJointChanges: true, completedPlayback: true };
}

export function assertAvatarPlacement(before: RoomAgentState, after: RoomAgentState, capability: 'object.definition' | 'object.position') {
  const read = (state: RoomAgentState) => {
    const fact = factReply(state), value = fact.value;
    if (fact.capability !== capability || !fact.available || fact.arguments?.target !== 'maestro' || !record(value)) throw new Error('Expected available Maestro placement evidence.');
    if (capability === 'object.position') {
      if (!validVector(value)) throw new Error('Invalid live Maestro position.');
      return { x: value.x, y: value.y, z: value.z };
    }
    if (value.target !== 'maestro' || !validVector(value.position) || !validRotation(value.rotation)
      || typeof value.scale !== 'number' || !Number.isFinite(value.scale) || value.scale <= 0) throw new Error('Invalid saved Maestro placement.');
    return { position: value.position, rotation: value.rotation, scale: value.scale };
  };
  if (!isDeepStrictEqual(read(before), read(after))) throw new Error('In-place animation changed Maestro placement.');
}

export function assertAvatarPreferences(before: RoomAgentState, after: RoomAgentState) {
  const preferences = (state: RoomAgentState) => ({
    model: state.activityProfile?.modelHash,
    roles: state.activityProfile?.roles,
    walk: state.walk && { source: state.walk.source, motionId: state.walk.motionId, modelHash: state.walk.modelHash, clipIndex: state.walk.clipIndex },
    movement: state.objects.find(object => object.id === 'maestro')?.movement,
  });
  if (!before.walk || !before.activityProfile || !isDeepStrictEqual(preferences(before), preferences(after))) throw new Error('Animation request changed avatar, walking or tutor-activity preferences.');
}

export async function runAgentAnimationProof(input: { client: HeadlessClient; before: RoomAgentState; execute: (commands: RoomCommand[]) => Promise<RoomAgentState>; directory: string; probeId: string }) {
  const { client, before, execute, directory, probeId } = input;
  const evidence: Record<string, unknown> = { phase: 'saving', before };
  const save = () => writeFile(join(directory, 'provider-animation.json'), JSON.stringify(evidence, null, 2));
  const placement = () => execute([{ action: 'catalog', catalog: { operation: 'inspect', category: 'facts', capability: 'object.definition', version: 1, arguments: { target: 'maestro' } } }]);
  const position = () => execute([{ action: 'catalog', catalog: { operation: 'inspect', category: 'facts', capability: 'object.position', version: 2, arguments: { target: 'maestro' } } }]);
  const beforePlacement = await placement(), beforePosition = await position(); evidence.beforePlacement = beforePlacement; evidence.beforePosition = beforePosition;
  assertAvatarPlacement(beforePlacement, beforePlacement, 'object.definition'); assertAvatarPlacement(beforePosition, beforePosition, 'object.position');
  const saveJourney = await runHeadlessRoomTurn(client, { text: PARITY_MOTION_REQUEST }); evidence.saveJourney = saveJourney; await save();
  const saved = await execute([{ action: 'rules', rule: { action: 'inspect' } }]); evidence.saved = saved; await save();
  assertSameRoomObjects(before, saved); assertAvatarPreferences(before, saved);
  const added = saved.rules!.sequences.filter(sequence => !before.rules!.sequences.some(old => old.id === sequence.id));
  if (added.length !== 1 || saved.rules!.running.length || saved.rules!.buttons.length !== before.rules!.buttons.length
    || saved.rules!.bindingCount !== before.rules!.bindingCount || (saved.rules!.outcomes?.length || 0) !== (before.rules!.outcomes?.length || 0)) throw new Error('Animation save-only request executed work or changed authoring.');
  const inspected = await execute([{ action: 'rules', rule: { action: 'inspect', target: added[0].id } }]);
  const source = inspected.rules!.selected!; evidence.source = source;
  const motions = await execute([{ action: 'motions', target: 'maestro', motionQuery: { query: 'Agree_Gesture', offset: 0, includeShort: false, favouritesOnly: false, archivedOnly: false } }]);
  evidence.motions = motions; await save();
  const entry = motions.motions?.entries.find(entry => entry.name === 'Agree_Gesture' && entry.downloaded);
  if (!motions.motions?.ready || !entry) throw new Error('Requested compatible included motion is unavailable.');
  const bundled = JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Avatars/IncludedMotions.json', 'utf8'));
  const expected = bundled.catalogue.entries.find((item: { id: string }) => item.id === entry.id);
  if (!expected || expected.name !== entry.name || expected.duration !== entry.duration || bundled.avatarHash !== motions.motions.modelHash) throw new Error('Native library does not match the included avatar/motion identity.');
  assertMotionProgram(source, entry.id, entry.duration);
  const journal = await client.roomAgent!.store.get(saveJourney.task!.id);
  const saveIndex = journal?.operations.findIndex(operation => operation.commands.some(command => command.action === 'rules' && command.rule?.edits?.some(edit => edit.kind === 'save' && edit.sequence?.name === 'ParityMotion'))) ?? -1;
  const discovered = saveIndex > 0 && journal!.operations.slice(0, saveIndex).some(operation => operation.commands.some(command => command.action === 'motions' && command.target === 'maestro')
    && operation.receipt?.motions?.ready && operation.receipt.motions.entries.some(found => found.id === entry.id && found.downloaded));
  if (!discovered) throw new Error('Agent did not discover the exact compatible motion before saving it.');
  evidence.discoveredBeforeSave = true;

  const baseline = readTrace(JSON.parse(await readFile(join(directory, 'avatar-playback.json'), 'utf8')), probeId);
  if (baseline.frames.some(frame => frame.playing)) throw new Error('Save-only request played an animation.');
  evidence.phase = 'starting'; await save();
  evidence.startJourney = await runHeadlessRoomTurn(client, { text: 'Ask the room agent to start the saved ParityMotion program now, exactly as saved. Do not edit its source, start anything else, or change any preferences.' }); await save();
  const deadline = Date.now() + 40_000; let finalState: RoomAgentState;
  do {
    finalState = await execute([{ action: 'rules', rule: { action: 'inspect', target: source.id } }]);
    const done = finalState.rules?.outcomes?.filter(outcome => outcome.sequenceId === source.id);
    if (done?.length && !finalState.rules!.running.some(run => run.sequenceId === source.id)) break;
    await new Promise(resolve => setTimeout(resolve, 100));
  } while (Date.now() < deadline);
  evidence.finalState = finalState!; await save();
  const outcomes = finalState!.rules?.outcomes?.filter(outcome => outcome.sequenceId === source.id);
  if (outcomes?.length !== 1 || outcomes[0].phase !== 'completed' || finalState!.rules!.running.length
    || finalState!.rules!.selected?.program !== source.program) throw new Error('Exact animation program did not complete once with its source intact.');
  assertSameRoomObjects(before, finalState!); assertAvatarPreferences(before, finalState!);
  const finalPlacement = await placement(); evidence.finalPlacement = finalPlacement;
  const finalPosition = await position(); evidence.finalPosition = finalPosition;
  assertAvatarPlacement(beforePlacement, finalPlacement, 'object.definition'); assertAvatarPlacement(beforePosition, finalPosition, 'object.position');
  // The Editor observes skin transforms independently while the provider is busy.
  // This evidence adapter neither plays nor samples animation clips itself.
  await new Promise(resolve => setTimeout(resolve, 250));
  const trace = JSON.parse(await readFile(join(directory, 'avatar-playback.json'), 'utf8'));
  evidence.playback = assertAvatarPlayback(trace, { id: probeId, motionId: entry.id, modelHash: bundled.avatarHash,
    rigHash: expected.rigHash, duration: entry.duration, runId: outcomes[0].id });
  evidence.phase = 'passed'; await save(); return evidence;
}
