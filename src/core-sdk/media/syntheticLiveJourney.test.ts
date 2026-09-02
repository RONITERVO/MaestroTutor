// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it, vi } from 'vitest';
import { createCoreEventJournal } from '../events';
import type { CoreGeminiClient } from '../managedGeminiClient';
import { createCoreRuntime } from '../runtime';
import { createSyntheticPcmSource } from './pcmInput';
import { runSyntheticLiveJourney } from './syntheticLiveJourney';
import { LIVE_OPEN_TRIGGER } from '../../../shared/liveOpenReason';

describe('synthetic Live journey', () => {
  it('routes PCM through capture, packetizer, speech gate and the real Live client contract', async () => {
    const sent: any[] = [];
    let callbacks: Record<string, (...args: any[]) => void> = {};
    let completed = false;
    const session = {
      sendRealtimeInput: vi.fn((message: any) => {
        sent.push(message);
        if (message.audioStreamEnd && !completed) {
          completed = true;
          queueMicrotask(() => callbacks.onmessage?.({
            serverContent: {
              inputTranscription: { text: 'hola' },
              outputTranscription: { text: 'Hola.' },
              modelTurn: { parts: [{ inlineData: { mimeType: 'audio/pcm;rate=24000', data: 'AA==' } }] },
              turnComplete: true,
            },
          }));
        }
      }),
      close: vi.fn(),
    };
    const ai: CoreGeminiClient = {
      models: {
        generateContent: vi.fn(),
        generateContentStream: vi.fn(),
      },
      live: {
        connect: vi.fn(async (request: any) => {
          callbacks = request.callbacks;
          callbacks.onopen?.();
          return session;
        }),
        music: { connect: vi.fn() },
      },
    };
    const events = createCoreEventJournal({ now: () => 1_000 });
    const runtime = createCoreRuntime({
      events,
      clock: {
        now: () => 1_000,
        sleep: async () => undefined,
        setInterval: () => 0,
        clearInterval: () => undefined,
      },
    });
    const pcm = new Int16Array(16_000);
    pcm.fill(6_000);
    const source = createSyntheticPcmSource({ pcm, sampleRate: 16_000, pace: false, runtime });

    const result = await runSyntheticLiveJourney(ai, {
      liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source,
      gateInputOnSpeech: true,
      semanticSpeech: true,
      includeModelAudio: true,
      videoFrames: [{ dataBase64: 'data:image/png;base64,AA==' }],
    }, { runtime });

    expect(result.transcript).toBe('Hola.');
    expect(result.sentSamples).toBeGreaterThan(0);
    expect(result.packetizer.totalInputSamples).toBe(16_000);
    expect(result.gate.gatedPackets).toBeGreaterThan(0);
    expect(result.modelAudioChunksBase64).toEqual(['AA==']);
    expect(ai.live.connect).toHaveBeenCalledWith(expect.objectContaining({
      liveOpenReason: expect.objectContaining({ trigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE }),
    }));
    expect(sent.some(message => message.audio?.mimeType === 'audio/pcm;rate=16000')).toBe(true);
    expect(sent.some(message => message.video?.mimeType === 'image/png')).toBe(true);
    expect(events.snapshot().some(event => event.phase === 'input.frame' && event.data?.source === 'synthetic')).toBe(true);
  });

  it('rejects a provider turn that completes without model output', async () => {
    let callbacks: Record<string, (...args: any[]) => void> = {};
    const session = {
      sendRealtimeInput: vi.fn((message: any) => {
        if (message.audioStreamEnd) queueMicrotask(() => callbacks.onmessage?.({
          serverContent: { inputTranscription: { text: 'Play' }, turnComplete: true },
        }));
      }),
      close: vi.fn(),
    };
    const ai = {
      models: {} as CoreGeminiClient['models'],
      live: {
        connect: vi.fn(async (request: any) => {
          callbacks = request.callbacks;
          return session;
        }),
        music: { connect: vi.fn() },
      },
    } as CoreGeminiClient;
    const pcm = new Int16Array(16_000);
    pcm.fill(6_000);

    await expect(runSyntheticLiveJourney(ai, {
      liveOpenTrigger: LIVE_OPEN_TRIGGER.USER_HEADLESS_LIVE,
      source: createSyntheticPcmSource({ pcm, sampleRate: 16_000, pace: false }),
      gateInputOnSpeech: false,
    })).rejects.toThrow('Live turn completed without model output');
  });
});
