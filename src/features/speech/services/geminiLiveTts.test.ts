// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  connect: vi.fn(),
  callbacks: null as Record<string, (...args: any[]) => void> | null,
  sessionClose: vi.fn(),
  logComplete: vi.fn(),
  logError: vi.fn(),
}));

vi.mock('@google/genai', () => ({
  Modality: { AUDIO: 'AUDIO' },
  ThinkingLevel: { MINIMAL: 'MINIMAL' },
}));

vi.mock('../../../api/gemini/client', () => ({
  getAi: vi.fn(async () => ({ live: { connect: mocks.connect } })),
}));

vi.mock('../../diagnostics', () => ({
  debugLogService: {
    logRequest: vi.fn(() => ({ complete: mocks.logComplete, error: mocks.logError })),
  },
}));

vi.mock('../../../core/config/models', () => ({
  getGeminiModels: () => ({ audio: { tts: 'gemini-2.5-live-test' } }),
}));

vi.mock('../../../core-sdk/media/triggerAudioAsset', () => ({
  TRIGGER_AUDIO_PCM_24K: 'AAAAAA==',
  TRIGGER_SAMPLE_RATE: 24_000,
}));

vi.mock('../../../shared/utils/costTracker', () => ({
  createLiveUsageTracker: () => ({
    trackSnapshot: vi.fn(),
    completeTurn: vi.fn(),
    flush: vi.fn(),
  }),
}));

vi.mock('../../../../shared/liveOpenReason', () => ({
  createLiveOpenReason: () => ({ trigger: 'test' }),
}));

import { streamGeminiLiveTts } from './geminiLiveTts';

class FakeBufferSource {
  buffer: AudioBuffer | null = null;
  onended: (() => void) | null = null;
  readonly connect = vi.fn();
  readonly disconnect = vi.fn();
  readonly start = vi.fn();
  readonly stop = vi.fn();

  finish() {
    this.onended?.();
  }
}

class FakeAudioContext {
  currentTime = 0;
  destination = {} as AudioDestinationNode;
  readonly sources: FakeBufferSource[] = [];

  createBuffer(_channels: number, frameCount: number, sampleRate: number): AudioBuffer {
    const channel = new Float32Array(frameCount);
    return {
      duration: frameCount / sampleRate,
      getChannelData: () => channel,
    } as unknown as AudioBuffer;
  }

  createBufferSource(): AudioBufferSourceNode {
    const source = new FakeBufferSource();
    this.sources.push(source);
    return source as unknown as AudioBufferSourceNode;
  }
}

const pcmBase64 = (samples: number): string => {
  const pcm = new Int16Array(samples).fill(4_000);
  return Buffer.from(pcm.buffer).toString('base64');
};

describe('Gemini Live TTS audible completion', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.callbacks = null;
    vi.stubGlobal('requestAnimationFrame', vi.fn(() => 1));
    vi.stubGlobal('cancelAnimationFrame', vi.fn());
    mocks.connect.mockImplementation(async (options: { callbacks: Record<string, (...args: any[]) => void> }) => {
      mocks.callbacks = options.callbacks;
      mocks.sessionClose.mockImplementation(() => options.callbacks.onclose?.());
      return { close: mocks.sessionClose, sendRealtimeInput: vi.fn() };
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('keeps turn-complete audio alive until its scheduled source ends', async () => {
    const context = new FakeAudioContext();
    let settled = false;
    const resultPromise = streamGeminiLiveTts({
      lines: [{ text: 'This response must be heard completely.', langCode: 'en-US' }],
      audioContext: context as unknown as AudioContext,
      liveOpenTrigger: 'voice-tts-click' as any,
    }).then(result => {
      settled = true;
      return result;
    });

    await vi.waitFor(() => expect(mocks.callbacks).not.toBeNull());
    mocks.callbacks!.onmessage({
      serverContent: { modelTurn: { parts: [{ inlineData: { data: pcmBase64(2_400) } }] } },
    });
    mocks.callbacks!.onmessage({ serverContent: { turnComplete: true } });
    await Promise.resolve();

    expect(settled).toBe(false);
    expect(context.sources).toHaveLength(1);
    expect(context.sources[0].stop).not.toHaveBeenCalled();
    expect(mocks.sessionClose).toHaveBeenCalledOnce();

    context.sources[0].finish();
    await expect(resultPromise).resolves.toMatchObject({ isComplete: true });
    expect(mocks.logComplete).toHaveBeenCalledWith(expect.objectContaining({
      status: 'complete',
      playbackDrainResult: 'drained',
    }));
  });

  it('preserves already scheduled audio when the transport closes unexpectedly', async () => {
    const context = new FakeAudioContext();
    const onError = vi.fn();
    let settled = false;
    const resultPromise = streamGeminiLiveTts({
      lines: [{ text: 'Keep the received speech audible.', langCode: 'en-US' }],
      audioContext: context as unknown as AudioContext,
      liveOpenTrigger: 'voice-tts-click' as any,
      onError,
    }).then(result => {
      settled = true;
      return result;
    });

    await vi.waitFor(() => expect(mocks.callbacks).not.toBeNull());
    mocks.callbacks!.onmessage({
      serverContent: { modelTurn: { parts: [{ inlineData: { data: pcmBase64(1_200) } }] } },
    });
    mocks.callbacks!.onclose();
    await Promise.resolve();

    expect(settled).toBe(false);
    expect(context.sources[0].stop).not.toHaveBeenCalled();
    context.sources[0].finish();

    await expect(resultPromise).resolves.toMatchObject({ isComplete: false });
    expect(onError).toHaveBeenCalledWith('Connection closed before the TTS turn completed');
  });

  it('still lets an explicit user stop interrupt playback after network completion', async () => {
    const context = new FakeAudioContext();
    const controller = new AbortController();
    const resultPromise = streamGeminiLiveTts({
      lines: [{ text: 'The user may intentionally stop this.', langCode: 'en-US' }],
      audioContext: context as unknown as AudioContext,
      liveOpenTrigger: 'voice-tts-click' as any,
      abortSignal: controller.signal,
    });

    await vi.waitFor(() => expect(mocks.callbacks).not.toBeNull());
    mocks.callbacks!.onmessage({
      serverContent: { modelTurn: { parts: [{ inlineData: { data: pcmBase64(2_400) } }] } },
    });
    mocks.callbacks!.onmessage({ serverContent: { turnComplete: true } });
    controller.abort();

    await expect(resultPromise).resolves.toMatchObject({ isComplete: false, error: 'ABORTED' });
    expect(context.sources[0].stop).toHaveBeenCalledOnce();
    expect(mocks.logComplete).toHaveBeenCalledWith(expect.objectContaining({ status: 'aborted' }));
  });
});
