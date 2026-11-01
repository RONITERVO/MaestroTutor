// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import {
  createLiveGatewayUsageCheckpoint,
  getLiveGatewayBillableUsage,
  mergeLiveGatewayUsageCheckpoints,
  observeLiveGatewayClientMessage,
  observeLiveGatewayProviderMessage,
  observeLiveGatewayToolResponse,
} from './liveGateway';

const pcmBase64 = (bytes: number): string => Buffer.alloc(bytes).toString('base64');

describe('managed Live gateway usage evidence', () => {
  it('releases a setup-only timeout even when the client sent audio', () => {
    let checkpoint = createLiveGatewayUsageCheckpoint();
    checkpoint = observeLiveGatewayClientMessage(checkpoint, {
      audio: { data: pcmBase64(32_000), mimeType: 'audio/pcm;rate=16000' },
    });
    checkpoint = observeLiveGatewayProviderMessage(checkpoint, { setupComplete: {} });

    expect(checkpoint).toMatchObject({
      inputAudioBytes: 32_000,
      setupComplete: true,
      usefulOutput: false,
      providerTurnCompleteCount: 0,
      providerMessageCount: 1,
    });
    expect(getLiveGatewayBillableUsage(checkpoint)).toEqual({
      billable: false,
      source: 'none',
      usageMetadata: {},
    });
  });

  it('prices successful audio from periodic provider usage metadata', () => {
    let checkpoint = createLiveGatewayUsageCheckpoint();
    checkpoint = observeLiveGatewayProviderMessage(checkpoint, {
      usageMetadata: {
        promptTokenCount: 50,
        responseTokenCount: 20,
        totalTokenCount: 70,
        promptTokensDetails: [{ modality: 'AUDIO', tokenCount: 40 }, { modality: 'TEXT', tokenCount: 10 }],
        responseTokensDetails: [{ modality: 'AUDIO', tokenCount: 20 }],
      },
    });
    checkpoint = observeLiveGatewayProviderMessage(checkpoint, {
      serverContent: {
        modelTurn: { parts: [{ inlineData: { data: pcmBase64(4_800), mimeType: 'audio/pcm;rate=24000' } }] },
        turnComplete: true,
      },
      usageMetadata: {
        promptTokenCount: 80,
        responseTokenCount: 30,
        totalTokenCount: 110,
        promptTokensDetails: [{ modality: 'AUDIO', tokenCount: 70 }, { modality: 'TEXT', tokenCount: 10 }],
        responseTokensDetails: [{ modality: 'AUDIO', tokenCount: 30 }],
      },
    });

    expect(getLiveGatewayBillableUsage(checkpoint)).toEqual({
      billable: true,
      source: 'provider',
      usageMetadata: expect.objectContaining({
        promptTokenCount: 80,
        responseTokenCount: 30,
        totalTokenCount: 110,
        promptTokensDetails: [
          { modality: 'AUDIO', tokenCount: 70 },
          { modality: 'TEXT', tokenCount: 10 },
        ],
        responseTokensDetails: [{ modality: 'AUDIO', tokenCount: 30 }],
      }),
    });
    expect(checkpoint.providerTurnCompleteCount).toBe(1);
  });

  it('counts every completed turn and sums re-billed context on one provider socket', () => {
    let checkpoint = createLiveGatewayUsageCheckpoint();
    for (let turn = 1; turn <= 6; turn += 1) {
      checkpoint = observeLiveGatewayProviderMessage(checkpoint, {
        serverContent: { turnComplete: true },
        usageMetadata: { totalTokenCount: turn * 100 },
      });
    }

    expect(checkpoint.providerTurnComplete).toBe(true);
    expect(checkpoint.providerTurnCompleteCount).toBe(6);
    expect(checkpoint.providerTurnUsage).toHaveLength(6);
    expect(checkpoint.providerUsageMetadata?.totalTokenCount).toBe(2_100);
  });

  it('associates provider usage sent after turn-complete with the completed turn', () => {
    let checkpoint = createLiveGatewayUsageCheckpoint();
    checkpoint = observeLiveGatewayClientMessage(checkpoint, { audioStreamEnd: true });
    checkpoint = observeLiveGatewayProviderMessage(checkpoint, {
      serverContent: { turnComplete: true },
    });
    checkpoint = observeLiveGatewayProviderMessage(checkpoint, {
      usageMetadata: { promptTokenCount: 80, responseTokenCount: 20, totalTokenCount: 100 },
    });

    expect(checkpoint.providerTurnUsage).toEqual([{
      turn: 1,
      usageMetadata: expect.objectContaining({ totalTokenCount: 100 }),
    }]);
    expect(checkpoint.providerUsageMetadata?.totalTokenCount).toBe(100);
  });

  it('falls back to server-observed PCM duration when a useful response has no usage metadata', () => {
    let checkpoint = createLiveGatewayUsageCheckpoint();
    checkpoint = observeLiveGatewayClientMessage(checkpoint, {
      audio: { data: pcmBase64(64_000), mimeType: 'audio/pcm;rate=16000' },
    });
    checkpoint = observeLiveGatewayProviderMessage(checkpoint, {
      serverContent: {
        modelTurn: { parts: [{ inlineData: { data: pcmBase64(48_000), mimeType: 'audio/pcm;rate=24000' } }] },
      },
    });

    expect(getLiveGatewayBillableUsage(checkpoint)).toEqual({
      billable: true,
      source: 'transport',
      usageMetadata: {
        promptTokenCount: 64,
        responseTokenCount: 32,
        totalTokenCount: 96,
        promptTokensDetails: [{ modality: 'AUDIO', tokenCount: 64 }],
        responseTokensDetails: [{ modality: 'AUDIO', tokenCount: 32 }],
      },
    });
  });

  it('uses transport evidence to classify provider counts that omit modality details', () => {
    let checkpoint = createLiveGatewayUsageCheckpoint();
    checkpoint = observeLiveGatewayClientMessage(checkpoint, {
      audio: { data: pcmBase64(32_000), mimeType: 'audio/pcm;rate=16000' },
    });
    checkpoint = observeLiveGatewayProviderMessage(checkpoint, {
      serverContent: { outputTranscription: { text: 'heard' } },
      usageMetadata: { promptTokenCount: 42, responseTokenCount: 8, totalTokenCount: 50 },
    });

    expect(getLiveGatewayBillableUsage(checkpoint)).toMatchObject({
      billable: true,
      source: 'provider+transport',
      usageMetadata: {
        promptTokensDetails: [
          { modality: 'AUDIO', tokenCount: 32 },
          { modality: 'TEXT', tokenCount: 10 },
        ],
        responseTokensDetails: [{ modality: 'TEXT', tokenCount: 8 }],
      },
    });
  });

  it('fills a missing periodic provider output count from observed audio', () => {
    let checkpoint = createLiveGatewayUsageCheckpoint();
    checkpoint = observeLiveGatewayProviderMessage(checkpoint, {
      usageMetadata: {
        promptTokenCount: 42,
        totalTokenCount: 42,
        promptTokensDetails: [{ modality: 'TEXT', tokenCount: 10 }, { modality: 'AUDIO', tokenCount: 32 }],
      },
    });
    checkpoint = observeLiveGatewayProviderMessage(checkpoint, {
      serverContent: {
        modelTurn: {
          parts: [{ inlineData: { data: pcmBase64(48_000), mimeType: 'audio/pcm;rate=24000' } }],
        },
      },
    });

    expect(getLiveGatewayBillableUsage(checkpoint)).toEqual({
      billable: true,
      source: 'provider+transport',
      usageMetadata: expect.objectContaining({
        promptTokenCount: 42,
        responseTokenCount: 32,
        totalTokenCount: 74,
        responseTokensDetails: [{ modality: 'AUDIO', tokenCount: 32 }],
      }),
    });
  });

  it('does not discard provider totals when a periodic message omits categories', () => {
    let checkpoint = createLiveGatewayUsageCheckpoint();
    checkpoint = observeLiveGatewayClientMessage(checkpoint, {
      audio: { data: pcmBase64(32_000), mimeType: 'audio/pcm;rate=16000' },
    });
    checkpoint = observeLiveGatewayProviderMessage(checkpoint, {
      serverContent: {
        modelTurn: {
          parts: [{ inlineData: { data: pcmBase64(24_000), mimeType: 'audio/pcm;rate=24000' } }],
        },
      },
      usageMetadata: { totalTokenCount: 100 },
    });

    expect(getLiveGatewayBillableUsage(checkpoint)).toEqual({
      billable: true,
      source: 'provider+transport',
      usageMetadata: expect.objectContaining({
        promptTokenCount: 84,
        responseTokenCount: 16,
        totalTokenCount: 100,
        promptTokensDetails: [
          { modality: 'AUDIO', tokenCount: 32 },
          { modality: 'TEXT', tokenCount: 52 },
        ],
        responseTokensDetails: [{ modality: 'AUDIO', tokenCount: 16 }],
      }),
    });
  });

  it('merges recovery checkpoints monotonically so a stale write cannot erase output', () => {
    const previous = observeLiveGatewayProviderMessage(createLiveGatewayUsageCheckpoint(), {
      serverContent: {
        modelTurn: { parts: [{ inlineData: { data: pcmBase64(4_800), mimeType: 'audio/pcm;rate=24000' } }] },
      },
      usageMetadata: { promptTokenCount: 40, responseTokenCount: 10, totalTokenCount: 50 },
    });
    const stale = {
      ...createLiveGatewayUsageCheckpoint(),
      inputAudioBytes: 32_000,
      providerMessageCount: 1,
    };

    expect(mergeLiveGatewayUsageCheckpoints(previous, stale)).toMatchObject({
      inputAudioBytes: 32_000,
      outputAudioBytes: 4_800,
      usefulOutput: true,
      providerMessageCount: 1,
      providerUsageMetadata: {
        promptTokenCount: 40,
        responseTokenCount: 10,
        totalTokenCount: 50,
      },
    });
  });
});


