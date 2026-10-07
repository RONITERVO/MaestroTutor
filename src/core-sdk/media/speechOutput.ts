// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/** One owned mono PCM output. Providers, caching and transcript state stay with
 * Maestro; adapters own rendering, buffering and the output-device tail. */
export interface SpeechOutput {
  readonly sampleRate: number;
  /** Copies samples before returning. Throws on closed output or buffer overflow. */
  write(pcm: Int16Array): void;
  /** Sample positions exclude underrun silence and belong to the current reset epoch. */
  read(): { submittedSamples: number; playedSamples: number; started: boolean };
  /** Fence at the samples submitted now; later writes cannot satisfy it early. */
  drain(): Promise<'drained' | 'cancelled'>;
  /** Discards queued audio and cancels every old drain. Late callbacks have no effect. */
  reset(): void;
  /** Terminal reset; never closes an AudioContext borrowed from its caller. */
  dispose(): void;
}
