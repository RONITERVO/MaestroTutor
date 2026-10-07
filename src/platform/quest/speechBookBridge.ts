// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { SpeechOutput, SpeechOutputEvents } from '../../core-sdk/media/speechOutput';

const RATE = 24000, CHUNK = 4800, MAX_QUEUED = RATE * 120, WINDOW = RATE * 2;
const unavailable = () => new Error('Maestro’s voice connection was interrupted. Start speech again when the book is ready.');
type Fence = { sample: number; resolve: (result: 'drained' | 'cancelled') => void; reject: (error: Error) => void };
type Chunk = { sequence: number; first: number; last: number; samples: Int16Array; pcm?: string };
const integer = (value: unknown): value is number => Number.isSafeInteger(value) && (value as number) >= 0;
const token = (value: unknown): value is string => typeof value === 'string' && /^[a-f0-9]{32}$/.test(value);

/** Transient top-document transport. No PCM enters saved room state or the agent journal. */
export class SpeechBookClient {
  private readonly session = crypto.randomUUID().replace(/-/g, '');
  private revision = 0;
  private host = '';
  private lastSeen = -Infinity;
  private current: BookSpeechOutput | null = null;
  private closed = false;
  private ready = false;
  constructor(private readonly now = () => performance.now()) {}

  create(events: SpeechOutputEvents = {}): SpeechOutput {
    if (this.closed || !this.ready || !this.host || this.now() - this.lastSeen > 1500) throw unavailable();
    // There is only one voice owner. Replacing it cancels every old fence.
    this.current?.dispose();
    const output = new BookSpeechOutput(this, events);
    this.current = output; this.revision++;
    return output;
  }
  changed(output: BookSpeechOutput) { if (this.current === output) this.revision++; }
  release(output: BookSpeechOutput) { if (this.current === output) { this.current = null; this.revision++; } }
  alive() { return !this.closed && this.now() - this.lastSeen <= 1500; }

  /** Called only by native's origin-checked top-document evaluateJavascript. */
  readonly exchange = (input: unknown) => {
    if (this.closed) return null;
    if (input && typeof input === 'object' && !Array.isArray(input)) {
      const value = input as Record<string, unknown>;
      if (value.version === 1 && value.session === this.session && token(value.host)
        && integer(value.revision) && value.revision <= this.revision
        && ['ready', 'playing', 'failed'].includes(String(value.status))) {
        if (this.host && this.host !== value.host) this.current?.fail(unavailable());
        this.host = value.host; this.lastSeen = this.now();
        this.ready = value.status !== 'failed';
        if (value.revision === this.revision && this.current) {
          if (value.status === 'failed') this.current.fail(unavailable());
          else this.current.accept(value);
        }
      }
    }
    return { version: 1, session: this.session, revision: this.revision,
      open: this.current !== null, chunks: this.current?.poll() ?? [] };
  };
  suspend() { this.current?.fail(unavailable()); this.host = ''; this.ready = false; this.lastSeen = -Infinity; }
  dispose() { this.suspend(); this.closed = true; }
}

