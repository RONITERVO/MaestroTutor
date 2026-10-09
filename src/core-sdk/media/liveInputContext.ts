// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { isCameraImageOrigin } from '../../../shared/imageOrigin';
import type { CameraImageOrigin } from '../../../shared/imageOrigin';
import { pcmToWav } from './audioProcessing';

// App limits per delegated turn, not provider upload limits.
export const LIVE_INPUT_LIMITS = { bytes: 4 * 1024 * 1024, samples: 16000 * 90, frames: 90, packets: 2048 } as const;
export type LiveInputIssue = 'limit' | 'invalid' | 'interrupted' | 'missing';
export interface LiveInputMedia {
  version: 1;
  complete: boolean;
  issue?: LiveInputIssue;
  audio?: { mimeType: 'audio/wav'; data: string; sampleRate: 16000; samples: number };
  /** Client delivery times. WAV concatenates sent packets without adding silence. */
  packets: Array<{ atMs: number; sampleOffset: number; samples: number }>;
  frames: Array<{ mimeType: 'image/jpeg'; data: string; atMs: number; audioOffsetSamples: number; origin?: CameraImageOrigin }>;
}
export const missingLiveInput = (): LiveInputMedia => ({ version: 1, complete: false, issue: 'missing', packets: [], frames: [] });
export class LiveInputContextError extends Error {
  constructor() {
    super('The original audio or camera context is unavailable or exceeded the handoff limit. No room actions were started. Please repeat a shorter request.');
    this.name = 'LiveInputContextError';
  }
}
const invalid = (): never => { throw new LiveInputContextError(); };
const decode = (data: string, maxBytes: number): Uint8Array => {
  if (typeof data !== 'string' || !data.length || data.length > Math.ceil(maxBytes / 3) * 4
      || data.length % 4 || !/^[A-Za-z0-9+/]*={0,2}$/.test(data)) return invalid();
  let raw: string;
  try { raw = atob(data); } catch { return invalid(); }
  if (raw.length > maxBytes || btoa(raw) !== data) return invalid();
  const bytes = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
  return bytes;
};
const jpeg = (bytes: Uint8Array) => bytes.length >= 4 && bytes[0] === 255 && bytes[1] === 216
  && bytes[bytes.length - 2] === 255 && bytes[bytes.length - 1] === 217;
const shape = (value: unknown, fields: readonly string[]) => value !== null && typeof value === 'object'
  && !Array.isArray(value) && Object.keys(value).every(key => fields.includes(key));
const integer = (value: number) => Number.isSafeInteger(value) && value >= 0;

/** Record only after sendRealtimeInput succeeds: evidence of client submission,
 * not an acknowledgement that the provider processed these bytes. */
export class LiveInputContext {
  private chunks: Uint8Array[] = [];
  private packets: LiveInputMedia['packets'] = [];
  private frames: LiveInputMedia['frames'] = [];
  private samples = 0;
  private bytes = 44;
  private origin: number | undefined;
  private lastMs = 0;
  private issue?: LiveInputIssue;
  private sealed = false;
  constructor(private now: () => number = () => performance.now()) {}
  private time(): number {
    const now = this.now();
    if (!Number.isFinite(now)) return invalid();
    this.origin ??= now;
    this.lastMs = Math.max(this.lastMs, Math.round(now - this.origin));
    return this.lastMs;
  }
  invalidate(issue: LiveInputIssue): void {
    if (this.sealed) return;
    this.issue ??= issue;
    this.chunks = []; this.packets = []; this.frames = []; this.samples = 0; this.bytes = 44;
  }
  recordAudio(data: string): void {
    if (this.sealed || this.issue) return;
    try {
      const bytes = decode(data, LIVE_INPUT_LIMITS.bytes);
      if (bytes.length % 2) return this.invalidate('invalid');
      if (this.bytes + bytes.length > LIVE_INPUT_LIMITS.bytes
          || this.samples + bytes.length / 2 > LIVE_INPUT_LIMITS.samples
          || this.packets.length >= LIVE_INPUT_LIMITS.packets) return this.invalidate('limit');
      this.packets.push({ atMs: this.time(), sampleOffset: this.samples, samples: bytes.length / 2 });
      this.chunks.push(bytes); this.samples += bytes.length / 2; this.bytes += bytes.length;
    } catch { this.invalidate('invalid'); }
  }
  recordFrame(data: string, origin?: CameraImageOrigin): void {
    if (this.sealed || this.issue) return;
    try {
      if (origin !== undefined && !isCameraImageOrigin(origin)) return this.invalidate('invalid');
      const bytes = decode(data, LIVE_INPUT_LIMITS.bytes);
      if (!jpeg(bytes)) return this.invalidate('invalid');
      if (this.bytes + bytes.length > LIVE_INPUT_LIMITS.bytes || this.frames.length >= LIVE_INPUT_LIMITS.frames) return this.invalidate('limit');
      this.frames.push({ mimeType: 'image/jpeg', data, atMs: this.time(), audioOffsetSamples: this.samples, ...(origin ? { origin } : {}) });
      this.bytes += bytes.length;
    } catch { this.invalidate('invalid'); }
  }
  finish(): LiveInputMedia {
    if (this.sealed) return missingLiveInput();
    const issue = this.issue || (!this.samples ? 'missing' : undefined);
    const result: LiveInputMedia = { version: 1, complete: !issue,
      ...(issue ? { issue } : {}), packets: issue ? [] : this.packets, frames: issue ? [] : this.frames };
    if (result.complete) {
      const bytes = new Uint8Array(this.samples * 2);
      let offset = 0;
      for (const chunk of this.chunks) { bytes.set(chunk, offset); offset += chunk.length; }
      result.audio = { mimeType: 'audio/wav', data: pcmToWav(new Int16Array(bytes.buffer), 16000).split(',')[1], sampleRate: 16000, samples: this.samples };
    }
    this.discard();
    return result;
  }
  discard(): void {
    this.sealed = true; this.chunks = []; this.packets = []; this.frames = []; this.samples = 0; this.bytes = 44;
  }
}

