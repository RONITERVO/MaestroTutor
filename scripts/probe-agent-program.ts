// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import type { HeadlessClient } from '../src/headless/client';
import { runHeadlessRoomTurn } from '../src/headless/roomJourney';
import type { RoomAgentState, RoomCommand } from '../src/core-sdk/room/roomAgent';
import { parseProgram, type BehaviourProgram, type ProgramNode } from '../src/core-sdk/room/programs';
import type { RuleSequence, RuleRun } from '../src/core-sdk/room/rules';
import { assertSameRoomObjects } from './agent-provider-contract';

export const PARITY_SIGNAL_REQUEST = `Ask the room agent to save an editable program called ParitySignal for the existing ParityBall. Do not start it yet, change any objects, create buttons or add automatic start bindings. When I start it later, handle exactly two text signals named user.parityColour, then finish. Wait for each signal without polling the model. Put the colour decision in a separate function: the exact text red paints only that ball red; any other text paints it blue. Keep a program state counter named handledCount starting at zero; after each paint increment it, then pause one second before waiting again or finishing. Preserve the ball's size, position and identity. Save the program only now.`;

function nodes(body: ProgramNode[]): ProgramNode[] {
  return body.flatMap(node => [node, ...('body' in node ? nodes(node.body) : []),
    ...(node.op === 'if' ? [...nodes(node.then), ...nodes(node.else)] : []),
    ...(node.op === 'switch' ? [...node.cases.flatMap(arm => nodes(arm.body)), ...nodes(node.default)] : [])]);
}
export function assertParitySignalSource(sequence: RuleSequence, target: string): BehaviourProgram {
  const parsed = parseProgram(sequence.program);
  if (!parsed.program || parsed.error) throw new Error('Generated program is not editable valid source: ' + parsed.error);
  const program = parsed.program, blocks = program.functions.flatMap(fn => nodes(fn.body));
  if (sequence.name !== 'ParitySignal' || sequence.repeat || program.version !== 3
    || !program.resources.includes(target) || program.functions.length < 2
    || !program.state?.some(value => value.name === 'handledCount' && value.initial === 0)
    || !program.events?.some(event => event.name === 'user.parityColour' && event.type === 'text')
    || !blocks.some(node => node.op === 'call') || !blocks.some(node => node.op === 'if' || node.op === 'switch')
    || !blocks.some(node => node.op === 'awaitEvent' && node.event === 'user.parityColour')
    || !blocks.some(node => node.op === 'setState' && node.variable === 'handledCount')
    || !blocks.some(node => node.op === 'sleep' || node.op === 'invoke' && node.capability === 'time.wait')) {
    throw new Error('Saved source lacks the requested event, function, branch, counter or delay.');
  }
  return program;
}
export function isProgramDelay(run: RuleRun | undefined, program: BehaviourProgram): boolean {
  if (!run || run.waitEvent) return false;
  if (run.waiting && (run.waitSeconds || 0) > 0) return true;
  // time.wait is a native invocation; sleep is an interpreter timer. Both are
  // legitimate pauses and expose different live scheduler fields.
  return run.status === 'Running' && program.functions.flatMap(fn => nodes(fn.body))
    .some(node => node.id === run.nodeId && node.op === 'invoke' && node.capability === 'time.wait');
}
export function assertProgramBall(before: RoomAgentState, after: RoomAgentState, target: string, red: boolean) {
  const ball = after.objects.find(object => object.id === target);
  if (!ball || ball.color.a !== 1 || (red ? ball.color.r < .8 || ball.color.g > .2 || ball.color.b > .2
    : ball.color.b < .8 || ball.color.r > .2 || ball.color.g > .2)) throw new Error('Program produced the wrong branch colour.');
  assertSameRoomObjects({ ...before, objects: before.objects.map(object => object.id === target ? { ...object, color: ball.color } : object) }, after);
}

