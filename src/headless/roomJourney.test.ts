// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, describe, expect, it, vi } from 'vitest';
import { dispatchHeadlessMethod } from './dispatcher';
import { rm } from 'node:fs/promises';
import { createHeadlessClient, type HeadlessClient } from './client';
import { HeadlessRoomAgent, runHeadlessRoomTurn } from './roomJourney';
import { HeadlessRoomTaskStore } from './roomTaskStore';
import { runHeadlessChatTurn, selectHeadlessLanguage } from './chatJourney';
import { runHeadlessSuggestionAftersteps } from './suggestionJourney';
import { runHeadlessLiveTurn } from './liveJourney';
import { liveInputHashes, providerMediaHashes } from './roomLiveJourney';
import { createHash } from 'node:crypto';
import { LiveInputContext } from '../core-sdk/media/liveInputContext';
import { LIVE_TURN_CALLBACK_QUIET_MS } from '../core-sdk/media/liveTurnFinalizer';
import type { RoomAgentState } from '../core-sdk/room/roomAgent';
import type { RoomTaskRecord } from '../core-sdk/room/roomTaskHandoff';

const proposal = 'Lo prepararé.\n[EN]I will prepare it.\n```maestro-tool\n{"tool":"agent"}\n```';
const verification = JSON.stringify({ suggestions: [{ target: 'Gracias.', native: 'Thanks.' }], toolRequest: { tool: 'agent' } });
const clients: HeadlessClient[] = [];
afterEach(async () => { for (const client of clients.splice(0)) { await client.roomAgent?.disconnect(); await rm(client.profile.directory, { recursive: true, force: true }); } });
async function setup(mode: 'managed' | 'byok' = 'byok') {
  // Managed construction avoids resolving any local BYOK key; both test clients
  // inject a deterministic provider at the same transport port, never the agent.
  const client = await createHeadlessClient({ accessMode: 'managed' }); clients.push(client); client.accessMode = mode;
  vi.spyOn(client.credentials, 'getUserId').mockResolvedValue('fixture-user');
  await selectHeadlessLanguage(client, { targetLanguageCode: 'es-ES', nativeLanguageCode: 'en-US' });
  const pairId = client.state.settings.selectedLanguagePairId!;
  client.state.chats[pairId].push({ id: 'earlier', role: 'user', text: 'The ball should be blue.', timestamp: 1 });
  const outputs = [proposal, verification, '{"commands":[{"action":"create","reference":"ball","name":"Blue ball","kind":"ball"}]}', '{"commands":[]}', 'Listo.\n[EN]Ready.'];
  const requests: any[] = [];
  const send = vi.fn(async (request: any) => {
    requests.push(request); const text = outputs.shift(); if (!text) throw new Error('Unexpected provider request');
    return (async function* () { yield { text, usageMetadata: { totalTokenCount: 20 }, candidates: [{ content: { role: 'model', parts: [{ text }] } }] }; })();
  });
  client.ai = { models: { generateContentStream: send } } as any;
  const scene: RoomAgentState = { version: 1, session: 'native', revision: 1, sceneRevision: 1, ack: 0, ok: true, status: 'Ready', objects: [], created: [], canUndo: false, canRedo: false, physicsRunning: false };
  let valid = true;
  const execute = vi.fn(async () => {
    const records = await agent.store.list();
    expect(records[records.length - 1]?.operations.slice(-1)[0]?.receipt).toBeUndefined();
    expect(records[records.length - 1]?.operations.slice(-1)[0]?.commands[0].action).toBe('create');
    scene.sceneRevision++; scene.ack++; scene.objects.push({ id: 'ball1', name: 'Blue ball', kind: 'ball', position: { x: 0, y: 1, z: 0 }, scale: 1, color: { r: 0, g: 0, b: 1, a: 1 }, animated: false });
    return structuredClone(scene);
  });
  const agent = new HeadlessRoomAgent(client, () => ({ valid: () => valid, state: () => scene, execute })); client.roomAgent = agent;
  return { client, pairId, agent, outputs, requests, send, scene, execute, invalidate: () => { valid = false; } };
}

