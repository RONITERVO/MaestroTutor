// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it, vi } from 'vitest';
import { RoomTaskHandoff, type RoomHandoff, type RoomTaskRecord, type RoomTaskPorts } from './roomTaskHandoff';
import { runRoomActionTask, type RoomAgentState } from './roomAgent';

const scene: RoomAgentState = { version: 1, session: 'native-one', revision: 1, sceneRevision: 1, ack: 0,
  ok: true, status: 'Ready', objects: [], created: [], canUndo: false, canRedo: false, physicsRunning: false };
const handoff: RoomHandoff = { version: 1, id: 'task-1', sourceAssistantId: 'a1', sourceUserId: 'u1', conversationId: 'pair1', nativeSession: scene.session, accessScope: 'byok',
  input: { model: 'test-model', prompt: '  Please make a robot.\nKeep its feet blue.  ', history: [{ role: 'user', text: 'Prior context' }],
    systemInstruction: 'Original tutor context', nativeLanguageCode: 'en', currentFileParts: [{ fileUri: 'test://drawing', mimeType: 'image/png' }] } };
const reply = { rawResponse: 'Ready.', parsed: { visibleText: 'Ready.', translations: [], hasSkippedNonLanguageContent: false } };
function deferred<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>(yes => { resolve = yes; }); return { resolve, promise }; }
function harness() {
  const saved = new Map<string, RoomTaskRecord>();
  const changed = vi.fn(), activity = vi.fn(), valid = vi.fn(async () => true);
  const execute = vi.fn(async () => ({ ...scene, ack: 1, status: 'Created robot' }));
  const ai = { models: { generateContent: vi.fn(), generateContentStream: vi.fn(async () => {
    const text = execute.mock.calls.length ? '{"commands":[]}' : '{"commands":[{"action":"create","reference":"r","name":"Robot","kind":"boxRobot"}]}';
    return (async function* () { yield { text, candidates: [{ content: { role: 'model', parts: [{ text }] } }] }; })();
  }) } } as any;
  const ports: RoomTaskPorts = {
    store: {
      get: vi.fn(async id => structuredClone(saved.get(id))),
      claim: vi.fn(async record => {
        if (saved.has(record.id)) return { claimed: false, record: structuredClone(saved.get(record.id)!) };
        saved.set(record.id, structuredClone(record)); return { claimed: true, record };
      }),
      save: vi.fn(async record => { saved.set(record.id, structuredClone(record)); }),
    },
    lease: () => ({ state: () => scene, valid: () => true, execute }),
    run: vi.fn((input, lease, control) => runRoomActionTask(input, { aiClient: ai }, lease, () => {}, control)),
    reply: vi.fn(async () => reply), changed, activity, now: () => 100,
  };
  const manager = new RoomTaskHandoff(ports);
  manager.capture(handoff, valid);
  return { manager, ports, saved, execute, ai, valid, changed, activity };
}
describe('shared app-owned room handoff', () => {
  it('preserves the exact input snapshot and persists pending intent before native dispatch', async () => {
    const h = harness();
    const source = structuredClone(handoff);
    h.manager.capture(source, h.valid);
    source.input.prompt = 'REPLACED'; (source.input.history[0] as any).text = 'REPLACED';
    h.execute.mockImplementation(async () => {
      expect(h.saved.get('task-1')?.operations[0]).toMatchObject({ commands: [{ action: 'create' }] });
      expect(h.saved.get('task-1')?.operations[0].receipt).toBeUndefined();
      return { ...scene, ack: 1, status: 'Created robot' };
    });
    const record = await h.manager.start('a1');
    expect(record.handoff.input).toEqual(handoff.input);
    expect(h.ports.run).toHaveBeenCalledWith(handoff.input, expect.anything(), expect.anything());
    expect(record.operations[0].receipt?.status).toBe('Created robot');
    expect(record.phase).toBe('completed'); expect(record.reply).toEqual(reply);
    const payload = h.ai.models.generateContentStream.mock.calls[0][0];
    expect(JSON.stringify(payload)).toContain('Original tutor context');
    expect(JSON.stringify(payload)).toContain('Prior context');
    expect(JSON.stringify(payload)).toContain('test://drawing');
    expect(h.activity.mock.calls).toEqual([[true], [false]]);
  });
  it('coalesces duplicate calls and never replays a saved task', async () => {
    const h = harness(); const first = h.manager.start('a1');
    expect(h.manager.start('a1')).toBe(first);
    await first; await h.manager.start('a1');
    expect(h.execute).toHaveBeenCalledOnce(); expect(h.ports.run).toHaveBeenCalledOnce();
  });
  it('does not start unknown or provider-authored task context', async () => {
    const h = harness(); await expect(h.manager.start('invented')).rejects.toThrow('original request');
    expect(h.ports.run).not.toHaveBeenCalled();
  });
  it('refuses concurrent independent mutation tasks', async () => {
    const h = harness(); h.manager.capture({ ...handoff, id: 'task-2', sourceAssistantId: 'a2' }, h.valid);
    const first = h.manager.start('a1');
    await expect(h.manager.start('a2')).rejects.toThrow('Another agent task'); await first;
    expect(h.execute).toHaveBeenCalledOnce();
  });
  it('does not dispatch if the operation journal cannot commit', async () => {
    const h = harness(); vi.mocked(h.ports.store.save).mockImplementation(async record => {
      if (record.operations.length) throw new Error('Disk full'); h.saved.set(record.id, structuredClone(record));
    });
    const result = await h.manager.start('a1');
    expect(h.execute).not.toHaveBeenCalled(); expect(result.phase).toBe('interrupted');
    expect(result.note).toContain('could not be saved');
  });
  it('keeps acknowledged effects if final narration fails', async () => {
    const h = harness(); vi.mocked(h.ports.reply).mockRejectedValue(new Error('Provider unavailable'));
    const result = await h.manager.start('a1');
    expect(result.phase).toBe('failed'); expect(result.operations[0].receipt?.ok).toBe(true);
    await h.manager.start('a1'); expect(h.execute).toHaveBeenCalledOnce();
  });
  it('does not apply a plan after access or conversation loss', async () => {
    const h = harness(); h.ai.models.generateContentStream.mockImplementation(async () => {
      h.valid.mockResolvedValue(false);
      return (async function* () { yield { text: '{"commands":[{"action":"workspace","visible":true}]}' }; })();
    });
    const result = await h.manager.start('a1'); expect(result.phase).toBe('stopped');
    expect(h.execute).not.toHaveBeenCalled(); expect(h.ports.reply).not.toHaveBeenCalled();
  });
  it('retains a native receipt that races an explicit Stop', async () => {
    const h = harness(); h.execute.mockImplementation(async () => { h.manager.stop('task-1'); return { ...scene, ack: 1, status: 'Created robot' }; });
    const result = await h.manager.start('a1');
    expect(result.phase).toBe('stopped'); expect(h.saved.get('task-1')?.operations[0].receipt?.ok).toBe(true);
    expect(h.ports.reply).not.toHaveBeenCalled();
  });
  it('stops pending execution and records uncertainty without blind retry', async () => {
    const h = harness(); const started = deferred<void>();
    h.execute.mockImplementation((_commands?: any, _revision?: any, _objects?: any, signal?: AbortSignal) => new Promise((_resolve, reject) => {
      signal?.addEventListener('abort', () => reject(new DOMException('Stopped', 'AbortError')), { once: true }); started.resolve();
    }));
    const done = h.manager.start('a1'); await started.promise; h.manager.stop('task-1');
    const result = await done; expect(result.phase).toBe('interrupted'); expect(result.operations[0].receipt).toBeUndefined();
    await h.manager.start('a1'); expect(h.execute).toHaveBeenCalledOnce();
  });
  it('does not reuse a handoff after the native session is replaced', async () => {
    const h = harness(); h.ports.lease = () => ({ state: () => ({ ...scene, session: 'another-room' }), valid: () => true, execute: h.execute });
    await expect(h.manager.start('a1')).rejects.toMatchObject({ name: 'AbortError' }); expect(h.ports.run).not.toHaveBeenCalled();
  });
});

