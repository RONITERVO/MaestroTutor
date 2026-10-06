// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { createHash } from 'node:crypto';
import type { HeadlessClient } from './client';
import { runHeadlessLiveTurn, type HeadlessLiveTurnInput } from './liveJourney';
import { validateLiveInputMedia, type LiveInputMedia } from '../core-sdk/media/liveInputContext';
import { summarizeRoomTask } from '../core-sdk/room/roomTaskProjection';
import { captureManagedJourneyBilling, evaluateManagedJourneyBilling, waitForManagedJourneyBillingSettlement } from './managedJourneyBilling';

export function liveInputHashes(media: LiveInputMedia) {
  validateLiveInputMedia(media);
  const digest = (data: string) => createHash('sha256').update(Buffer.from(data, 'base64')).digest('hex');
  return { audio: digest(media.audio!.data), pcm: createHash('sha256').update(Buffer.from(media.audio!.data, 'base64').subarray(44)).digest('hex'), samples: media.audio!.samples, packets: media.packets.length,
    frames: media.frames.map(frame => ({ sha256: digest(frame.data), atMs: frame.atMs, audioOffsetSamples: frame.audioOffsetSamples })) };
}
/** Fingerprints of actual provider request contents; no text or inline bytes are returned. */
export function providerMediaHashes(contents: unknown): Array<{ mimeType: string; sha256: string }> {
  const hashes: Array<{ mimeType: string; sha256: string }> = [];
  const visit = (value: unknown) => {
    if (Array.isArray(value)) { value.forEach(visit); return; }
    if (!value || typeof value !== 'object') return;
    const item = value as Record<string, unknown>;
    const media = item.inlineData as { mimeType?: unknown; data?: unknown } | undefined;
    if (media && typeof media.data === 'string' && typeof media.mimeType === 'string') {
      hashes.push({ mimeType: media.mimeType, sha256: createHash('sha256').update(Buffer.from(media.data, 'base64')).digest('hex') });
    }
    for (const [key, child] of Object.entries(item)) if (key !== 'inlineData') visit(child);
  };
  visit(contents);
  return hashes;
}

/** A spoken handoff must not expose the text transport's machine envelope. */
export function isSpokenRoomHandoff(text: string | undefined): boolean {
  return !!text?.trim() && !/```|\bmaestro-tool\b|["']tool["']\s*:/i.test(text);
}

/** Release evidence around the ordinary Live/observer path, not another executor. */
export async function runHeadlessRoomLiveTurn(client: HeadlessClient, input: HeadlessLiveTurnInput) {
  const agent = client.roomAgent;
  if (!agent) throw new Error('Connect a native room first.');
  if (input.mode === 'stt' || !input.expectedTranscript?.trim() || input.pace !== true) {
    throw new Error('Agent Live proof requires conversation/observer, an expected transcript and real-time pacing.');
  }
  const operationId = client.runtime.ids.create('headless-room-live');
  const before = client.accessMode === 'managed' ? await captureManagedJourneyBilling(client, operationId) : null;
  const usageStart = agent.usage.length;
  let turn: Awaited<ReturnType<typeof runHeadlessLiveTurn>> | undefined, failure: unknown;
  try { turn = await runHeadlessLiveTurn(client, { ...input, runSuggestionAftersteps: true }); }
  catch (error) { failure = error; }
  let billing;
  try {
    billing = before ? evaluateManagedJourneyBilling(before, await waitForManagedJourneyBillingSettlement(client, operationId), { requirePaidUsage: !failure })
      : { applicable: false, passed: true, payer: 'byok-api-key-owner' };
  } catch (error) { billing = { applicable: true, passed: false, error: error instanceof Error ? error.message : String(error) }; }
  const usage = agent.usage.slice(usageStart);
  const record = turn ? await agent.store.get('room-task:' + turn.assistantMessage.id) : undefined;
  let inputHashes: ReturnType<typeof liveInputHashes> | undefined;
  try { if (record?.handoff.input.liveInputMedia) inputHashes = liveInputHashes(record.handoff.input.liveInputMedia); } catch { /* incomplete evidence fails below */ }
  const tokens = (metadata: any) => Number(metadata?.totalTokenCount) > 0;
  const coverage = {
    realtime: turn?.realtimeEvidence?.required === true && turn?.realtimeEvidence?.passed === true,
    understoodSpeech: turn?.transcriptEvidence?.passed === true,
    liveOutput: turn?.inputTranscriptDeltaCount > 0 && turn?.outputTranscriptDeltaCount > 0 && turn?.modelAudioSampleCount > 0,
    spokenHandoff: isSpokenRoomHandoff(turn?.outputTranscript),
    originalContext: turn?.contextEvidence?.historyMessageCount > 0,
    verifiedHandoff: turn?.aftersteps?.decisionSource === 'model' && turn?.aftersteps?.toolRequest?.tool === 'agent',
    exactRequest: !!record && record.handoff.input.prompt === turn?.inputTranscript && record.handoff.sourceUserId === turn?.userMessage.id,
    originalAudio: !!inputHashes && inputHashes.samples === turn?.sentSamples,
    originalFrames: !!inputHashes && inputHashes.frames.length === turn?.sentVideoFrameCount && (!input.includeVisual || inputHashes.frames.length > 0),
    nativeReceipts: !!record && record.operations.length > 0 && record.operations.every(operation => operation.receipt?.ok === true),
    completed: record?.phase === 'completed',
    replyInChat: !!record?.reply && !!client.state.chats[record.handoff.conversationId]?.some(message => message.id === record.id && message.agentTask?.phase === 'completed'),
    providerUsage: tokens(turn?.providerUsageMetadata) && tokens(turn?.aftersteps?.suggestionResult?.usageMetadata)
      && ['planning', 'reply'].every(stage => usage.some(item => item.stage === stage && tokens(item.usageMetadata))),
    billing: billing.passed && turn?.managedBillingEvidence?.passed === true,
  };
  const failed = failure as { operationId?: string; code?: string; liveDiagnostics?: unknown } | undefined;
  const evidence = { operationId, accessMode: client.accessMode, mode: input.mode, coverage, billing, usage, inputHashes,
    failure: failed ? { operationId: failed.operationId, code: failed.code, liveDiagnostics: failed.liveDiagnostics } : undefined,
    live: turn || null, task: record ? summarizeRoomTask(record) : null };
  if (failure) throw Object.assign(new Error(failure instanceof Error ? failure.message : String(failure)), { cause: failure, evidence });
  if (Object.values(coverage).some(value => !value)) throw Object.assign(
    new Error('Agent Live coverage failed: ' + Object.entries(coverage).filter(([, value]) => !value).map(([key]) => key).join(', ')), { evidence });
  return evidence;
}
