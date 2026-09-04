// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import {
  LIVE_AUDIO_TOKENS_PER_SECOND,
  LIVE_VIDEO_TOKENS_PER_FRAME_LOW,
} from '../pricing/credits';
import type {
  ModalityTokenCountLike,
  UsageMetadataLike,
} from '../pricing/usage';

export interface LiveGatewayUsageCheckpoint {
  inputAudioBytes: number;
  inputVideoBytes: number;
  inputVideoFrameCount: number;
  /** User turn boundaries accepted by the gateway. */
  clientTurnBoundaryCount: number;
  outputAudioBytes: number;
  inputAudioSampleRate: number;
  outputAudioSampleRate: number;
  setupComplete: boolean;
  usefulOutput: boolean;
  providerTurnComplete: boolean;
  /** Number of completed model turns observed over this one provider socket. */
  providerTurnCompleteCount: number;
  providerMessageCount: number;
  /** Latest provider usage snapshot for each billable turn. */
  providerTurnUsage: Array<{
    turn: number;
    usageMetadata: UsageMetadataLike;
  }>;
  /** Sum of the per-turn snapshots, including re-billed retained context. */
  providerUsageMetadata: UsageMetadataLike | null;
}

export interface LiveGatewayBillableUsage {
  billable: boolean;
  source: 'none' | 'provider' | 'provider+transport' | 'transport';
  usageMetadata: UsageMetadataLike;
}

const finiteCount = (value: unknown): number => (
  typeof value === 'number' && Number.isFinite(value) && value > 0
    ? Math.floor(value)
    : 0
);

const base64ByteLength = (value: unknown): number => {
  if (typeof value !== 'string') return 0;
  const data = value.replace(/^data:[^,]*,/i, '').replace(/\s/g, '');
  if (!data || data.length % 4 !== 0 || !/^[A-Za-z0-9+/]*={0,2}$/.test(data)) return 0;
  const padding = data.endsWith('==') ? 2 : data.endsWith('=') ? 1 : 0;
  return Math.max(0, (data.length / 4) * 3 - padding);
};

const sampleRateFromMimeType = (value: unknown, fallback: number): number => {
  if (typeof value !== 'string') return fallback;
  const match = /(?:^|;)\s*rate=(\d+)/i.exec(value);
  const parsed = Number(match?.[1]);
  return Number.isFinite(parsed) && parsed > 0 ? Math.floor(parsed) : fallback;
};

const mergeDetails = (
  previous: ModalityTokenCountLike[] | undefined,
  next: ModalityTokenCountLike[] | undefined,
): ModalityTokenCountLike[] | undefined => {
  const totals = new Map<string, number>();
  for (const detail of [...(previous || []), ...(next || [])]) {
    const modality = String(detail?.modality || 'TEXT').toUpperCase();
    const count = finiteCount(detail?.tokenCount ?? detail?.tokens);
    totals.set(modality, Math.max(totals.get(modality) || 0, count));
  }
  return totals.size > 0
    ? [...totals.entries()].map(([modality, tokenCount]) => ({ modality, tokenCount }))
    : undefined;
};

const mergeProviderUsage = (
  previous: UsageMetadataLike | null,
  nextValue: unknown,
): UsageMetadataLike | null => {
  if (!nextValue || typeof nextValue !== 'object' || Array.isArray(nextValue)) return previous;
  const next = nextValue as UsageMetadataLike;
  const merged: UsageMetadataLike = {};
  const numericKeys: Array<keyof UsageMetadataLike> = [
    'promptTokenCount',
    'cachedContentTokenCount',
    'candidatesTokenCount',
    'responseTokenCount',
    'thoughtsTokenCount',
    'toolUsePromptTokenCount',
    'totalTokenCount',
  ];
  for (const key of numericKeys) {
    const count = Math.max(
      finiteCount(previous?.[key]),
      finiteCount(next[key]),
    );
    if (count > 0) Object.assign(merged, { [key]: count });
  }
  const detailKeys: Array<keyof UsageMetadataLike> = [
    'promptTokensDetails',
    'cacheTokensDetails',
    'candidatesTokensDetails',
    'responseTokensDetails',
    'toolUsePromptTokensDetails',
  ];
  for (const key of detailKeys) {
    const details = mergeDetails(
      previous?.[key] as ModalityTokenCountLike[] | undefined,
      next[key] as ModalityTokenCountLike[] | undefined,
    );
    if (details) Object.assign(merged, { [key]: details });
  }
  return Object.keys(merged).length > 0 ? merged : previous;
};

