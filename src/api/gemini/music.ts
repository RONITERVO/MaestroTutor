// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { getGeminiModels } from '../../core/config/models';
import {
  DEFAULT_MUSIC_SAMPLE_RATE,
  runCoreManagedMusicGeneration,
  runCoreMusicGeneration,
  type CoreMusicChunk,
  type CoreMusicGenerationResult,
} from '../../core-sdk/media/musicGeneration';
import { debugLogService } from '../../features/diagnostics';
import { trackMusicGeneration } from '../../shared/utils/costTracker';
import { getAi } from './client';
import { maestroAccessService } from '../../services/access/maestroAccessService';
import { maestroBackendService } from '../../services/backend/maestroBackendService';

const STREAM_PLAYBACK_GAIN = 0.22;

type ActiveMusicPlayback = {
  audioContext: AudioContext;
  gainNode: GainNode;
  nextStartTime: number;
  activeSources: Set<AudioBufferSourceNode>;
};

let activeMusicPlayback: ActiveMusicPlayback | null = null;

const base64ToUint8 = (base64: string): Uint8Array => {
  const binary = globalThis.atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index);
  }
  return bytes;
};

const pcmToAudioBuffer = (
  pcmData: Uint8Array,
  audioContext: AudioContext,
  sampleRate: number,
  numChannels: number,
): AudioBuffer => {
  const data = new Int16Array(pcmData.buffer, pcmData.byteOffset, Math.floor(pcmData.byteLength / 2));
  const frameCount = Math.max(1, Math.floor(data.length / Math.max(1, numChannels)));
  const buffer = audioContext.createBuffer(numChannels, frameCount, sampleRate);
  for (let channel = 0; channel < numChannels; channel += 1) {
    const channelData = buffer.getChannelData(channel);
    for (let index = 0; index < frameCount; index += 1) {
      channelData[index] = (data[index * numChannels + channel] || 0) / 32768;
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

  const audioContext = new AudioContextCtor({ sampleRate: DEFAULT_MUSIC_SAMPLE_RATE });
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
  channels: number,
): Promise<boolean> => {
  const playback = await ensureMusicPlayback();
  if (!playback) return false;
  const audioBuffer = pcmToAudioBuffer(
    base64ToUint8(base64Chunk),
    playback.audioContext,
    sampleRate,
    channels,
  );
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

export type GeminiMusicResult = Omit<CoreMusicGenerationResult, 'operationId' | 'sampleCount'>;

/** Browser adapter around the framework-free Core SDK Lyria journey. */
export const generateMusic = async (params: {
  prompt: string;
  durationSeconds?: number;
  abortSignal?: AbortSignal;
  streamPlayback?: boolean;
  onStreamPlaybackStart?: () => void;
}): Promise<GeminiMusicResult> => {
  const model = getGeminiModels().music.generation;
  const streamPlayback = params.streamPlayback !== false;
  if (streamPlayback) await stopActiveMusicPlayback();
  const log = debugLogService.logRequest('generateMusic', model, {
    prompt: params.prompt,
    durationSeconds: params.durationSeconds,
  });

  try {
    const common = {
      model,
      prompt: params.prompt,
      durationSeconds: params.durationSeconds,
      abortSignal: params.abortSignal,
      ...(streamPlayback ? {
        onPcmChunk: (chunk: CoreMusicChunk) => queueMusicChunkForPlayback(
          chunk.pcmBase64,
          chunk.sampleRate,
          chunk.channels,
        ),
        onPcmObserverStart: params.onStreamPlaybackStart,
      } : {}),
    };
    const result = (await maestroAccessService.resolveAccessMode()) === 'managed'
      ? await runCoreManagedMusicGeneration({ backend: maestroBackendService, ...common })
      : await runCoreMusicGeneration({
        aiClient: await getAi({ apiVersion: 'v1alpha' }),
        ...common,
      });
    trackMusicGeneration(model, result.durationSeconds);
    log.complete({
      durationSeconds: result.durationSeconds,
      sampleRate: result.sampleRate,
      channels: result.channels,
      sampleCount: result.sampleCount,
    });
    return {
      dataUrl: result.dataUrl,
      mimeType: result.mimeType,
      durationSeconds: result.durationSeconds,
      sampleRate: result.sampleRate,
      channels: result.channels,
    };
  } catch (error) {
    if (streamPlayback) await stopActiveMusicPlayback();
    log.error(error);
    throw error;
  }
};
