// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest';
const ports = vi.hoisted(() => ({ lease: vi.fn(), key: vi.fn(), managed: vi.fn(), source: vi.fn(), history: vi.fn(), usage: vi.fn(),
  claim: vi.fn(), save: vi.fn(), get: vi.fn() }));
vi.mock('../../../platform/quest/roomAgentBridge', () => ({ currentRoomAgentLease: ports.lease }));
vi.mock('../../../core/security/apiKeyStorage', () => ({ loadApiKey: ports.key }));
vi.mock('../../../core/security/managedAccessSessionStorage', () => ({ loadManagedAccessSession: ports.managed, hasManagedSession: (session: any) => Boolean(session?.user?.id && session?.firebaseIdToken) }));
vi.mock('../../../api/gemini/browserClientSource', () => ({ browserClientSource: ports.source }));
vi.mock('../../../shared/utils/costTracker', () => ({ trackGeminiUsage: ports.usage }));
vi.mock('./chatHistory', () => ({ safeSaveChatHistoryDB: ports.history }));
vi.mock('./roomTaskStore', () => ({ roomTaskStore: { claim: ports.claim, save: ports.save, get: ports.get } }));
import { useMaestroStore, initialSettings } from '../../../store';
import { prepareRoomAgentHandoff, roomAgentRequestForVerification, startRoomAgentTask, roomAgentTasks } from './roomAgentTasks';
import type { TutorTextTurnInput } from '../../../core-sdk/chat/tutorTextTurn';
import type { RoomTaskRecord } from '../../../core-sdk/room/roomTaskHandoff';
import { selectIsAgentWorking, selectIsSending } from '../../../store/slices/uiSlice';
const proposal = 'I will ask the agent.\n```maestro-tool {"tool":"agent"}```';
const input: TutorTextTurnInput = { model: 'test-model', prompt: '  Make a blue robot.\nPlease keep it small.  ', history: [{ role: 'user', text: 'Earlier context' }],
  nativeLanguageCode: 'en', systemInstruction: 'Tutor fixture', currentFileParts: [{ fileUri: 'test://current-drawing', mimeType: 'image/png' }] };
