// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { missingLiveInput, validateLiveInputMedia, type LiveInputMedia } from '../../../core-sdk/media/liveInputContext';
import type { TutorTextTurnInput } from '../../../core-sdk/chat/tutorTextTurn';
import { runTutorTextTurn } from '../../../core-sdk/chat/tutorTextTurn';
import { RoomTaskHandoff, type RoomTaskRecord } from '../../../core-sdk/room/roomTaskHandoff';
import { runRoomActionTask } from '../../../core-sdk/room/roomAgent';
import { hasAgentHandoffProposal } from '../../../core-sdk/chat/suggestionAftersteps';
import { buildRoomResultInstruction, buildRoomTaskReplyInstruction, ROOM_HANDOFF_TUTOR_INSTRUCTION, ROOM_HANDOFF_LIVE_INSTRUCTION } from '../../../../shared/prompts';
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

const contexts = new Map<string, { prompt: string; conversationId: string; valid: () => Promise<boolean>; acceptsReply: (raw: string) => boolean }>();
let activityCount = 0;
let activityToken: string | undefined;
const usage = (response: { modelUsed?: string; modelVersion?: string; usageMetadata?: Parameters<typeof trackGeminiUsage>[0]['usageMetadata'] }, model: string) =>
  trackGeminiUsage({ feature: 'tutor', configuredModel: response.modelUsed || model, modelVersion: response.modelVersion, usageMetadata: response.usageMetadata });

function project(record: RoomTaskRecord) {
  const state = useMaestroStore.getState();
  if (state.settings.selectedLanguagePairId !== record.handoff.conversationId
      || !state.messages.some(message => message.id === record.handoff.sourceAssistantId)) return;
  const parsed = record.reply?.parsed;
  const patch = {
    role: parsed ? 'assistant' as const : 'status' as const,
    text: parsed ? (parsed.translations.length ? undefined : parsed.visibleText) : record.note,
    translations: parsed?.translations.length ? parsed.translations : undefined,
    rawAssistantResponse: parsed?.visibleText,
    // Persist only the language reply in normal chat, never plans/full scene receipts.
    llmRawResponse: parsed ? parsed.visibleText : undefined,
    maestroToolKind: 'agent' as const,
    agentTask: { id: record.id, phase: record.phase, note: record.note },
  };
  if (state.messages.some(message => message.id === record.id)) state.updateMessage(record.id, patch);
  else state.addMessage({ ...patch, id: record.id });
  void safeSaveChatHistoryDB(record.handoff.conversationId, useMaestroStore.getState().messages);
}
export const roomAgentTasks = new RoomTaskHandoff({
  store: roomTaskStore,
  lease: currentRoomAgentLease,
  run: (input, lease, control) => runRoomActionTask(input, browserClientSource(), lease, response => usage(response, input.model), control),
  reply: async (input, result, signal) => {
    const turn = await runTutorTextTurn({ ...input,
      systemInstruction: input.systemInstruction + '\n\n' + buildRoomResultInstruction(result.receipts, result.scene) + buildRoomTaskReplyInstruction(result.budgetExhausted),
      configOverrides: { maxOutputTokens: 2048 },
    }, { ...browserClientSource(), signal });
    usage(turn.response, input.model);
    return { parsed: turn.parsed, rawResponse: turn.parsed.visibleText };
  },
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
  const lease = currentRoomAgentLease();
  if (!lease || !conversationId) return null;
  const nativeSession = lease.state().session;
  const localCurrent = () => {
    const state = useMaestroStore.getState();
    return state.settings.selectedLanguagePairId === conversationId && !state.isLoadingHistory
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
  access: NonNullable<Awaited<ReturnType<typeof captureAccess>>>, acceptsReply: (raw: string) => boolean, sourceReply?: string) {
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
    sourceAssistantId: source.sourceAssistantId, nativeSession: access.nativeSession, accessScope: access.accessScope, input }, valid);
  contexts.set(source.sourceAssistantId, { prompt: input.prompt, conversationId: source.conversationId, valid, acceptsReply });
  while (contexts.size > 8) contexts.delete(contexts.keys().next().value!);
  return true;
}

