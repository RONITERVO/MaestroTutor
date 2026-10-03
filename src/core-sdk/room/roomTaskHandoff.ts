// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { parseRoomTaskDirective, relatedRoomTask, RoomTaskSteeringError, type RoomTaskDirective, type RoomTaskTarget, type RelatedRoomTask } from './taskSteering';
import { LiveInputContextError, validateLiveInputMedia } from '../media/liveInputContext';
import type { TutorTextTurnInput, TutorTextTurnResult } from '../chat/tutorTextTurn';
import type { RoomAgentLease, RoomAgentState, RoomCommand, RoomTaskControl, RoomTaskResult } from './roomAgent';

export type RoomTaskPhase = 'working' | 'replying' | 'completed' | 'limited' | 'stopped' | 'interrupted' | 'failed';
export interface RoomHandoff {
  version: 1;
  id: string;
  conversationId: string;
  sourceUserId: string;
  sourceAssistantId: string;
  nativeSession: string;
  accessScope: string;
  /** Exact prepared tutor input. Never supplied or rewritten by the model. */
  input: TutorTextTurnInput;
}
export interface RoomTaskRecord {
  version: 1;
  /** Restored evidence is never writable by a previously running task. */
  readOnly?: true;
  directive?: RoomTaskDirective;
  relatedTask?: RelatedRoomTask;
  id: string;
  handoff: RoomHandoff;
  phase: RoomTaskPhase;
  note: string;
  startedAt: number;
  updatedAt: number;
  operations: Array<{
    commands: RoomCommand[];
    sceneRevision: number;
    receipt?: RoomAgentState;
  }>;
  reply?: Pick<TutorTextTurnResult, 'parsed' | 'rawResponse'>;
}
export interface RoomTaskStore {
  /** Atomic, durable claim. Existing records MUST NOT be re-executed. */
  claim(record: RoomTaskRecord): Promise<{ claimed: boolean; record: RoomTaskRecord }>;
  save(record: RoomTaskRecord): Promise<void>;
  get(id: string): Promise<RoomTaskRecord | undefined>;
}
export interface RoomTaskPorts {
  store: RoomTaskStore;
  lease(): RoomAgentLease | null;
  run(input: TutorTextTurnInput, lease: RoomAgentLease, control: RoomTaskControl): Promise<RoomTaskResult>;
  reply(input: TutorTextTurnInput, result: RoomTaskResult, signal: AbortSignal): Promise<Pick<TutorTextTurnResult, 'parsed' | 'rawResponse'>>;
  changed(record: RoomTaskRecord): void;
  activity(active: boolean): void;
  now(): number;
}
const clone = <T>(value: T): T => structuredClone(value);
const interrupted = () => new DOMException('The agent task was stopped or its session changed.', 'AbortError');

/** Task lifetime is independent of a tutor response. Store full evidence here;
 * the chat adapter projects only status and the final language response. */
export class RoomTaskHandoff {
  private captures = new Map<string, { handoff: RoomHandoff; valid: () => Promise<boolean>; targets: RoomTaskTarget[] }>();
  private executing = new Set<string>();
  private active = new Map<string, { controller: AbortController; done: Promise<RoomTaskRecord> }>();
  constructor(private ports: RoomTaskPorts) {}

  capture(handoff: RoomHandoff, valid: () => Promise<boolean>, targets: RoomTaskTarget[] = []): void {
    if (!handoff.sourceUserId || !handoff.sourceAssistantId || !handoff.conversationId) throw new Error('Missing source turn.');
    this.captures.set(handoff.sourceAssistantId, { handoff: clone(handoff), valid, targets: clone(targets) });
    // Unused proposals are not an unbounded cache of conversation media.
    while (this.captures.size > 8) this.captures.delete(this.captures.keys().next().value!);
  }
  available(assistantId: string): boolean { return this.captures.has(assistantId); }
  stop(id: string): void { this.active.get(id)?.controller.abort(); }
  stopAll(): void { for (const item of this.active.values()) item.controller.abort(); }
  running(id: string): boolean { return this.executing.has(id); }
  async reset(): Promise<void> {
    this.captures.clear(); this.stopAll();
    await Promise.allSettled([...this.active.values()].map(task => task.done));
  }

  start(assistantId: string, directive?: RoomTaskDirective): Promise<RoomTaskRecord> {
    const captured = this.captures.get(assistantId);
    if (!captured) return Promise.reject(new Error('This handoff has no original request context. Please ask again.'));
    const id = captured.handoff.id;
    const existing = this.active.get(id);
    if (existing) return existing.done;
    if (directive && (!parseRoomTaskDirective(directive, captured.targets) || directive.taskId === id)) return Promise.reject(new Error('This task was not available in the original request.'));
    if ([...this.active.keys()].some(activeId => !directive || directive.action === 'continue' || activeId !== directive.taskId)) return Promise.reject(new Error('Another agent task is running. Stop it or wait before starting a new one.'));
    const controller = new AbortController();
    // Register before any async port can re-enter start().
    const done = Promise.resolve().then(() => this.execute(captured, controller, directive));
    this.active.set(id, { controller, done });
    void done.finally(() => this.active.delete(id)).catch(() => {});
    return done;
  }