let records: Map<string, RoomTaskRecord>;
let execute: ReturnType<typeof vi.fn>;
let requests: any[];
let sequence = 0;
function setup() {
  const id = `a${++sequence}`, userId = `u${sequence}`;
  useMaestroStore.setState({ settings: { ...initialSettings, selectedLanguagePairId: 'pair' }, isLoadingHistory: false, activityTokens: new Set(),
    messages: [{ id: userId, role: 'user', text: input.prompt, timestamp: 1 }, { id, role: 'assistant', llmRawResponse: proposal, text: 'I will ask the agent.', timestamp: 2 }] });
  return { id, source: { sourceUserId: userId, sourceAssistantId: id, conversationId: 'pair' } };
}
beforeEach(() => {
  vi.clearAllMocks(); records = new Map(); requests = [];
  ports.key.mockResolvedValue('synthetic-key-not-a-credential'); ports.managed.mockResolvedValue(null); ports.history.mockResolvedValue(true);
  const scene = { version: 1, session: 'native', revision: 1, sceneRevision: 1, ack: 0, ok: true, status: 'Ready', objects: [], created: [], canUndo: false, canRedo: false, physicsRunning: false };
  execute = vi.fn(async () => ({ ...scene, ack: 1, status: 'Created robot' }));
  ports.lease.mockReturnValue({ valid: () => true, state: () => scene, execute });
  ports.claim.mockImplementation(async record => { const old = records.get(record.id); if (old) return { claimed: false, record: old }; records.set(record.id, structuredClone(record)); return { claimed: true, record }; });
  ports.save.mockImplementation(async record => { records.set(record.id, structuredClone(record)); });
  const outputs = ['{"commands":[{"action":"create","reference":"r","name":"Robot","kind":"boxRobot"}]}', '{"commands":[]}', 'Listo.\n[EN]Ready.'];
  const ai = { models: { generateContent: vi.fn(), generateContentStream: async (request: any) => {
    requests.push(request); const text = outputs.shift();
    return (async function* () { yield { text, candidates: [{ content: { role: 'model', parts: [{ text }] } }] }; })();
  } } };
  ports.source.mockReturnValue({ resolveAiClient: async () => ai });
});
describe('browser chat room handoff composition', () => {
  it.each(['byok', 'managed'])('uses the original app client for %s and keeps working history outside chat', async mode => {
    if (mode === 'managed') { ports.key.mockResolvedValue(null); ports.managed.mockResolvedValue({ user: { id: 'user-1' }, firebaseIdToken: 'synthetic-session' }); }
    const { id, source } = setup();
    const prepared = await prepareRoomAgentHandoff(input, source);
    expect(prepared.prompt).toBe(input.prompt); expect(prepared.history).toEqual(input.history);
    expect(prepared.currentFileParts).toEqual(input.currentFileParts);
    useMaestroStore.getState().addMessage({ role: 'user', text: 'Let us discuss Spanish meanwhile.' });
    expect(roomAgentRequestForVerification(id, proposal)).toBe(input.prompt);
    await startRoomAgentTask(id);
    expect(execute).toHaveBeenCalledOnce(); expect(ports.source).toHaveBeenCalledTimes(2);
    expect(requests).toHaveLength(3);
    expect(JSON.stringify(requests[0])).toContain('Earlier context');
    expect(JSON.stringify(requests[0])).toContain('test://current-drawing');
    const record = records.get(`room-task:${id}`)!;
    expect(record.handoff.input).toEqual(prepared);
    expect(record.handoff.accessScope).toBe(mode === 'managed' ? 'managed:user-1' : 'byok');
    expect(JSON.stringify(record)).not.toContain('synthetic-key');
    const message = useMaestroStore.getState().messages.find(item => item.id === record.id)!;
    expect(message.role).toBe('assistant'); expect(message.translations?.[0]).toEqual({ target: 'Listo.', native: 'Ready.' });
    expect(JSON.stringify(message)).not.toContain('sceneRevision'); expect(JSON.stringify(message)).not.toContain('commands');
    expect(selectIsSending(useMaestroStore.getState())).toBe(false);
    await startRoomAgentTask(id); expect(execute).toHaveBeenCalledOnce();
  });
  it.each(['planning', 'replying'])('Stop releases a stalled %s request and keeps completed room actions', async phase => {
    const { id, source } = setup();
    let started!: () => void, finishProvider!: (stream: AsyncIterable<any>) => void;
    const waiting = new Promise<void>(resolve => { started = resolve; });
    const pending = new Promise<AsyncIterable<any>>(resolve => { finishProvider = resolve; });
    let signal!: AbortSignal;
    const outputs = ['{"commands":[{"action":"create","reference":"r","name":"Robot","kind":"boxRobot"}]}', '{"commands":[]}'];
    const send = vi.fn(async (request: any) => {
      requests.push(request);
      if (phase === 'planning' || requests.length === 3) {
        signal = request.config.abortSignal; started(); return pending;
      }
      const text = outputs.shift();
      return (async function* () { yield { text }; })();
    });
    ports.source.mockReturnValue({ aiClient: { models: { generateContentStream: send } } });
    await prepareRoomAgentHandoff(input, source);
    const done = startRoomAgentTask(id); await waiting;
    expect(selectIsAgentWorking(useMaestroStore.getState())).toBe(true);
    expect(selectIsSending(useMaestroStore.getState())).toBe(false);
    roomAgentTasks.stop(`room-task:${id}`); await done;
    expect(signal.aborted).toBe(true);
    expect(selectIsAgentWorking(useMaestroStore.getState())).toBe(false);
    const record = records.get(`room-task:${id}`)!;
    expect(record.phase).toBe('stopped'); expect(record.reply).toBeUndefined();
    expect(execute).toHaveBeenCalledTimes(phase === 'planning' ? 0 : 1);
    if (phase === 'replying') expect(record.operations[0].receipt?.ok).toBe(true);
    finishProvider((async function* () { yield { text: 'Late reply that must not replace Stop' }; })());
    await new Promise(resolve => setTimeout(resolve, 0));
    expect(useMaestroStore.getState().messages.find(message => message.id === record.id)?.agentTask?.phase).toBe('stopped');
    expect(send).toHaveBeenCalledTimes(phase === 'planning' ? 1 : 3);
  });
  it('does not expose room actions on an ordinary phone chat or a proactive message', async () => {
    const { source } = setup(); ports.lease.mockReturnValue(null);
    expect(await prepareRoomAgentHandoff(input, source)).toBe(input); expect(ports.key).not.toHaveBeenCalled();
    ports.lease.mockReturnValue({ valid: () => true });
    expect(await prepareRoomAgentHandoff(input, { ...source, sourceUserId: undefined })).toBe(input);
  });
  it('requires the actual original request, never an assistant summary', async () => {
    const { id, source } = setup();
    expect(await prepareRoomAgentHandoff({ ...input, prompt: 'Short rewritten task' }, source)).toMatchObject({ prompt: 'Short rewritten task', systemInstruction: input.systemInstruction });
    expect(roomAgentRequestForVerification(id, proposal)).toBeUndefined();
  });
  it('refuses stale authorization after an access change', async () => {
    const { id, source } = setup(); await prepareRoomAgentHandoff(input, source);
    ports.key.mockResolvedValue('different-synthetic-key');
    await startRoomAgentTask(id); expect(execute).not.toHaveBeenCalled();
    expect(useMaestroStore.getState().messages.find(message => message.id === `room-task:${id}`)?.text).toContain('no longer available');
  });
  it('does not advertise task results as fresh handoffs', async () => {
    const { id, source } = setup(); await prepareRoomAgentHandoff(input, source);
    expect(roomAgentRequestForVerification(id, '```maestro-tool {"tool":"agent","compactHistory":true}```')).toBeUndefined();
    expect(roomAgentTasks.available(id)).toBe(true);
  });
});
