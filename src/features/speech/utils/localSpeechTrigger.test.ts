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
  static initialState: AudioContextState = 'running';
  state: AudioContextState = 'running';
  readonly audioWorklet = { addModule: vi.fn(async () => undefined) };
  readonly resume = vi.fn(async () => {
    this.state = 'running';
  });
  readonly close = vi.fn(async () => {
    this.state = 'closed';
  });
  readonly createMediaStreamSource = vi.fn(() => ({ connect: vi.fn() }));

  constructor() {
    this.state = FakeAudioContext.initialState;
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
    FakeAudioContext.initialState = 'running';
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
    const vadActivity: boolean[] = [];
    let resolved = false;
    const resultPromise = waitForLocalSpeechTrigger({
      detector: detector as any,
      onPhaseChange: phase => phases.push(phase),
      onVadActivityChange: active => vadActivity.push(active),
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

    for (let index = 0; index < 25; index += 1) await emitPacket(silence);
    expect(detector.transcribe).not.toHaveBeenCalled();
    expect(resolved).toBe(false);
    expect(track.stop).not.toHaveBeenCalled();

    for (let index = 0; index < 12; index += 1) await emitPacket(speech);
    const result = await resultPromise;
    const context = FakeAudioContext.instances[0];

    expect(result.microphoneStream).toBe(stream);
    expect(result.transcript).toBe('hello from the microphone');
    expect(result.pcm.length).toBe(16_000 * 3);
    expect(detector.transcribe).toHaveBeenCalledOnce();
    expect(track.stop).not.toHaveBeenCalled();
    expect(context.close).toHaveBeenCalledOnce();
    expect(phases).toContain('vad-listening');
    expect(phases).toContain('whisper-checking');
    expect(phases[phases.length - 1]).toBe('speech-confirmed');
    expect(vadActivity).toEqual([true, false]);
  });

  it('does not transcribe or send a short utterance even when its buffered window exceeds 1.2 seconds', async () => {
    let now = 1_000;
    vi.spyOn(Date, 'now').mockImplementation(() => now);
    const detector = {
      initialize: vi.fn(async () => undefined),
      transcribe: vi.fn(async () => 'hi'),
    };
    const controller = new AbortController();
    const resultPromise = waitForLocalSpeechTrigger({
      detector: detector as any,
      signal: controller.signal,
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

    for (let index = 0; index < 25; index += 1) await emitPacket(silence);
    for (let index = 0; index < 4; index += 1) await emitPacket(speech);
    for (let index = 0; index < 12; index += 1) await emitPacket(silence);

    expect(detector.transcribe).not.toHaveBeenCalled();
    controller.abort();
    await expect(resultPromise).rejects.toMatchObject({ name: 'AbortError' });
  });

  it('rejects an already-aborted request without opening the microphone', async () => {
    const controller = new AbortController();
    controller.abort();

    await expect(waitForLocalSpeechTrigger({
      detector: { initialize: vi.fn(), transcribe: vi.fn() } as any,
      signal: controller.signal,
    })).rejects.toMatchObject({ name: 'AbortError' });

    expect(navigator.mediaDevices.getUserMedia).not.toHaveBeenCalled();
    expect(FakeAudioContext.instances).toHaveLength(0);
  });

  it('aborts during Whisper initialization and stops the retained microphone', async () => {
    let finishInitialize: (() => void) | undefined;
    const initialize = vi.fn(() => new Promise<void>(resolve => {
      finishInitialize = resolve;
    }));
    const controller = new AbortController();
    const resultPromise = waitForLocalSpeechTrigger({
      detector: { initialize, transcribe: vi.fn() } as any,
      signal: controller.signal,
    });

    await vi.waitFor(() => expect(initialize).toHaveBeenCalledOnce());
    controller.abort();
    await expect(resultPromise).rejects.toMatchObject({ name: 'AbortError' });
    finishInitialize?.();

    expect(track.stop).toHaveBeenCalledOnce();
    expect(FakeAudioContext.instances).toHaveLength(0);
  });

  it('resumes suspended capture and tears it down when aborted', async () => {
    FakeAudioContext.initialState = 'suspended';
    const controller = new AbortController();
    const resultPromise = waitForLocalSpeechTrigger({
      detector: { initialize: vi.fn(async () => undefined), transcribe: vi.fn() } as any,
      signal: controller.signal,
    });

    await vi.waitFor(() => expect(FakeAudioWorkletNode.instances).toHaveLength(1));
    const context = FakeAudioContext.instances[0];
    expect(context.resume).toHaveBeenCalledOnce();

    controller.abort();
    await expect(resultPromise).rejects.toMatchObject({ name: 'AbortError' });
    expect(track.stop).toHaveBeenCalledOnce();
    expect(context.close).toHaveBeenCalledOnce();
  });

  it('returns to local listening after non-speech and stays pending until aborted', async () => {
    let now = 1_000;
    vi.spyOn(Date, 'now').mockImplementation(() => now);
    const detector = {
      initialize: vi.fn(async () => undefined),
      transcribe: vi.fn(async () => '[BLANK_AUDIO]'),
    };
    const phases: string[] = [];
    const controller = new AbortController();
    let resolved = false;
    const resultPromise = waitForLocalSpeechTrigger({
      detector: detector as any,
      signal: controller.signal,
      onPhaseChange: phase => phases.push(phase),
    }).then(result => {
      resolved = true;
      return result;
    });

    await vi.waitFor(() => expect(FakeAudioWorkletNode.instances).toHaveLength(1));
    const speech = new Int16Array(1_600).fill(10_000);
    for (let index = 0; index < 12; index += 1) {
      now += 100;
      FakeAudioWorkletNode.instances[0].emit(speech);
      await new Promise(resolve => setTimeout(resolve, 0));
    }
    await vi.waitFor(() => expect(detector.transcribe).toHaveBeenCalledOnce());
    expect(phases[phases.length - 1]).toBe('vad-listening');
    expect(resolved).toBe(false);
    expect(track.stop).not.toHaveBeenCalled();

    controller.abort();
    await expect(resultPromise).rejects.toMatchObject({ name: 'AbortError' });
    expect(track.stop).toHaveBeenCalledOnce();
    expect(FakeAudioContext.instances[0].close).toHaveBeenCalledOnce();
  });
});