class BookSpeechOutput implements SpeechOutput {
  readonly sampleRate = RATE;
  readonly microphonePolicy = 'suppress-during-playback' as const;
  private chunks: Chunk[] = [];
  private sequence = 0;
  private offered = 0;
  private accepted = 0;
  private acceptedSamples = 0;
  private submitted = 0;
  private played = 0;
  private started = false;
  private closed = false;
  private failure: Error | null = null;
  private readonly fences = new Set<Fence>();
  private readonly watchdog: ReturnType<typeof setInterval>;
  constructor(private readonly owner: SpeechBookClient, private readonly events: SpeechOutputEvents) {
    this.watchdog = setInterval(() => { if (!owner.alive()) this.fail(unavailable()); }, 250);
  }
  private assertOpen() { if (this.failure) throw this.failure; if (this.closed) throw unavailable(); }
  write(pcm: Int16Array) {
    this.assertOpen();
    if (!(pcm instanceof Int16Array)) throw new Error('Speech requires mono PCM16.');
    if (this.submitted - this.played + pcm.length > MAX_QUEUED) throw new Error('Speech output buffer is full.');
    if (this.chunks.length + Math.ceil(pcm.length / CHUNK) > 1024) throw new Error('Speech output buffer is full.');
    // Coalesce small provider packets before offering them. Once offered, bytes
    // and sequence are immutable even if their receipt has not arrived yet.
    for (let start = 0; start < pcm.length;) {
      let chunk = this.chunks[this.chunks.length - 1];
      if (!chunk || chunk.sequence <= this.offered || chunk.last - chunk.first === CHUNK) {
        chunk = { sequence: ++this.sequence, first: this.submitted, last: this.submitted, samples: new Int16Array(CHUNK) };
        this.chunks.push(chunk);
      }
      const size = chunk.last - chunk.first, length = Math.min(CHUNK - size, pcm.length - start);
      chunk.samples.set(pcm.subarray(start, start + length), size);
      chunk.last += length; this.submitted += length; start += length;
    }
  }
  poll() {
    // Retransmit unacknowledged chunks, bounded by native's actual playback cursor.
    return this.chunks.filter(chunk => chunk.last <= this.played + WINDOW).slice(0, 8).map(chunk => {
      this.offered = Math.max(this.offered, chunk.sequence);
      if (!chunk.pcm) {
        let binary = '';
        for (let i = 0; i < chunk.last - chunk.first; i++) binary += String.fromCharCode(chunk.samples[i] & 255, (chunk.samples[i] >>> 8) & 255);
        chunk.pcm = btoa(binary);
      }
      return { sequence: chunk.sequence, pcm: chunk.pcm };
    });
  }
  accept(value: Record<string, unknown>) {
    if (!integer(value.acceptedSequence) || !integer(value.submittedSamples) || !integer(value.playedSamples)) return;
    const sequence = value.acceptedSequence, samples = value.submittedSamples, played = value.playedSamples;
    if (sequence < this.accepted || played < this.played) return;
    const expected = sequence === this.accepted ? this.acceptedSamples : this.chunks.find(chunk => chunk.sequence === sequence)?.last;
    if (sequence > this.offered || samples !== expected || played > samples) { this.fail(unavailable()); return; }
    this.accepted = sequence; this.acceptedSamples = samples; this.played = played;
    this.chunks = this.chunks.filter(chunk => chunk.sequence > sequence);
    if (!this.started && played > 0) { this.started = true; this.events.onEvent?.('started'); }
    for (const fence of this.fences) if (played >= fence.sample) { this.fences.delete(fence); fence.resolve('drained'); }
  }
  read() { return { submittedSamples: this.submitted, playedSamples: this.played, started: this.started }; }
  drain(): Promise<'drained' | 'cancelled'> {
    if (this.failure) return Promise.reject(this.failure);
    if (this.closed) return Promise.resolve('cancelled');
    if (this.played >= this.submitted) return Promise.resolve('drained');
    if (this.fences.size >= 64) return Promise.reject(new Error('Too many speech drain requests.'));
    return new Promise((resolve, reject) => this.fences.add({ sample: this.submitted, resolve, reject }));
  }
  reset() {
    if (this.closed) return;
    for (const fence of this.fences) fence.resolve('cancelled');
    this.fences.clear(); this.chunks = [];
    this.sequence = this.offered = this.accepted = this.acceptedSamples = this.submitted = this.played = 0; this.started = false;
    this.owner.changed(this);
  }
  dispose() {
    if (this.closed) return;
    this.reset(); this.closed = true; clearInterval(this.watchdog); this.owner.release(this);
  }
  fail(error: Error) {
    if (this.closed) return;
    this.failure = error;
    for (const fence of this.fences) fence.reject(error);
    this.fences.clear(); this.dispose(); this.events.onError?.(error);
  }
}

let current: SpeechBookClient | null = null;
export function createBookSpeechOutput(events?: SpeechOutputEvents): SpeechOutput {
  if (!current) throw unavailable();
  return current.create(events);
}
export function registerBookSpeech(client: SpeechBookClient) {
  current?.dispose(); current = client;
  return () => { client.dispose(); if (current === client) current = null; };
}