const addDetails = (
  entries: Array<ModalityTokenCountLike[] | undefined>,
): ModalityTokenCountLike[] | undefined => {
  const totals = new Map<string, number>();
  for (const details of entries) {
    for (const detail of details || []) {
      const modality = String(detail?.modality || 'TEXT').toUpperCase();
      totals.set(modality, (totals.get(modality) || 0) + finiteCount(detail?.tokenCount ?? detail?.tokens));
    }
  }
  return totals.size > 0
    ? [...totals.entries()].map(([modality, tokenCount]) => ({ modality, tokenCount }))
    : undefined;
};

const addProviderUsage = (values: UsageMetadataLike[]): UsageMetadataLike | null => {
  if (values.length === 0) return null;
  const sum: UsageMetadataLike = {};
  const numericKeys: Array<keyof UsageMetadataLike> = [
    'promptTokenCount',
    'cachedContentTokenCount',
    'thoughtsTokenCount',
    'toolUsePromptTokenCount',
    'totalTokenCount',
  ];
  for (const key of numericKeys) {
    const total = values.reduce((result, value) => result + finiteCount(value[key]), 0);
    if (total > 0) Object.assign(sum, { [key]: total });
  }
  const outputTotal = values.reduce((result, value) => (
    result + finiteCount(value.candidatesTokenCount ?? value.responseTokenCount)
  ), 0);
  if (outputTotal > 0) sum.responseTokenCount = outputTotal;
  const detailKeys: Array<keyof UsageMetadataLike> = [
    'promptTokensDetails',
    'cacheTokensDetails',
    'toolUsePromptTokensDetails',
  ];
  for (const key of detailKeys) {
    const details = addDetails(values.map(value => (
      value[key] as ModalityTokenCountLike[] | undefined
    )));
    if (details) Object.assign(sum, { [key]: details });
  }
  const outputDetails = addDetails(values.map(value => (
    value.candidatesTokensDetails ?? value.responseTokensDetails
  )));
  if (outputDetails) sum.responseTokensDetails = outputDetails;
  return Object.keys(sum).length > 0 ? sum : null;
};

const mergeTurnUsage = (
  previous: LiveGatewayUsageCheckpoint['providerTurnUsage'] | undefined,
  next: LiveGatewayUsageCheckpoint['providerTurnUsage'] | undefined,
): LiveGatewayUsageCheckpoint['providerTurnUsage'] => {
  const byTurn = new Map<number, UsageMetadataLike>();
  for (const entry of [...(previous || []), ...(next || [])]) {
    const turn = finiteCount(entry?.turn);
    if (turn <= 0 || !entry?.usageMetadata) continue;
    byTurn.set(turn, mergeProviderUsage(byTurn.get(turn) || null, entry.usageMetadata) || {});
  }
  return [...byTurn.entries()]
    .sort(([left], [right]) => left - right)
    .map(([turn, usageMetadata]) => ({ turn, usageMetadata }));
};

export const mergeLiveProviderTurnUsage = mergeTurnUsage;

export const sumLiveProviderTurnUsage = (
  entries: LiveGatewayUsageCheckpoint['providerTurnUsage'],
): UsageMetadataLike | null => addProviderUsage(entries.map(entry => entry.usageMetadata));

