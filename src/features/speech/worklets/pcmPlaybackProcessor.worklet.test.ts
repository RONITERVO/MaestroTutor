// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { afterEach, describe, expect, it, vi } from 'vitest';

type TestPlaybackProcessor = {
  port: {
    onmessage: ((event: MessageEvent) => void) | null;
    postMessage: ReturnType<typeof vi.fn>;
  };
  process(inputs: Float32Array[][], outputs: Float32Array[][], parameters: Record<string, Float32Array>): boolean;
};

describe('PCM playback worklet drain protocol', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.resetModules();
  });

  it('acknowledges drain only after all queued PCM has been rendered', async () => {
    let Processor: (new () => TestPlaybackProcessor) | undefined;
    class FakeAudioWorkletProcessor {
      readonly port = {
        onmessage: null as ((event: MessageEvent) => void) | null,
        postMessage: vi.fn(),
      };
    }
    vi.stubGlobal('AudioWorkletProcessor', FakeAudioWorkletProcessor);
    vi.stubGlobal('sampleRate', 24_000);
    vi.stubGlobal('registerProcessor', (_name: string, ctor: new () => TestPlaybackProcessor) => {
      Processor = ctor;
    });
    await import('./pcmPlaybackProcessor.worklet');

    expect(Processor).toBeDefined();
    const processor = new Processor!();
    processor.port.onmessage?.({
      data: { type: 'push', pcm: new Int16Array(240).fill(16_384), inputSampleRate: 24_000 },
    } as MessageEvent);
    processor.port.onmessage?.({ data: { type: 'request-drain', requestId: 7 } } as MessageEvent);

    const firstOutput = new Float32Array(128);
    processor.process([], [[firstOutput]], {});
    expect(firstOutput.some(sample => sample > 0)).toBe(true);
    expect(processor.port.postMessage).not.toHaveBeenCalledWith({ type: 'drained', requestId: 7 });

    const secondOutput = new Float32Array(128);
    processor.process([], [[secondOutput]], {});
    expect(processor.port.postMessage).toHaveBeenCalledWith({ type: 'drained', requestId: 7 });
  });

  it('still schedules an empty-queue acknowledgement on the render thread', async () => {
    let Processor: (new () => TestPlaybackProcessor) | undefined;
    class FakeAudioWorkletProcessor {
      readonly port = {
        onmessage: null as ((event: MessageEvent) => void) | null,
        postMessage: vi.fn(),
      };
    }
    vi.stubGlobal('AudioWorkletProcessor', FakeAudioWorkletProcessor);
    vi.stubGlobal('sampleRate', 24_000);
    vi.stubGlobal('registerProcessor', (_name: string, ctor: new () => TestPlaybackProcessor) => {
      Processor = ctor;
    });
    await import('./pcmPlaybackProcessor.worklet');

    const processor = new Processor!();
    processor.port.onmessage?.({ data: { type: 'request-drain', requestId: 8 } } as MessageEvent);
    expect(processor.port.postMessage).not.toHaveBeenCalled();
    processor.process([], [[new Float32Array(128)]], {});
    expect(processor.port.postMessage).toHaveBeenCalledWith({ type: 'drained', requestId: 8 });
  });
});
