// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { LiveInputContext } from '../../../core-sdk/media/liveInputContext';
const sentMedia = () => { const input = new LiveInputContext(() => 0); input.recordAudio('AAA='); input.recordFrame('/9j/2Q=='); return input.finish(); };
// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest';
const ports = vi.hoisted(() => ({ lease: vi.fn(), key: vi.fn(), managed: vi.fn(), source: vi.fn(), history: vi.fn(), usage: vi.fn(),
  summaries: vi.fn(), claim: vi.fn(), save: vi.fn(), get: vi.fn() }));
vi.mock('../../../platform/quest/roomAgentBridge', () => ({ currentRoomAgentLease: ports.lease }));
vi.mock('../../../core/security/apiKeyStorage', () => ({ loadApiKey: ports.key }));
vi.mock('../../../core/security/managedAccessSessionStorage', () => ({ loadManagedAccessSession: ports.managed, hasManagedSession: (session: any) => Boolean(session?.user?.id && session?.firebaseIdToken) }));
vi.mock('../../../api/gemini/browserClientSource', () => ({ browserClientSource: ports.source }));
vi.mock('../../../shared/utils/costTracker', () => ({ trackGeminiUsage: ports.usage }));
vi.mock('./chatHistory', () => ({ safeSaveChatHistoryDB: ports.history }));
vi.mock('./roomTaskSummaries', () => ({ isRoomTaskHidden: () => false, loadRoomTaskSummaries: ports.summaries }));
vi.mock('./roomTaskStore', () => ({ roomTaskStore: { claim: ports.claim, save: ports.save, get: ports.get } }));
import { useMaestroStore, initialSettings, allGeneratedLanguagePairs } from '../../../store';
import { prepareRoomAgentHandoff, prepareLiveRoomAgentContext, captureLiveRoomAgentHandoff, roomAgentRequestForVerification, roomAgentTargetsForVerification, startRoomAgentTask, roomAgentTasks, resetRoomAgentTasks } from './roomAgentTasks';
import type { TutorTextTurnInput } from '../../../core-sdk/chat/tutorTextTurn';
import { summarizeRoomTask } from '../../../core-sdk/room/roomTaskProjection';
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
  ports.summaries.mockResolvedValue([]); ports.get.mockImplementation(async id => structuredClone(records.get(id)));
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

function liveSetup() {
  const result = setup();
  result.source.conversationId = allGeneratedLanguagePairs[0].id;
  const state = useMaestroStore.getState();
  useMaestroStore.setState({ settings: { ...state.settings, selectedLanguagePairId: result.source.conversationId } });
  state.updateMessage(result.id, { llmRawResponse: 'I will ask the room agent to do that.' });
  return result;
}
const spokenReply = 'I will ask the room agent to do that.';