export const mergeLiveGatewayUsageCheckpoints = (
  previous: LiveGatewayUsageCheckpoint,
  next: LiveGatewayUsageCheckpoint,
): LiveGatewayUsageCheckpoint => {
  const providerTurnUsage = mergeTurnUsage(previous.providerTurnUsage, next.providerTurnUsage);
  return {
    inputAudioBytes: Math.max(previous.inputAudioBytes, next.inputAudioBytes),
    inputVideoBytes: Math.max(previous.inputVideoBytes || 0, next.inputVideoBytes || 0),
    inputVideoFrameCount: Math.max(previous.inputVideoFrameCount || 0, next.inputVideoFrameCount || 0),
    clientTurnBoundaryCount: Math.max(previous.clientTurnBoundaryCount || 0, next.clientTurnBoundaryCount || 0),
    outputAudioBytes: Math.max(previous.outputAudioBytes, next.outputAudioBytes),
    inputAudioSampleRate: next.inputAudioSampleRate || previous.inputAudioSampleRate,
    outputAudioSampleRate: next.outputAudioSampleRate || previous.outputAudioSampleRate,
    setupComplete: previous.setupComplete || next.setupComplete,
    usefulOutput: previous.usefulOutput || next.usefulOutput,
    providerTurnComplete: previous.providerTurnComplete || next.providerTurnComplete,
    providerTurnCompleteCount: Math.max(
      previous.providerTurnCompleteCount || 0,
      next.providerTurnCompleteCount || 0,
    ),
    providerMessageCount: Math.max(previous.providerMessageCount, next.providerMessageCount),
    providerTurnUsage,
    providerUsageMetadata: providerTurnUsage.length > 0
      ? addProviderUsage(providerTurnUsage.map(entry => entry.usageMetadata))
      : mergeProviderUsage(previous.providerUsageMetadata, next.providerUsageMetadata),
  };
};

export const createLiveGatewayUsageCheckpoint = (): LiveGatewayUsageCheckpoint => ({
  inputAudioBytes: 0,
  inputVideoBytes: 0,
  inputVideoFrameCount: 0,
  clientTurnBoundaryCount: 0,
  outputAudioBytes: 0,
  inputAudioSampleRate: 16_000,
  outputAudioSampleRate: 24_000,
  setupComplete: false,
  usefulOutput: false,
  providerTurnComplete: false,
  providerTurnCompleteCount: 0,
  providerMessageCount: 0,
  providerTurnUsage: [],
  providerUsageMetadata: null,
});

export const observeLiveGatewayClientMessage = (
  checkpoint: LiveGatewayUsageCheckpoint,
  messageValue: unknown,
): LiveGatewayUsageCheckpoint => {
  if (!messageValue || typeof messageValue !== 'object' || Array.isArray(messageValue)) return checkpoint;
  const message = messageValue as {
    audio?: { data?: unknown; mimeType?: unknown };
    video?: { data?: unknown };
    audioStreamEnd?: unknown;
  };
  const audioBytes = base64ByteLength(message.audio?.data);
  const videoBytes = base64ByteLength(message.video?.data);
  return {
    ...checkpoint,
    inputAudioBytes: checkpoint.inputAudioBytes + audioBytes,
    inputVideoBytes: (checkpoint.inputVideoBytes || 0) + videoBytes,
    inputVideoFrameCount: (checkpoint.inputVideoFrameCount || 0) + (videoBytes > 0 ? 1 : 0),
    clientTurnBoundaryCount: (checkpoint.clientTurnBoundaryCount || 0)
      + (message.audioStreamEnd ? 1 : 0),
    inputAudioSampleRate: audioBytes > 0
      ? sampleRateFromMimeType(message.audio?.mimeType, checkpoint.inputAudioSampleRate)
      : checkpoint.inputAudioSampleRate,
  };
};

