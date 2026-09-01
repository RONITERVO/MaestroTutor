// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { Modality } from '@google/genai';
import type { CoreGeminiClient } from '../managedGeminiClient';
import { createCoreRuntime, type CoreRuntime } from '../runtime';
import { mergeInt16Arrays, pcmToWav } from './audioProcessing';
import { getLiveMinimalThinkingConfig } from './liveModelCompatibility';

const OUTPUT_SAMPLE_RATE = 24_000;
const SESSION_TIMEOUT_MS = 180_000;
const MAX_TRIGGER_DURATION_MS = 10_000;

const base64ToBytes = (base64: string): Uint8Array => {
  const binary = globalThis.atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) bytes[index] = binary.charCodeAt(index);
  return bytes;
};

const bytesToBase64 = (bytes: Uint8Array): string => {
  let binary = '';
  for (let index = 0; index < bytes.length; index += 0x8000) {
    binary += String.fromCharCode(...bytes.subarray(index, index + 0x8000));
  }
  return globalThis.btoa(binary);
};

const base64ToInt16 = (base64: string): Int16Array => {
  const bytes = base64ToBytes(base64);
  return new Int16Array(bytes.buffer, 0, Math.floor(bytes.byteLength / 2));
};

export interface CoreAudioNoteResult {
  operationId: string;
  dataUrl: string;
  mimeType: 'audio/wav';
  durationSeconds: number;
  sampleCount: number;
}

