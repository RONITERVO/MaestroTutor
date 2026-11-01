// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { runHeadlessChatTurn } from './chatJourney';
import { runHeadlessSuggestionAftersteps } from './suggestionJourney';
import { beginManagedJourneyBilling, evaluateManagedJourneyBilling, waitForManagedJourneyBillingSettlement } from './managedJourneyBilling';
import { join } from 'node:path';
import type { HeadlessClient } from './client';
import type { TutorTextTurnInput } from '../core-sdk/chat/tutorTextTurn';
import type { RoomAgentLease } from '../core-sdk/room/roomAgent';
import { RoomTaskHandoff, type RoomTaskRecord } from '../core-sdk/room/roomTaskHandoff';
import { roomTaskProvider } from '../core-sdk/room/roomTaskProvider';
import { hasRoomTaskSources, projectRoomTaskSummaries, summarizeRoomTask } from '../core-sdk/room/roomTaskProjection';
import { hasAgentHandoffProposal } from '../core-sdk/chat/suggestionAftersteps';
import { parseRoomTaskDirective, type RoomTaskDirective, type RoomTaskTarget } from '../core-sdk/room/taskSteering';
import { ROOM_HANDOFF_TUTOR_INSTRUCTION, ROOM_HANDOFF_LIVE_INSTRUCTION, buildRoomTaskCatalogue } from '../../shared/prompts';
import { missingLiveInput, validateLiveInputMedia, type LiveInputMedia } from '../core-sdk/media/liveInputContext';
import { HeadlessRoomTaskStore } from './roomTaskStore';
import { HeadlessRoomTransport } from './roomTransport';

type Source = { sourceUserId: string; sourceAssistantId: string; conversationId: string };
type Scope = { nativeSession: string; accessScope: string; valid(): Promise<boolean> };
type Context = { input: TutorTextTurnInput; source: Source; scope: Scope; targets: RoomTaskTarget[]; accepts(raw: string): boolean };

/** Host adapter only: the browser's RoomTaskHandoff + provider + projection own
 * execution. No fake scene, fallback planner, or replay of persisted commands. */