describe('headless conversational agent parity (deterministic transport)', () => {
  it.each([['managed', undefined], ['byok', undefined], ['managed', 'generated'], ['byok', 'generated'], ['managed', 'virtual-scene'], ['byok', 'virtual-scene']] as const)('runs tutor → verifier → journal → native receipt → chat in %s with origin %s', async (mode, origin) => {
    const f = await setup(mode);
    const params = { text: 'Make that ball.', fileParts: [{ fileUri: 'test://drawing', mimeType: 'image/png', ...(origin ? { origin } : {}) }], useGoogleSearch: false };
    const turn = origin === 'virtual-scene'
      ? await dispatchHeadlessMethod(f.client, 'chat.turn', params) as Awaited<ReturnType<typeof runHeadlessChatTurn>>
      : await runHeadlessChatTurn(f.client, params);
    const result = await runHeadlessSuggestionAftersteps(f.client, { assistantMessageId: turn.assistantMessage.id });
    expect(result.decisionSource).toBe('model'); expect(result.toolRequest).toEqual({ tool: 'agent' });
    expect(JSON.stringify(result.toolResult)).not.toMatch(/commands|sceneRevision|test:\/\/drawing/);
    const record = await f.agent.store.get(`room-task:${turn.assistantMessage.id}`);
    expect(record?.phase).toBe('completed'); expect(f.execute).toHaveBeenCalledOnce();
    expect(record?.handoff.input.prompt).toBe('Make that ball.');
    expect(JSON.stringify(record?.handoff.input.history)).toContain('The ball should be blue.');
    expect(record?.handoff.input.currentFileParts?.[0].fileUri).toBe('test://drawing');
    expect(record?.handoff.accessScope).toBe(mode === 'byok' ? 'byok' : 'managed:fixture-user');
    expect(JSON.stringify(f.requests[2])).toContain('The ball should be blue.');
    for (const index of [0, 2, 3, 4]) {
      expect(JSON.stringify(f.requests[index]).includes('AI-generated illustration')).toBe(origin === 'generated');
      expect(JSON.stringify(f.requests[index]).includes('Virtual-scene render')).toBe(origin === 'virtual-scene');
      const files = f.requests[index].contents.flatMap((c: any) => c.parts).filter((p: any) => p.fileData);
      expect(files[0].fileData).toEqual({ fileUri: 'test://drawing', mimeType: 'image/png' });
    }
    expect(turn.userMessage?.uploadedFileVariants?.[0].origin).toBe(origin);
    expect(JSON.stringify(f.requests[1]).includes('Virtual-scene render')).toBe(origin === 'virtual-scene');
    expect(record?.handoff.input.currentFileParts?.[0].origin).toBe(origin);
    const message = f.client.state.chats[f.pairId].find(message => message.id === record?.id)!;
    expect(message.translations?.[0]).toEqual({ target: 'Listo.', native: 'Ready.' });
    expect(JSON.stringify(message)).not.toMatch(/commands|sceneRevision|test:\/\/drawing/);
    const persisted = await f.client.profile.load(); expect(persisted.chats[f.pairId]).toContainEqual(message);
    expect(f.client.events.snapshot().filter(e => e.phase === 'activity.changed').map(e => e.data?.active)).toEqual([true, false]);
    await f.agent.start(turn.assistantMessage.id); expect(f.execute).toHaveBeenCalledOnce(); expect(f.send).toHaveBeenCalledTimes(5);
  });
  it('does not spend or dispatch while earlier managed requests remain reserved', async () => {
    const f = await setup('managed');
    vi.spyOn(f.client.account, 'refreshAccount').mockResolvedValue({ account: { billingSummary: { reservedCredits: 20 } } } as any);
    vi.spyOn(f.client.account, 'listLedgers').mockResolvedValue({ usage: { entries: [] }, billing: { entries: [] } } as any);
    const sleep = vi.spyOn(f.client.runtime.clock, 'sleep').mockResolvedValue(undefined);
    await expect(runHeadlessRoomTurn(f.client, { text: 'Make that ball.' })).rejects.toThrow('before earlier billing has settled');
    expect(sleep).toHaveBeenCalledTimes(19);
    expect(f.send).not.toHaveBeenCalled(); expect(f.execute).not.toHaveBeenCalled();
    expect(await f.agent.store.list()).toEqual([]);
  });
  it('does not allow a synthetic verifier override to launch an agent', async () => {
    const f = await setup(); const turn = await runHeadlessChatTurn(f.client, { text: 'Make a ball.' });
    const result = await runHeadlessSuggestionAftersteps(f.client, { assistantMessageId: turn.assistantMessage.id, syntheticDecision: { toolRequest: { tool: 'agent' } } });
    expect(result.toolRequest).toBeNull(); expect(f.execute).not.toHaveBeenCalled(); expect(await f.agent.store.list()).toEqual([]);
  });
  it('rejects invented verifier handoffs without a tutor proposal', async () => {
    const f = await setup(); f.outputs[0] = 'Hola.\n[EN]Hello.';
    const turn = await runHeadlessChatTurn(f.client, { text: 'Hello.' });
    const result = await runHeadlessSuggestionAftersteps(f.client, { assistantMessageId: turn.assistantMessage.id });
    expect(result.toolRequest).toBeNull(); expect(f.execute).not.toHaveBeenCalled();
  });
  it.each(['session', 'source', 'account', 'conversation'] as const)('revokes a prepared handoff after %s loss', async reason => {
    const f = await setup('managed'); const turn = await runHeadlessChatTurn(f.client, { text: 'Make a ball.' });
    if (reason === 'session') f.invalidate();
    if (reason === 'source') f.client.state.chats[f.pairId] = [];
    if (reason === 'account') vi.mocked(f.client.credentials.getUserId).mockResolvedValue('other');
    if (reason === 'conversation') f.client.state.settings.selectedLanguagePairId = 'other';
    await expect(f.agent.start(turn.assistantMessage.id)).rejects.toThrow(/no longer available/);
    expect(f.execute).not.toHaveBeenCalled(); expect(await f.agent.store.list()).toEqual([]);
  });
  it('Stop cancels a pending provider and persists the stopped task', async () => {
    const f = await setup(); const turn = await runHeadlessChatTurn(f.client, { text: 'Make a ball.' });
    let signal: AbortSignal | undefined;
    f.send.mockImplementationOnce(async request => { signal = request.config.abortSignal; return new Promise(() => {}); });
    const task = f.agent.start(turn.assistantMessage.id);
    await vi.waitFor(() => expect(signal).toBeDefined()); f.agent.tasks.stop(`room-task:${turn.assistantMessage.id}`);
    expect((await task).phase).toBe('stopped'); expect(signal?.aborted).toBe(true); expect(f.execute).not.toHaveBeenCalled();
  });
  it.each(['conversation', 'observer'] as const)('delegates the actual %s stream, original media and final reply through shared chat', async mode => {
    const f = await setup(); f.outputs.shift();
    // This fixture checks handoff data, not wall-clock playback. Advance the
    // injected clock through the normal quiet window without a real CI sleep;
    // real-time pacing and late callbacks have separate journey coverage.
    let now = f.client.runtime.clock.now();
    const sleeps: number[] = [];
    f.client.runtime.clock = { ...f.client.runtime.clock, now: () => now,
      sleep: async ms => { sleeps.push(ms); now += ms; } };
    const sent: Array<{ audio?: { data: string }; video?: { data: string } }> = [];
    const connect = vi.fn(async (params: any) => ({
      close: vi.fn(),
      sendRealtimeInput: (message: any) => {
        sent.push(message);
        if (!message.audioStreamEnd && !message.activityEnd) return;
        params.callbacks.onmessage({ serverContent: {
          inputTranscription: { text: 'Make a ball.' },
          outputTranscription: { text: 'I will ask the room agent to make it.' },
          modelTurn: { parts: [{ inlineData: { mimeType: 'audio/pcm;rate=24000', data: Buffer.alloc(4800).toString('base64') } }] },
          turnComplete: true,
        } });
      },
    }));
    Object.assign(f.client.ai, { live: { connect } });
    const result = await runHeadlessLiveTurn(f.client, { mode, pcm: new Int16Array(32_000).fill(6000),
      languagePairId: f.pairId, pace: false, includeVisual: true, expectedTranscript: 'Make a ball.' });
    expect(sleeps).toContain(LIVE_TURN_CALLBACK_QUIET_MS);
    const instruction = connect.mock.calls[0][0].config.systemInstruction;
    expect(instruction).toContain('Propose the handoff in natural speech only.');
    expect(instruction).not.toContain('Propose the same {"tool":"agent"}');
    expect(result.aftersteps.toolRequest?.tool).toBe('agent');
    const record = (await f.agent.store.list())[0];
    expect(record.phase).toBe('completed'); expect(f.execute).toHaveBeenCalledOnce();
    const hashes = liveInputHashes(record.handoff.input.liveInputMedia!);
    const pcm = Buffer.concat(sent.filter(value => value.audio).map(value => Buffer.from(value.audio!.data, 'base64')));
    expect(hashes.pcm).toBe(createHash('sha256').update(pcm).digest('hex'));
    expect(hashes.samples * 2).toBe(pcm.length);
    const frames = sent.filter(value => value.video).map(value => createHash('sha256').update(Buffer.from(value.video!.data, 'base64')).digest('hex'));
    expect(hashes.frames.map(value => value.sha256)).toEqual(frames);
    expect(frames.length).toBeGreaterThan(0);
    expect(providerMediaHashes(f.requests[1].contents)).toEqual(expect.arrayContaining([
      { mimeType: 'audio/wav', sha256: hashes.audio }, { mimeType: 'image/jpeg', sha256: frames[0] },
    ]));
    expect(result.liveInputMedia).toBeUndefined();
    expect(JSON.stringify(result.aftersteps.toolResult)).not.toContain(record.handoff.input.liveInputMedia!.audio!.data);
    expect(f.client.state.chats[f.pairId].find(value => value.id === record.id)?.agentTask?.phase).toBe('completed');
  });
  it.each([true, false])('freezes Live context and requires complete original sent media (complete=%s)', async complete => {
    const f = await setup();
    const context = await f.agent.prepareLive({ model: 'fixture', history: [], systemInstruction: 'Original Live context.', nativeLanguageCode: 'en-US' }, f.pairId);
    const media = new LiveInputContext(() => 0); media.recordAudio('AAA='); media.recordFrame('/9j/2Q==');
    const source = { sourceUserId: 'live-user', sourceAssistantId: 'live-assistant', conversationId: f.pairId };
    f.client.state.chats[f.pairId].push({ id: source.sourceUserId, role: 'user', text: 'Make a ball.', timestamp: 2 },
      { id: source.sourceAssistantId, role: 'assistant', llmRawResponse: 'I will ask the agent.', timestamp: 3 });
    await context!.capture(source, 'Make a ball.', 'I will ask the agent.', complete ? media.finish() : undefined);
    f.outputs.splice(0, 2);
    const record = await f.agent.start(source.sourceAssistantId);
    expect(record.phase).toBe(complete ? 'completed' : 'failed');
    expect(f.execute).toHaveBeenCalledTimes(complete ? 1 : 0);
    expect(f.send).toHaveBeenCalledTimes(complete ? 3 : 0);
    if (complete) { expect(record.handoff.input.liveInputMedia?.frames).toHaveLength(1); expect(JSON.stringify(f.requests[0])).toContain('audio/wav'); }
  });
  it.each(['tutor', 'verifier'] as const)('retains settlement evidence when the %s throws before dispatch', async stage => {
    const f = await setup('managed');
    const summary = { availableCredits: 100, reservedCredits: 0, lifetimeSpentCredits: 0, lifetimeSpentUsd: 0 };
    const refresh = vi.spyOn(f.client.account, 'refreshAccount').mockResolvedValue({ account: { billingSummary: summary } } as any);
    vi.spyOn(f.client.account, 'listLedgers').mockResolvedValue({ usage: { entries: [] }, billing: { entries: [] } } as any);
    const implementation = f.send.getMockImplementation()!;
    f.send.mockImplementation(async () => { throw Object.assign(new Error('Provider refused the request'), { status: 400 }); });
    if (stage === 'verifier') f.send.mockImplementationOnce(implementation);
    const error = await runHeadlessRoomTurn(f.client, { text: 'Make a ball.' }).catch(error => error);
    expect(error.message).toBe('Provider refused the request');
    expect(error.evidence.billing).toMatchObject({ passed: true, creditsSpent: 0, reservedCreditsAfter: 0 });
    expect(refresh).toHaveBeenCalledTimes(2);
    expect(error.evidence.coverage.completed).toBe(false);
    expect(f.execute).not.toHaveBeenCalled();
  });
  it('recovers an interrupted journal for display without executing it', async () => {
    const f = await setup(); const turn = await runHeadlessChatTurn(f.client, { text: 'Make a ball.' });
    const record: RoomTaskRecord = { version: 1, id: `room-task:${turn.assistantMessage.id}`, phase: 'working', note: 'Dispatching', startedAt: 1, updatedAt: 1, operations: [{ commands: [{ action: 'undo' }], sceneRevision: 1 }],
      handoff: { version: 1, id: `room-task:${turn.assistantMessage.id}`, sourceUserId: turn.userMessage!.id, sourceAssistantId: turn.assistantMessage.id, conversationId: f.pairId, nativeSession: 'native', accessScope: 'byok', input: { model: 'fixture', prompt: 'Make a ball.', history: [], nativeLanguageCode: 'en-US', systemInstruction: 'Fixture' } } };
    await f.agent.store.claim(record);
    const recovered = new HeadlessRoomTaskStore(f.client.profile.directory + '/room-tasks');
    const old = await recovered.get(record.id); expect(old?.phase).toBe('interrupted'); expect(old?.readOnly).toBe(true);
    expect((await recovered.claim(record)).claimed).toBe(false);
    await expect(recovered.save(record)).rejects.toThrow(/does not own/); expect(f.execute).not.toHaveBeenCalled();
  });
});
