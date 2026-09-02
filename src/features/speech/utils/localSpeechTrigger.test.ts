// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { waitForLocalSpeechTrigger } from './localSpeechTrigger';

class FakeAudioWorkletNode {
  static instances: FakeAudioWorkletNode[] = [];
  readonly port: { onmessage: ((event: MessageEvent<Int16Array>) => void) | null } = {
    onmessage: null,
  };

  constructor() {
    FakeAudioWorkletNode.instances.push(this);
  }

  disconnect() {}

  emit(pcm: Int16Array) {
    this.port.onmessage?.({ data: pcm } as MessageEvent<Int16Array>);
  }
}

class FakeAudioContext {
  static instances: FakeAudioContext[] = [];
  state: AudioContextState = 'running';
  readonly audioWorklet = { addModule: vi.fn(async () => undefined) };
  readonly close = vi.fn(async () => {
    this.state = 'closed';
  });
  readonly createMediaStreamSource = vi.fn(() => ({ connect: vi.fn() }));

  constructor() {
    FakeAudioContext.instances.push(this);
  }
}

describe('local speech trigger', () => {
  const track = { stop: vi.fn() };
  const stream = {
    getTracks: () => [track],
  } as unknown as MediaStream;
  beforeEach(() => {
    FakeAudioWorkletNode.instances = [];
    FakeAudioContext.instances = [];
    track.stop.mockReset();
    vi.stubGlobal('navigator', {
      mediaDevices: { getUserMedia: vi.fn(async () => stream) },
    });
    vi.stubGlobal('window', {
      AudioContext: FakeAudioContext,
    });
    vi.stubGlobal('AudioWorkletNode', FakeAudioWorkletNode);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('stays local through silence and transfers pre-roll only after Whisper finds words', async () => {
    let now = 1_000;
    vi.spyOn(Date, 'now').mockImplementation(() => now);
    const detector = {
      initialize: vi.fn(async () => undefined),
      transcribe: vi.fn(async () => 'hello from the microphone'),
    };
    const phases: string[] = [];
    let resolved = false;
    const resultPromise = waitForLocalSpeechTrigger({
      detector: detector as any,
      onPhaseChange: phase => phases.push(phase),
    }).then(result => {
      resolved = true;
      return result;
    });

    await vi.waitFor(() => expect(FakeAudioWorkletNode.instances).toHaveLength(1));
    const worklet = FakeAudioWorkletNode.instances[0];
    const silence = new Int16Array(1_600);
    const speech = new Int16Array(1_600).fill(10_000);
    const emitPacket = async (pcm: Int16Array) => {
      now += 100;
      worklet.emit(pcm);
      await new Promise(resolve => setTimeout(resolve, 0));
    };

    for (let index = 0; index < 15; index += 1) await emitPacket(silence);
    expect(detector.transcribe).not.toHaveBeenCalled();
    expect(resolved).toBe(false);
    expect(track.stop).not.toHaveBeenCalled();

    for (let index = 0; index < 12; index += 1) await emitPacket(speech);
    const result = await resultPromise;
    const context = FakeAudioContext.instances[0];

    expect(result.microphoneStream).toBe(stream);
    expect(result.transcript).toBe('hello from the microphone');
    expect(result.pcm.length).toBeGreaterThan(0);
    expect(detector.transcribe).toHaveBeenCalledOnce();
    expect(track.stop).not.toHaveBeenCalled();
    expect(context.close).toHaveBeenCalledOnce();
    expect(phases).toContain('vad-listening');
    expect(phases).toContain('whisper-checking');
    expect(phases[phases.length - 1]).toBe('speech-confirmed');
  });
});
