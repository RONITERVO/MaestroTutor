// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { GoogleGenAI, type WeightedPrompt } from '@google/genai';
import { debugLogService } from '../../features/diagnostics';
import { mergeInt16Arrays, pcmToWav } from '../../features/speech/utils/audioProcessing';
import { getGeminiModels } from '../../core/config/models';
import { resolveLiveConnectApiKey } from './client';

const DEFAULT_SAMPLE_RATE = 48000;
const DEFAULT_CHANNELS = 2;
const DEFAULT_DURATION_SECONDS = 12;
const MIN_DURATION_SECONDS = 8;
const MAX_DURATION_SECONDS = 20;
const SETUP_TIMEOUT_MS = 12000;
const GENERATION_TIMEOUT_MS = 90000;
const STREAM_PLAYBACK_GAIN = 0.22;

type ActiveMusicPlayback = {
  audioContext: AudioContext;
  gainNode: GainNode;
  nextStartTime: number;
  activeSources: Set<AudioBufferSourceNode>;
};

let activeMusicPlayback: ActiveMusicPlayback | null = null;

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

const pcmToAudioBuffer = (
  pcmData: Uint8Array,
  audioContext: AudioContext,
  sampleRate: number,
  numChannels: number
): AudioBuffer => {
  const dataInt16 = new Int16Array(pcmData.buffer, pcmData.byteOffset, Math.floor(pcmData.byteLength / 2));
  const frameCount = Math.max(1, Math.floor(dataInt16.length / Math.max(1, numChannels)));
  const buffer = audioContext.createBuffer(numChannels, frameCount, sampleRate);

  for (let channel = 0; channel < numChannels; channel++) {
    const channelData = buffer.getChannelData(channel);
    for (let i = 0; i < frameCount; i++) {
      const sampleIndex = i * numChannels + channel;
      channelData[i] = (dataInt16[sampleIndex] || 0) / 32768;
    }
  }

  return buffer;
};

const stopActiveMusicPlayback = async () => {
  const playback = activeMusicPlayback;
  activeMusicPlayback = null;
  if (!playback) return;

  for (const source of playback.activeSources) {
    try { source.stop(); } catch {}
    try { source.disconnect(); } catch {}
  }
  playback.activeSources.clear();

  try { playback.gainNode.disconnect(); } catch {}
  try { await playback.audioContext.close(); } catch {}
};

const ensureMusicPlayback = async (): Promise<ActiveMusicPlayback | null> => {
  if (typeof window === 'undefined') return null;

  const AudioContextCtor = window.AudioContext || (window as any).webkitAudioContext;
  if (!AudioContextCtor) return null;

  if (activeMusicPlayback && activeMusicPlayback.audioContext.state !== 'closed') {
    if (activeMusicPlayback.audioContext.state === 'suspended') {
      try { await activeMusicPlayback.audioContext.resume(); } catch {}
    }
    return activeMusicPlayback;
  }

  const audioContext = new AudioContextCtor({ sampleRate: DEFAULT_SAMPLE_RATE });
  if (audioContext.state === 'suspended') {
    try { await audioContext.resume(); } catch {}
  }

  const gainNode = audioContext.createGain();
  gainNode.gain.value = STREAM_PLAYBACK_GAIN;
  gainNode.connect(audioContext.destination);

  activeMusicPlayback = {
    audioContext,
    gainNode,
    nextStartTime: audioContext.currentTime,
    activeSources: new Set(),
  };

  return activeMusicPlayback;
};