it('retains receipts and reports failure rather than a successful empty narration', async () => {
  const h = harness();
  vi.mocked(h.ports.reply).mockResolvedValue({ rawResponse: 'Malformed language reply', parsed: { visibleText: '', translations: [], hasSkippedNonLanguageContent: true } });
  const result = await h.manager.start('a1');
  expect(result.phase).toBe('failed'); expect(result.operations[0].receipt?.ok).toBe(true);
  expect(result.reply).toBeUndefined(); expect(h.activity).toHaveBeenLastCalledWith(false);
});

it('reset revokes prepared handoffs, stops active work and waits for its final receipt', async () => {
  const h = harness(), started = deferred<void>(), acknowledged = deferred<any>();
  h.manager.capture({ ...handoff, id: 'prepared-task', sourceAssistantId: 'prepared' }, h.valid);
  h.execute.mockImplementation(async () => { started.resolve(); return acknowledged.promise; });
  const running = h.manager.start('a1'); await started.promise;
  let settled = false; const reset = h.manager.reset().then(() => { settled = true; });
  await Promise.resolve(); expect(settled).toBe(false);
  acknowledged.resolve({ ...scene, ack: 1, status: 'Created before Stop' });
  await reset; const result = await running;
  expect(result.phase).toBe('stopped'); expect(result.operations[0].receipt?.status).toBe('Created before Stop');
  await expect(h.manager.start('prepared')).rejects.toThrow('original request');
  expect(h.ports.reply).not.toHaveBeenCalled();
});

