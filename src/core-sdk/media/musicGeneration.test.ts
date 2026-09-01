// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it, vi } from 'vitest';
import { createCoreEventJournal } from '../events';
import { createCoreRuntime } from '../runtime';
import type { CoreGeminiClient } from '../managedGeminiClient';
import { runCoreManagedMusicGeneration, runCoreMusicGeneration } from './musicGeneration';

describe('core music generation', () => {
  it('drives the Lyria stream and produces a deterministic WAV without a browser', async () => {
    const setWeightedPrompts = vi.fn(async () => undefined);
    const setMusicGenerationConfig = vi.fn(async () => undefined);
    const play = vi.fn();
    const pause = vi.fn();
    const close = vi.fn();
    const pcm = new Int16Array([100, -100, 200, -200, 300, -300, 400, -400]);
    const pcmBase64 = Buffer.from(pcm.buffer).toString('base64');
    const connect = vi.fn(async (request: any) => {
      globalThis.setTimeout(async () => {
        request.callbacks.onmessage({ setupComplete: true });
        await Promise.resolve();
        await Promise.resolve();
        request.callbacks.onmessage({
          serverContent: {
            audioChunks: [{ data: pcmBase64, mimeType: 'audio/pcm;rate=1;channels=1' }],
          },
        });
        request.callbacks.onclose();
      }, 0);
      return { setWeightedPrompts, setMusicGenerationConfig, play, pause, close };
    });
    const events = createCoreEventJournal();
    const runtime = createCoreRuntime({ events });
    const aiClient = {
      models: {} as CoreGeminiClient['models'],
      live: { connect: vi.fn(), music: { connect } },
    } as CoreGeminiClient;

    const result = await runCoreMusicGeneration({
      aiClient,
      runtime,
      model: 'lyria-realtime-exp',
      prompt: 'A calm scale exercise',
      durationSeconds: 8,
    });

    expect(connect).toHaveBeenCalledWith(expect.objectContaining({ model: 'models/lyria-realtime-exp' }));
    expect(setWeightedPrompts).toHaveBeenCalledOnce();
    expect(setMusicGenerationConfig).toHaveBeenCalledOnce();
    expect(play).toHaveBeenCalledOnce();
    expect(result).toMatchObject({
      mimeType: 'audio/wav',
      sampleRate: 1,
      channels: 1,
      sampleCount: 8,
      durationSeconds: 8,
    });
    expect(result.dataUrl).toMatch(/^data:audio\/wav;base64,/);
    expect(events.snapshot().map(event => event.phase)).toEqual(expect.arrayContaining([
      'music.started',
      'music.setupComplete',
      'music.chunkReceived',
      'music.succeeded',
    ]));
  });

  it('rejects an empty prompt before opening a provider session', async () => {
    const connect = vi.fn();
    await expect(runCoreMusicGeneration({
      aiClient: { models: {} as any, live: { connect: vi.fn(), music: { connect } } },
      model: 'lyria-realtime-exp',
      prompt: '   ',
    })).rejects.toThrow('Music prompt is empty');
    expect(connect).not.toHaveBeenCalled();
  });

  it('uses the managed backend PCM route and preserves the Core event contract', async () => {
    const pcm = new Int16Array([10, -10, 20, -20]);
    const generateMusic = vi.fn(async () => ({
      pcmBase64: Buffer.from(pcm.buffer).toString('base64'),
      sampleRate: 2,
      channels: 1,
      sampleCount: pcm.length,
      durationSeconds: 2,
    }));
    const observer = vi.fn(async () => true);
    const observerStarted = vi.fn();
    const events = createCoreEventJournal();

    const result = await runCoreManagedMusicGeneration({
      backend: { generateMusic },
      runtime: createCoreRuntime({ events }),
      model: 'lyria-realtime-exp',
      prompt: 'Original scale exercise',
      durationSeconds: 8,
      onPcmChunk: observer,
      onPcmObserverStart: observerStarted,
    });

    expect(generateMusic).toHaveBeenCalledWith({
      model: 'models/lyria-realtime-exp',
      prompt: 'Original scale exercise',
      durationSeconds: 8,
    }, undefined);
    expect(observer).toHaveBeenCalledWith(expect.objectContaining({
      pcmBase64: expect.any(String),
      sampleRate: 2,
      channels: 1,
      totalSamples: 4,
    }));
    expect(observerStarted).toHaveBeenCalledOnce();
    expect(result).toMatchObject({ mimeType: 'audio/wav', sampleCount: 4, durationSeconds: 2 });
    expect(events.snapshot().map(event => event.phase)).toEqual(['music.started', 'music.succeeded']);
  });

  it('does not fail an already-completed managed generation when playback observers throw', async () => {
    const pcm = new Int16Array(16);
    const generateMusic = vi.fn(async () => ({
      pcmBase64: Buffer.from(pcm.buffer).toString('base64'),
      sampleRate: 2,
      channels: 1,
      sampleCount: pcm.length,
      durationSeconds: 8,
    }));

    await expect(runCoreManagedMusicGeneration({
      backend: { generateMusic },
      model: 'lyria-realtime-exp',
      prompt: 'Original scale exercise',
      durationSeconds: 8,
      onPcmChunk: async () => { throw new Error('playback unavailable'); },
      onPcmObserverStart: () => { throw new Error('UI callback failed'); },
    })).resolves.toMatchObject({ sampleCount: 16, durationSeconds: 8 });
  });

  it('trims the final provider chunk, ignores later chunks and serializes observers', async () => {
    const connect = vi.fn(async (request: any) => {
      queueMicrotask(() => {
        request.callbacks.onmessage({ setupComplete: true });
        request.callbacks.onmessage({
          serverContent: {
            audioChunks: [
              { data: Buffer.from(new Int16Array(6).buffer).toString('base64'), mimeType: 'audio/pcm;rate=1;channels=1' },
              { data: Buffer.from(new Int16Array(6).buffer).toString('base64'), mimeType: 'audio/pcm;rate=1;channels=1' },
              { data: Buffer.from(new Int16Array(4).buffer).toString('base64'), mimeType: 'audio/pcm;rate=1;channels=1' },
            ],
          },
        });
        request.callbacks.onclose();
      });
      return {
        setWeightedPrompts: vi.fn(async () => undefined),
        setMusicGenerationConfig: vi.fn(async () => undefined),
        play: vi.fn(),
        pause: vi.fn(),
        close: vi.fn(),
      };
    });
    const observedTotals: number[] = [];
    let activeObservers = 0;
    let maxActiveObservers = 0;
    const observerStarted = vi.fn();
    const result = await runCoreMusicGeneration({
      aiClient: { models: {} as any, live: { connect: vi.fn(), music: { connect } } },
      model: 'lyria-realtime-exp',
      prompt: 'A precise eight second exercise',
      durationSeconds: 8,
      onPcmChunk: async chunk => {
        activeObservers += 1;
        maxActiveObservers = Math.max(maxActiveObservers, activeObservers);
        observedTotals.push(chunk.totalSamples);
        await Promise.resolve();
        activeObservers -= 1;
        return true;
      },
      onPcmObserverStart: observerStarted,
    });
    await vi.waitFor(() => expect(observedTotals).toHaveLength(2));

    expect(result).toMatchObject({ sampleCount: 8, durationSeconds: 8 });
    expect(observedTotals).toEqual([6, 8]);
    expect(maxActiveObservers).toBe(1);
    expect(observerStarted).toHaveBeenCalledOnce();
  });

  it('rejects a stream that closes with only partial audio', async () => {
    const connect = vi.fn(async (request: any) => {
      queueMicrotask(() => {
        request.callbacks.onmessage({ setupComplete: true });
        request.callbacks.onmessage({
          serverContent: {
            audioChunks: [{
              data: Buffer.from(new Int16Array(2).buffer).toString('base64'),
              mimeType: 'audio/pcm;rate=1;channels=1',
            }],
          },
        });
        request.callbacks.onclose();
      });
      return {
        setWeightedPrompts: vi.fn(async () => undefined),
        setMusicGenerationConfig: vi.fn(async () => undefined),
        play: vi.fn(),
        pause: vi.fn(),
        close: vi.fn(),
      };
    });

    await expect(runCoreMusicGeneration({
      aiClient: { models: {} as any, live: { connect: vi.fn(), music: { connect } } },
      model: 'lyria-realtime-exp',
      prompt: 'A complete exercise',
      durationSeconds: 8,
    })).rejects.toThrow('before the requested 8s duration');
  });
});
