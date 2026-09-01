// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it, vi } from 'vitest';
import type { CoreGeminiClient } from '../managedGeminiClient';
import { createCoreEventJournal } from '../events';
import { createCoreRuntime } from '../runtime';
import { runCoreAudioNoteGeneration } from './audioNoteGeneration';

describe('core audio-note generation', () => {
  it('injects trigger PCM and returns streamed model audio without browser APIs', async () => {
    const sendRealtimeInput = vi.fn();
    const close = vi.fn();
    const pcm = new Int16Array([100, -100, 200, -200]);
    const audioBase64 = Buffer.from(pcm.buffer).toString('base64');
    const connect = vi.fn(async (request: any) => {
      globalThis.setTimeout(() => {
        request.callbacks.onmessage({
          serverContent: {
            modelTurn: { parts: [{ inlineData: { data: audioBase64, mimeType: 'audio/pcm;rate=24000' } }] },
            turnComplete: true,
          },
        });
      }, 120);
      return { sendRealtimeInput, close };
    });
    const events = createCoreEventJournal();
    const aiClient = {
      models: {} as CoreGeminiClient['models'],
      live: { connect, music: { connect: vi.fn() } },
    } as CoreGeminiClient;

    const result = await runCoreAudioNoteGeneration({
      aiClient,
      runtime: createCoreRuntime({ events }),
      model: 'gemini-live-test',
      text: 'Hola',
      triggerPcmBase64: Buffer.from(new Int16Array([1, 2, 3, 4]).buffer).toString('base64'),
      triggerSampleRate: 24_000,
    });

    expect(connect).toHaveBeenCalledOnce();
    expect(sendRealtimeInput).toHaveBeenCalledWith(expect.objectContaining({
      audio: expect.objectContaining({ mimeType: 'audio/pcm;rate=24000' }),
    }));
    expect(result).toMatchObject({
      mimeType: 'audio/wav',
      sampleCount: 4,
      triggerAudioSamplesSent: 4,
      triggerPacketCount: 1,
    });
    expect(result.dataUrl).toMatch(/^data:audio\/wav;base64,/);
    expect(events.snapshot().map(event => event.phase)).toEqual(expect.arrayContaining([
      'audioNote.started',
      'audioNote.chunkReceived',
      'audioNote.triggerChunkSent',
      'audioNote.succeeded',
    ]));
  });
});
