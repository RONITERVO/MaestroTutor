// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import ObserverWhisperWorker from '../workers/observerWhisper.worker.ts?worker';
import type {
  ObserverWhisperRequest,
  ObserverWhisperResponse,
} from '../workers/observerWhisperProtocol';

export type ObserverWhisperStatus = 'idle' | 'loading' | 'ready' | 'failed' | 'disposed';

export interface ObserverWhisperClientOptions {
  model: string;
  allowFp32Fallback: boolean;
}

/** Prevent one lost worker message from blocking every later transcription. */
export const LOCAL_WHISPER_TRANSCRIPTION_TIMEOUT_MS = 30_000;

interface PendingTranscription {
  resolve: (text: string) => void;
  reject: (error: Error) => void;
  timeoutId: ReturnType<typeof setTimeout>;
}

/** A lazy, reusable facade around the expensive local Whisper worker. */
export class ObserverWhisperClient {
  private readonly worker = new ObserverWhisperWorker();
  private readonly options: ObserverWhisperClientOptions;
  private nextRequestId = 1;
  private pending = new Map<number, PendingTranscription>();
  private transcriptionQueue: Promise<void> = Promise.resolve();
  private currentStatus: ObserverWhisperStatus = 'idle';
  private initializationStartedAt = 0;
  private readyPromise: Promise<void> | null = null;
  private resolveReady: (() => void) | null = null;
  private rejectReady: ((error: Error) => void) | null = null;

  constructor(options: ObserverWhisperClientOptions) {
    this.options = options;
    this.worker.onmessage = (event: MessageEvent<ObserverWhisperResponse>) => {
      this.handleMessage(event.data);
    };
    this.worker.onerror = (event) => {
      this.fail(new Error(event.message || 'Local Whisper worker crashed'));
    };
  }

  get status(): ObserverWhisperStatus {
    return this.currentStatus;
  }

  /** Wall-clock start of the current load, used by callers' availability grace period. */
  get loadingStartedAt(): number {
    return this.initializationStartedAt;
  }

  initialize(): Promise<void> {
    if (this.currentStatus === 'ready') return Promise.resolve();
    if (this.currentStatus === 'disposed') {
      return Promise.reject(new Error('Local Whisper worker has been disposed'));
    }
    if (this.readyPromise) return this.readyPromise;

    this.currentStatus = 'loading';
    this.initializationStartedAt = Date.now();
    this.readyPromise = new Promise<void>((resolve, reject) => {
      this.resolveReady = resolve;
      this.rejectReady = reject;
    });
    const request: ObserverWhisperRequest = {
      kind: 'init',
      model: this.options.model,
      allowFp32Fallback: this.options.allowFp32Fallback,
    };
    this.worker.postMessage(request);
    return this.readyPromise;
  }

  transcribe(audio: Float32Array): Promise<string> {
    if (this.currentStatus !== 'ready') {
      return Promise.reject(new Error(`Local Whisper is ${this.currentStatus}`));
    }
    const buffer = new ArrayBuffer(audio.byteLength);
    new Float32Array(buffer).set(audio);
    const result = this.transcriptionQueue.then(() => {
      if (this.currentStatus !== 'ready') {
        throw new Error(`Local Whisper is ${this.currentStatus}`);
      }
      const requestId = this.nextRequestId++;
      return new Promise<string>((resolve, reject) => {
        const timeoutId = globalThis.setTimeout(() => {
          const pending = this.takePending(requestId);
          pending?.reject(new Error('Local Whisper transcription timed out'));
        }, LOCAL_WHISPER_TRANSCRIPTION_TIMEOUT_MS);
        this.pending.set(requestId, { resolve, reject, timeoutId });
        const request: ObserverWhisperRequest = { kind: 'transcribe', requestId, audio: buffer };
        try {
          this.worker.postMessage(request, [buffer]);
        } catch (error) {
          const pending = this.takePending(requestId);
          pending?.reject(error instanceof Error ? error : new Error(String(error)));
        }
      });
    });
    // The observer and STT share this client. Serialize their requests because
    // the Transformers pipeline is not guaranteed to be re-entrant.
    this.transcriptionQueue = result.then(() => undefined, () => undefined);
    return result;
  }

  dispose(): void {
    if (this.currentStatus === 'disposed') return;
    this.currentStatus = 'disposed';
    this.worker.terminate();
    this.failPending(new Error('Local Whisper worker disposed'));
    this.rejectReady?.(new Error('Local Whisper worker disposed'));
    this.clearReadyCallbacks();
  }

  private handleMessage(message: ObserverWhisperResponse): void {
    if (this.currentStatus === 'disposed') return;
    if (message.kind === 'loading') {
      this.currentStatus = 'loading';
      return;
    }
    if (message.kind === 'ready') {
      this.currentStatus = 'ready';
      this.resolveReady?.();
      this.clearReadyCallbacks();
      return;
    }
    if (message.kind === 'result') {
      const pending = this.takePending(message.requestId);
      if (!pending) return;
      pending.resolve(message.text);
      return;
    }

    const error = new Error(message.message || 'Local Whisper failed');
    if (message.requestId !== undefined) {
      const pending = this.takePending(message.requestId);
      if (!pending) return;
      pending.reject(error);
      return;
    }
    this.fail(error);
  }

  private fail(error: Error): void {
    if (this.currentStatus === 'disposed') return;
    this.currentStatus = 'failed';
    this.rejectReady?.(error);
    this.readyPromise = null;
    this.clearReadyCallbacks();
    this.failPending(error);
  }

  private failPending(error: Error): void {
    this.pending.forEach(request => {
      globalThis.clearTimeout(request.timeoutId);
      request.reject(error);
    });
    this.pending.clear();
  }

  private takePending(requestId: number): PendingTranscription | undefined {
    const pending = this.pending.get(requestId);
    if (!pending) return undefined;
    this.pending.delete(requestId);
    globalThis.clearTimeout(pending.timeoutId);
    return pending;
  }

  private clearReadyCallbacks(): void {
    this.resolveReady = null;
    this.rejectReady = null;
  }
}

export default ObserverWhisperClient;