/** Framework-free Gemini Live audio-note journey shared by UI and headless. */
export const runCoreAudioNoteGeneration = async (params: {
  aiClient: CoreGeminiClient;
  model: string;
  text: string;
  triggerPcmBase64: string;
  triggerSampleRate: number;
  langCode?: string;
  voiceName?: string;
  abortSignal?: AbortSignal;
  operationId?: string;
  runtime?: CoreRuntime;
  onUsageMetadata?: (metadata: Record<string, unknown>) => void;
}): Promise<CoreAudioNoteResult> => {
  const text = (params.text || '').trim();
  if (!text) throw new Error('Audio note text is empty.');
  if (!params.triggerPcmBase64) throw new Error('Audio note trigger PCM is empty.');

  const runtime = params.runtime || createCoreRuntime();
  const operationId = params.operationId || runtime.ids.create('audio-note');
  const model = params.model.trim();
  const voiceName = (params.voiceName || 'Kore').trim() || 'Kore';
  const systemInstruction = [
    'You are a professional text-to-speech engine.',
    'Read the provided text aloud exactly as written.',
    'Do not add any intro, explanation, or extra words.',
    'Keep the delivery warm and clear.',
    params.langCode ? `Language hint: ${params.langCode}` : '',
    'TEXT TO READ:',
    text,
  ].filter(Boolean).join('\n');
  const emit = (phase: string, data?: Record<string, unknown>) => runtime.events.emit({
    operationId,
    journey: 'media',
    phase,
    data,
  });
  emit('audioNote.started', { model, voiceName, textLength: text.length, langCode: params.langCode });

  return new Promise<CoreAudioNoteResult>((resolve, reject) => {
    let session: any = null;
    let settled = false;
    let streaming = false;
    let intervalId: ReturnType<typeof setInterval> | null = null;
    let timeoutId: ReturnType<typeof setTimeout> | null = null;
    const chunks: Int16Array[] = [];

    const cleanup = () => {
      streaming = false;
      if (intervalId) globalThis.clearInterval(intervalId);
      if (timeoutId) globalThis.clearTimeout(timeoutId);
      intervalId = null;
      timeoutId = null;
      params.abortSignal?.removeEventListener('abort', handleAbort);
    };
    const close = () => {
      try { session?.close(); } catch {}
    };
    const rejectOnce = (error: Error) => {
      if (settled) return;
      settled = true;
      cleanup();
      close();
      emit('audioNote.failed', { errorName: error.name, errorMessage: error.message });
      reject(error);
    };
    const finalize = () => {
      if (settled) return;
      const merged = mergeInt16Arrays(chunks);
      if (!merged.length) {
        rejectOnce(new Error('No audio note was generated.'));
        return;
      }
      settled = true;
      cleanup();
      close();
      const result: CoreAudioNoteResult = {
        operationId,
        dataUrl: pcmToWav(merged, OUTPUT_SAMPLE_RATE, 1),
        mimeType: 'audio/wav',
        durationSeconds: merged.length / OUTPUT_SAMPLE_RATE,
        sampleCount: merged.length,
      };
      emit('audioNote.succeeded', {
        model,
        durationSeconds: result.durationSeconds,
        sampleCount: result.sampleCount,
      });
      resolve(result);
    };
    function handleAbort() {
      rejectOnce(new Error('Audio note generation aborted.'));
    }

    if (params.abortSignal?.aborted) {
      handleAbort();
      return;
    }
    params.abortSignal?.addEventListener('abort', handleAbort, { once: true });

    void params.aiClient.live.connect({
      model,
      config: {
        responseModalities: [Modality.AUDIO],
        speechConfig: { voiceConfig: { prebuiltVoiceConfig: { voiceName } } },
        systemInstruction: { parts: [{ text: systemInstruction }] },
        outputAudioTranscription: {},
        thinkingConfig: getLiveMinimalThinkingConfig(model),
      },
      callbacks: {
        onopen: () => emit('audioNote.sessionOpened', { model }),
        onmessage: (message: any) => {
          if (settled) return;
          if (message?.usageMetadata && typeof message.usageMetadata === 'object') {
            params.onUsageMetadata?.(message.usageMetadata as Record<string, unknown>);
          }
          const parts = message?.serverContent?.modelTurn?.parts;
          if (Array.isArray(parts)) {
            for (const part of parts) {
              const data = part?.inlineData?.data;
              if (typeof data !== 'string' || !data) continue;
              const pcm = base64ToInt16(data);
              chunks.push(pcm);
              emit('audioNote.chunkReceived', { samples: pcm.length });
            }
          }
          if (message?.serverContent?.turnComplete) finalize();
        },
        onclose: () => {
          if (settled) return;
          if (chunks.length > 0) finalize();
          else rejectOnce(new Error('Audio note session closed before generating audio.'));
        },
        onerror: (error: unknown) => rejectOnce(new Error(
          error instanceof Error ? error.message : 'Audio note generation failed.',
        )),
      },
    }).then(connectedSession => {
      if (settled) {
        try { connectedSession?.close(); } catch {}
        return;
      }
      session = connectedSession;
      streaming = true;
      const triggerBytes = base64ToBytes(params.triggerPcmBase64);
      const chunkDurationMs = 100;
      const chunkSize = Math.floor(params.triggerSampleRate * chunkDurationMs / 1000) * 2;
      let sendOffset = 0;
      const startedAt = runtime.clock.now();
      timeoutId = globalThis.setTimeout(() => {
        if (streaming && !settled) rejectOnce(new Error('Audio note generation timed out.'));
      }, SESSION_TIMEOUT_MS);
      intervalId = globalThis.setInterval(() => {
        if (!streaming || !session || settled) return;
        const elapsed = runtime.clock.now() - startedAt;
        let chunk: Uint8Array;
        if (sendOffset < triggerBytes.length) {
          const end = Math.min(sendOffset + chunkSize, triggerBytes.length);
          chunk = triggerBytes.slice(sendOffset, end);
          sendOffset = end;
        } else if (elapsed <= MAX_TRIGGER_DURATION_MS) {
          chunk = new Uint8Array(chunkSize);
        } else {
          return;
        }
        try {
          session.sendRealtimeInput({
            audio: {
              mimeType: `audio/pcm;rate=${params.triggerSampleRate}`,
              data: bytesToBase64(chunk),
            },
          });
        } catch (error) {
          rejectOnce(new Error(error instanceof Error ? error.message : 'Audio note trigger failed.'));
        }
      }, chunkDurationMs);
    }).catch(error => rejectOnce(new Error(
      error instanceof Error ? error.message : 'Audio note session failed.',
    )));
  });
};
