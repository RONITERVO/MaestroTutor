// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { WeightedPrompt } from '@google/genai';
import { debugLogService } from '../../features/diagnostics';
import { mergeInt16Arrays, pcmToWav } from '../../features/speech/utils/audioProcessing';
import { getGeminiModels } from '../../core/config/models';
import { getAi } from './client';

const DEFAULT_SAMPLE_RATE = 48000;
const DEFAULT_CHANNELS = 2;
const DEFAULT_DURATION_SECONDS = 12;
const MIN_DURATION_SECONDS = 8;
const MAX_DURATION_SECONDS = 20;
const SETUP_TIMEOUT_MS = 12000;
const GENERATION_TIMEOUT_MS = 90000;

const base64ToUint8 = (base64: string): Uint8Array => {
  const binaryString = atob(base64);
  const bytes = new Uint8Array(binaryString.length);
  for (let i = 0; i < binaryString.length; i++) {
    bytes[i] = binaryString.charCodeAt(i);
  }
  return bytes;
};

const base64ToInt16 = (base64: string): Int16Array => {
  const bytes = base64ToUint8(base64);
  return new Int16Array(bytes.buffer);
};

const clampDuration = (value?: number): number => {
  if (!Number.isFinite(value)) return DEFAULT_DURATION_SECONDS;
  return Math.max(MIN_DURATION_SECONDS, Math.min(MAX_DURATION_SECONDS, Math.round(value as number)));
};

const normalizeMusicModel = (model: string): string => {
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

export interface GeminiMusicResult {
  dataUrl: string;
  mimeType: 'audio/wav';
  durationSeconds: number;
  sampleRate: number;
  channels: number;
}

export const generateMusic = async (params: {
  prompt: string;
  durationSeconds?: number;
  abortSignal?: AbortSignal;
}): Promise<GeminiMusicResult> => {
  const prompt = (params.prompt || '').trim();
  if (!prompt) {
    throw new Error('Music prompt is empty.');
  }

  const ai = await getAi({ apiVersion: 'v1alpha' });
  const model = normalizeMusicModel(getGeminiModels().music.generation);
  const targetDurationSeconds = clampDuration(params.durationSeconds);
  const weightedPrompts: WeightedPrompt[] = [
    {
      text: `${prompt}. Instrumental only. No vocals, no lyrics, no copyrighted melodies. Original educational backing track.`,
      weight: 1,
    },
  ];

  const log = debugLogService.logRequest('generateMusic', model, {
    prompt,
    durationSeconds: targetDurationSeconds,
  });

  return new Promise<GeminiMusicResult>((resolve, reject) => {
    let session: any = null;
    let isSettled = false;
    let sampleRate = DEFAULT_SAMPLE_RATE;
    let channels = DEFAULT_CHANNELS;
    let totalSamples = 0;
    let setupTimer: ReturnType<typeof setTimeout> | null = null;
    let generationTimer: ReturnType<typeof setTimeout> | null = null;
    let startedPlayback = false;
    let pausedForEnoughAudio = false;
    const pcmChunks: Int16Array[] = [];

    const cleanup = () => {
      if (setupTimer) clearTimeout(setupTimer);
      if (generationTimer) clearTimeout(generationTimer);
      setupTimer = null;
      generationTimer = null;
    };

    const resolveOnce = (result: GeminiMusicResult) => {
      if (isSettled) return;
      isSettled = true;
      cleanup();
      resolve(result);
    };

    const rejectOnce = (error: any) => {
      if (isSettled) return;
      isSettled = true;
      cleanup();
      reject(error);
    };

    const finalize = () => {
      const merged = mergeInt16Arrays(pcmChunks);
      if (!merged.length) {
        const error = new Error('No music audio was generated.');
        log.error(error);
        rejectOnce(error);
        return;
      }

      const durationSeconds = merged.length / Math.max(1, sampleRate * channels);
      log.complete({
        durationSeconds,
        sampleRate,
        channels,
        sampleCount: merged.length,
      });
      resolveOnce({
        dataUrl: pcmToWav(merged, sampleRate, channels),
        mimeType: 'audio/wav',
        durationSeconds,
        sampleRate,
        channels,
      });
    };

    const abort = (reason: string) => {
      cleanup();
      try { session?.close(); } catch {}
      rejectOnce(new Error(reason));
    };

    const maybeStopAfterEnoughAudio = () => {
      const durationSeconds = totalSamples / Math.max(1, sampleRate * channels);
      if (!pausedForEnoughAudio && durationSeconds >= targetDurationSeconds) {
        pausedForEnoughAudio = true;
        try { session?.pause(); } catch {}
        window.setTimeout(() => {
          try { session?.close(); } catch {}
          finalize();
        }, 250);
      }
    };

    const startPlayback = async () => {
      if (startedPlayback || isSettled) return;
      startedPlayback = true;
      try {
        await session.setWeightedPrompts({ weightedPrompts });
        await session.setMusicGenerationConfig({
          musicGenerationConfig: {
            musicGenerationMode: 'QUALITY',
            temperature: 1.1,
            guidance: 4,
            sampleRateHz: DEFAULT_SAMPLE_RATE,
            audioFormat: 'pcm16',
          } as any,
        });
        session.play();
      } catch (error: any) {
        abort(error?.message || 'Music playback could not be started.');
      }
    };

    setupTimer = setTimeout(() => {
      abort('Music generation setup timed out.');
    }, SETUP_TIMEOUT_MS);

    generationTimer = setTimeout(() => {
      if (pcmChunks.length > 0) {
        try { session?.pause(); } catch {}
        try { session?.close(); } catch {}
        finalize();
        return;
      }
      abort('Music generation timed out.');
    }, GENERATION_TIMEOUT_MS);

    ai.live.music.connect({
      model,
      callbacks: {
        onmessage: (message: any) => {
          if (message?.setupComplete) {
            if (setupTimer) {
              clearTimeout(setupTimer);
              setupTimer = null;
            }
            void startPlayback();
          }

          const filteredReason = message?.filteredPrompt?.filteredReason;
          if (filteredReason && !pcmChunks.length) {
            abort(`Music prompt was filtered: ${filteredReason}`);
            return;
          }

          const audioChunks = Array.isArray(message?.serverContent?.audioChunks)
            ? message.serverContent.audioChunks
            : [];
          for (const chunk of audioChunks) {
            if (!chunk?.data) continue;
            sampleRate = parseIntParam(chunk.mimeType, 'rate') || parseIntParam(chunk.mimeType, 'sampleRate') || sampleRate;
            channels = parseIntParam(chunk.mimeType, 'channels') || channels;
            const pcm = base64ToInt16(chunk.data);
            pcmChunks.push(pcm);
            totalSamples += pcm.length;
            maybeStopAfterEnoughAudio();
          }
        },
        onclose: () => {
          cleanup();
          if (!isSettled && pcmChunks.length > 0) {
            finalize();
          }
        },
        onerror: (error: any) => {
          const message = error?.message || 'Music generation failed.';
          log.error({ message });
          rejectOnce(new Error(message));
        },
      },
    }).then((connectedSession: any) => {
      session = connectedSession;
      if (params.abortSignal) {
        if (params.abortSignal.aborted) {
          abort('Music generation aborted.');
          return;
        }
        params.abortSignal.addEventListener('abort', () => abort('Music generation aborted.'), { once: true });
      }
    }).catch((error: any) => {
      const message = error?.message || 'Music session failed.';
      log.error({ message });
      rejectOnce(new Error(message));
    });
  });
};
