// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { DEFAULT_SPEECH_GATE, isSpeechLike, measureEnergy } from '../../../../shared/audio/speechGate';

/** Ariadne's proven local detector settings. Gemini remains transcript authority. */
export const OBSERVER_WHISPER_MODEL = 'onnx-community/whisper-tiny.en';
export const OBSERVER_WHISPER_WINDOW_MS = 2600;
export const OBSERVER_WHISPER_MIN_AUDIO_MS = 1200;
export const OBSERVER_WHISPER_REQUEST_INTERVAL_MS = 900;
export const OBSERVER_SPEECH_BUFFER_MS = 7000;
export const OBSERVER_SPEECH_PREROLL_MS = 6000;

/**
 * Whisper's stock output for silence, music and subtitle-like source audio.
 * These are exact normalized transcripts, so saying a longer sentence that
 * happens to contain one of the words still works.
 */
const NON_SPEECH_TRANSCRIPTS = new Set([
  'you',
  'thank you',
  'thanks for watching',
  'music',
  'silence',
  'subtitles',
]);

export const isLikelySpeechTranscript = (text: string): boolean => {
  const clean = text
    .replace(/\[[^\]]+\]/g, ' ')
    .replace(/\([^)]*\)/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
  if (clean.length < 2 || !/[\p{L}\p{N}]/u.test(clean)) return false;

  const normalized = clean
    .toLowerCase()
    .replace(/[^\p{L}\p{N}\s]/gu, '')
    .replace(/\s+/g, ' ')
    .trim();
  return Boolean(normalized) && !NON_SPEECH_TRANSCRIPTS.has(normalized);
};

/** Return at most the newest `maxSamples`, retaining chronological order. */
export const recentPcmPackets = (
  packets: readonly Int16Array[],
  maxSamples: number,
): Int16Array[] => {
  let remaining = Math.max(0, Math.floor(maxSamples));
  const recent: Int16Array[] = [];

  for (let index = packets.length - 1; index >= 0 && remaining > 0; index -= 1) {
    const packet = packets[index];
    if (!packet.length) continue;
    const take = Math.min(packet.length, remaining);
    recent.push(take === packet.length ? packet : packet.slice(packet.length - take));
    remaining -= take;
  }

  recent.reverse();
  return recent;
};

/** Build the normalized 16kHz Float32 window expected by Transformers.js. */
export const pcmPacketsToWhisperWindow = (
  packets: readonly Int16Array[],
  sampleRate: number,
  windowMs = OBSERVER_WHISPER_WINDOW_MS,
): Float32Array | null => {
  if (sampleRate <= 0 || windowMs <= 0) return null;
  const maxSamples = Math.round(sampleRate * windowMs / 1000);
  const minimumSamples = Math.round(sampleRate * OBSERVER_WHISPER_MIN_AUDIO_MS / 1000);
  const recent = recentPcmPackets(packets, maxSamples);
  const totalSamples = recent.reduce((total, packet) => total + packet.length, 0);
  if (totalSamples < minimumSamples) return null;

  const pcm = new Int16Array(totalSamples);
  let writeOffset = 0;
  for (const packet of recent) {
    pcm.set(packet, writeOffset);
    writeOffset += packet.length;
  }
  if (!isSpeechLike(measureEnergy(pcm), DEFAULT_SPEECH_GATE)) return null;

  const audio = new Float32Array(pcm.length);
  for (let index = 0; index < pcm.length; index += 1) {
    audio[index] = pcm[index] / 32768;
  }
  return audio;
};