describe('Live connection provenance for the shared room dispatcher', () => {
  it.each(['byok', 'managed'])('retains the actual connection instruction and original transcript for %s', async mode => {
    const { id, source } = liveSetup();
    if (mode === 'managed') { ports.key.mockResolvedValue(null); ports.managed.mockResolvedValue({ user: { id: 'user-1' }, firebaseIdToken: 'synthetic-session' }); }
    const connection = await prepareLiveRoomAgentContext('Original profile, bookmark summary and history.');
    expect(connection.systemInstruction).toContain('Propose the handoff in natural speech only.');
    expect(connection.systemInstruction).not.toContain('Propose the same {"tool":"agent"}');
    expect(connection.handoffId).toBeTruthy();
    expect(connection.systemInstruction).toContain('Do not speak JSON');
    useMaestroStore.getState().addMessage({ role: 'user', text: 'A later unrelated message' });
    expect(await captureLiveRoomAgentHandoff(connection.handoffId!, source, input.prompt, spokenReply, sentMedia())).toBe(true);
    expect(roomAgentRequestForVerification(id, spokenReply)).toBe(input.prompt);
    expect(execute).not.toHaveBeenCalled(); // Only capture: suggestions must still accept the handoff.
    await startRoomAgentTask(id);
    const record = records.get(`room-task:${id}`)!;
    expect(record.handoff.input.prompt).toBe(input.prompt);
    expect(record.handoff.input.systemInstruction).toBe(connection.systemInstruction);
    expect(record.handoff.input.history).toEqual([]); // Already serialized in the connection instruction.
    expect(JSON.stringify(requests[0])).toContain('Original profile, bookmark summary and history.');
    expect(JSON.stringify(requests[0])).not.toContain('A later unrelated message');
    const media = sentMedia();
    expect(record.handoff.input.liveInputMedia).toEqual(media);
    for (const request of requests) {
      const parts = request.contents.flatMap((content: any) => content.parts);
      expect(parts).toContainEqual({ inlineData: { mimeType: 'audio/wav', data: media.audio!.data } });
      expect(parts).toContainEqual({ inlineData: { mimeType: 'image/jpeg', data: media.frames[0].data } });
    }
    expect(record.phase).toBe('completed'); expect(execute).toHaveBeenCalledOnce();
    expect(JSON.stringify(record)).not.toContain('synthetic-session');
    expect(JSON.stringify(record)).not.toContain('synthetic-key');
  });

  it('does not advertise or record room handoff for an ordinary phone connection', async () => {
    liveSetup(); ports.lease.mockReturnValue(null);
    expect(await prepareLiveRoomAgentContext('Unchanged instruction')).toEqual({ systemInstruction: 'Unchanged instruction' });
    expect(ports.key).not.toHaveBeenCalled();
  });

  it('rejects invented connection identities, absent speech and duplicate completion', async () => {
    const { source } = liveSetup();
    expect(await captureLiveRoomAgentHandoff('invented', source, input.prompt, spokenReply, sentMedia())).toBe(false);
    const silent = await prepareLiveRoomAgentContext('Context');
    expect(await captureLiveRoomAgentHandoff(silent.handoffId!, source, '', spokenReply)).toBe(false);
    const connection = await prepareLiveRoomAgentContext('Context');
    expect(await captureLiveRoomAgentHandoff(connection.handoffId!, source, input.prompt, spokenReply, sentMedia())).toBe(true);
    expect(await captureLiveRoomAgentHandoff(connection.handoffId!, source, input.prompt, spokenReply, sentMedia())).toBe(false);
    expect(execute).not.toHaveBeenCalled();
  });

  it.each(['account', 'conversation', 'native', 'source'])('refuses a completed turn after its %s identity changes', async change => {
    const { id, source } = liveSetup();
    const connection = await prepareLiveRoomAgentContext('Context');
    if (change === 'account') ports.key.mockResolvedValue('changed-key');
    if (change === 'conversation') useMaestroStore.setState({ settings: { ...useMaestroStore.getState().settings, selectedLanguagePairId: 'another-pair' } });
    if (change === 'native') ports.lease.mock.results[ports.lease.mock.results.length - 1].value.valid = () => false;
    if (change === 'source') useMaestroStore.getState().updateMessage(source.sourceUserId, { text: 'Edited transcript' });
    expect(await captureLiveRoomAgentHandoff(connection.handoffId!, source, input.prompt, spokenReply, sentMedia())).toBe(false);
    expect(roomAgentRequestForVerification(id, spokenReply)).toBeUndefined();
    expect(execute).not.toHaveBeenCalled();
  });

  it('does not authorize a changed tutor reply or copied text from a different turn', async () => {
    const { id, source } = liveSetup();
    const connection = await prepareLiveRoomAgentContext('Context');
    await captureLiveRoomAgentHandoff(connection.handoffId!, source, input.prompt, spokenReply, sentMedia());
    expect(roomAgentRequestForVerification(id, 'Changed reply')).toBeUndefined();
    useMaestroStore.getState().updateMessage(id, { llmRawResponse: 'Changed reply' });
    await startRoomAgentTask(id); expect(execute).not.toHaveBeenCalled();
  });

  it('rechecks conversation identity after a pending credential read', async () => {
    liveSetup(); let resolve!: (value: string) => void;
    ports.key.mockReturnValue(new Promise<string>(done => { resolve = done; }));
    const preparing = prepareLiveRoomAgentContext('Context');
    useMaestroStore.setState({ settings: { ...useMaestroStore.getState().settings, selectedLanguagePairId: 'another-pair' } });
    resolve('synthetic-key');
    expect(await preparing).toEqual({ systemInstruction: 'Context' });
  });
});

describe('incomplete Live media handoffs', () => {
  it.each(['missing', 'limit', 'interrupted'] as const)('persists a readable %s failure before planning, without native effects', async issue => {
    const { id, source } = liveSetup();
    const context = await prepareLiveRoomAgentContext('Original instruction');
    const media = issue === 'missing' ? undefined : { version: 1 as const, complete: false, issue, frames: [], packets: [] };
    expect(await captureLiveRoomAgentHandoff(context.handoffId!, source, input.prompt, spokenReply, media)).toBe(true);
    await startRoomAgentTask(id);
    const record = records.get(`room-task:${id}`)!;
    expect(record.phase).toBe('failed'); expect(record.operations).toEqual([]);
    expect(record.note).toContain('Please repeat a shorter request');
    expect(ports.source).not.toHaveBeenCalled(); expect(execute).not.toHaveBeenCalled();
    expect(useMaestroStore.getState().messages.find(message => message.id === record.id)?.text).toBe(record.note);
    expect(selectIsAgentWorking(useMaestroStore.getState())).toBe(false);
  });
});

it('announces a fresh task result once and does not announce a saved claim again', async () => {
  const { subscribeRoomTaskResults } = await import('./roomTaskResults');
  const announced = vi.fn(), unsubscribe = subscribeRoomTaskResults(announced);
  try {
    const { id, source } = setup(); await prepareRoomAgentHandoff(input, source); await startRoomAgentTask(id);
    expect(announced).toHaveBeenCalledOnce();
    const result = announced.mock.calls[0][0]; expect(result.id).toBe(`room-task:${id}`); expect(await result.valid()).toBe(true);
    await startRoomAgentTask(id); expect(announced).toHaveBeenCalledOnce();
    ports.key.mockResolvedValue('different-synthetic-key'); expect(await result.valid()).toBe(false);
  } finally { unsubscribe(); }
});

