// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

export type CoreJourney =
  | 'access'
  | 'account'
  | 'billing'
  | 'report'
  | 'persistence'
  | 'chat'
  | 'suggestions'
  | 'media'
  | 'speech'
  | 'live';

export interface CoreEvent {
  version: 1;
  sequence: number;
  operationId: string;
  journey: CoreJourney;
  phase: string;
  at: number;
  data?: Record<string, unknown>;
}

export type CoreEventListener = (event: CoreEvent) => void;

export type CoreEventInput = Omit<CoreEvent, 'version' | 'sequence' | 'at'> & {
  at?: number;
};

export interface CoreEventSink {
  emit(event: CoreEventInput): CoreEvent;
  subscribe(listener: CoreEventListener): () => void;
}

const SECRET_KEY_PATTERN = /(?:authorization|api[-_]?key|app[-_]?check|bearer|card|credential|password|refresh[-_]?token|secret|token)/i;
const BEARER_PATTERN = /^Bearer\s+/i;

const sanitizeTraceValue = (value: unknown, seen: WeakSet<object>): unknown => {
  if (typeof value === 'string') {
    return BEARER_PATTERN.test(value) ? '[redacted]' : value;
  }
  if (value === null || typeof value !== 'object') return value;
  if (seen.has(value)) return '[circular]';
  seen.add(value);

  if (Array.isArray(value)) {
    return value.map(item => sanitizeTraceValue(item, seen));
  }

  const sanitized: Record<string, unknown> = {};
  for (const [key, item] of Object.entries(value as Record<string, unknown>)) {
    sanitized[key] = SECRET_KEY_PATTERN.test(key)
      ? '[redacted]'
      : sanitizeTraceValue(item, seen);
  }
  return sanitized;
};

export const sanitizeTraceData = (
  data?: Record<string, unknown>,
): Record<string, unknown> | undefined => (
  data ? sanitizeTraceValue(data, new WeakSet()) as Record<string, unknown> : undefined
);

export const createCoreEventJournal = (options?: {
  now?: () => number;
  onEvent?: CoreEventListener;
}): CoreEventSink & { snapshot: () => CoreEvent[]; clear: () => void } => {
  const now = options?.now || Date.now;
  const listeners = new Set<CoreEventListener>();
  const events: CoreEvent[] = [];
  let sequence = 0;

  if (options?.onEvent) listeners.add(options.onEvent);

  return {
    emit(input) {
      const event: CoreEvent = {
        ...input,
        version: 1,
        sequence: ++sequence,
        at: input.at ?? now(),
        data: sanitizeTraceData(input.data),
      };
      events.push(event);
      for (const listener of listeners) listener(event);
      return event;
    },
    subscribe(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    snapshot: () => events.map(event => ({
      ...event,
      data: event.data ? { ...event.data } : undefined,
    })),
    clear: () => {
      events.length = 0;
      sequence = 0;
    },
  };
};