  private async execute(captured: { handoff: RoomHandoff; valid: () => Promise<boolean> }, controller: AbortController, directive?: RoomTaskDirective): Promise<RoomTaskRecord> {
    const { handoff, valid } = captured;
    const lease = this.ports.lease();
    const check = async () => {
      if (controller.signal.aborted || !lease?.valid() || lease.state().session !== handoff.nativeSession || !await valid()) throw interrupted();
      if (controller.signal.aborted || !lease.valid()) throw interrupted();
    };
    await check();
    const now = this.ports.now();
    const claim = await this.ports.store.claim({ version: 1, id: handoff.id, handoff: clone(handoff), phase: 'working',
      note: 'Working on your request.', startedAt: now, updatedAt: now, operations: [], ...(directive ? { directive: clone(directive) } : {}) });
    if (!claim.claimed) {
      // Could belong to another process or an interrupted prior run. Display it,
      // but never infer that pending dispatch is safe to replay.
      this.ports.changed(clone(claim.record));
      return claim.record;
    }
    const record = claim.record;
    const publish = async () => {
      record.updatedAt = this.ports.now();
      await this.ports.store.save(clone(record));
      this.ports.changed(clone(record));
    };
    this.executing.add(record.id);
    this.ports.activity(true);
    try {
      await publish();
      await check();
      if (handoff.input.liveInputMedia) validateLiveInputMedia(handoff.input.liveInputMedia);
      if (directive) {
        let parent = await this.ports.store.get(directive.taskId);
        if (!parent || parent.readOnly || parent.handoff.conversationId !== handoff.conversationId
          || parent.handoff.accessScope !== handoff.accessScope || parent.handoff.nativeSession !== handoff.nativeSession) {
          throw new RoomTaskSteeringError('That task is no longer available in this room and conversation. Please make a new request.');
        }
        await check();
        const running = this.active.get(directive.taskId);
        if (directive.action === 'continue' && running) throw new RoomTaskSteeringError('This task is still running. Ask to revise or stop it first.');
        // The durable claim records control intent before cancellation has any effect.
        if (running) { running.controller.abort(); await running.done.catch(() => {}); }
        await check();
        parent = await this.ports.store.get(directive.taskId);
        if (!parent || parent.readOnly) throw new RoomTaskSteeringError('The original task is no longer available. Please make a new request.');
        record.relatedTask = relatedRoomTask(parent, directive, !!running);
        record.note = directive.action === 'stop' ? 'Task stopped or already inactive. Recorded actions remain.' : 'Inspecting the room for your follow-up.';
        await publish(); await check();
      }
      const result = directive?.action === 'stop'
        ? { receipts: [], scene: clone(lease!.state()), budgetExhausted: false, relatedTask: record.relatedTask }
        : await this.ports.run(clone(handoff.input), lease!, {
        relatedTask: record.relatedTask,
        signal: controller.signal,
        isCurrent: () => !controller.signal.aborted,
        beforePlan: check,
        beforeDispatch: async (commands, scene) => {
          await check();
          record.operations.push({ commands: clone(commands), sceneRevision: scene.sceneRevision });
          record.note = 'Applying the next action.';
          // The write must commit before the native bridge sees this operation.
          await publish();
          await check();
        },
        onReceipt: async receipt => {
          const pending = record.operations[record.operations.length - 1];
          if (!pending || pending.receipt) throw new Error('Unexpected native receipt.');
          pending.receipt = clone(receipt);
          record.note = receipt.ok ? 'Action recorded. Checking the result.' : 'Checking an action that could not be applied.';
          await publish();
        },
      });
      await check();
      record.phase = 'replying'; record.note = 'Preparing the result.';
      await publish();
      const reply = await this.ports.reply(clone(handoff.input), result, controller.signal);
      await check();
      if (!reply.parsed.visibleText.trim()) throw new Error('The agent result did not contain a readable reply.');
      record.reply = clone(reply);
      record.phase = result.budgetExhausted || result.needsReview ? 'limited' : 'completed';
      record.note = result.needsReview ? 'An earlier action is unconfirmed. Inspect the room and make a new specific request before further edits.' : result.budgetExhausted ? 'Action limit reached. Review the result before continuing.' : 'Finished checking this request.';
      await publish();
    } catch (error) {
      const uncertain = record.operations.some(operation => !operation.receipt);
      const aborted = controller.signal.aborted || (error instanceof Error && error.name === 'AbortError');
      record.phase = uncertain ? 'interrupted' : aborted ? 'stopped' : 'failed';
      record.note = uncertain ? 'Stopped with an unconfirmed action. Inspect the room before trying again.'
        : aborted ? 'Stopped. Recorded actions remain in the room.'
        : error instanceof LiveInputContextError || error instanceof RoomTaskSteeringError ? error.message
        : record.directive?.action === 'stop' && record.relatedTask ? 'The earlier task is no longer running here. Its recorded effects remain, but the result reply could not be completed.'
        : 'Could not finish this request. Recorded actions are available in task details.';
      try { await publish(); } catch {
        // Keep the in-memory receipt available even if storage stops working.
        record.note += ' The latest task update could not be saved.';
        this.ports.changed(clone(record));
      }
    } finally { this.executing.delete(record.id); this.ports.activity(false); }
    return clone(record);
  }
}
