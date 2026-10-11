// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { parseRoomTaskDirective, type RoomTaskDirective, type RoomTaskTarget } from '../../../core-sdk/room/taskSteering';
import { missingLiveInput, validateLiveInputMedia, type LiveInputMedia } from '../../../core-sdk/media/liveInputContext';
import type { TutorTextTurnInput } from '../../../core-sdk/chat/tutorTextTurn';
import { roomTaskProvider } from '../../../core-sdk/room/roomTaskProvider';
import { RoomTaskHandoff, type RoomTaskRecord } from '../../../core-sdk/room/roomTaskHandoff';
import { hasAgentHandoffProposal } from '../../../core-sdk/chat/suggestionAftersteps';
import { buildRoomTaskCatalogue, ROOM_HANDOFF_TUTOR_INSTRUCTION, ROOM_HANDOFF_LIVE_INSTRUCTION } from '../../../../shared/prompts';
import { browserClientSource } from '../../../api/gemini/browserClientSource';
import { currentRoomAgentLease } from '../../../platform/quest/roomAgentBridge';
import { useMaestroStore } from '../../../store';
import { getGeminiModels } from '../../../core-sdk/modelRegistry';
import { selectSelectedLanguagePair } from '../../../store/slices/settingsSlice';
import { loadApiKey } from '../../../core/security/apiKeyStorage';
import { loadManagedAccessSession, hasManagedSession } from '../../../core/security/managedAccessSessionStorage';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../core/config/activityTokens';
import { trackGeminiUsage } from '../../../shared/utils/costTracker';
import { safeSaveChatHistoryDB } from './chatHistory';
import { roomTaskStore } from './roomTaskStore';
import { publishRoomTaskResult } from './roomTaskResults';
import { summarizeRoomTask, hasRoomTaskSources } from '../../../core-sdk/room/roomTaskProjection';
import { isRoomTaskHidden, loadRoomTaskSummaries } from './roomTaskSummaries';

const contexts = new Map<string, { prompt: string; conversationId: string; valid: () => Promise<boolean>; acceptsReply: (raw: string) => boolean; targets: RoomTaskTarget[] }>();
const deliveryContexts = new Map<string, { valid: () => Promise<boolean> }>();
let contextGeneration = 0;
let activityCount = 0;
let activityToken: string | undefined;
const usage = (response: { modelUsed?: string; modelVersion?: string; usageMetadata?: Parameters<typeof trackGeminiUsage>[0]['usageMetadata'] }, model: string) =>
  trackGeminiUsage({ feature: 'tutor', configuredModel: response.modelUsed || model, modelVersion: response.modelVersion, usageMetadata: response.usageMetadata });

function project(record: RoomTaskRecord) {
  const state = useMaestroStore.getState();
  const summary = summarizeRoomTask(record);
  if (state.isLoadingHistory || state.settings.selectedLanguagePairId !== record.handoff.conversationId
      || !hasRoomTaskSources(state.messages, summary) || isRoomTaskHidden(record.id)) return;
  const patch = summary.message;
  if (state.messages.some(message => message.id === record.id)) state.updateMessage(record.id, patch);
  else state.addMessage({ ...patch, id: record.id });
  void safeSaveChatHistoryDB(record.handoff.conversationId, useMaestroStore.getState().messages);
  const context = deliveryContexts.get(record.id);
  if (context && roomAgentTasks.running(record.id) && ['completed', 'limited', 'failed', 'interrupted'].includes(record.phase)) {
    publishRoomTaskResult({ id: record.id, conversationId: record.handoff.conversationId, valid: context.valid });
  }
}
export const roomAgentTasks = new RoomTaskHandoff({
  store: roomTaskStore,
  lease: currentRoomAgentLease,
  ...roomTaskProvider(browserClientSource, usage),
  changed: project,
  activity: active => {
    activityCount += active ? 1 : -1;
    if (activityCount > 0 && !activityToken) activityToken = useMaestroStore.getState().addActivityToken(TOKEN_CATEGORY.AGENT, TOKEN_SUBTYPE.TASK);
    if (activityCount <= 0 && activityToken) { useMaestroStore.getState().removeActivityToken(activityToken); activityToken = undefined; }
  },
  now: Date.now,
});

type HandoffSource = { sourceUserId?: string; sourceAssistantId: string; conversationId: string | null };

/** Account and conversation identity are captured before either tutor transport.
 * Recheck after asynchronous access reads too; no credentials enter the journal. */