const queueMusicChunkForPlayback = async (
  base64Chunk: string,
  sampleRate: number,
  channels: number
): Promise<boolean> => {
  const playback = await ensureMusicPlayback();
  if (!playback) return false;

  const chunkBytes = base64ToUint8(base64Chunk);
  const audioBuffer = pcmToAudioBuffer(chunkBytes, playback.audioContext, sampleRate, channels);
  const source = playback.audioContext.createBufferSource();
  source.buffer = audioBuffer;
  source.connect(playback.gainNode);
  playback.activeSources.add(source);

  source.onended = () => {
    playback.activeSources.delete(source);
    try { source.disconnect(); } catch {}
  };

  playback.nextStartTime = Math.max(playback.audioContext.currentTime, playback.nextStartTime);
  source.start(playback.nextStartTime);
  playback.nextStartTime += audioBuffer.duration;
  return true;
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
  streamPlayback?: boolean;
  onStreamPlaybackStart?: () => void;
}): Promise<GeminiMusicResult> => {
  const prompt = (params.prompt || '').trim();
  if (!prompt) {
    throw new Error('Music prompt is empty.');
  }

  const model = normalizeMusicModel(getGeminiModels().music.generation);
  const targetDurationSeconds = clampDuration(params.durationSeconds);
  const apiKey = await resolveLiveConnectApiKey({
    purpose: 'music',
    durationSeconds: targetDurationSeconds,
  });
  const ai = new GoogleGenAI({ apiKey, apiVersion: 'v1alpha' });
  const shouldStreamPlayback = params.streamPlayback !== false;

  if (shouldStreamPlayback) {
    await stopActiveMusicPlayback();
  }

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
    let hasSetupComplete = false;
    let isStartingPlayback = false;
    let startedPlayback = false;
    let hasStartedStreamingPlayback = false;
    let hasReachedTargetDuration = false;
    let lastWarning = '';
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
      if (shouldStreamPlayback) {
        void stopActiveMusicPlayback();
      }
      rejectOnce(new Error(reason));
    };

    const startPlayback = async () => {
      if (startedPlayback || isStartingPlayback || isSettled || !session || !hasSetupComplete) return;
      isStartingPlayback = true;
      try {
        await session.setWeightedPrompts({ weightedPrompts });
        await session.setMusicGenerationConfig({
          musicGenerationConfig: {
            musicGenerationMode: 'QUALITY',
            temperature: 1.1,
            guidance: 4,
          } as any,
        });
        session.play();
        startedPlayback = true;
      } catch (error: any) {
        abort(error?.message || 'Music playback could not be started.');
      } finally {
        isStartingPlayback = false;
      }
    };

    setupTimer = setTimeout(() => {
      abort('Music generation setup timed out before setupComplete.');
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

    if (params.abortSignal) {
      if (params.abortSignal.aborted) {
        abort('Music generation aborted.');
        return;
      }
      params.abortSignal.addEventListener('abort', () => abort('Music generation aborted.'), { once: true });
    }

    ai.live.music.connect({
      model,
      callbacks: {
        onmessage: (message: any) => {
          if (typeof message?.warning === 'string' && message.warning.trim()) {
            lastWarning = message.warning.trim();
          }

          if (message?.setupComplete) {
            hasSetupComplete = true;
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

            if (setupTimer) {
              clearTimeout(setupTimer);
              setupTimer = null;
            }

            sampleRate = parseIntParam(chunk.mimeType, 'rate')
              || parseIntParam(chunk.mimeType, 'sampleRate')
              || sampleRate;
            channels = parseIntParam(chunk.mimeType, 'channels') || channels;

            const pcm = base64ToInt16(chunk.data);
            pcmChunks.push(pcm);
            totalSamples += pcm.length;

            if (shouldStreamPlayback) {
              void queueMusicChunkForPlayback(chunk.data, sampleRate, channels).then((didStartPlayback) => {
                if (isSettled || !didStartPlayback || hasStartedStreamingPlayback) return;
                hasStartedStreamingPlayback = true;
                params.onStreamPlaybackStart?.();
              }).catch(() => {});
            }

            const durationSeconds = totalSamples / Math.max(1, sampleRate * channels);
            if (!hasReachedTargetDuration && durationSeconds >= targetDurationSeconds) {
              hasReachedTargetDuration = true;
              try { session?.pause(); } catch {}
              window.setTimeout(() => {
                try { session?.close(); } catch {}
                finalize();
              }, 250);
            }
          }
        },
        onclose: () => {
          cleanup();
          if (isSettled) return;

          if (pcmChunks.length > 0) {
            finalize();
            return;
          }

          if (shouldStreamPlayback) {
            void stopActiveMusicPlayback();
          }

          if (!hasSetupComplete) {
            rejectOnce(new Error(lastWarning || 'Lyria RealTime closed before setup completed.'));
            return;
          }

          rejectOnce(new Error(lastWarning || 'Lyria RealTime stream closed before generating audio.'));
        },
        onerror: (error: any) => {
          const message = error?.message || lastWarning || 'Music generation failed.';
          log.error({ message });
          if (shouldStreamPlayback) {
            void stopActiveMusicPlayback();
          }
          rejectOnce(new Error(message));
        },
      },
    }).then((connectedSession: any) => {
      if (isSettled) {
        try { connectedSession?.close(); } catch {}
        return;
      }
      session = connectedSession;
    }).catch((error: any) => {
      const message = error?.message || 'Music session failed.';
      log.error({ message });
      rejectOnce(new Error(message));
    });
  });
};
