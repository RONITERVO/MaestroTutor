// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { LIVE_AUDIO_TOKENS_PER_SECOND } from '../pricing/credits';
import type {
  ModalityTokenCountLike,
  UsageMetadataLike,
} from '../pricing/usage';

export interface LiveGatewayUsageCheckpoint {
  inputAudioBytes: number;
  outputAudioBytes: number;
  inputAudioSampleRate: number;
  outputAudioSampleRate: number;
  setupComplete: boolean;
  usefulOutput: boolean;
  providerTurnComplete: boolean;
  providerMessageCount: number;
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

export const mergeLiveGatewayUsageCheckpoints = (
  previous: LiveGatewayUsageCheckpoint,
  next: LiveGatewayUsageCheckpoint,
): LiveGatewayUsageCheckpoint => ({
  inputAudioBytes: Math.max(previous.inputAudioBytes, next.inputAudioBytes),
  outputAudioBytes: Math.max(previous.outputAudioBytes, next.outputAudioBytes),
  inputAudioSampleRate: next.inputAudioSampleRate || previous.inputAudioSampleRate,
  outputAudioSampleRate: next.outputAudioSampleRate || previous.outputAudioSampleRate,
  setupComplete: previous.setupComplete || next.setupComplete,
  usefulOutput: previous.usefulOutput || next.usefulOutput,
  providerTurnComplete: previous.providerTurnComplete || next.providerTurnComplete,
  providerMessageCount: Math.max(previous.providerMessageCount, next.providerMessageCount),
  providerUsageMetadata: mergeProviderUsage(
    previous.providerUsageMetadata,
    next.providerUsageMetadata,
  ),
});

export const createLiveGatewayUsageCheckpoint = (): LiveGatewayUsageCheckpoint => ({
  inputAudioBytes: 0,
  outputAudioBytes: 0,
  inputAudioSampleRate: 16_000,
  outputAudioSampleRate: 24_000,
  setupComplete: false,
  usefulOutput: false,
  providerTurnComplete: false,
  providerMessageCount: 0,
  providerUsageMetadata: null,
});

export const observeLiveGatewayClientMessage = (
  checkpoint: LiveGatewayUsageCheckpoint,
  messageValue: unknown,
): LiveGatewayUsageCheckpoint => {
  if (!messageValue || typeof messageValue !== 'object' || Array.isArray(messageValue)) return checkpoint;
  const message = messageValue as { audio?: { data?: unknown; mimeType?: unknown } };
  const bytes = base64ByteLength(message.audio?.data);
  if (bytes <= 0) return checkpoint;
  return {
    ...checkpoint,
    inputAudioBytes: checkpoint.inputAudioBytes + bytes,
    inputAudioSampleRate: sampleRateFromMimeType(
      message.audio?.mimeType,
      checkpoint.inputAudioSampleRate,
    ),
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
  return {
    ...checkpoint,
    outputAudioBytes,
    outputAudioSampleRate,
    setupComplete: checkpoint.setupComplete || Boolean(message.setupComplete),
    usefulOutput,
    providerTurnComplete: checkpoint.providerTurnComplete || Boolean(message.serverContent?.turnComplete),
    providerMessageCount: checkpoint.providerMessageCount + 1,
    providerUsageMetadata: mergeProviderUsage(
      checkpoint.providerUsageMetadata,
      message.usageMetadata,
    ),
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
): UsageMetadataLike => {
  const result: UsageMetadataLike = { ...provider };
  const providerInput = finiteCount(provider.promptTokenCount);
  const providerOutput = finiteCount(provider.responseTokenCount ?? provider.candidatesTokenCount);
  const transportInput = finiteCount(transport.promptTokenCount);
  const transportOutput = finiteCount(transport.responseTokenCount);
  if (providerInput === 0 && transportInput > 0) {
    result.promptTokenCount = transportInput;
    result.promptTokensDetails = [{ modality: 'AUDIO', tokenCount: transportInput }];
  } else if (providerInput > 0 && detailsTotal(provider.promptTokensDetails) === 0) {
    const audio = Math.min(providerInput, finiteCount(transport.promptTokenCount));
    result.promptTokensDetails = [
      ...(audio > 0 ? [{ modality: 'AUDIO', tokenCount: audio }] : []),
      ...(providerInput > audio ? [{ modality: 'TEXT', tokenCount: providerInput - audio }] : []),
    ];
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
    // Some periodic Live usage messages expose only a total. Attribute the
    // unexplained remainder to text input (the lower of the relevant rates)
    // instead of silently treating paid provider tokens as free.
    mergedInput += providerTotal - minimumTotal;
    result.promptTokenCount = mergedInput;
    minimumTotal = providerTotal;
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
      usageMetadata: addTransportBreakdown(provider, observedTransport),
    };
  }
  return {
    billable: true,
    source: 'transport',
    usageMetadata: observedTransport,
  };
};
