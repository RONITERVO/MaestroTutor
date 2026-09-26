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
