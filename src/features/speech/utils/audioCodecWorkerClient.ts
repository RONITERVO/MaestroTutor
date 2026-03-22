// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import AudioCodecWorker from '../workers/audioCodec.worker.ts?worker';
import type {
  AudioCodecWorkerRequest,
  AudioCodecWorkerResponse,
} from '../workers/audioCodecProtocol';

type PendingRequest =
  | { kind: 'encode'; resolve: (base64: string) => void; reject: (error: Error) => void }
  | { kind: 'decode'; resolve: (buffer: ArrayBuffer) => void; reject: (error: Error) => void };

export class AudioCodecWorkerClient {
  private worker: Worker;
  private nextRequestId = 1;
  private pending = new Map<number, PendingRequest>();
  private disposed = false;

  constructor() {
    this.worker = new AudioCodecWorker();
    this.worker.onmessage = (event: MessageEvent<AudioCodecWorkerResponse>) => {
      const message = event.data;
      const pending = this.pending.get(message.requestId);
      if (!pending) return;
      this.pending.delete(message.requestId);

      if (message.kind === 'error') {
        pending.reject(new Error(message.message || 'Audio codec worker error'));
        return;
      }

      if (message.kind === 'encode-pcm-base64-result' && pending.kind === 'encode') {
        pending.resolve(message.base64);
        return;
      }

      if (message.kind === 'decode-base64-pcm16-result' && pending.kind === 'decode') {
        pending.resolve(message.buffer);
        return;
      }

      pending.reject(new Error('Audio codec worker protocol mismatch'));
    };
    this.worker.onerror = (event) => {
      const error = new Error(event.message || 'Audio codec worker crashed');
      this.pending.forEach((pendingRequest) => pendingRequest.reject(error));
      this.pending.clear();
    };
  }

  encodePcmToBase64(buffer: ArrayBuffer): Promise<string> {
    if (this.disposed) {
      return Promise.reject(new Error('Audio codec worker has been disposed'));
    }

    return new Promise<string>((resolve, reject) => {
      const requestId = this.nextRequestId++;
      this.pending.set(requestId, { kind: 'encode', resolve, reject });
      const request: AudioCodecWorkerRequest = {
        kind: 'encode-pcm-base64',
        requestId,
        buffer,
      };
      this.worker.postMessage(request, [buffer]);
    });
  }

  decodeBase64ToPcmBuffer(base64: string): Promise<ArrayBuffer> {
    if (this.disposed) {
      return Promise.reject(new Error('Audio codec worker has been disposed'));
    }

    return new Promise<ArrayBuffer>((resolve, reject) => {
      const requestId = this.nextRequestId++;
      this.pending.set(requestId, { kind: 'decode', resolve, reject });
      const request: AudioCodecWorkerRequest = {
        kind: 'decode-base64-pcm16',
        requestId,
        base64,
      };
      this.worker.postMessage(request);
    });
  }

  dispose() {
    if (this.disposed) return;
    this.disposed = true;
    this.worker.terminate();
    const error = new Error('Audio codec worker disposed');
    this.pending.forEach((pendingRequest) => pendingRequest.reject(error));
    this.pending.clear();
  }
}

export default AudioCodecWorkerClient;