export class HeadlessRoomAgent {
  readonly store: HeadlessRoomTaskStore;
  readonly tasks: RoomTaskHandoff;
  readonly usage: Array<{ stage: string; model: string; modelVersion?: string; usageMetadata: unknown }> = [];
  private contexts = new Map<string, Context>();
  private generation = 0;
  constructor(private client: HeadlessClient, private lease: () => RoomAgentLease | null, private close?: () => Promise<void>) {
    this.store = new HeadlessRoomTaskStore(join(client.profile.directory, 'room-tasks'));
    this.tasks = new RoomTaskHandoff({
      store: this.store, lease,
      ...roomTaskProvider(() => ({ aiClient: client.ai, runtime: client.runtime }), (response, model, stage) => {
        this.usage.push({ stage, model: response.modelUsed || model, modelVersion: response.modelVersion, usageMetadata: response.usageMetadata });
      }),
      changed: record => {
        this.project(record);
        client.runtime.events.emit({ operationId: record.id, journey: 'agent', phase: 'task.' + record.phase,
          data: { id: record.id, note: record.note, operationCount: record.operations.length,
            confirmedOperationCount: record.operations.filter(operation => operation.receipt?.ok === true).length } });
      },
      activity: active => client.runtime.events.emit({ operationId: 'room-agent', journey: 'agent', phase: 'activity.changed', data: { active } }),
      now: () => client.runtime.clock.now(),
    });
  }
  private project(record: RoomTaskRecord) {
    const id = record.handoff.conversationId;
    this.client.state.chats[id] = projectRoomTaskSummaries(this.client.state.chats[id] || [], [summarizeRoomTask(record)], id);
  }
  async restore() { for (const record of await this.store.list()) this.project(record); await this.client.save(); }
  private async scope(conversationId: string): Promise<Scope | null> {
    const lease = this.lease(), generation = this.generation, mode = this.client.accessMode;
    if (!lease?.valid()) return null;
    const nativeSession = lease.state().session;
    const owner = mode === 'managed' ? await this.client.credentials.getUserId() : null;
    if (mode === 'managed' && !owner) return null;
    const local = () => generation === this.generation && this.client.accessMode === mode
      && this.client.state.settings.selectedLanguagePairId === conversationId && lease.valid() && lease.state().session === nativeSession;
    return { nativeSession, accessScope: mode === 'byok' ? 'byok' : `managed:${owner}`,
      valid: async () => local() && (mode === 'byok' || await this.client.credentials.getUserId() === owner) && local() };
  }
  private async targets(conversationId: string, scope: Scope): Promise<RoomTaskTarget[]> {
    const history = this.client.state.chats[conversationId] || [];
    return (await this.store.list()).filter(record => record.handoff.conversationId === conversationId
      && record.handoff.nativeSession === scope.nativeSession && record.handoff.accessScope === scope.accessScope
      && !record.readOnly && record.directive?.action !== 'stop' && hasRoomTaskSources(history, summarizeRoomTask(record)))
      .sort((a, b) => b.startedAt - a.startedAt).slice(0, 4).map(record => ({ id: record.id, phase: record.phase,
        running: this.tasks.running(record.id), requestPreview: record.handoff.input.prompt.slice(0, 500),
        replyPreview: (record.reply?.parsed.visibleText || '').slice(0, 500) }));
  }
  private capture(context: Context) {
    const { source, input, targets } = structuredClone({ source: context.source, input: context.input, targets: context.targets });
    const scope = context.scope;
    const sourceCurrent = () => {
      const history = this.client.state.chats[source.conversationId] || [];
      return history.some(message => message.id === source.sourceUserId && message.role === 'user' && message.text === input.prompt)
        && history.some(message => message.id === source.sourceAssistantId && message.role === 'assistant');
    };
    const valid = async () => sourceCurrent() && await scope.valid() && sourceCurrent();
    this.tasks.capture({ version: 1, id: `room-task:${source.sourceAssistantId}`, ...source,
      nativeSession: scope.nativeSession, accessScope: scope.accessScope, input }, valid, targets);
    this.contexts.set(source.sourceAssistantId, { ...context, source, input, targets, scope: { ...scope, valid } });
    while (this.contexts.size > 8) this.contexts.delete(this.contexts.keys().next().value!);
  }
  async prepare(input: TutorTextTurnInput, source: Source) {
    const scope = await this.scope(source.conversationId); if (!scope) return input;
    const targets = await this.targets(source.conversationId, scope);
    const prepared = structuredClone({ ...input, systemInstruction: input.systemInstruction + ROOM_HANDOFF_TUTOR_INSTRUCTION + buildRoomTaskCatalogue(targets) });
    this.capture({ input: prepared, source, scope, targets, accepts: hasAgentHandoffProposal });
    return prepared;
  }
  async prepareLive(input: Omit<TutorTextTurnInput, 'prompt'>, conversationId: string) {
    const scope = await this.scope(conversationId); if (!scope) return null;
    const targets = await this.targets(conversationId, scope);
    const frozen = structuredClone({ ...input, systemInstruction: input.systemInstruction + ROOM_HANDOFF_LIVE_INSTRUCTION + buildRoomTaskCatalogue(targets, 'live') });
    let consumed = false;
    return { systemInstruction: frozen.systemInstruction, current: scope.valid, capture: async (source: Source, prompt: string, reply: string, media?: LiveInputMedia) => {
      if (consumed) return false; consumed = true;
      if (source.conversationId !== conversationId || !prompt.trim() || !reply.trim()) return false;
      const capturedSource = structuredClone(source);
      let liveInputMedia = missingLiveInput();
      try { if (media) { validateLiveInputMedia(media); liveInputMedia = structuredClone(media); } } catch { liveInputMedia.issue = 'invalid'; }
      if (!await scope.valid()) return false;
      this.capture({ input: { ...frozen, prompt, liveInputMedia }, source: capturedSource, scope, targets, accepts: raw => raw === reply });
      return true;
    } };
  }
  verification(assistantId: string, raw: string) {
    const context = this.contexts.get(assistantId);
    return context && this.tasks.available(assistantId) && context.accepts(raw)
      ? { agentRequest: context.input.prompt, agentTargets: structuredClone(context.targets) } : {};
  }
  async start(assistantId: string, directive?: RoomTaskDirective) {
    const context = this.contexts.get(assistantId);
    const source = context && (this.client.state.chats[context.source.conversationId] || []).find(message => message.id === assistantId);
    if (!context || !source || !context.accepts(source.llmRawResponse || '') || !await context.scope.valid()) throw new Error('The original agent handoff is no longer available.');
    if (directive && !parseRoomTaskDirective(directive, context.targets)) throw new Error('That task was not available in this request.');
    const id = `room-task:${assistantId}`;
    const monitor = setInterval(() => { void context.scope.valid().then(valid => { if (!valid) this.tasks.stop(id); }, () => this.tasks.stop(id)); }, 100);
    try { return await this.tasks.start(assistantId, directive); }
    finally { clearInterval(monitor); await this.client.save(); }
  }
  async disconnect() { this.generation++; this.contexts.clear(); await this.tasks.reset(); await this.client.save(); await this.close?.(); }
}