async function captureAccess(conversationId: string | null) {
  const generation = contextGeneration;
  const lease = currentRoomAgentLease();
  if (!lease || !conversationId) return null;
  const nativeSession = lease.state().session;
  const localCurrent = () => {
    const state = useMaestroStore.getState();
    return contextGeneration === generation && state.settings.selectedLanguagePairId === conversationId && !state.isLoadingHistory
      && lease.valid() && lease.state().session === nativeSession;
  };
  if (!localCurrent()) return null;
  const key = await loadApiKey();
  const managed = key ? null : await loadManagedAccessSession();
  if ((!key && !hasManagedSession(managed)) || !localCurrent()) return null;
  const accessScope = key ? 'byok' : `managed:${managed!.user.id}`;
  const valid = async () => {
    if (!localCurrent()) return false;
    const currentKey = await loadApiKey();
    const currentManaged = key ? null : await loadManagedAccessSession();
    return localCurrent() && (key ? currentKey === key
      : !currentKey && hasManagedSession(currentManaged) && currentManaged?.user.id === managed?.user.id);
  };
  return { nativeSession, accessScope, valid };
}

async function captureHandoff(input: TutorTextTurnInput, source: HandoffSource,
  access: NonNullable<Awaited<ReturnType<typeof captureAccess>>>, acceptsReply: (raw: string) => boolean, targets: RoomTaskTarget[], sourceReply?: string) {
  if (!source.sourceUserId || !source.conversationId) return false;
  const localCurrent = () => {
    const state = useMaestroStore.getState();
    return state.messages.some(message => message.id === source.sourceUserId && message.role === 'user' && message.text === input.prompt)
      && state.messages.some(message => message.id === source.sourceAssistantId && message.role === 'assistant'
        && (sourceReply === undefined || message.llmRawResponse === sourceReply));
  };
  const valid = async () => localCurrent() && await access.valid() && localCurrent();
  if (!await valid()) return false;
  const id = `room-task:${source.sourceAssistantId}`;
  roomAgentTasks.capture({ version: 1, id, conversationId: source.conversationId, sourceUserId: source.sourceUserId,
    sourceAssistantId: source.sourceAssistantId, nativeSession: access.nativeSession, accessScope: access.accessScope, input }, valid, targets);
  contexts.set(source.sourceAssistantId, { prompt: input.prompt, conversationId: source.conversationId, valid, acceptsReply, targets: structuredClone(targets) });
  while (contexts.size > 8) contexts.delete(contexts.keys().next().value!);
  return true;
}

async function captureTaskTargets(conversationId: string, access: NonNullable<Awaited<ReturnType<typeof captureAccess>>>): Promise<RoomTaskTarget[]> {
  const summaries = await loadRoomTaskSummaries(conversationId);
  const state = useMaestroStore.getState();
  return summaries.filter(summary => !summary.hidden && !isRoomTaskHidden(summary.id) && hasRoomTaskSources(state.messages, summary)
    && summary.taskScope?.nativeSession === access.nativeSession && summary.taskScope.accessScope === access.accessScope
    && !summary.taskScope.readOnly && !summary.taskScope.controlOnly)
    .sort((a, b) => b.message.timestamp - a.message.timestamp).slice(0, 4).map(summary => ({
      id: summary.id, phase: summary.message.agentTask!.phase, running: roomAgentTasks.running(summary.id),
      requestPreview: (state.messages.find(message => message.id === summary.sourceUserId)?.text || '').slice(0, 500),
      replyPreview: (summary.message.rawAssistantResponse || summary.message.text || '').slice(0, 500),
    }));
}

/** Capture the exact input before the ordinary text tutor request. */
export async function prepareRoomAgentHandoff(input: TutorTextTurnInput, source: HandoffSource): Promise<TutorTextTurnInput> {
  if (!source.sourceUserId || !source.conversationId) return input;
  const user = useMaestroStore.getState().messages.find(message => message.id === source.sourceUserId && message.role === 'user');
  if (!user || user.text !== input.prompt) return input;
  const access = await captureAccess(source.conversationId);
  if (!access) return input;
  const targets = await captureTaskTargets(source.conversationId, access);
  const prepared = structuredClone({ ...input, systemInstruction: input.systemInstruction + ROOM_HANDOFF_TUTOR_INSTRUCTION
    + buildRoomTaskCatalogue(targets) });
  return await captureHandoff(prepared, source, access, hasAgentHandoffProposal, targets) ? prepared : input;
}

type LiveCapture = { input: Omit<TutorTextTurnInput, 'prompt'>; conversationId: string;
  access: NonNullable<Awaited<ReturnType<typeof captureAccess>>>; targets: RoomTaskTarget[] };
const liveContexts = new Map<string, LiveCapture>();
/** Called once, immediately before the actual Live connection, after its fresh
 * history/profile/bookmark instruction has been built. No latest-chat lookup at completion. */
