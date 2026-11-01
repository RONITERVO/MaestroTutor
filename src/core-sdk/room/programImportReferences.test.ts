// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, it, expect, vi } from 'vitest';
import { moduleHash } from '../../../shared/programModuleIdentity';
import { copyVerifiedProgramModule, resolveProgramImportReferences } from './programImportReferences';
import { parseProgram } from './programs';
import { runRoomActionTask, parseRoomCommands, type RoomAgentState, type RoomCommand } from './roomAgent';
const module = { version: 1, name: 'Reusable', exports: ['main'], program: { version: 3, entry: 'main', resources: [], state: [], events: [], functions: [{ name: 'main', returns: 'void', parameters: [], locals: [], body: [] }] } };
const hash = moduleHash(module);
const draft = () => ({ version: 3, moduleVersion: 1, entry: 'main', resources: [], state: [], events: [],
  functions: [{ name: 'main', returns: 'void', parameters: [], locals: [], body: [{ id: 'usePin', op: 'call', module: 'kit', function: 'main', args: [] }] }],
  imports: [{ alias: 'kit', hash, module: null, signals: {} }] });
const observed = [{ hash, definition: module }];
describe('shared verified module authoring', () => {
  it('embeds only the exact inspected definition and copies without granting resources or wiring signals', () => {
    const original = JSON.stringify(draft()), source = resolveProgramImportReferences(original, observed), program = JSON.parse(source);
    expect(program.imports[0].module).toEqual(module); expect(program.resources).toEqual([]); expect(program.imports[0].signals).toEqual({});
    expect(parseProgram(source).error).toBeNull(); expect(JSON.stringify(draft())).toBe(original);
    const copy = copyVerifiedProgramModule(hash, module); copy.program.functions[0].name = 'Changed'; expect(module.program.functions[0].name).toBe('main');
  });
  it('refuses an unknown hash, corrupted definition and oversized authoring source', () => {
    expect(() => resolveProgramImportReferences(JSON.stringify(draft()), [])).toThrow(/Inspect this exact/);
    expect(() => resolveProgramImportReferences(JSON.stringify(draft()), [{ hash, definition: { ...module, name: 'Changed' } }])).toThrow(/do not match/);
    expect(() => resolveProgramImportReferences(' '.repeat(24001), observed)).toThrow(/too large/);
  });
  it('enforces the combined source bound after expansion and refuses duplicate JSON keys', () => {
    const large = { ...module, program: { ...module.program, padding: 'x'.repeat(23000) } }, key = moduleHash(large);
    const references = draft(); references.imports = [{ alias: 'one', hash: key, module: null, signals: {} }, { alias: 'two', hash: key, module: null, signals: {} }];
    expect(() => resolveProgramImportReferences(JSON.stringify(references), [{ hash: key, definition: large }])).toThrow(/too large/);
    expect(() => resolveProgramImportReferences('{"imports":[],"imports":[]}', observed)).toThrow();
  });
  it('never replaces a supplied embedded definition or erases native resource requirements', () => {
    const supplied = { ...draft(), imports: [{ alias: 'kit', hash, module: { ...module, name: 'Changed' }, signals: {} }] };
    const source = JSON.stringify(supplied); expect(resolveProgramImportReferences(source, observed)).toBe(source); expect(parseProgram(source).error).toMatch(/hash/);
    const restricted = { ...module, program: { ...module.program, resources: ['book'] } }, restrictedHash = moduleHash(restricted);
    const reference = draft(); reference.imports[0].hash = restrictedHash;
    expect(parseProgram(resolveProgramImportReferences(JSON.stringify(reference), [{ hash: restrictedHash, definition: restricted }])).error).toMatch(/resource/);
  });
  it('expands before durable intent, so the journal and native dispatch see exactly the validated full source', async () => {
    const search = { action: 'catalog', catalog: { operation: 'inspect', category: 'modules', capability: hash, version: 1 } };
    const edit = { action: 'rules', rule: { action: 'edit', revision: 1, edits: [{ kind: 'save', reference: 'saved', sequence: { id: '', name: 'Uses module', repeat: false, interruption: 0, program: JSON.stringify(draft()) } }] } };
    // The raw authoring shorthand is not accepted by the native command contract.
    expect(() => parseRoomCommands({ commands: [edit] })).toThrow(/Invalid behaviour/);
    const outputs = [JSON.stringify({ commands: [search] }), JSON.stringify({ commands: [edit] }), '{"commands":[]}'];
    const ai = { models: { generateContent: vi.fn(), generateContentStream: vi.fn(async () => (async function* () { yield { text: outputs.shift(), usageMetadata: { promptTokenCount: 1, candidatesTokenCount: 1 } }; })()) }, live: { connect: vi.fn(), music: { connect: vi.fn() } } };
    const state = { version: 1, session: 'a'.repeat(32), revision: 1, sceneRevision: 1, ack: 0, ok: true, status: 'Ready', objects: [], created: [], canUndo: false, canRedo: false, physicsRunning: false,
      capabilities: ['catalog.v1', 'moduleLibrary.v1', 'programModules.v1', 'behaviourPrograms.v3', 'eventPrograms.v1'] } as RoomAgentState;
    const execute = vi.fn(async (commands: RoomCommand[]) => commands[0].action === 'catalog'
      ? { ...state, catalog: { operation: 'inspect', category: 'modules', capability: hash, version: 1, definition: module, included: true } } as unknown as RoomAgentState : { ...state, ack: 2 });
    const beforeDispatch = vi.fn(); await runRoomActionTask({ model: 'fixture', prompt: 'Save using inspected module', history: [] }, { aiClient: ai }, { state: () => state, valid: () => true, execute }, () => {}, { beforeDispatch });
    const sent = execute.mock.calls[1][0][0], prepared = beforeDispatch.mock.calls[1][0][0];
    expect(sent).toEqual(prepared); expect(sent.rule!.edits![0].sequence!.program).toBe(resolveProgramImportReferences(edit.rule.edits[0].sequence.program, observed));
    expect(() => parseRoomCommands({ commands: [sent] })).not.toThrow();
  });
  it('does not treat a stale catalog view carried by another acknowledgement as an inspected pin', async () => {
    const edit = { action: 'rules', rule: { action: 'edit', revision: 1, edits: [{ kind: 'save', reference: 'saved', sequence: { id: '', name: 'Uses module', repeat: false, interruption: 0, program: JSON.stringify(draft()) } }] } };
    const outputs = ['{"commands":[{"action":"workspace","visible":true}]}', JSON.stringify({ commands: [edit] }), '{"commands":[]}'];
    const ai = { models: { generateContent: vi.fn(), generateContentStream: vi.fn(async () => (async function* () { yield { text: outputs.shift() }; })()) }, live: { connect: vi.fn(), music: { connect: vi.fn() } } };
    const state = { version: 1, session: 'a'.repeat(32), revision: 1, sceneRevision: 1, ack: 0, ok: true, status: 'Ready', objects: [], created: [], canUndo: false, canRedo: false, physicsRunning: false,
      catalog: { operation: 'inspect', category: 'modules', capability: hash, version: 1, definition: module, included: true } } as unknown as RoomAgentState;
    const execute = vi.fn(async () => state), beforeDispatch = vi.fn();
    await expect(runRoomActionTask({ model: 'fixture', prompt: 'Save using module', history: [] }, { aiClient: ai }, { state: () => state, valid: () => true, execute }, () => {}, { beforeDispatch })).rejects.toThrow(/Inspect this exact/);
    expect(execute).toHaveBeenCalledOnce(); expect(beforeDispatch).toHaveBeenCalledOnce();
  });

});
