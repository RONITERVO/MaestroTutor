// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { CoreGeminiClient } from '../managedGeminiClient';
import { createCoreRuntime, type CoreRuntime } from '../runtime';
import { mergeInt16Arrays, pcmToWav } from './audioProcessing';
import type {
  BackendMusicGenerationRequest,
  BackendMusicGenerationResponse,
} from '../../core/contracts/backend';

export const DEFAULT_MUSIC_SAMPLE_RATE = 48_000;
export const DEFAULT_MUSIC_CHANNELS = 2;
export const DEFAULT_MUSIC_DURATION_SECONDS = 12;
export const MIN_MUSIC_DURATION_SECONDS = 8;
export const MAX_MUSIC_DURATION_SECONDS = 20;

const SETUP_TIMEOUT_MS = 12_000;
const GENERATION_TIMEOUT_MS = 90_000;

export interface CoreMusicGenerationResult {
  operationId: string;
  dataUrl: string;
  mimeType: 'audio/wav';
  durationSeconds: number;
  sampleRate: number;
  channels: number;
  sampleCount: number;
}

export interface CoreMusicChunk {
  pcmBase64: string;
  sampleRate: number;
  channels: number;
  totalSamples: number;
}

export interface CoreManagedMusicBackendPort {
  generateMusic(
    request: BackendMusicGenerationRequest,
    signal?: AbortSignal | null,
  ): Promise<BackendMusicGenerationResponse>;
}

export const clampMusicDuration = (value?: number): number => {
  if (!Number.isFinite(value)) return DEFAULT_MUSIC_DURATION_SECONDS;
  return Math.max(
    MIN_MUSIC_DURATION_SECONDS,
    Math.min(MAX_MUSIC_DURATION_SECONDS, Math.round(value as number)),
  );
};

export const normalizeMusicModel = (model: string): string => {
  const normalized = (model || '').trim();
  if (!normalized) return 'models/lyria-realtime-exp';
  return normalized.startsWith('models/') ? normalized : `models/${normalized}`;
};

const parseIntParam = (mimeType: string | undefined, name: string): number | undefined => {
  const match = (mimeType || '').match(new RegExp(`${name}=([0-9]+)`, 'i'));
  if (!match) return undefined;
  const value = Number(match[1]);
  return Number.isFinite(value) && value > 0 ? value : undefined;
};

const base64ToInt16 = (base64: string): Int16Array => {
  const binary = globalThis.atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index);
  }
  return new Int16Array(bytes.buffer, 0, Math.floor(bytes.byteLength / 2));
};

const readProviderErrorMessage = (value: unknown, fallback: string): string => {
  if (value instanceof Error && value.message) return value.message;
  if (value && typeof value === 'object') {
    const event = value as { message?: unknown; error?: unknown; reason?: unknown };
    if (typeof event.message === 'string' && event.message) return event.message;
    if (event.error instanceof Error && event.error.message) return event.error.message;
    if (typeof event.reason === 'string' && event.reason) return event.reason;
  }
  return fallback;
};

/**
 * Managed Lyria journey shared verbatim by the visual client and headless
 * harness. The backend owns the provider WebSocket because Lyria does not
 * accept Gemini ephemeral tokens; the returned PCM boundary remains available
 * to browser playback and deterministic automation observers.
 */
export const runCoreManagedMusicGeneration = async (params: {
  backend: CoreManagedMusicBackendPort;
  model: string;
  prompt: string;
  durationSeconds?: number;
  abortSignal?: AbortSignal;
  operationId?: string;
  runtime?: CoreRuntime;
  onPcmChunk?: (chunk: CoreMusicChunk) => boolean | void | Promise<boolean | void>;
  onPcmObserverStart?: () => void;
}): Promise<CoreMusicGenerationResult> => {
  const prompt = (params.prompt || '').trim();
  if (!prompt) throw new Error('Music prompt is empty.');

  const runtime = params.runtime || createCoreRuntime();
  const operationId = params.operationId || runtime.ids.create('music-generate');
  const model = normalizeMusicModel(params.model);
  const targetDurationSeconds = clampMusicDuration(params.durationSeconds);
  const emit = (phase: string, data?: Record<string, unknown>) => runtime.events.emit({
    operationId,
    journey: 'media',
    phase,
    data,
  });

  emit('music.started', { model, targetDurationSeconds, transport: 'managed-backend' });
  try {
    const response = await params.backend.generateMusic({
      model,
      prompt,
      durationSeconds: targetDurationSeconds,
    }, params.abortSignal);
    const pcm = base64ToInt16(response.pcmBase64);
    if (!pcm.length) throw new Error('Managed music generation returned no audio.');
    if (params.onPcmChunk) {
      const observed = await params.onPcmChunk({
        pcmBase64: response.pcmBase64,
        sampleRate: response.sampleRate,
        channels: response.channels,
        totalSamples: response.sampleCount,
      });
      if (observed) params.onPcmObserverStart?.();
    }
    const result: CoreMusicGenerationResult = {
      operationId,
      dataUrl: pcmToWav(pcm, response.sampleRate, response.channels),
      mimeType: 'audio/wav',
      durationSeconds: response.durationSeconds,
      sampleRate: response.sampleRate,
      channels: response.channels,
      sampleCount: response.sampleCount,
    };
    emit('music.succeeded', {
      model,
      durationSeconds: result.durationSeconds,
      sampleRate: result.sampleRate,
      channels: result.channels,
      sampleCount: result.sampleCount,
      transport: 'managed-backend',
    });
    return result;
  } catch (error) {
    const normalized = error instanceof Error ? error : new Error('Music generation failed.');
    emit('music.failed', { errorName: normalized.name, errorMessage: normalized.message });
    throw normalized;
  }
};