export async function prepareLiveRoomAgentContext(systemInstruction?: string): Promise<{ systemInstruction?: string; handoffId?: string }> {
  const state = useMaestroStore.getState(), conversationId = state.settings.selectedLanguagePairId;
  const languagePair = selectSelectedLanguagePair(state);
  if (!systemInstruction || !conversationId || !languagePair) return { systemInstruction };
  const access = await captureAccess(conversationId);
  if (!access) return { systemInstruction };
  const targets = await captureTaskTargets(conversationId, access);
  if (!await access.valid()) return { systemInstruction };
  const prepared = systemInstruction + ROOM_HANDOFF_LIVE_INSTRUCTION + buildRoomTaskCatalogue(targets, 'live');
  const handoffId = crypto.randomUUID();
  liveContexts.set(handoffId, { conversationId, access, targets, input: {
    model: getGeminiModels().text.default, systemInstruction: prepared,
    nativeLanguageCode: languagePair.nativeLanguageCode, history: [],
  } });
  while (liveContexts.size > 8) liveContexts.delete(liveContexts.keys().next().value!);
  return { systemInstruction: prepared, handoffId };
}
/** Only a locally issued connection identity can associate a completed spoken
 * turn with agent context. Capturing is not execution: the shared verifier still decides. */
export async function captureLiveRoomAgentHandoff(handoffId: string, source: HandoffSource, userText: string, modelText: string, liveInputMedia?: LiveInputMedia): Promise<boolean> {
  const captured = liveContexts.get(handoffId);
  liveContexts.delete(handoffId);
  if (!captured || captured.conversationId !== source.conversationId || !userText.trim() || !modelText.trim()) return false;
  // Bound and freeze media before the asynchronous account check or durable claim.
  // Invalid/incomplete input keeps a failure marker, never a partial media payload.
  let media = missingLiveInput();
  if (liveInputMedia) {
    if (!liveInputMedia.complete && ['limit', 'invalid', 'interrupted', 'missing'].includes(liveInputMedia.issue || '')) {
      media.issue = liveInputMedia.issue;
    } else {
      try { validateLiveInputMedia(liveInputMedia); media = structuredClone(liveInputMedia); }
      catch { media.issue = 'invalid'; }
    }
  }
  const input = { ...structuredClone(captured.input), prompt: userText, liveInputMedia: media };
  return captureHandoff(input, source, captured.access, raw => raw === modelText, captured.targets, modelText);
}
export function roomAgentRequestForVerification(assistantId: string, rawReply: string): string | undefined {
  const context = contexts.get(assistantId);
  return roomAgentTasks.available(assistantId) && context?.acceptsReply(rawReply) ? context.prompt : undefined;
}
export function roomAgentTargetsForVerification(assistantId: string, rawReply: string): RoomTaskTarget[] {
  return roomAgentRequestForVerification(assistantId, rawReply) !== undefined ? structuredClone(contexts.get(assistantId)?.targets || []) : [];
}
export async function startRoomAgentTask(sourceAssistantId: string, directive?: RoomTaskDirective): Promise<void> {
  const context = contexts.get(sourceAssistantId);
  const id = `room-task:${sourceAssistantId}`;
  let timer: ReturnType<typeof setInterval> | undefined;
  let ownsDeliveryContext = false;
  try {
    const source = useMaestroStore.getState().messages.find(message => message.id === sourceAssistantId);
    if (!context || !source || !context.acceptsReply(source.llmRawResponse || '') || !await context.valid()) throw new Error('The agent handoff is no longer available. Please ask again.');
    if (directive && !parseRoomTaskDirective(directive, context.targets)) throw new Error('The selected task was not available in this request.');
    if (!deliveryContexts.has(id)) { deliveryContexts.set(id, context); ownsDeliveryContext = true; }
    // Stop promptly on scope/native-session loss while provider work is in flight.
    const lease = currentRoomAgentLease();
    timer = setInterval(() => {
      void context.valid().then(valid => { if (!valid || !lease?.valid()) roomAgentTasks.stop(id); }, () => roomAgentTasks.stop(id));
    }, 500);
    await roomAgentTasks.start(sourceAssistantId, directive);
  } catch (error) {
    const state = useMaestroStore.getState();
    if (!isRoomTaskHidden(id) && context?.conversationId === state.settings.selectedLanguagePairId && state.messages.some(message => message.id === sourceAssistantId)) {
      const note = error instanceof Error ? error.message : 'The agent task could not start.';
      if (!state.messages.some(message => message.id === id)) {
        state.addMessage({ id, role: 'status', text: note, maestroToolKind: 'agent', agentTask: { id, phase: 'failed', note } });
        void safeSaveChatHistoryDB(context.conversationId, useMaestroStore.getState().messages);
        publishRoomTaskResult({ id, conversationId: context.conversationId, valid: context.valid });
      }
    }
  } finally { if (timer) clearInterval(timer); if (ownsDeliveryContext) deliveryContexts.delete(id); }
}
export const loadRoomAgentTask = roomTaskStore.get;

/** History replacement revokes both prepared handoffs and currently executing work. */
export async function resetRoomAgentTasks(): Promise<void> {
  contextGeneration++; contexts.clear(); liveContexts.clear(); deliveryContexts.clear();
  await roomAgentTasks.reset();
}
