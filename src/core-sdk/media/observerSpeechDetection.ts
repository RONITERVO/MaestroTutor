// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { DEFAULT_SPEECH_GATE, isSpeechLike, measureEnergy } from '../../../shared/audio/speechGate';

/** Ariadne's proven local detector settings. Gemini remains transcript authority. */
export const OBSERVER_WHISPER_MODEL = 'onnx-community/whisper-tiny.en';
export const OBSERVER_WHISPER_WINDOW_MS = 2600;
export const OBSERVER_WHISPER_MIN_AUDIO_MS = 1200;
export const OBSERVER_WHISPER_REQUEST_INTERVAL_MS = 900;
export const OBSERVER_SPEECH_BUFFER_MS = 7000;
/**
 * Audio retained before local Whisper confirms words, including quiet first
 * syllables and every packet recorded while inference is in flight.
 *
 * This deliberately matches Ariadne's provider pre-roll. It is a time window,
 * not a VAD-selected collection of packets.
 */
export const OBSERVER_SPEECH_PREROLL_MS = 6000;
/**
 * Legacy active-speech duration retained for callers that explicitly want a
 * duration meter. It must not gate Whisper: Ariadne requires a 1.2 second
 * *audio window*, not 1.2 seconds that individually pass VAD.
 */
export const OBSERVER_SPEECH_MIN_ACTIVE_MS = 1200;

export interface SpeechActivityObservation {
  /** Whether the current packet passes the same energy thresholds as the gate. */
  active: boolean;
  /** A stale, short candidate was cleared after a sustained gap. */
  candidateReset: boolean;
  /** Whether the current candidate is long enough to reach Whisper/the provider. */
  hasMinimumSpeech: boolean;
}

/**
 * Counts VAD-active audio rather than total buffered time.
 *
 * Whisper windows intentionally contain pre-roll and pauses, so their wall-clock
 * length cannot prove that the user spoke for long enough. Keeping this tracker
 * beside the shared detector makes browser Live, Live STT and the headless path
 * enforce the same minimum without trusting a delayed transcript as the onset.
 */
export class SpeechActivityTracker {
  private readonly minimumActiveSamples: number;
  private readonly resetAfterSilenceMs: number;
  private activeSamples = 0;
  private lastActiveAt: number | null = null;

  constructor(options: {
    sampleRate: number;
    minimumActiveMs?: number;
    resetAfterSilenceMs?: number;
  }) {
    const sampleRate = Math.max(1, options.sampleRate);
    const minimumActiveMs = Math.max(0, options.minimumActiveMs ?? OBSERVER_SPEECH_MIN_ACTIVE_MS);
    this.minimumActiveSamples = Math.ceil(sampleRate * minimumActiveMs / 1000);
    this.resetAfterSilenceMs = Math.max(
      0,
      options.resetAfterSilenceMs ?? DEFAULT_SPEECH_GATE.hangoverMs,
    );
  }

  observe(packetSamples: number, active: boolean, now: number): SpeechActivityObservation {
    let candidateReset = false;
    if (active) {
      this.activeSamples += Math.max(0, Math.floor(packetSamples));
      this.lastActiveAt = now;
    } else if (
      this.lastActiveAt !== null
      && now - this.lastActiveAt > this.resetAfterSilenceMs
    ) {
      this.reset();
      candidateReset = true;
    }

    return {
      active,
      candidateReset,
      hasMinimumSpeech: this.hasMinimumSpeech,
    };
  }

  get hasMinimumSpeech(): boolean {
    return this.activeSamples >= this.minimumActiveSamples;
  }

  reset(): void {
    this.activeSamples = 0;
    this.lastActiveAt = null;
  }
}

/**
 * Lossless ownership of microphone PCM while a semantic speech check is
 * pending.
 *
 * In rolling mode this retains the same bounded pre-roll used by Ariadne. Once
 * Whisper starts, trimming stops until its result is handled, so a slow worker
 * cannot move the replay window past a short utterance or the beginning of a
 * long sentence. VAD may decide when to request Whisper, but it never removes
 * packets from the candidate.
 */
export class SemanticSpeechCapture {
  private readonly maxRollingSamples: number;
  private packets: Int16Array[] = [];
  private protectedByWhisper = false;

  constructor(options: { sampleRate: number; preRollMs?: number }) {
    const sampleRate = Math.max(1, options.sampleRate);
    const preRollMs = Math.max(0, options.preRollMs ?? OBSERVER_SPEECH_PREROLL_MS);
    this.maxRollingSamples = Math.round(sampleRate * preRollMs / 1000);
  }

  get isWhisperCheckPending(): boolean {
    return this.protectedByWhisper;
  }

  get sampleCount(): number {
    return this.packets.reduce((total, packet) => total + packet.length, 0);
  }

  append(pcm: Int16Array): void {
    if (!pcm.length) return;
    this.packets.push(pcm.slice());
    if (!this.protectedByWhisper) this.trimToRollingWindow();
  }

  /**
   * Freeze the replay candidate and return an intact recent Whisper window.
   * Returns null without freezing when the window is too short or too quiet.
   */
  beginWhisperCheck(sampleRate: number): Float32Array | null {
    if (this.protectedByWhisper) return null;
    const audio = pcmPacketsToWhisperWindow(this.packets, sampleRate);
    if (!audio) return null;
    this.protectedByWhisper = true;
    return audio;
  }

  /** Keep recent raw PCM after a rejection so speech recorded during inference can retry. */
  finishWhisperCheck(): void {
    this.protectedByWhisper = false;
    this.trimToRollingWindow();
  }

  /** Transfer every protected packet exactly once after semantic acceptance. */
  takeConfirmedPackets(): Int16Array[] {
    const confirmed = this.packets;
    this.packets = [];
    this.protectedByWhisper = false;
    return confirmed;
  }

  reset(): void {
    this.packets = [];
    this.protectedByWhisper = false;
  }

  private trimToRollingWindow(): void {
    this.packets = recentPcmPackets(this.packets, this.maxRollingSamples);
  }
}

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
