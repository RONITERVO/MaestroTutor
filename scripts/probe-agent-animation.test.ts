// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, it, expect } from 'vitest';
import { assertAvatarPlayback, assertMotionProgram, assertAvatarPreferences, assertAvatarPlacement } from './probe-agent-animation';
import type { RoomAgentState } from '../src/core-sdk/room/roomAgent';
const expected = { id: 'probe', motionId: 'a'.repeat(32), modelHash: 'b'.repeat(64), rigHash: 'c'.repeat(64), duration: 1, runId: 'run' };
function trace() {
  return { version: 1, id: 'probe', discarded: 0, frames: [0, .1, .5, .9, 1.1].map((time, index) => ({
    time: time + 1, frame: index + 1, ready: true, playing: index > 0 && index < 4, motionId: index > 0 && index < 4 ? expected.motionId : '',
    modelHash: expected.modelHash, rigHash: expected.rigHash, error: null as string | null, truncatedBones: false, runs: ['run'],
    joints: Array.from({ length: 20 }, (_, bone) => ({ path: 'skeleton/bone-' + bone, position: [0, 0, 0], rotation: [0, Math.sin(time / 2), 0, Math.cos(time / 2)] })),
  })) };
}
describe('native avatar playback proof', () => {
  it('requires observed skin motion, exact asset/rig/run identity, full duration and a stop', () => {
    expect(assertAvatarPlayback(trace(), expected)).toMatchObject({ samples: 3, displayedBones: 20, changedBones: 20, observedSeconds: 1, completedPlayback: true });
    for (const wrong of [{ motionId: 'other' }, { modelHash: 'other' }, { rigHash: 'other' }, { runId: 'other' }, { duration: 3 }]) expect(() => assertAvatarPlayback(trace(), { ...expected, ...wrong })).toThrow();
    const still = trace(); still.frames.forEach(frame => frame.joints.forEach(joint => { joint.rotation = [0, 0, 0, 1]; }));
    expect(() => assertAvatarPlayback(still, expected)).toThrow(/joint animation/);
    const short = trace(); short.frames.pop(); expect(() => assertAvatarPlayback(short, expected)).toThrow(/stop/);
    const unskinned = trace(); unskinned.frames.forEach(frame => { frame.joints.length = 14; }); expect(() => assertAvatarPlayback(unskinned, expected)).toThrow(/15/);
  });
  it('refuses stale, truncated, duplicate, nonfinite or errored observations', () => {
    expect(() => assertAvatarPlayback({ ...trace(), id: 'stale' }, expected)).toThrow();
    expect(() => assertAvatarPlayback({ ...trace(), discarded: 1 }, expected)).toThrow();
    const malformed = trace(); malformed.frames[2].joints[0].rotation[0] = NaN; expect(() => assertAvatarPlayback(malformed, expected)).toThrow(/joint/);
    const duplicate = trace(); duplicate.frames[2].joints[0].path = duplicate.frames[2].joints[1].path; expect(() => assertAvatarPlayback(duplicate, expected)).toThrow(/joint/);
    const failed = trace(); failed.frames[2].error = 'Lost model'; expect(() => assertAvatarPlayback(failed, expected)).toThrow(/sample/);
  });
  it('validates the exact saved source without depending on JSON key order', () => {
    const source = (motionId = expected.motionId, loop = false) => ({ id: 'd'.repeat(32), name: 'ParityMotion', repeat: false, interruption: 0,
      program: JSON.stringify({ version: 2, entry: 'main', resources: ['maestro'], functions: [{ name: 'main', returns: 'void', parameters: [], locals: [], body: [{ id: 'play', op: 'invoke', capability: 'animation.play', version: 1, arguments: { target: 'maestro', source: { motionId, kind: 'library' }, channel: 'wholeTarget', seconds: 0, loop }, bindings: {} }] }] }) });
    expect(() => assertMotionProgram(source(), expected.motionId, 1)).not.toThrow();
    expect(() => assertMotionProgram(source('e'.repeat(32)), expected.motionId, 1)).toThrow(/exact library/);
    expect(() => assertMotionProgram(source(expected.motionId, true), expected.motionId, 1)).toThrow(/exact library/);
  });
  it('requires supported, available avatar facts and detects changed actual position or saved rotation', () => {
    const fact = (capability: string, value: unknown, available = true) => ({ catalog: { operation: 'inspect', category: 'facts', capability, arguments: { target: 'maestro' }, available, value } }) as unknown as RoomAgentState;
    const live = fact('object.position', { x: 1, y: 1, z: 1 });
    expect(() => assertAvatarPlacement(live, live, 'object.position')).not.toThrow();
    expect(() => assertAvatarPlacement(live, fact('object.position', { x: 2, y: 1, z: 1 }), 'object.position')).toThrow(/changed/);
    const value = { target: 'maestro', position: { x: 1, y: 1, z: 1 }, rotation: { x: 0, y: 0, z: 0, w: 1 }, scale: 1 };
    const saved = fact('object.definition', value);
    expect(() => assertAvatarPlacement(saved, saved, 'object.definition')).not.toThrow();
    expect(() => assertAvatarPlacement(saved, fact('object.definition', { ...value, rotation: { x: 0, y: 1, z: 0, w: 0 } }), 'object.definition')).toThrow(/changed/);
    expect(() => assertAvatarPlacement(fact('object.placement', null, false), saved, 'object.definition')).toThrow(/available/);
  });
  it('rejects collateral activity or walk changes', () => {
    const state = { objects: [], walk: { source: 'library', motionId: expected.motionId, modelHash: expected.modelHash, clipIndex: -1 }, activityProfile: { modelHash: expected.modelHash, roles: [] } } as unknown as RoomAgentState;
    expect(() => assertAvatarPreferences(state, structuredClone(state))).not.toThrow();
    expect(() => assertAvatarPreferences(state, { ...state, walk: { ...state.walk!, motionId: 'other' } })).toThrow(/preferences/);
    expect(() => assertAvatarPreferences(state, { ...state, activityProfile: { ...state.activityProfile!, modelHash: 'other' } })).toThrow(/preferences/);
  });
});
