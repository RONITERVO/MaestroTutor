// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { acquireCameraMedia } from './cameraSources';
/** Browser resource policy. Ordinary web/phone sessions remain active; a native
 * host may suspend and require an explicit user action before media restarts. */
export class SessionActivity {
  private active = true;
  private suspended = false;
  private settled = true;
  private epoch = 0;
  private readonly listeners = new Set<() => void>();
  private readonly stops = new Set<() => void | Promise<unknown>>();
  private readonly streams = new Set<MediaStream>();
  readonly isActive = () => this.active;
  readonly subscribe = (listener: () => void) => { this.listeners.add(listener); return () => { this.listeners.delete(listener); }; };
  readonly onSuspend = (stop: () => void | Promise<unknown>) => { this.stops.add(stop); return () => { this.stops.delete(stop); }; };
  private publish() { for (const listener of this.listeners) listener(); }

  requireResume() { this.active = false; this.epoch++; this.publish(); }
  status() { return { suspended: this.suspended, settled: this.settled, active: this.active }; }
  resume() {
    if (this.suspended || !this.settled) return false;
    this.active = true; this.publish(); return true;
  }
  setSuspended(value: boolean) {
    if (this.suspended === value) return;
    this.suspended = value;
    if (!value) { this.publish(); return; } // Visibility alone never resumes media.
    this.active = false; this.settled = false; this.epoch++;
    for (const stream of this.streams) stream.getTracks().forEach(track => track.stop());
    this.streams.clear();
    this.publish();
    // Invoke every owner synchronously, including when another owner throws.
    const pending = [...this.stops].map(stop => {
      try { return Promise.resolve(stop()); } catch (error) { return Promise.reject(error); }
    });
    const epoch = this.epoch;
    void Promise.allSettled(pending).then(results => {
      if (epoch !== this.epoch) return;
      this.settled = results.every(result => result.status === 'fulfilled');
      this.publish();
    });
  }

  async capture(constraints: MediaStreamConstraints, acquire: (constraints: MediaStreamConstraints) => Promise<MediaStream>): Promise<MediaStream> {
    if (!this.active) throw new DOMException('Resume audio on the book before starting media.', 'AbortError');
    const epoch = this.epoch;
    const stream = await acquire(constraints);
    if (!this.active || epoch !== this.epoch) {
      stream.getTracks().forEach(track => track.stop());
      throw new DOMException('Media capture was interrupted.', 'AbortError');
    }
    // Explicit track.stop() does not emit ended. Prune stopped streams here too.
    for (const existing of this.streams) if (existing.getTracks().every(track => track.readyState === 'ended')) this.streams.delete(existing);
    this.streams.add(stream);
    for (const track of stream.getTracks()) track.addEventListener?.('ended', () => {
      if (stream.getTracks().every(value => value.readyState === 'ended')) this.streams.delete(stream);
    }, { once: true });
    return stream;
  }
}

export const sessionActivity = new SessionActivity();
export const acquireUserMedia = (constraints: MediaStreamConstraints, signal?: AbortSignal) => sessionActivity.capture(constraints, async value => {
  if (signal?.aborted) throw new DOMException('Camera selection changed.', 'AbortError');
  const stream = await acquireCameraMedia(value, signal);
  if (signal?.aborted) { stream.getTracks().forEach(track => track.stop()); throw new DOMException('Camera selection changed.', 'AbortError'); }
  return stream;
});
