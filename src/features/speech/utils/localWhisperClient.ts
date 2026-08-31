// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import {
  ObserverWhisperClient,
  type ObserverWhisperClientOptions,
} from './observerWhisperClient';

/**
 * Local Whisper is a large runtime, particularly inside Android WebView.
 * Keep one worker/model shared by the observer and Gemini Live STT instead of
 * loading an identical copy for every mounted hook.
 */
export type LocalWhisperClient = ObserverWhisperClient;
export type LocalWhisperClientOptions = ObserverWhisperClientOptions;

let sharedClient: LocalWhisperClient | null = null;
let consumerCount = 0;

export const acquireLocalWhisperClient = (
  options: LocalWhisperClientOptions,
): LocalWhisperClient => {
  if (!sharedClient || sharedClient.status === 'disposed') {
    sharedClient = new ObserverWhisperClient(options);
    // Holders of the previous disposed instance are no longer consumers of
    // this replacement; their later release is ignored by identity below.
    consumerCount = 0;
  }
  consumerCount += 1;
  return sharedClient;
};

export const releaseLocalWhisperClient = (client: LocalWhisperClient | null): void => {
  if (!client || client !== sharedClient) return;
  consumerCount = Math.max(0, consumerCount - 1);
  if (consumerCount > 0) return;

  client.dispose();
  sharedClient = null;
};
