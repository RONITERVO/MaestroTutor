// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { createCoreEventJournal, type CoreEventSink } from './events';

export interface CoreClock {
  now(): number;
  sleep(milliseconds: number): Promise<void>;
  setInterval(callback: () => void, milliseconds: number): unknown;
  clearInterval(handle: unknown): void;
}

export interface CoreIdFactory {
  create(prefix: string): string;
}

export interface CoreRuntime {
  clock: CoreClock;
  ids: CoreIdFactory;
  events: CoreEventSink;
}

const systemClock: CoreClock = {
  now: Date.now,
  sleep: milliseconds => new Promise(resolve => globalThis.setTimeout(resolve, milliseconds)),
  setInterval: (callback, milliseconds) => globalThis.setInterval(callback, milliseconds),
  clearInterval: handle => globalThis.clearInterval(handle as ReturnType<typeof setInterval>),
};

const systemIds: CoreIdFactory = {
  create: prefix => {
    const suffix = typeof globalThis.crypto?.randomUUID === 'function'
      ? globalThis.crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
    return `${prefix}-${suffix}`;
  },
};

export const createCoreRuntime = (overrides?: Partial<CoreRuntime>): CoreRuntime => ({
  clock: overrides?.clock || systemClock,
  ids: overrides?.ids || systemIds,
  events: overrides?.events || createCoreEventJournal(),
});
