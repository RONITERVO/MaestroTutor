// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

export interface EncodePcmBase64Request {
  kind: 'encode-pcm-base64';
  requestId: number;
  buffer: ArrayBuffer;
}

export interface DecodeBase64Pcm16Request {
  kind: 'decode-base64-pcm16';
  requestId: number;
  base64: string;
}

export type AudioCodecWorkerRequest =
  | EncodePcmBase64Request
  | DecodeBase64Pcm16Request;

export interface EncodePcmBase64Result {
  kind: 'encode-pcm-base64-result';
  requestId: number;
  base64: string;
}

export interface DecodeBase64Pcm16Result {
  kind: 'decode-base64-pcm16-result';
  requestId: number;
  buffer: ArrayBuffer;
}

export interface AudioCodecWorkerError {
  kind: 'error';
  requestId: number;
  message: string;
}

export type AudioCodecWorkerResponse =
  | EncodePcmBase64Result
  | DecodeBase64Pcm16Result
  | AudioCodecWorkerError;
