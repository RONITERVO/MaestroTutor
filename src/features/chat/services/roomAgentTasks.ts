// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { TutorTextTurnInput } from '../../../core-sdk/chat/tutorTextTurn';
import { runTutorTextTurn } from '../../../core-sdk/chat/tutorTextTurn';
import { RoomTaskHandoff, type RoomTaskRecord } from '../../../core-sdk/room/roomTaskHandoff';
import { runRoomActionTask } from '../../../core-sdk/room/roomAgent';
import { hasAgentHandoffProposal } from '../../../core-sdk/chat/suggestionAftersteps';
import { buildRoomResultInstruction, buildRoomTaskReplyInstruction, ROOM_HANDOFF_TUTOR_INSTRUCTION } from '../../../../shared/prompts';
import { browserClientSource } from '../../../api/gemini/browserClientSource';
import { currentRoomAgentLease } from '../../../platform/quest/roomAgentBridge';
import { useMaestroStore } from '../../../store';
import { loadApiKey } from '../../../core/security/apiKeyStorage';
import { loadManagedAccessSession, hasManagedSession } from '../../../core/security/managedAccessSessionStorage';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../core/config/activityTokens';
import { trackGeminiUsage } from '../../../shared/utils/costTracker';
import { safeSaveChatHistoryDB } from './chatHistory';
import { roomTaskStore } from './roomTaskStore';

const contexts = new Map<string, { input: TutorTextTurnInput; conversationId: string; valid: () => Promise<boolean> }>();
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

/** Browser composition captures identities and access without persisting secrets. */
export async function prepareRoomAgentHandoff(input: TutorTextTurnInput, source: { sourceUserId?: string; sourceAssistantId: string; conversationId: string | null }): Promise<TutorTextTurnInput> {
  const lease = currentRoomAgentLease();
  if (!lease || !source.sourceUserId || !source.conversationId) return input;
  const state = useMaestroStore.getState();
  const user = state.messages.find(message => message.id === source.sourceUserId && message.role === 'user');
  if (!user || user.text !== input.prompt) return input;
  // This check reads the app's established access route. Secrets remain only in
  // the closure and are never part of the handoff record or model context.
  const key = await loadApiKey();
  const managed = key ? null : await loadManagedAccessSession();
  if (!key && !hasManagedSession(managed)) return input;
  const accessScope = key ? 'byok' : `managed:${managed!.user.id}`;
  if (!lease.valid()) return input;
  const nativeSession = lease.state().session;
  const prepared = structuredClone({ ...input, systemInstruction: input.systemInstruction + ROOM_HANDOFF_TUTOR_INSTRUCTION });
  const valid = async () => {
    const current = useMaestroStore.getState();
    if (current.settings.selectedLanguagePairId !== source.conversationId || current.isLoadingHistory
        || !current.messages.some(message => message.id === source.sourceUserId && message.text === input.prompt)
        || !current.messages.some(message => message.id === source.sourceAssistantId && message.role === 'assistant')) return false;
    const currentKey = await loadApiKey();
    if (key) return currentKey === key;
    const currentManaged = await loadManagedAccessSession();
    return !currentKey && hasManagedSession(currentManaged) && currentManaged?.user.id === managed?.user.id;
  };
  if (!await valid() || !lease.valid()) return input;
  const id = `room-task:${source.sourceAssistantId}`;
  roomAgentTasks.capture({ version: 1, id, conversationId: source.conversationId, sourceUserId: source.sourceUserId,
    sourceAssistantId: source.sourceAssistantId, nativeSession, accessScope, input: prepared }, valid);
  contexts.set(source.sourceAssistantId, { input: structuredClone(prepared), conversationId: source.conversationId, valid });
  while (contexts.size > 8) contexts.delete(contexts.keys().next().value!);
  return prepared;
}
export function roomAgentRequestForVerification(assistantId: string, rawReply: string): string | undefined {
  return roomAgentTasks.available(assistantId) && hasAgentHandoffProposal(rawReply) ? contexts.get(assistantId)?.input.prompt : undefined;
}
export async function startRoomAgentTask(sourceAssistantId: string): Promise<void> {
  const context = contexts.get(sourceAssistantId);
  const id = `room-task:${sourceAssistantId}`;
  let timer: ReturnType<typeof setInterval> | undefined;
  try {
    const source = useMaestroStore.getState().messages.find(message => message.id === sourceAssistantId);
    if (!context || !source || !hasAgentHandoffProposal(source.llmRawResponse || '') || !await context.valid()) throw new Error('The agent handoff is no longer available. Please ask again.');
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
