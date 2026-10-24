// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { createHash, randomUUID } from 'node:crypto';
import { mkdir, open, readFile, readdir, rename, rm } from 'node:fs/promises';
import { join } from 'node:path';
import type { RoomTaskRecord, RoomTaskStore } from '../core-sdk/room/roomTaskHandoff';

/** A claim survives crashes. A lock without a journal is uncertain, never retried.
 * Journal is authoritative; chat projections are rebuilt from it on connect. */
export class HeadlessRoomTaskStore implements RoomTaskStore {
  private owned = new Set<string>();
  constructor(private directory: string) {}
  private path(id: string, suffix = '.json') {
    return join(this.directory, createHash('sha256').update(id).digest('hex') + suffix);
  }
  private async write(record: RoomTaskRecord) {
    const path = this.path(record.id), temporary = path + '.' + randomUUID() + '.tmp';
    try {
      const file = await open(temporary, 'wx', 0o600);
      try { await file.writeFile(JSON.stringify(record)); await file.sync(); } finally { await file.close(); }
      await rename(temporary, path);
    } finally { await rm(temporary, { force: true }); }
  }
  async claim(record: RoomTaskRecord) {
    await mkdir(this.directory, { recursive: true });
    try {
      const lock = await open(this.path(record.id, '.claim'), 'wx', 0o600);
      await lock.close();
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code !== 'EEXIST') throw error;
      const existing = await this.get(record.id);
      if (!existing) throw new Error('An earlier task claim has no confirmed journal. It cannot be replayed.');
      return { claimed: false, record: existing };
    }
    await this.write(record);
    this.owned.add(record.id);
    return { claimed: true, record: structuredClone(record) };
  }
  async save(record: RoomTaskRecord) {
    if (!this.owned.has(record.id)) throw new Error('This process does not own the task claim.');
    const old = await this.get(record.id);
    if (!old || old.readOnly) throw new Error('The task journal is unavailable or read-only.');
    await this.write(record);
  }
  async get(id: string): Promise<RoomTaskRecord | undefined> {
    try {
      const record = JSON.parse(await readFile(this.path(id), 'utf8')) as RoomTaskRecord;
      if (record.version !== 1 || record.id !== id || record.handoff?.id !== id || !Array.isArray(record.operations)) {
        throw new Error('Invalid task journal. It cannot be replayed.');
      }
      // A reopened task may have dispatched a command just before process loss.
      // Preserve evidence and do not imply a saved "working" task is still active.
      if (!this.owned.has(id) && ['working', 'replying'].includes(record.phase)) {
        record.phase = 'interrupted'; record.readOnly = true;
        record.note = 'This process cannot resume the unfinished task. It may be running elsewhere; inspect the room before making a new request.';
      }
      return record;
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code === 'ENOENT') return undefined;
      throw error;
    }
  }
  async list(): Promise<RoomTaskRecord[]> {
    await mkdir(this.directory, { recursive: true });
    const records: RoomTaskRecord[] = [];
    for (const name of await readdir(this.directory)) {
      if (!/^[a-f0-9]{64}\.json$/.test(name)) continue;
      const value = JSON.parse(await readFile(join(this.directory, name), 'utf8'));
      if (typeof value.id !== 'string' || this.path(value.id) !== join(this.directory, name)) throw new Error('Invalid task journal identity.');
      const record = await this.get(value.id); if (record) records.push(record);
    }
    return records;
  }
}
