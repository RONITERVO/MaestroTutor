/// <reference lib="webworker" />

// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import type {
  AudioCodecWorkerRequest,
  AudioCodecWorkerResponse,
} from './audioCodecProtocol';

declare const self: DedicatedWorkerGlobalScope;

const BASE64_CHUNK_BYTES = 0x8000;

const bytesToBase64 = (bytes: Uint8Array): string => {
  let binary = '';
  for (let offset = 0; offset < bytes.length; offset += BASE64_CHUNK_BYTES) {
    const slice = bytes.subarray(offset, offset + BASE64_CHUNK_BYTES);
    for (let i = 0; i < slice.length; i++) {
      binary += String.fromCharCode(slice[i]);
    }
  }
  return btoa(binary);
};

const base64ToArrayBuffer = (base64: string): ArrayBuffer => {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes.buffer;
};

self.onmessage = (event: MessageEvent<AudioCodecWorkerRequest>) => {
  const request = event.data;

  try {
    if (request.kind === 'encode-pcm-base64') {
      const response: AudioCodecWorkerResponse = {
        kind: 'encode-pcm-base64-result',
        requestId: request.requestId,
        base64: bytesToBase64(new Uint8Array(request.buffer)),
      };
      self.postMessage(response);
      return;
    }

    if (request.kind === 'decode-base64-pcm16') {
      const buffer = base64ToArrayBuffer(request.base64);
      const response: AudioCodecWorkerResponse = {
        kind: 'decode-base64-pcm16-result',
        requestId: request.requestId,
        buffer,
      };
      self.postMessage(response, [buffer]);
    }
  } catch (error) {
    const response: AudioCodecWorkerResponse = {
      kind: 'error',
      requestId: request.requestId,
      message: error instanceof Error ? error.message : String(error),
    };
    self.postMessage(response);
  }
};

export {};