export const observeLiveGatewayProviderMessage = (
  checkpoint: LiveGatewayUsageCheckpoint,
  messageValue: unknown,
): LiveGatewayUsageCheckpoint => {
  if (!messageValue || typeof messageValue !== 'object' || Array.isArray(messageValue)) return checkpoint;
  const message = messageValue as {
    setupComplete?: unknown;
    usageMetadata?: unknown;
    serverContent?: {
      turnComplete?: unknown;
      outputTranscription?: { text?: unknown };
      modelTurn?: { parts?: unknown };
    };
  };
  const parts = Array.isArray(message.serverContent?.modelTurn?.parts)
    ? message.serverContent!.modelTurn!.parts as Array<{
        text?: unknown;
        inlineData?: { data?: unknown; mimeType?: unknown };
      }>
    : [];
  let outputAudioBytes = checkpoint.outputAudioBytes;
  let outputAudioSampleRate = checkpoint.outputAudioSampleRate;
  let usefulOutput = checkpoint.usefulOutput
    || (typeof message.serverContent?.outputTranscription?.text === 'string'
      && Boolean(message.serverContent.outputTranscription.text.trim()));
  for (const part of parts) {
    if (typeof part?.text === 'string' && part.text.trim()) usefulOutput = true;
    if (typeof part?.inlineData?.mimeType !== 'string' || !part.inlineData.mimeType.startsWith('audio/')) continue;
    const bytes = base64ByteLength(part.inlineData.data);
    if (bytes <= 0) continue;
    usefulOutput = true;
    outputAudioBytes += bytes;
    outputAudioSampleRate = sampleRateFromMimeType(part.inlineData.mimeType, outputAudioSampleRate);
  }
  const turnComplete = Boolean(message.serverContent?.turnComplete);
  let providerTurnUsage = checkpoint.providerTurnUsage || [];
  if (message.usageMetadata && typeof message.usageMetadata === 'object') {
    const providerTurn = turnComplete
      ? checkpoint.providerTurnCompleteCount + 1
      : checkpoint.clientTurnBoundaryCount <= checkpoint.providerTurnCompleteCount
        ? Math.max(1, checkpoint.providerTurnCompleteCount)
        : checkpoint.providerTurnCompleteCount + 1;
    providerTurnUsage = mergeTurnUsage(providerTurnUsage, [{
      turn: providerTurn,
      usageMetadata: message.usageMetadata as UsageMetadataLike,
    }]);
  }
  return {
    ...checkpoint,
    outputAudioBytes,
    outputAudioSampleRate,
    setupComplete: checkpoint.setupComplete || Boolean(message.setupComplete),
    usefulOutput,
    providerTurnComplete: checkpoint.providerTurnComplete || turnComplete,
    providerTurnCompleteCount: checkpoint.providerTurnCompleteCount + (turnComplete ? 1 : 0),
    providerMessageCount: checkpoint.providerMessageCount + 1,
    providerTurnUsage,
    providerUsageMetadata: providerTurnUsage.length > 0
      ? addProviderUsage(providerTurnUsage.map(entry => entry.usageMetadata))
      : checkpoint.providerUsageMetadata,
  };
};

const audioTokensForBytes = (bytes: number, sampleRate: number): number => {
  if (bytes <= 0 || sampleRate <= 0) return 0;
  const samples = bytes / 2;
  return Math.ceil((samples / sampleRate) * LIVE_AUDIO_TOKENS_PER_SECOND);
};

const transportUsage = (checkpoint: LiveGatewayUsageCheckpoint): UsageMetadataLike => {
  const inputAudioTokens = audioTokensForBytes(
    checkpoint.inputAudioBytes,
    checkpoint.inputAudioSampleRate,
  );
  const outputAudioTokens = audioTokensForBytes(
    checkpoint.outputAudioBytes,
    checkpoint.outputAudioSampleRate,
  );
  return {
    promptTokenCount: inputAudioTokens,
    responseTokenCount: outputAudioTokens,
    totalTokenCount: inputAudioTokens + outputAudioTokens,
    ...(inputAudioTokens > 0
      ? { promptTokensDetails: [{ modality: 'AUDIO', tokenCount: inputAudioTokens }] }
      : {}),
    ...(outputAudioTokens > 0
      ? { responseTokensDetails: [{ modality: 'AUDIO', tokenCount: outputAudioTokens }] }
      : {}),
  };
};

const detailsTotal = (details: ModalityTokenCountLike[] | undefined): number => (
  (details || []).reduce((total, detail) => total + finiteCount(detail.tokenCount ?? detail.tokens), 0)
);

