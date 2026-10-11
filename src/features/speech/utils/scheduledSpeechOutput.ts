// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { SpeechOutput } from '../../../core-sdk/media/speechOutput';
import { getAudioOutputTailDelayMs } from './playbackDrain';

interface Segment {
  source: AudioBufferSourceNode;
  start: number;
  first: number;
  last: number;
  ended: boolean;
  tail?: ReturnType<typeof setTimeout>;
}

/** Browser adapter. A native adapter can replace this entire output without
 * moving the provider connection or accidentally playing a second dry copy. */
export class ScheduledSpeechOutput implements SpeechOutput {
  private readonly segments: Segment[] = [];
  private readonly drains = new Set<{ fence: number; resolve: (result: 'drained' | 'cancelled') => void }>();
  private submitted = 0;
  private completed = 0;
  private nextStart = 0;
  private epoch = 0;
  private disposed = false;
  constructor(private readonly context: AudioContext, readonly sampleRate = 24000, private readonly maxQueuedSeconds = 120) {
    if (!Number.isInteger(sampleRate) || sampleRate < 8000 || sampleRate > 48000 || !Number.isFinite(maxQueuedSeconds) || maxQueuedSeconds <= 0)
      throw new Error('Invalid speech output format or queue limit.');
  }
  write(pcm: Int16Array): void {
    if (this.disposed || this.context.state === 'closed') throw new Error('Speech output is closed.');
    if (!(pcm instanceof Int16Array)) throw new Error('Speech output requires mono PCM16.');
    if (!pcm.length) return;
    if (this.submitted - this.completed + pcm.length > this.sampleRate * this.maxQueuedSeconds)
      throw new Error('Speech output buffer is full.');
    const buffer = this.context.createBuffer(1, pcm.length, this.sampleRate);
    const channel = buffer.getChannelData(0);
    for (let i = 0; i < pcm.length; i++) channel[i] = pcm[i] / 32768;
    const source = this.context.createBufferSource();
    source.buffer = buffer;
    const segment: Segment = { source, first: this.submitted, last: this.submitted + pcm.length,
      start: Math.max(this.context.currentTime, this.nextStart), ended: false };
    const epoch = this.epoch;
    source.onended = () => {
      if (this.disposed || epoch !== this.epoch || segment.ended || segment.tail !== undefined) return;
      segment.tail = setTimeout(() => {
        if (this.disposed || epoch !== this.epoch) return;
        segment.ended = true;
        while (this.segments[0]?.ended) this.completed = this.segments.shift()!.last;
        for (const drain of this.drains) if (drain.fence <= this.completed) {
          this.drains.delete(drain); drain.resolve('drained');
        }
      }, getAudioOutputTailDelayMs(this.context));
      try { source.disconnect(); } catch { /* Context may already be closing. */ }
    };
    try { source.connect(this.context.destination); source.start(segment.start); }
    catch (error) { source.onended = null; try { source.stop(); } catch {} try { source.disconnect(); } catch {} throw error; }
    this.segments.push(segment);
    this.submitted = segment.last;
    this.nextStart = segment.start + pcm.length / this.sampleRate;
  }
  read() {
    let played = this.completed;
    const audibleTime = this.context.currentTime - getAudioOutputTailDelayMs(this.context) / 1000;
    for (const segment of this.segments) {
      if (audibleTime < segment.start) break;
      played = Math.max(played, segment.first + Math.min(segment.last - segment.first,
        Math.max(0, Math.floor((audibleTime - segment.start) * this.sampleRate))));
    }
    return { submittedSamples: this.submitted, playedSamples: played, started: played > 0 };
  }
  drain(): Promise<'drained' | 'cancelled'> {
    if (this.disposed) return Promise.resolve('cancelled');
    if (this.completed >= this.submitted) return Promise.resolve('drained');
    return new Promise(resolve => this.drains.add({ fence: this.submitted, resolve }));
  }
  reset(): void {
    this.epoch++;
    for (const drain of this.drains) drain.resolve('cancelled');
    this.drains.clear();
    for (const segment of this.segments) {
      if (segment.tail !== undefined) clearTimeout(segment.tail);
      segment.source.onended = null;
      try { segment.source.stop(); } catch {}
      try { segment.source.disconnect(); } catch {}
    }
    this.segments.length = 0;
    this.submitted = this.completed = this.nextStart = 0;
  }
  dispose(): void { this.disposed = true; this.reset(); }
}
