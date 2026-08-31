// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

interface MockWhisperWorker {
  onmessage: ((event: MessageEvent) => void) | null;
  onerror: ((event: ErrorEvent) => void) | null;
  postMessage: ReturnType<typeof vi.fn>;
  terminate: ReturnType<typeof vi.fn>;
}

const workerState = vi.hoisted(() => ({
  instances: [] as MockWhisperWorker[],
}));

vi.mock('../workers/observerWhisper.worker.ts?worker', () => ({
  default: class {
    onmessage: ((event: MessageEvent) => void) | null = null;
    onerror: ((event: ErrorEvent) => void) | null = null;
    postMessage = vi.fn();
    terminate = vi.fn();

    constructor() {
      workerState.instances.push(this);
    }
  },
}));

import {
  LOCAL_WHISPER_TRANSCRIPTION_TIMEOUT_MS,
  ObserverWhisperClient,
} from './observerWhisperClient';
import type { ObserverWhisperResponse } from '../workers/observerWhisperProtocol';

const latestWorker = (): MockWhisperWorker => (
  workerState.instances[workerState.instances.length - 1]
);

const emit = (worker: MockWhisperWorker, data: ObserverWhisperResponse): void => {
  worker.onmessage?.({ data } as MessageEvent<ObserverWhisperResponse>);
};

const readyClient = async (): Promise<{ client: ObserverWhisperClient; worker: MockWhisperWorker }> => {
  const client = new ObserverWhisperClient({
    model: 'onnx-community/whisper-tiny.en',
    allowFp32Fallback: false,
  });
  const worker = latestWorker();
  const initialized = client.initialize();
  emit(worker, { kind: 'ready', model: 'onnx-community/whisper-tiny.en', profile: 'q4' });
  await initialized;
  return { client, worker };
};

describe('ObserverWhisperClient failure recovery', () => {
  beforeEach(() => {
    workerState.instances.length = 0;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('creates a fresh initialization promise after a load failure', async () => {
    const client = new ObserverWhisperClient({
      model: 'onnx-community/whisper-tiny.en',
      allowFp32Fallback: false,
    });
    const worker = latestWorker();
    const first = client.initialize();
    const firstRejection = expect(first).rejects.toThrow('load failed');
    emit(worker, { kind: 'error', message: 'load failed' });
    await firstRejection;

    const retry = client.initialize();
    expect(retry).not.toBe(first);
    expect(worker.postMessage).toHaveBeenCalledTimes(2);
    emit(worker, { kind: 'ready', model: 'onnx-community/whisper-tiny.en', profile: 'q4' });
    await expect(retry).resolves.toBeUndefined();
    client.dispose();
  });

  it('times out a missing response and allows the next queued request to run', async () => {
    vi.useFakeTimers();
    const { client, worker } = await readyClient();

    const first = client.transcribe(new Float32Array([0.25]));
    await Promise.resolve();
    const firstRejection = expect(first).rejects.toThrow('timed out');
    await vi.advanceTimersByTimeAsync(LOCAL_WHISPER_TRANSCRIPTION_TIMEOUT_MS);
    await firstRejection;

    const second = client.transcribe(new Float32Array([0.5]));
    await Promise.resolve();
    const calls = worker.postMessage.mock.calls;
    const request = calls[calls.length - 1][0] as { requestId: number };
    emit(worker, { kind: 'result', requestId: request.requestId, text: 'hello', elapsedMs: 5 });
    await expect(second).resolves.toBe('hello');
    client.dispose();
  });
});