/** Capture the exact input before the ordinary text tutor request. */
export async function prepareRoomAgentHandoff(input: TutorTextTurnInput, source: HandoffSource): Promise<TutorTextTurnInput> {
  if (!source.sourceUserId || !source.conversationId) return input;
  const user = useMaestroStore.getState().messages.find(message => message.id === source.sourceUserId && message.role === 'user');
  if (!user || user.text !== input.prompt) return input;
  const access = await captureAccess(source.conversationId);
  if (!access) return input;
  const prepared = structuredClone({ ...input, systemInstruction: input.systemInstruction + ROOM_HANDOFF_TUTOR_INSTRUCTION });
  return await captureHandoff(prepared, source, access, hasAgentHandoffProposal) ? prepared : input;
}

type LiveCapture = { input: Omit<TutorTextTurnInput, 'prompt'>; conversationId: string;
  access: NonNullable<Awaited<ReturnType<typeof captureAccess>>> };
const liveContexts = new Map<string, LiveCapture>();
/** Called once, immediately before the actual Live connection, after its fresh
 * history/profile/bookmark instruction has been built. No latest-chat lookup at completion. */
export async function prepareLiveRoomAgentContext(systemInstruction?: string): Promise<{ systemInstruction?: string; handoffId?: string }> {
  const state = useMaestroStore.getState(), conversationId = state.settings.selectedLanguagePairId;
  const languagePair = selectSelectedLanguagePair(state);
  if (!systemInstruction || !conversationId || !languagePair) return { systemInstruction };
  const access = await captureAccess(conversationId);
  if (!access) return { systemInstruction };
  const prepared = systemInstruction + ROOM_HANDOFF_LIVE_INSTRUCTION;
  const handoffId = crypto.randomUUID();
  liveContexts.set(handoffId, { conversationId, access, input: {
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
  return captureHandoff(input, source, captured.access, raw => raw === modelText, modelText);
}
export function roomAgentRequestForVerification(assistantId: string, rawReply: string): string | undefined {
  const context = contexts.get(assistantId);
  return roomAgentTasks.available(assistantId) && context?.acceptsReply(rawReply) ? context.prompt : undefined;
}
export async function startRoomAgentTask(sourceAssistantId: string): Promise<void> {
  const context = contexts.get(sourceAssistantId);
  const id = `room-task:${sourceAssistantId}`;
  let timer: ReturnType<typeof setInterval> | undefined;
  try {
    const source = useMaestroStore.getState().messages.find(message => message.id === sourceAssistantId);
    if (!context || !source || !context.acceptsReply(source.llmRawResponse || '') || !await context.valid()) throw new Error('The agent handoff is no longer available. Please ask again.');
    // Stop promptly on scope/native-session loss while provider work is in flight.
    const lease = currentRoomAgentLease();
    timer = setInterval(() => {
      void context.valid().then(valid => { if (!valid || !lease?.valid()) roomAgentTasks.stop(id); }, () => roomAgentTasks.stop(id));
    }, 500);
    await roomAgentTasks.start(sourceAssistantId);
  } catch (error) {
    const state = useMaestroStore.getState();
    if (context?.conversationId === state.settings.selectedLanguagePairId && state.messages.some(message => message.id === sourceAssistantId)) {
      const note = error instanceof Error ? error.message : 'The agent task could not start.';
      if (!state.messages.some(message => message.id === id)) {
        state.addMessage({ id, role: 'status', text: note, maestroToolKind: 'agent', agentTask: { id, phase: 'failed', note } });
        void safeSaveChatHistoryDB(context.conversationId, useMaestroStore.getState().messages);
      }
    }
  } finally { if (timer) clearInterval(timer); }
}
export const loadRoomAgentTask = roomTaskStore.get;