describe('room tool billing evidence', () => {
  const tool = {toolCall:{functionCalls:[{id:'c1',name:'observeMaestroRoomV1',args:{}}]}};
  const response = {functionResponses:[{id:'c1',name:'observeMaestroRoomV1',response:{scene:'Ää 界'}}]};
  it('keeps useful tool output billable even when speech fails, with text-only fallback counts', () => {
    let checkpoint = observeLiveGatewayProviderMessage(createLiveGatewayUsageCheckpoint(),tool);
    checkpoint = observeLiveGatewayToolResponse(checkpoint,response);
    expect(checkpoint.usefulOutput).toBe(true);
    expect(checkpoint.inputToolResponseBytes).toBe(Buffer.byteLength(JSON.stringify(response),'utf8'));
    const usage = getLiveGatewayBillableUsage(checkpoint);
    expect(usage.billable).toBe(true); expect(usage.source).toBe('transport');
    expect(usage.usageMetadata.promptTokensDetails).toEqual([{modality:'TEXT',tokenCount:Math.ceil(checkpoint.inputToolResponseBytes!/4)}]);
    expect(usage.usageMetadata.responseTokensDetails).toEqual([{modality:'TEXT',tokenCount:Math.ceil(checkpoint.outputToolCallBytes!/4)}]);
  });
  it('uses provider totals without charging observed tool bytes a second time', () => {
    let checkpoint = observeLiveGatewayProviderMessage(createLiveGatewayUsageCheckpoint(),tool);
    checkpoint = observeLiveGatewayToolResponse(checkpoint,response);
    checkpoint = observeLiveGatewayProviderMessage(checkpoint,{usageMetadata:{promptTokenCount:200,responseTokenCount:100,totalTokenCount:300,
      promptTokensDetails:[{modality:'TEXT',tokenCount:200}],responseTokensDetails:[{modality:'TEXT',tokenCount:100}]}});
    expect(getLiveGatewayBillableUsage(checkpoint)).toMatchObject({source:'provider',usageMetadata:{totalTokenCount:300,promptTokenCount:200,responseTokenCount:100}});
  });
  it('does not label tool text as audio when inferring missing provider modality detail', () => {
    let checkpoint = observeLiveGatewayProviderMessage(createLiveGatewayUsageCheckpoint(),tool);
    checkpoint = observeLiveGatewayToolResponse(checkpoint,response);
    checkpoint = observeLiveGatewayProviderMessage(checkpoint,{usageMetadata:{promptTokenCount:200,responseTokenCount:100,totalTokenCount:300}});
    const usage = getLiveGatewayBillableUsage(checkpoint).usageMetadata;
    expect(usage.promptTokensDetails).toEqual([{modality:'TEXT',tokenCount:200}]);
    expect(usage.responseTokensDetails).toEqual([{modality:'TEXT',tokenCount:100}]);
  });
  it('merges byte counters idempotently with older checkpoints during recovery', () => {
    const old = createLiveGatewayUsageCheckpoint(); delete old.inputToolResponseBytes; delete old.outputToolCallBytes;
    const next = observeLiveGatewayToolResponse(observeLiveGatewayProviderMessage(old,tool),response);
    const merged = mergeLiveGatewayUsageCheckpoints(old,next);
    expect(merged).toEqual(next);
    expect(mergeLiveGatewayUsageCheckpoints(merged,next)).toEqual(next);
  });
});