const addTransportBreakdown = (
  provider: UsageMetadataLike,
  transport: UsageMetadataLike,
  checkpoint: LiveGatewayUsageCheckpoint,
): UsageMetadataLike => {
  const result: UsageMetadataLike = { ...provider };
  const providerInput = finiteCount(provider.promptTokenCount);
  const providerOutput = finiteCount(provider.responseTokenCount ?? provider.candidatesTokenCount);
  const transportInput = finiteCount(transport.promptTokenCount);
  const transportOutput = finiteCount(transport.responseTokenCount);
  if (providerInput === 0 && transportInput > 0) {
    result.promptTokenCount = transportInput;
  }
  if (providerOutput === 0 && transportOutput > 0) {
    result.responseTokenCount = transportOutput;
    result.responseTokensDetails = [{ modality: 'AUDIO', tokenCount: transportOutput }];
  } else if (providerOutput > 0 && detailsTotal(provider.responseTokensDetails ?? provider.candidatesTokensDetails) === 0) {
    const audio = Math.min(providerOutput, finiteCount(transport.responseTokenCount));
    result.responseTokensDetails = [
      ...(audio > 0 ? [{ modality: 'AUDIO', tokenCount: audio }] : []),
      ...(providerOutput > audio ? [{ modality: 'TEXT', tokenCount: providerOutput - audio }] : []),
    ];
  }
  let mergedInput = finiteCount(result.promptTokenCount);
  const mergedOutput = finiteCount(result.responseTokenCount ?? result.candidatesTokenCount);
  let minimumTotal = mergedInput
    + mergedOutput
    + finiteCount(result.thoughtsTokenCount)
    + finiteCount(result.toolUsePromptTokenCount);
  const providerTotal = finiteCount(provider.totalTokenCount);
  if (providerTotal > minimumTotal) {
    // Some periodic Live usage messages expose only a total. Preserve the
    // unexplained remainder as input so paid provider tokens are not dropped.
    mergedInput += providerTotal - minimumTotal;
    result.promptTokenCount = mergedInput;
    minimumTotal = providerTotal;
  }
  if (mergedInput > 0 && detailsTotal(provider.promptTokensDetails) === 0) {
    const billedTurns = Math.max(
      1,
      checkpoint.providerTurnUsage?.length || 0,
      checkpoint.providerTurnCompleteCount || 0,
    );
    // Retained user and model audio can both re-enter the paid prompt on later
    // turns. Camera frames use the enforced low-resolution token allocation.
    const audio = Math.min(
      mergedInput,
      transportInput * billedTurns + transportOutput * Math.max(0, billedTurns - 1),
    );
    const video = Math.min(
      mergedInput - audio,
      (checkpoint.inputVideoFrameCount || 0) * LIVE_VIDEO_TOKENS_PER_FRAME_LOW * billedTurns,
    );
    result.promptTokensDetails = [
      ...(audio > 0 ? [{ modality: 'AUDIO', tokenCount: audio }] : []),
      ...(video > 0 ? [{ modality: 'VIDEO', tokenCount: video }] : []),
      ...(mergedInput > audio + video
        ? [{ modality: 'TEXT', tokenCount: mergedInput - audio - video }]
        : []),
    ];
  }
  if (minimumTotal > finiteCount(result.totalTokenCount)) result.totalTokenCount = minimumTotal;
  return result;
};

export const getLiveGatewayBillableUsage = (
  checkpoint: LiveGatewayUsageCheckpoint,
): LiveGatewayBillableUsage => {
  if (!checkpoint.usefulOutput) {
    return { billable: false, source: 'none', usageMetadata: {} };
  }
  const observedTransport = transportUsage(checkpoint);
  const provider = checkpoint.providerUsageMetadata;
  const providerTokens = finiteCount(provider?.totalTokenCount)
    || finiteCount(provider?.promptTokenCount) + finiteCount(provider?.responseTokenCount);
  if (provider && providerTokens > 0) {
    const needsTransportBreakdown = (
      detailsTotal(provider.promptTokensDetails) === 0
      || detailsTotal(provider.responseTokensDetails ?? provider.candidatesTokensDetails) === 0
    );
    return {
      billable: true,
      source: needsTransportBreakdown ? 'provider+transport' : 'provider',
      usageMetadata: addTransportBreakdown(provider, observedTransport, checkpoint),
    };
  }
  return {
    billable: true,
    source: 'transport',
    usageMetadata: observedTransport,
  };
};