function followup(h: ReturnType<typeof harness>, action: 'stop' | 'revise' | 'continue', running: boolean, parent = 'task-1') {
  const next = { ...structuredClone(handoff), id: 'followup', sourceAssistantId: 'a2', sourceUserId: 'u2', input: { ...handoff.input, prompt: action === 'stop' ? 'Stop that task.' : 'Make it blue instead.' } };
  h.manager.capture(next, h.valid, [{ id: parent, phase: running ? 'working' : 'completed', requestPreview: handoff.input.prompt, replyPreview: '', running }]);
  return () => h.manager.start('a2', { action, taskId: parent });
}
describe('verified conversational task steering', () => {
  it.each(['stop', 'revise'] as const)('%s awaits the old acknowledgement and does not overlap dispatch', async action => {
    const h = harness(), started = deferred<void>(), acknowledgement = deferred<any>();
    h.execute.mockImplementationOnce(async () => { started.resolve(); return acknowledgement.promise; });
    const parent = h.manager.start('a1'); await started.promise;
    const run = followup(h, action, true), next = run();
    await vi.waitFor(() => expect(h.saved.get('followup')?.directive?.action).toBe(action));
    expect(h.execute).toHaveBeenCalledOnce();
    acknowledgement.resolve({ ...scene, ack: 1, status: 'Created before control' });
    const result = await next;
    expect((await parent).phase).toBe('stopped'); expect(result.phase).toBe('completed');
    expect(result.relatedTask).toMatchObject({ id: 'task-1', wasRunning: true, unconfirmed: false, operations: [{ receipt: { status: 'Created before control' } }] });
    expect(result.handoff.input.prompt).toBe(action === 'stop' ? 'Stop that task.' : 'Make it blue instead.');
    expect(h.ports.run).toHaveBeenCalledTimes(action === 'stop' ? 1 : 2);
    if (action === 'revise') expect(vi.mocked(h.ports.run).mock.calls[1][2].relatedTask).toMatchObject({ requests: [handoff.input.prompt] });
    await run(); expect(h.execute).toHaveBeenCalledOnce();
  });
  it('continues with new input and earlier receipts, never replays a saved batch', async () => {
    const h = harness(); await h.manager.start('a1');
    const run = followup(h, 'continue', false); const result = await run();
    expect(result.relatedTask?.operations).toHaveLength(1); expect(result.operations).toHaveLength(0);
    expect(h.execute).toHaveBeenCalledOnce(); expect(h.ports.run).toHaveBeenCalledTimes(2);
    expect(h.saved.get('task-1')?.phase).toBe('completed');
  });
  it('does not cancel a different task when a delayed stop target has finished', async () => {
    const h = harness(); await h.manager.start('a1'); const stopOld = followup(h, 'stop', true);
    const other = { ...handoff, id: 'other', sourceAssistantId: 'other-a' }; h.manager.capture(other, h.valid);
    const waiting = deferred<any>(), began = deferred<void>(); vi.mocked(h.ports.run).mockImplementationOnce(async () => { began.resolve(); return waiting.promise; });
    const otherDone = h.manager.start('other-a'); await began.promise;
    await expect(stopOld()).rejects.toThrow('Another agent task'); expect(h.manager.running('other')).toBe(true);
    waiting.resolve({ receipts: [], scene, budgetExhausted: false }); await otherDone;
  });
  it.each(['foreign', 'imported', 'deleted'] as const)('rejects a %s target without planner or native execution', async kind => {
    const h = harness(); await h.manager.start('a1'); const count = vi.mocked(h.ports.run).mock.calls.length;
    if (kind === 'deleted') h.saved.delete('task-1');
    else { const parent = h.saved.get('task-1')!; if (kind === 'foreign') parent.handoff.nativeSession = 'other'; else parent.readOnly = true; }
    const result = await followup(h, 'continue', false)();
    expect(result.phase).toBe('failed'); expect(result.note).toContain('new request'); expect(h.ports.run).toHaveBeenCalledTimes(count);
  });
  it('cannot steer an invented target or continue a still-running captured task', async () => {
    const h = harness();
    await expect(h.manager.start('a1', { action: 'stop', taskId: 'invented' })).rejects.toThrow('not available');
    await expect(followup(h, 'continue', true)()).rejects.toThrow('not available');
    expect(h.execute).not.toHaveBeenCalled();
  });
  it('does not stop work if control intent cannot be durably claimed', async () => {
    const h = harness(), started = deferred<void>(), acknowledgement = deferred<any>();
    h.execute.mockImplementation(async () => { started.resolve(); return acknowledgement.promise; });
    const parent = h.manager.start('a1'); await started.promise;
    vi.mocked(h.ports.store.claim).mockRejectedValueOnce(new Error('Disk full'));
    await expect(followup(h, 'stop', true)()).rejects.toThrow('Disk full');
    expect(h.manager.running('task-1')).toBe(true);
    acknowledgement.resolve({ ...scene, ack: 1 }); expect((await parent).phase).toBe('completed');
  });
});

