// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export interface TaskSpeechResult { id: string; conversationId: string; valid(): Promise<boolean> }
export interface TaskSpeechLease { current(): boolean; ready(): boolean; release(resume: boolean): void }
export interface TaskSpeechPorts {
  available(task: TaskSpeechResult): boolean;
  ready(): boolean;
  claim(): Promise<TaskSpeechLease | null>;
  speak(task: TaskSpeechResult): void;
  playing(): boolean;
  stop(): void | Promise<unknown>;
}
/** Completed task notifications only: no model, native command or persisted
 * replay. The host pumps this queue when its shared audio state changes. */
export class TaskSpeechQueue {
  private pending: TaskSpeechResult[] = [];
  private seen = new Set<string>();
  private active?: { task: TaskSpeechResult; lease: TaskSpeechLease };
  private preparing: symbol | null = null;
  private nextValidityCheck = 0;
  private epoch = 0;
  private resumeOnCancel = true;
  constructor(private ports: TaskSpeechPorts) {}
  enqueue(task: TaskSpeechResult): void {
    if (this.seen.has(task.id)) return;
    this.seen.add(task.id);
    while (this.seen.size > 128) this.seen.delete(this.seen.values().next().value!);
    this.pending.push(task);
    if (this.pending.length > 8) this.pending.shift();
  }
  hasWork(): boolean { return !!this.active || this.preparing !== null || this.pending.length > 0; }
  private stopAndRelease(active: { lease: TaskSpeechLease }, resume: boolean): Promise<void> {
    try {
      const stopped = this.ports.stop();
      if (stopped) return Promise.resolve(stopped).then(() => active.lease.release(resume), () => active.lease.release(resume));
    } catch { /* Still release ownership when an output adapter fails. */ }
    active.lease.release(resume);
    return Promise.resolve();
  }
  // A returned promise owns output shutdown; callers can await it instead of
  // stopping the same transport a second time. No active output returns void.
  cancel(resume = true): void | Promise<void> {
    this.epoch++; this.resumeOnCancel = resume; this.pending = []; this.preparing = null;
    const active = this.active; this.active = undefined;
    if (active) return this.stopAndRelease(active, resume);
  }
  async tick(): Promise<void> {
    if (this.preparing) return;
    const owner = Symbol(); this.preparing = owner;
    const epoch = this.epoch;
    let claimed: TaskSpeechLease | null = null;
    let candidate: TaskSpeechResult | undefined;
    try {
      if (this.active) {
        const active = this.active;
        let valid = this.ports.available(active.task) && active.lease.current();
        if (valid && Date.now() >= this.nextValidityCheck) {
          valid = await active.task.valid(); this.nextValidityCheck = Date.now() + 500;
        }
        if (epoch !== this.epoch) return;
        if (!valid || !this.ports.playing()) {
          this.active = undefined;
          if (!valid) this.stopAndRelease(active, true);
          else active.lease.release(true);
        }
        return;
      }
      this.pending = this.pending.filter(task => this.ports.available(task));
      const task = this.pending[0]; candidate = task;
      if (!task || !this.ports.ready()) return;
      if (!await task.valid()) { if (epoch === this.epoch) this.pending.shift(); return; }
      if (epoch !== this.epoch || !this.ports.available(task) || !this.ports.ready()) return;
      claimed = await this.ports.claim();
      if (!claimed) return;
      if (epoch !== this.epoch || !this.ports.available(task) || !claimed.current()) return;
      const valid = await task.valid();
      if (epoch !== this.epoch) return;
      if (!valid) { this.pending.shift(); return; }
      if (!this.ports.available(task) || !claimed.current() || !claimed.ready()) return;
      this.pending.shift();
      this.nextValidityCheck = Date.now() + 500;
      this.active = { task, lease: claimed };
      claimed = null;
      this.ports.speak(task);
    } catch {
      // The message stays in chat for manual playback. Never automatically retry
      // a potentially started utterance after a transport or callback failure.
      if (epoch !== this.epoch) return;
      if (candidate && this.pending[0]?.id === candidate.id) this.pending.shift();
      const active = this.active; this.active = undefined;
      if (active) this.stopAndRelease(active, true);
    } finally {
      claimed?.release(epoch === this.epoch || this.resumeOnCancel);
      if (this.preparing === owner) this.preparing = null;
    }
  }
}
