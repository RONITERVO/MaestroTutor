import {validActivityProfile,type ActivityProfile} from '../../../shared/avatarActivities';
export type {ActivityProfile} from '../../../shared/avatarActivities';
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export interface LibraryEntry {
  id: string; name: string; tags: string[]; duration: number;
  favourite: boolean; compatible: boolean; shortClip: boolean;
  archived?: boolean; removed?: boolean; downloaded?: boolean; bytes?: number;
}
export interface MotionUsage {
  total: number; page: number; pages: number; uses: string[];
  history: boolean; saved: boolean; uncertain: boolean; playing: boolean; protection: string | null;
}
export interface LibraryState {
  usage?: MotionUsage | null; archivedOnly?: boolean; canRemoveDownload?: boolean; canForgetMotion?: boolean;
  activityProfile?: ActivityProfile | null;
  version: 1; revision: number; ack: number; session: string; visible: boolean; busy: boolean; readOnly: boolean;
  query: string; offset: number; total: number; pageSize: number;
  compatibleOnly: boolean; favouritesOnly: boolean; includeShort: boolean;
  entries: LibraryEntry[]; selected: LibraryEntry | null;
  canPreview: boolean; canWalk: boolean; canAssign: boolean;
  ruleId: string | null; ruleName: string | null; stepIndex: number;
  sourceIndex: number; sourceCount: number; sourceName: string | null;
  attribution: string; termsPage: number; termsPages: number; status: string;
}
export type LibraryAction = 'query' | 'select' | 'save' | 'preview' | 'stop' | 'walk' | 'rule' | 'close' | 'roleAssign' | 'roleRemove' | 'roleClear' | 'roleUndo' | 'roleRedo' | 'archive' | 'restore' | 'removeDownload' | 'forgetMotion';
export interface LibraryRequest {
  version: 1; session: string; sequence: number; action: LibraryAction;
  archivedOnly?: boolean; usagePage?: number;
  query?: string; offset?: number; compatibleOnly?: boolean; favouritesOnly?: boolean; includeShort?: boolean;
  motionId?: string; name?: string; tags?: string[]; favourite?: boolean; loop?: boolean;
  profileRevision?: number; modelHash?: string; role?: number; weight?: number; speed?: number; cooldown?: number;
  ruleId?: string; stepIndex?: number; sourceIndex?: number; termsPage?: number;
}
const id = (value: unknown): value is string => typeof value === 'string' && /^[a-f0-9]{32}$/.test(value);
const integer = (value: unknown, min: number, max: number) => typeof value === 'number' && Number.isInteger(value) && value >= min && value <= max;
const text = (value: unknown, max: number): value is string => typeof value === 'string' && value.length <= max;
const record = (value: unknown): value is Record<string, unknown> => value !== null && typeof value === 'object' && !Array.isArray(value);
function entry(value: unknown): value is LibraryEntry {
  return record(value) && id(value.id) && text(value.name, 100) && Array.isArray(value.tags) && value.tags.length <= 16 && value.tags.every(tag => text(tag, 32)) &&
    typeof value.duration === 'number' && Number.isFinite(value.duration) && value.duration > 0 && value.duration <= 3600 &&
    ['favourite', 'compatible', 'shortClip'].every(key => typeof value[key] === 'boolean') &&
    ['archived', 'removed', 'downloaded'].every(key => value[key] === undefined || typeof value[key] === 'boolean') &&
    (value.bytes === undefined || integer(value.bytes, 28, 8 * 1024 * 1024));
}
function usage(value: unknown): boolean {
  return record(value) && integer(value.total, 0, 2048) && integer(value.page, 0, 512) && integer(value.pages, 1, 513) &&
    (value.page as number) < (value.pages as number) && Array.isArray(value.uses) && value.uses.length <= 8 && value.uses.every(line => text(line, 160)) &&
    ['history', 'saved', 'uncertain', 'playing'].every(key => typeof value[key] === 'boolean') && (value.protection === null || text(value.protection, 512));
}
export function parseLibraryState(value: unknown): LibraryState | null {
  if (!record(value) || value.version !== 1 || !id(value.session) || !integer(value.revision, 1, 2147483647) || !integer(value.ack, 0, 2147483647) ||
      !integer(value.offset, 0, 1024) || !integer(value.total, 0, 1024) || value.pageSize !== 12 || !text(value.query, 80) || !text(value.status, 2048) ||
      !Array.isArray(value.entries) || value.entries.length > 12 || !value.entries.every(entry) || new Set(value.entries.map(item => item.id)).size !== value.entries.length ||
      value.selected !== null && !entry(value.selected) ||
      !['visible', 'busy', 'readOnly', 'compatibleOnly', 'favouritesOnly', 'includeShort', 'canPreview', 'canWalk', 'canAssign'].every(key => typeof value[key] === 'boolean') ||
      value.ruleId !== null && !id(value.ruleId) || value.ruleName !== null && !text(value.ruleName, 32) || !integer(value.stepIndex, 0, 15) ||
      !integer(value.sourceIndex, 0, 1023) || !integer(value.sourceCount, 0, 1024) || value.sourceName !== null && !text(value.sourceName, 100) ||
      !text(value.attribution, 1500) || !integer(value.termsPage, 0, 64) || !integer(value.termsPages, 1, 65)) return null;
  if (value.usage !== undefined && value.usage !== null && !usage(value.usage) ||
      value.archivedOnly !== undefined && typeof value.archivedOnly !== 'boolean' || value.canRemoveDownload !== undefined && typeof value.canRemoveDownload !== 'boolean' || value.canForgetMotion !== undefined && typeof value.canForgetMotion !== 'boolean') return null;
  if (value.activityProfile !== undefined && value.activityProfile !== null && !validActivityProfile(value.activityProfile)) return null;
  return value as unknown as LibraryState;
}
/** Requests are acknowledged by native sequence, never retried as new actions.
 * Stop/close can preempt a pending load. Every session change drops stale work. */