it('keeps uncertainty across a follow-up and refuses new mutations until the room is reviewed', async () => {
  const h = harness(); await h.manager.start('a1');
  const parent = h.saved.get('task-1')!; parent.phase = 'interrupted'; delete parent.operations[0].receipt;
  h.ai.models.generateContentStream.mockImplementation(async () => (async function* () { yield { text: '{"commands":[{"action":"workspace","visible":true}]}' }; })());
  const result = await followup(h, 'continue', false)();
  expect(result.phase).toBe('limited'); expect(result.note).toContain('unconfirmed');
  expect(result.operations).toHaveLength(0); expect(result.relatedTask?.unconfirmed).toBe(true); expect(h.execute).toHaveBeenCalledOnce();
});

it('honors scope loss while a revision waits for the previous acknowledgement', async () => {
  const h = harness(), started = deferred<void>(), acknowledgement = deferred<any>();
  h.execute.mockImplementation(async () => { started.resolve(); return acknowledgement.promise; });
  const parent = h.manager.start('a1'); await started.promise;
  const next = followup(h, 'revise', true)();
  await vi.waitFor(() => expect(h.saved.get('followup')?.directive?.action).toBe('revise'));
  h.valid.mockResolvedValue(false); acknowledgement.resolve({ ...scene, ack: 1 });
  await parent; expect((await next).phase).toBe('stopped'); expect(h.ports.run).toHaveBeenCalledOnce();
});

it('can stop a long prior request without forwarding its entire request lineage', async () => {
  const h = harness(); await h.manager.start('a1'); h.saved.get('task-1')!.handoff.input.prompt = 'x'.repeat(64001);
  const result = await followup(h, 'stop', true)();
  expect(result.phase).toBe('completed'); expect(result.relatedTask?.requests).toEqual([]); expect(h.execute).toHaveBeenCalledOnce();
});

it('reports an established stop even when its final narration fails', async () => {
  const h = harness(); await h.manager.start('a1'); vi.mocked(h.ports.reply).mockRejectedValueOnce(new Error('Provider unavailable'));
  const result = await followup(h, 'stop', true)();
  expect(result.phase).toBe('failed'); expect(result.note).toContain('earlier task is no longer running');
  expect(result.relatedTask?.phase).toBe('completed'); expect(h.execute).toHaveBeenCalledOnce();
});
