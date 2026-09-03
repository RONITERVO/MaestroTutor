// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import {
  createLiveGatewayUsageCheckpoint,
  getLiveGatewayBillableUsage,
  mergeLiveGatewayUsageCheckpoints,
  observeLiveGatewayClientMessage,
  observeLiveGatewayProviderMessage,
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
        promptTokensDetails: [{ modality: 'AUDIO', tokenCount: 32 }],
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