export class LibraryBookClient {
  private listeners = new Set<() => void>();
  private queue: LibraryRequest[] = [];
  private sequence = 0;
  private blocked = false;
  private value: { state: LibraryState | null; pending: boolean } = { state: null, pending: false };
  subscribe = (listener: () => void) => { this.listeners.add(listener); return () => { this.listeners.delete(listener); }; };
  getSnapshot = () => this.value;
  private changed() { this.value = { ...this.value, pending: this.queue.length > 0 }; for (const listener of this.listeners) listener(); }
  receive = (input: unknown) => {
    const next = parseLibraryState(input); if (!next) return false;
    const previous = this.value.state;
    if (previous?.session === next.session && next.revision <= previous.revision) return false;
    if (previous?.session !== next.session) { this.queue = []; this.sequence = next.ack; }
    this.sequence = Math.max(this.sequence, next.ack);
    this.queue = next.visible ? this.queue.filter(request => request.sequence > next.ack) : [];
    this.blocked = false; this.value = { state: next, pending: false }; this.changed(); return true;
  };
  request(action: LibraryAction, fields: Omit<Partial<LibraryRequest>, 'version' | 'session' | 'sequence' | 'action'> = {}) {
    const state = this.value.state;
    if (!state?.visible || this.blocked || this.sequence >= 2147483647) return false;
    const interrupt = action === 'stop' || action === 'close';
    if (!interrupt && (this.queue.length || state.busy)) return false;
    const request: LibraryRequest = { ...fields, version: 1, session: state.session, sequence: ++this.sequence, action };
    if (JSON.stringify(request).length > 3000) return false;
    this.queue = [request]; this.changed(); return true;
  }
  close() { if (this.value.state?.visible) this.request('close'); }
  suspend() { this.blocked = true; this.queue = []; this.changed(); }
  snapshot() {
    return { librarySession: this.value.state?.session ?? '', libraryRevision: this.value.state?.revision ?? 0, libraryRequest: this.queue[0] ?? null };
  }
}