/** Validate persisted snapshots before client resolution or provider spend. */
export function validateLiveInputMedia(media: LiveInputMedia): void {
  if (!shape(media, ['version', 'complete', 'issue', 'audio', 'packets', 'frames']) || media.version !== 1 || media.complete !== true || media.issue !== undefined
      || !Array.isArray(media.packets) || !media.packets.length || media.packets.length > LIVE_INPUT_LIMITS.packets
      || !Array.isArray(media.frames) || media.frames.length > LIVE_INPUT_LIMITS.frames) return invalid();
  const audio = media.audio;
  if (!audio || !shape(audio, ['mimeType', 'data', 'sampleRate', 'samples']) || audio.mimeType !== 'audio/wav' || audio.sampleRate !== 16000 || !integer(audio.samples)
      || !audio.samples || audio.samples > LIVE_INPUT_LIMITS.samples) return invalid();
  const bytes = decode(audio.data, 44 + LIVE_INPUT_LIMITS.samples * 2);
  if (bytes.length !== 44 + audio.samples * 2) return invalid();
  const view = new DataView(bytes.buffer);
  const tag = (offset: number, text: string) => [...text].every((char, i) => bytes[offset + i] === char.charCodeAt(0));
  if (!tag(0, 'RIFF') || !tag(8, 'WAVE') || !tag(12, 'fmt ') || !tag(36, 'data')
      || view.getUint32(4, true) !== bytes.length - 8 || view.getUint32(16, true) !== 16
      || view.getUint16(20, true) !== 1 || view.getUint16(22, true) !== 1
      || view.getUint32(24, true) !== 16000 || view.getUint32(28, true) !== 32000
      || view.getUint16(32, true) !== 2 || view.getUint16(34, true) !== 16
      || view.getUint32(40, true) !== audio.samples * 2) return invalid();
  let samples = 0, time = 0, total = bytes.length;
  for (const packet of media.packets) {
    if (!shape(packet, ['atMs', 'sampleOffset', 'samples']) || !integer(packet.atMs) || packet.atMs < time || !integer(packet.samples)
        || !packet.samples || packet.sampleOffset !== samples) return invalid();
    time = packet.atMs; samples += packet.samples;
  }
  if (samples !== audio.samples) return invalid();
  time = 0;
  let previousOffset = 0;
  for (const frame of media.frames) {
    if (!shape(frame, ['mimeType', 'data', 'atMs', 'audioOffsetSamples', 'origin']) || frame.mimeType !== 'image/jpeg' || !integer(frame.atMs) || frame.atMs < time
        || (frame.origin !== undefined && !isCameraImageOrigin(frame.origin)) || !integer(frame.audioOffsetSamples) || frame.audioOffsetSamples < previousOffset || frame.audioOffsetSamples > samples) return invalid();
    const image = decode(frame.data, LIVE_INPUT_LIMITS.bytes - total);
    if (!jpeg(image)) return invalid();
    total += image.length; time = frame.atMs; previousOffset = frame.audioOffsetSamples;
  }
  if (total > LIVE_INPUT_LIMITS.bytes) return invalid();
}
