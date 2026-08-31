/// <reference lib="webworker" />

// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { env, pipeline, type AutomaticSpeechRecognitionPipeline } from '@huggingface/transformers';
import type { ObserverWhisperRequest, ObserverWhisperResponse } from './observerWhisperProtocol';

declare const self: DedicatedWorkerGlobalScope;

type LoadProfile = {
  label: 'q4' | 'fp32';
  dtype: 'fp32' | Record<string, 'q4'>;
};

const Q4_PROFILE: LoadProfile = {
  label: 'q4',
  dtype: {
    encoder_model: 'q4',
    decoder_model_merged: 'q4',
  },
};
const FP32_PROFILE: LoadProfile = { label: 'fp32', dtype: 'fp32' };

let modelId = '';
let allowFp32Fallback = false;
let activeProfile: LoadProfile = Q4_PROFILE;
let transcriberPromise: Promise<AutomaticSpeechRecognitionPipeline> | null = null;

env.allowLocalModels = false;

const post = (message: ObserverWhisperResponse, transfer?: Transferable[]) => {
  self.postMessage(message, transfer ?? []);
};

const messageFrom = (error: unknown): string => {
  if (error instanceof Error) return error.message;
  if (error && typeof error === 'object' && 'message' in error) {
    return String((error as { message?: unknown }).message);
  }
  return 'Unexpected local Whisper error.';
};

const loadFirstWorkingTranscriber = async (): Promise<AutomaticSpeechRecognitionPipeline> => {
  const profiles = allowFp32Fallback ? [Q4_PROFILE, FP32_PROFILE] : [Q4_PROFILE];
  const errors: string[] = [];

  for (const profile of profiles) {
    activeProfile = profile;
    post({ kind: 'loading', model: modelId, profile: profile.label });
    try {
      return await pipeline('automatic-speech-recognition', modelId, {
        device: 'wasm',
        dtype: profile.dtype,
        progress_callback: progress => {
          post({
            kind: 'loading',
            model: modelId,
            profile: profile.label,
            file: 'file' in progress ? progress.file : undefined,
            progress: 'progress' in progress ? progress.progress : undefined,
          });
        },
      });
    } catch (error) {
      errors.push(`${profile.label}: ${messageFrom(error)}`);
    }
  }

  throw new Error(errors.join(' | '));
};

const loadTranscriber = async (): Promise<AutomaticSpeechRecognitionPipeline> => {
  transcriberPromise ??= loadFirstWorkingTranscriber();
  return transcriberPromise;
};

const initialize = async (): Promise<void> => {
  try {
    await loadTranscriber();
    post({ kind: 'ready', model: modelId, profile: activeProfile.label });
  } catch (error) {
    transcriberPromise = null;
    post({ kind: 'error', message: messageFrom(error) });
  }
};

const transcribe = async (request: Extract<ObserverWhisperRequest, { kind: 'transcribe' }>) => {
  try {
    const transcriber = await loadTranscriber();
    const startedAt = performance.now();
    const output = await transcriber(new Float32Array(request.audio), { max_new_tokens: 32 });
    post({
      kind: 'result',
      requestId: request.requestId,
      text: output.text ?? '',
      elapsedMs: Math.round(performance.now() - startedAt),
    });
  } catch (error) {
    post({ kind: 'error', requestId: request.requestId, message: messageFrom(error) });
  }
};

self.onmessage = (event: MessageEvent<ObserverWhisperRequest>) => {
  const request = event.data;
  if (request.kind === 'init') {
    modelId = request.model;
    allowFp32Fallback = request.allowFp32Fallback;
    void initialize();
    return;
  }
  void transcribe(request);
};

export {};
