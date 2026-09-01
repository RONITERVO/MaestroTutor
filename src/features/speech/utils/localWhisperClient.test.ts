// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { beforeEach, describe, expect, it, vi } from 'vitest';

const workerState = vi.hoisted(() => ({
  instances: [] as Array<{ status: string; dispose: ReturnType<typeof vi.fn> }>,
}));

vi.mock('./observerWhisperClient', () => ({
  ObserverWhisperClient: class {
    status = 'idle';
    dispose = vi.fn(() => {
      this.status = 'disposed';
    });

    constructor() {
      workerState.instances.push(this);
    }
  },
}));

import {
  acquireLocalWhisperClient,
  releaseLocalWhisperClient,
} from './localWhisperClient';

describe('shared local Whisper worker', () => {
  beforeEach(() => {
    workerState.instances.length = 0;
  });

  it('keeps one model alive until the final Live consumer releases it', () => {
    const options = {
      model: 'onnx-community/whisper-tiny.en',
      allowFp32Fallback: false,
    };

    const observer = acquireLocalWhisperClient(options);
    const stt = acquireLocalWhisperClient(options);

    expect(stt).toBe(observer);
    expect(workerState.instances).toHaveLength(1);

    releaseLocalWhisperClient(observer);
    expect(workerState.instances[0].dispose).not.toHaveBeenCalled();

    releaseLocalWhisperClient(stt);
    expect(workerState.instances[0].dispose).toHaveBeenCalledOnce();

    const nextSession = acquireLocalWhisperClient(options);
    expect(nextSession).not.toBe(observer);
    expect(workerState.instances).toHaveLength(2);
    releaseLocalWhisperClient(nextSession);
  });

  it('does not carry stale consumers into a replacement for a disposed worker', () => {
    const options = {
      model: 'onnx-community/whisper-tiny.en',
      allowFp32Fallback: false,
    };

    const oldObserver = acquireLocalWhisperClient(options);
    const oldStt = acquireLocalWhisperClient(options);
    oldObserver.dispose();

    const replacement = acquireLocalWhisperClient(options);
    releaseLocalWhisperClient(oldObserver);
    releaseLocalWhisperClient(oldStt);
    releaseLocalWhisperClient(replacement);

    expect(workerState.instances[workerState.instances.length - 1].dispose).toHaveBeenCalledOnce();
  });
});