it('reset invalidates context still waiting for credentials and previously prepared handoffs', async () => {
  const { id, source } = setup();
  await prepareRoomAgentHandoff(input, source);
  let resolveKey!: (key: string) => void;
  ports.key.mockImplementationOnce(() => new Promise<string>(resolve => { resolveKey = resolve; }));
  const pending = prepareRoomAgentHandoff(input, source);
  await resetRoomAgentTasks(); resolveKey('synthetic-key-not-a-credential');
  expect(await pending).toBe(input);
  expect(roomAgentRequestForVerification(id, proposal)).toBeUndefined();
  await startRoomAgentTask(id);
  expect(execute).not.toHaveBeenCalled();
});

it.each(['chat', 'live'])('carries an explicitly verified %s follow-up through the shared model route with earlier evidence', async mode => {
  const { id, source } = mode === 'chat' ? setup() : liveSetup();
  // The original creation uses the same prepared text context in both cases.
  useMaestroStore.getState().updateMessage(id, { llmRawResponse: proposal });
  await prepareRoomAgentHandoff(input, source); await startRoomAgentTask(id);
  const parent = records.get(`room-task:${id}`)!;
  ports.summaries.mockResolvedValue([summarizeRoomTask(parent)]);
  const state = useMaestroStore.getState(), userId = state.addMessage({ role: 'user', text: '  Blue, please.  ' });
  const assistantId = state.addMessage({ role: 'assistant', text: 'I will ask the agent.', llmRawResponse: mode === 'chat' ? proposal : spokenReply });
  const nextSource = { conversationId: source.conversationId, sourceUserId: userId, sourceAssistantId: assistantId };
  if (mode === 'chat') await prepareRoomAgentHandoff({ ...input, prompt: '  Blue, please.  ' }, nextSource);
  else {
    const connection = await prepareLiveRoomAgentContext('Current Live instruction');
    expect(connection.systemInstruction).toContain(parent.id);
    expect(await captureLiveRoomAgentHandoff(connection.handoffId!, nextSource, '  Blue, please.  ', spokenReply, sentMedia())).toBe(true);
  }
  const targets = roomAgentTargetsForVerification(assistantId, mode === 'chat' ? proposal : spokenReply);
  expect(targets).toMatchObject([{ id: parent.id, running: false }]);
  const outputs = ['{"commands":[]}', 'Azul.\n[EN]Blue.'];
  ports.source.mockReturnValue({ aiClient: { models: { generateContentStream: async (request: any) => {
    requests.push(request); return (async function* () { yield { text: outputs.shift() }; })();
  } } } });
  await startRoomAgentTask(assistantId, { action: 'continue', taskId: parent.id });
  const next = records.get(`room-task:${assistantId}`)!;
  expect(next.phase).toBe('completed'); expect(next.handoff.input.prompt).toBe('  Blue, please.  ');
  expect(next.relatedTask?.requests).toEqual([input.prompt]); expect(next.relatedTask?.operations[0].receipt?.ok).toBe(true);
  expect(execute).toHaveBeenCalledOnce(); // No blind replay from the prior creation.
  expect(JSON.stringify(requests[requests.length - 2])).toContain('relatedTask');
  await startRoomAgentTask(assistantId, { action: 'continue', taskId: parent.id }); expect(requests).toHaveLength(5);
});

it('does not offer imported, foreign-room, deleted-source or hidden tasks as steering targets', async () => {
  const { id, source } = setup(); await prepareRoomAgentHandoff(input, source); await startRoomAgentTask(id);
  const summary = summarizeRoomTask(records.get(`room-task:${id}`)!);
  ports.summaries.mockResolvedValue([
    { ...summary, id: 'imported', taskScope: { ...summary.taskScope, readOnly: true } },
    { ...summary, id: 'foreign', taskScope: { ...summary.taskScope, nativeSession: 'another' } },
    { ...summary, id: 'hidden', hidden: true }, { ...summary, id: 'orphan', sourceUserId: 'deleted' },
  ]);
  const user = useMaestroStore.getState().addMessage({ role: 'user', text: 'Continue.' });
  const assistant = useMaestroStore.getState().addMessage({ role: 'assistant', llmRawResponse: proposal });
  await prepareRoomAgentHandoff({ ...input, prompt: 'Continue.' }, { ...source, sourceUserId: user, sourceAssistantId: assistant });
  expect(roomAgentTargetsForVerification(assistant, proposal)).toEqual([]);
  await startRoomAgentTask(assistant, { action: 'continue', taskId: summary.id });
  expect(execute).toHaveBeenCalledOnce();
});