/** Actual provider authors/starts/stops; native user-event handlers drive the saved program. */
export async function runAgentProgramProof(input: { client: HeadlessClient; before: RoomAgentState; target: string;
  execute: (commands: RoomCommand[]) => Promise<RoomAgentState>; directory: string }) {
  const { client, before, target, execute, directory } = input;
  const evidence: Record<string, unknown> = { phase: 'saving', before, samples: [] };
  const samples = evidence.samples as Array<{ label: string; observedAt: number; state: RoomAgentState }>;
  const save = () => writeFile(join(directory, 'provider-program.json'), JSON.stringify(evidence, null, 2));
  const record = async (label: string, state: RoomAgentState) => { samples.push({ label, observedAt: Date.now(), state: structuredClone(state) }); await save(); return state; };
  evidence.savedJourney = await runHeadlessRoomTurn(client, { text: PARITY_SIGNAL_REQUEST }); await save();
  const saved = await record('saved', await execute([{ action: 'rules', rule: { action: 'inspect' } }]));
  assertSameRoomObjects(before, saved);
  const added = saved.rules!.sequences.filter(sequence => !before.rules!.sequences.some(old => old.id === sequence.id));
  if (added.length !== 1 || added[0].name !== 'ParitySignal' || saved.rules!.running.length
    || saved.rules!.buttons.length !== before.rules!.buttons.length || saved.rules!.bindingCount !== before.rules!.bindingCount
    || (saved.rules!.outcomes?.length || 0) !== (before.rules!.outcomes?.length || 0)) throw new Error('Save-only request executed work or changed unrelated authoring.');
  const sequenceId = added[0].id;
  const inspected = await record('source', await execute([{ action: 'rules', rule: { action: 'inspect', target: sequenceId } }]));
  const source = inspected.rules!.selected!; const program = assertParitySignalSource(source, target); evidence.source = source; await save();
  const inspect = () => execute([{ action: 'rules', rule: { action: 'inspect', target: sequenceId } }]);
  const running = (state: RoomAgentState) => state.rules?.running.find(run => run.sequenceId === sequenceId);
  const count = (state: RoomAgentState) => Number(running(state)?.state?.find(value => value.name === 'handledCount')?.value);
  const wait = async (label: string, accepts: (state: RoomAgentState) => boolean) => {
    const deadline = Date.now() + 12_000; let last: RoomAgentState;
    do { last = await inspect(); if (accepts(last)) return record(label, last); await new Promise(resolve => setTimeout(resolve, 100)); } while (Date.now() < deadline);
    await record('failed-' + label, last!); throw new Error('Native program did not reach ' + label);
  };
  evidence.phase = 'starting';
  evidence.startJourney = await runHeadlessRoomTurn(client, { text: 'Ask the room agent to start the saved ParitySignal program now. Do not emit any signals, edit the source or add buttons.' }); await save();
  const armed = await wait('armed', state => running(state)?.waitEvent === 'user.parityColour' && count(state) === 0);
  assertSameRoomObjects(before, armed); const firstRun = running(armed)!.id;
  const signal = async (value: string) => { const current = await inspect(); return execute([{ action: 'rules', rule: { action: 'signal', revision: current.rules!.revision, eventName: 'user.parityColour', value } }]); };
  for (const [index, value] of ['red', 'anythingElse'].entries()) {
    await record('signal-' + value, await signal(value));
    const paused = await wait('delay-' + value, state => count(state) === index + 1 && isProgramDelay(running(state), program));
    assertProgramBall(before, paused, target, index === 0);
    if (index === 0) {
      const waiting = await wait('second-signal-wait', state => running(state)?.waitEvent === 'user.parityColour' && count(state) === 1);
      assertProgramBall(before, waiting, target, true);
    }
  }
  const completed = await wait('completed', state => !running(state) && state.rules?.outcomes?.some(outcome => outcome.id === firstRun && outcome.phase === 'completed') === true);
  assertProgramBall(before, completed, target, false);
  if (completed.rules!.selected?.program !== source.program) throw new Error('Running modified the saved source.');
  // Native run-now is also the manual control's handler, without another provider call.
  await execute([{ action: 'rules', rule: { action: 'play', revision: completed.rules!.revision, target: sequenceId } }]);
  const restarted = await wait('restarted', state => running(state)?.waitEvent === 'user.parityColour' && count(state) === 0);
  const secondRun = running(restarted)!.id;
  if (secondRun === firstRun) throw new Error('Restart reused the previous execution identity.');
  evidence.phase = 'stopping'; await save();
  evidence.stopJourney = await runHeadlessRoomTurn(client, { text: 'Ask the room agent to stop the running ParitySignal program. Keep the saved program and the ball exactly as they are; do not undo anything or start another program.' }); await save();
  const stopped = await wait('stopped', state => !running(state) && state.rules?.outcomes?.some(outcome => outcome.id === secondRun && outcome.phase === 'cancelled') === true);
  assertSameRoomObjects(completed, stopped);
  await signal('red'); await new Promise(resolve => setTimeout(resolve, 400));
  const afterStop = await record('signal-after-stop', await inspect());
  assertSameRoomObjects(stopped, afterStop);
  if (running(afterStop) || afterStop.rules!.selected?.program !== source.program) throw new Error('Stopped program restarted or lost its editable source.');
  evidence.phase = 'passed'; evidence.semantics = { saveOnly: true, editableSource: true, functions: true, branches: true,
    nativeEvents: true, state: true, timedWaits: true, completed: true, freshRunState: true, agentStop: true, noPostStopEffects: true };
  await save(); return evidence;
}