/**
 * Framework-free Lyria stream journey shared by the visual UI and headless
 * client. Browser playback is an optional observer at the PCM stream boundary;
 * it cannot change provider requests, stop conditions or the returned WAV.
 */
export const runCoreMusicGeneration = async (params: {
  aiClient: CoreGeminiClient;
  model: string;
  prompt: string;
  durationSeconds?: number;
  abortSignal?: AbortSignal;
  operationId?: string;
  runtime?: CoreRuntime;
  onPcmChunk?: (chunk: CoreMusicChunk) => boolean | void | Promise<boolean | void>;
  onPcmObserverStart?: () => void;
}): Promise<CoreMusicGenerationResult> => {
  const prompt = (params.prompt || '').trim();
  if (!prompt) throw new Error('Music prompt is empty.');

  const runtime = params.runtime || createCoreRuntime();
  const operationId = params.operationId || runtime.ids.create('music-generate');
  const model = normalizeMusicModel(params.model);
  const targetDurationSeconds = clampMusicDuration(params.durationSeconds);
  const emit = (phase: string, data?: Record<string, unknown>) => runtime.events.emit({
    operationId,
    journey: 'media',
    phase,
    data,
  });

  emit('music.started', { model, targetDurationSeconds });

  return new Promise<CoreMusicGenerationResult>((resolve, reject) => {
    let session: any = null;
    let settled = false;
    let sampleRate = DEFAULT_MUSIC_SAMPLE_RATE;
    let channels = DEFAULT_MUSIC_CHANNELS;
    let totalSamples = 0;
    let setupComplete = false;
    let startingPlayback = false;
    let playbackStarted = false;
    let observerStarted = false;
    let targetReached = false;
    let lastWarning = '';
    let setupTimer: ReturnType<typeof setTimeout> | null = null;
    let generationTimer: ReturnType<typeof setTimeout> | null = null;
    let finalizeTimer: ReturnType<typeof setTimeout> | null = null;
    const chunks: Int16Array[] = [];

    const cleanup = () => {
      if (setupTimer) globalThis.clearTimeout(setupTimer);
      if (generationTimer) globalThis.clearTimeout(generationTimer);
      if (finalizeTimer) globalThis.clearTimeout(finalizeTimer);
      setupTimer = null;
      generationTimer = null;
      finalizeTimer = null;
      params.abortSignal?.removeEventListener('abort', handleAbort);
    };

    const closeSession = () => {
      try { session?.close(); } catch {}
    };

    const rejectOnce = (error: Error) => {
      if (settled) return;
      settled = true;
      cleanup();
      closeSession();
      emit('music.failed', { errorName: error.name, errorMessage: error.message });
      reject(error);
    };

    const finalize = () => {
      if (settled) return;
      const merged = mergeInt16Arrays(chunks);
      if (!merged.length) {
        rejectOnce(new Error('No music audio was generated.'));
        return;
      }
      settled = true;
      cleanup();
      closeSession();
      const durationSeconds = merged.length / Math.max(1, sampleRate * channels);
      const result: CoreMusicGenerationResult = {
        operationId,
        dataUrl: pcmToWav(merged, sampleRate, channels),
        mimeType: 'audio/wav',
        durationSeconds,
        sampleRate,
        channels,
        sampleCount: merged.length,
      };
      emit('music.succeeded', {
        model,
        durationSeconds,
        sampleRate,
        channels,
        sampleCount: merged.length,
      });
      resolve(result);
    };

    function handleAbort() {
      rejectOnce(new Error('Music generation aborted.'));
    }

    const startPlayback = async () => {
      if (playbackStarted || startingPlayback || settled || !session || !setupComplete) return;
      startingPlayback = true;
      try {
        await session.setWeightedPrompts({
          weightedPrompts: [{
            text: `${prompt}. Instrumental only. No vocals, no lyrics, no copyrighted melodies. Original educational backing track.`,
            weight: 1,
          }],
        });
        await session.setMusicGenerationConfig({
          musicGenerationConfig: {
            musicGenerationMode: 'QUALITY',
            temperature: 1.1,
            guidance: 4,
          },
        });
        session.play();
        playbackStarted = true;
        emit('music.providerPlaybackStarted', { model });
      } catch (error) {
        rejectOnce(new Error(
          error instanceof Error ? error.message : 'Music playback could not be started.',
        ));
      } finally {
        startingPlayback = false;
      }
    };

    setupTimer = globalThis.setTimeout(() => {
      rejectOnce(new Error('Music generation setup timed out before setupComplete.'));
    }, SETUP_TIMEOUT_MS);
    generationTimer = globalThis.setTimeout(() => {
      if (chunks.length > 0) {
        try { session?.pause(); } catch {}
        finalize();
        return;
      }
      rejectOnce(new Error('Music generation timed out.'));
    }, GENERATION_TIMEOUT_MS);

    if (params.abortSignal?.aborted) {
      handleAbort();
      return;
    }
    params.abortSignal?.addEventListener('abort', handleAbort, { once: true });

    void params.aiClient.live.music.connect({
      model,
      callbacks: {
        onmessage: (message: any) => {
          if (settled) return;
          if (typeof message?.warning === 'string' && message.warning.trim()) {
            lastWarning = message.warning.trim();
          }
          if (message?.setupComplete) {
            setupComplete = true;
            if (setupTimer) globalThis.clearTimeout(setupTimer);
            setupTimer = null;
            emit('music.setupComplete', { model });
            void startPlayback();
          }

          const filteredReason = message?.filteredPrompt?.filteredReason;
          if (filteredReason && chunks.length === 0) {
            rejectOnce(new Error(`Music prompt was filtered: ${filteredReason}`));
            return;
          }

          const audioChunks = Array.isArray(message?.serverContent?.audioChunks)
            ? message.serverContent.audioChunks
            : [];
          for (const chunk of audioChunks) {
            if (settled || typeof chunk?.data !== 'string' || !chunk.data) continue;
            if (setupTimer) globalThis.clearTimeout(setupTimer);
            setupTimer = null;
            sampleRate = parseIntParam(chunk.mimeType, 'rate')
              || parseIntParam(chunk.mimeType, 'sampleRate')
              || sampleRate;
            channels = parseIntParam(chunk.mimeType, 'channels') || channels;
            const pcm = base64ToInt16(chunk.data);
            chunks.push(pcm);
            totalSamples += pcm.length;
            emit('music.chunkReceived', { sampleRate, channels, samples: pcm.length, totalSamples });

            if (params.onPcmChunk) {
              void Promise.resolve(params.onPcmChunk({
                pcmBase64: chunk.data,
                sampleRate,
                channels,
                totalSamples,
              })).then(started => {
                if (!settled && started && !observerStarted) {
                  observerStarted = true;
                  params.onPcmObserverStart?.();
                }
              }).catch(() => undefined);
            }

            const durationSeconds = totalSamples / Math.max(1, sampleRate * channels);
            if (!targetReached && durationSeconds >= targetDurationSeconds) {
              targetReached = true;
              try { session?.pause(); } catch {}
              finalizeTimer = globalThis.setTimeout(finalize, 250);
            }
          }
        },
        onclose: () => {
          if (settled) return;
          if (chunks.length > 0) {
            finalize();
            return;
          }
          rejectOnce(new Error(
            lastWarning || (setupComplete
              ? 'Lyria RealTime stream closed before generating audio.'
              : 'Lyria RealTime closed before setup completed.'),
          ));
        },
        onerror: (error: unknown) => {
          const message = readProviderErrorMessage(
            error,
            lastWarning || 'Music generation failed.',
          );
          rejectOnce(new Error(message));
        },
      },
    }).then(connectedSession => {
      if (settled) {
        try { connectedSession?.close(); } catch {}
        return;
      }
      session = connectedSession;
      void startPlayback();
    }).catch(error => {
      rejectOnce(new Error(error instanceof Error ? error.message : 'Music session failed.'));
    });
  });
};