export async function connectHeadlessRoom(client: HeadlessClient, directory: string) {
  if (client.roomAgent) throw new Error('Disconnect the current room before connecting another.');
  const transport = await HeadlessRoomTransport.connect(directory);
  transport.setChatSource(() => { const scope = client.state.settings.selectedLanguagePairId || ''; return {scope, messages: client.state.chats[scope] || [], bookmark: client.state.settings.historyBookmarkMessageId}; });
  const agent = new HeadlessRoomAgent(client, () => transport.lease(), () => transport.close(false));
  try { await agent.restore(); client.roomAgent = agent; return { session: transport.lease().state().session, connected: true }; }
  catch (error) { await transport.close(false); throw error; }
}

/** Real provider journey; no synthetic tool decisions are accepted here. */
export async function runHeadlessRoomTurn(client: HeadlessClient, input: { text: string; languagePairId?: string; requireActions?: boolean }) {
  const agent = client.roomAgent; if (!agent) throw new Error('Connect a native room first.');
  const operationId = client.runtime.ids.create('headless-room-turn');
  const before = client.accessMode === 'managed' ? await beginManagedJourneyBilling(client, operationId) : null;
  const usageStart = agent.usage.length;
  let turn: Awaited<ReturnType<typeof runHeadlessChatTurn>> | undefined;
  let aftersteps: Awaited<ReturnType<typeof runHeadlessSuggestionAftersteps>> | undefined;
  let record: RoomTaskRecord | undefined;
  let failure: unknown;
  try {
    turn = await runHeadlessChatTurn(client, { ...input, useGoogleSearch: false });
    aftersteps = await runHeadlessSuggestionAftersteps(client, { assistantMessageId: turn.assistantMessage.id, languagePairId: input.languagePairId });
    record = await agent.store.get('room-task:' + turn.assistantMessage.id);
  } catch (error) { failure = error; }
  // Provider errors must not bypass settlement evidence. Successful earlier
  // stages may have charges even when a later stage fails before dispatch.
  let billing;
  try {
    billing = before ? evaluateManagedJourneyBilling(before, await waitForManagedJourneyBillingSettlement(client, operationId), { requirePaidUsage: !failure })
      : { applicable: false, passed: true, payer: 'byok-api-key-owner' };
  } catch (error) {
    billing = { applicable: true, passed: false, error: error instanceof Error ? error.message : String(error) };
  }
  const usage = agent.usage.slice(usageStart);
  const tokens = (metadata: any) => Number(metadata?.totalTokenCount) > 0;
  const history = turn ? client.state.chats[turn.languagePair.id] || [] : [];
  const coverage = {
    tutorStream: !!turn?.streaming.visiblyStreamed,
    verifierStream: !!aftersteps?.suggestionResult.streaming.visiblyStreamed,
    verifiedHandoff: aftersteps?.decisionSource === 'model' && aftersteps.toolRequest?.tool === 'agent',
    exactRequest: record?.handoff.input.prompt === input.text && record?.handoff.sourceUserId === turn?.userMessage?.id,
    originalHistory: (record?.handoff.input.history?.length || 0) > 0,
    nativeReceipts: !!record && record.operations.length > 0 && record.operations.every(operation => operation.receipt?.ok === true),
    completed: record?.phase === 'completed',
    replyInChat: !!record?.reply && history.some(message => message.id === record.id && message.agentTask?.phase === record.phase && message.role === 'assistant'),
    providerUsage: tokens(turn?.usageMetadata) && tokens(aftersteps?.suggestionResult.usageMetadata)
      && ['planning', 'reply'].every(stage => usage.some(item => item.stage === stage && tokens(item.usageMetadata))),
    billing: billing.passed,
  };
  if (input.requireActions === false) coverage.nativeReceipts = !!record && record.operations.every(operation => operation.receipt?.ok === true);
  const evidence = { operationId, accessMode: client.accessMode, coverage, billing, usage,
    tutor: turn ? { model: turn.modelUsed, usageMetadata: turn.usageMetadata } : null,
    verifier: aftersteps ? { model: aftersteps.suggestionResult.modelUsed, usageMetadata: aftersteps.suggestionResult.usageMetadata } : null,
    task: record ? summarizeRoomTask(record) : null };
  if (failure) throw Object.assign(new Error(failure instanceof Error ? failure.message : String(failure)), { cause: failure, evidence });
  if (Object.values(coverage).some(value => !value)) {
    throw Object.assign(new Error('Agent journey coverage failed: ' + Object.entries(coverage).filter(([, value]) => !value).map(([key]) => key).join(', ')), { evidence });
  }
  return evidence;
}
