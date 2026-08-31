// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

export type ObserverWhisperRequest =
  | {
      kind: 'init';
      model: string;
      allowFp32Fallback: boolean;
    }
  | {
      kind: 'transcribe';
      requestId: number;
      audio: ArrayBuffer;
    };

export type ObserverWhisperResponse =
  | {
      kind: 'loading';
      model: string;
      profile: string;
      file?: string;
      progress?: number;
    }
  | {
      kind: 'ready';
      model: string;
      profile: string;
    }
  | {
      kind: 'result';
      requestId: number;
      text: string;
      elapsedMs: number;
    }
  | {
      kind: 'error';
      requestId?: number;
      message: string;
    };
