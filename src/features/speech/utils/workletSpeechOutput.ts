// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { SpeechOutput, SpeechOutputEvents } from '../../../core-sdk/media/speechOutput';
import { getAudioOutputTailDelayMs } from './playbackDrain';

/** Owns one render-thread queue. Acknowledgements include a reset generation;
 * Stop also cancels already-acknowledged drains waiting for the device tail. */
export class WorkletSpeechOutput implements SpeechOutput {
  readonly sampleRate = 24000;
  private generation = 0;
  private submitted = 0;
  private rendered = 0;
  private played = 0;
  private nextRequest = 1;
  private disposed = false;
  private failure: Error | null = null;
  private readonly timers = new Set<ReturnType<typeof setTimeout>>();
  private readonly drains = new Map<number, { fence: number; acknowledged: boolean;
    resolve: (value: 'drained' | 'cancelled') => void; reject: (error: Error) => void }>();

  constructor(private readonly context: AudioContext, private readonly node: AudioWorkletNode,
    private readonly events: SpeechOutputEvents = {}, private readonly maxQueuedSeconds = 120) {
    if (!Number.isFinite(maxQueuedSeconds) || maxQueuedSeconds <= 0 || maxQueuedSeconds > 180)
      throw new Error('Invalid speech queue limit.');
    node.port.onmessage = event => this.receive(event.data);
    node.onprocessorerror = () => this.fail(new Error('Speech audio processor failed.'));
    try { node.connect(context.destination); }
    catch (error) { this.dispose(); throw error; }
  }
  write(pcm: Int16Array): void {
    if (this.failure) throw this.failure;
    if (this.disposed || this.context.state === 'closed') throw new Error('Speech output is closed.');
    if (!(pcm instanceof Int16Array)) throw new Error('Speech output requires mono PCM16.');
    if (!pcm.length) return;
    if (this.submitted - this.rendered + pcm.length > this.sampleRate * this.maxQueuedSeconds)
      throw new Error('Speech output buffer is full.');
    // Transfer our own copy, never the transcript/cache buffer owned by Maestro.
    const copy = pcm.slice();
    this.node.port.postMessage({ type: 'push', generation: this.generation, pcm: copy,
      inputSampleRate: this.sampleRate }, [copy.buffer]);
    this.submitted += pcm.length;
  }
  read() { return { submittedSamples: this.submitted, playedSamples: this.played, started: this.played > 0 }; }
  drain(): Promise<'drained' | 'cancelled'> {
    if (this.failure) return Promise.reject(this.failure);
    if (this.disposed) return Promise.resolve('cancelled');
    if (this.played >= this.submitted) return Promise.resolve('drained');
    if (this.drains.size >= 64) return Promise.reject(new Error('Too many speech drain requests.'));
    const requestId = this.nextRequest++;
    return new Promise((resolve, reject) => {
      this.drains.set(requestId, { fence: this.submitted, acknowledged: false, resolve, reject });
      try { this.node.port.postMessage({ type: 'request-drain', generation: this.generation, requestId }); }
      catch { this.fail(new Error('Speech output could not request playback completion.')); }
    });
  }
  private afterTail(callback: () => void): void {
    const generation = this.generation;
    const timer = setTimeout(() => {
      this.timers.delete(timer);
      if (!this.disposed && !this.failure && generation === this.generation) callback();
    }, getAudioOutputTailDelayMs(this.context));
    this.timers.add(timer);
  }
  private receive(value: unknown): void {
    if (this.disposed || this.failure || !value || typeof value !== 'object') return;
    const message = value as Record<string, unknown>;
    if (message.generation !== this.generation) return;
    if (message.type === 'error') { this.fail(new Error('Speech render queue rejected audio.')); return; }
    if (message.type === 'telemetry' && ['started', 'resumed', 'underrun'].includes(String(message.event))) {
      this.events.onEvent?.(message.event as 'started' | 'resumed' | 'underrun'); return;
    }
    const rendered = Number(message.renderedSamples);
    if (!Number.isSafeInteger(rendered) || rendered < this.rendered || rendered > this.submitted) return;
    if (message.type === 'progress') {
      this.rendered = rendered;
      this.afterTail(() => { this.played = Math.max(this.played, rendered); });
    } else if (message.type === 'drained') {
      const requestId = Number(message.requestId);
      const drain = this.drains.get(requestId);
      if (!drain || drain.acknowledged || rendered < drain.fence) return;
      drain.acknowledged = true;
      this.rendered = rendered;
      this.afterTail(() => {
        this.played = Math.max(this.played, rendered);
        this.drains.delete(requestId); drain.resolve('drained');
      });
    }
  }
  private clearPending(): void {
    for (const timer of this.timers) clearTimeout(timer);
    this.timers.clear();
    for (const drain of this.drains.values()) drain.resolve('cancelled');
    this.drains.clear(); this.submitted = this.rendered = this.played = 0;
  }
  private fail(error: Error): void {
    if (this.failure || this.disposed) return;
    this.failure = error;
    for (const drain of this.drains.values()) drain.reject(error);
    this.drains.clear(); this.dispose();
    this.events.onError?.(error);
  }
  reset(): void {
    this.generation++; this.clearPending();
    if (this.disposed || this.failure) return;
    try { this.node.port.postMessage({ type: 'reset', generation: this.generation }); }
    catch { this.fail(new Error('Speech output could not reset.')); }
  }
  dispose(): void {
    if (this.disposed) return;
    this.generation++; this.clearPending(); this.disposed = true;
    this.node.port.onmessage = null; this.node.onprocessorerror = null;
    try { this.node.port.postMessage({ type: 'reset', generation: this.generation }); } catch {}
    try { this.node.disconnect(); } catch {}
  }
}
