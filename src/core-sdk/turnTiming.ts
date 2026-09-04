// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

export interface TurnTimingEvent {
  name: string;
  elapsedMs: number;
  metrics?: Record<string, number>;
}

export interface TurnTimingReport {
  turnId: string;
  gatewaySessionId?: string;
  startedAt: string;
  clock: 'client-monotonic';
  events: TurnTimingEvent[];
}

const STORAGE_KEY = 'maestro.turn-timings.v1';
const MAX_REPORTS = 30;
const MAX_EVENTS = 120;
const reports: TurnTimingReport[] = [];
let loaded = false;
let pendingSave: ReturnType<typeof setTimeout> | undefined;

function cleanEvent(value: unknown): TurnTimingEvent | null {
  if (!value || typeof value !== 'object') return null;
  const event = value as TurnTimingEvent;
  if (typeof event.name !== 'string' || !/^[a-z][a-z0-9.-]{0,79}$/.test(event.name)
    || !Number.isFinite(event.elapsedMs) || event.elapsedMs < 0) return null;
  const metrics = event.metrics && typeof event.metrics === 'object'
    ? Object.fromEntries(Object.entries(event.metrics).slice(0, 12).filter(([key, number]) =>
      /^[a-zA-Z][a-zA-Z0-9]{0,59}$/.test(key) && typeof number === 'number' && Number.isFinite(number)))
    : undefined;
  return { name: event.name, elapsedMs: event.elapsedMs, ...(metrics ? { metrics } : {}) };
}

export function clearTurnTimings() {
  load();
  reports.length = 0;
  flushTurnTimings();
}

function storage(): Storage | undefined {
  try { return typeof localStorage === 'undefined' ? undefined : localStorage; }
  catch { return undefined; }
}

function load() {
  if (loaded) return;
  loaded = true;
  try {
    const saved: unknown = JSON.parse(storage()?.getItem(STORAGE_KEY) || '[]');
    if (Array.isArray(saved)) {
      for (const report of saved.slice(-MAX_REPORTS)) {
        if ((report?.clock === 'client-monotonic' || report?.clock === 'browser-monotonic') && typeof report.turnId === 'string'
          && typeof report.startedAt === 'string' && Array.isArray(report.events)) {
          reports.push({ turnId: report.turnId, startedAt: report.startedAt,
            ...(typeof report.gatewaySessionId === 'string' ? { gatewaySessionId: report.gatewaySessionId } : {}),
            clock: 'client-monotonic', events: report.events.slice(0, MAX_EVENTS).map(cleanEvent).filter((event: TurnTimingEvent | null): event is TurnTimingEvent => event !== null) });
        }
      }
    }
  } catch { /* Diagnostics must never interrupt a conversation. */ }
}

export function flushTurnTimings() {
  load();
  if (pendingSave !== undefined) clearTimeout(pendingSave);
  pendingSave = undefined;
  try { storage()?.setItem(STORAGE_KEY, JSON.stringify(reports)); } catch { /* Storage may be unavailable. */ }
}

if (typeof window !== 'undefined') {
  window.addEventListener('pagehide', flushTurnTimings);
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') flushTurnTimings();
  });
}

export function exportTurnTimings(): string {
  load();
  return JSON.stringify({ schemaVersion: 1, exportedAt: new Date().toISOString(), reports: reports.map(report => ({ ...report, events: [...report.events].sort((a, b) => a.elapsedMs - b.elapsedMs) })) }, null, 2);
}

/** Only event names and numeric measurements belong here; never pass speech or credentials. */
export function beginTurnTiming(turnId: string, now = () => performance.now()) {
  load();
  const start = now();
  const report: TurnTimingReport = { turnId, startedAt: new Date().toISOString(),
    clock: 'client-monotonic', events: [] };
  reports.push(report);
  if (reports.length > MAX_REPORTS) reports.splice(0, reports.length - MAX_REPORTS);
  const once = new Set<string>();
  const mark = (name: string, metrics?: Record<string, number>) => {
    if (report.events.length >= MAX_EVENTS) return;
    const safeMetrics = metrics && Object.fromEntries(Object.entries(metrics)
      .filter(([, value]) => typeof value === 'number' && Number.isFinite(value)));
    report.events.push({ name, elapsedMs: Math.max(0, now() - start),
      ...(safeMetrics ? { metrics: safeMetrics } : {}) });
    // At most one persistence write per second, with no React subscription or PCM-rate writes.
    pendingSave ??= setTimeout(flushTurnTimings, 1000);
  };
  mark('turn.started');
  return { turnId, mark, linkGateway(sessionId: string) {
    report.gatewaySessionId = sessionId;
    mark('gateway.ready');
  }, markLatest(name: string) {
    const event = report.events.find(item => item.name === name);
    if (event) {
      event.elapsedMs = Math.max(0, now() - start);
      pendingSave ??= setTimeout(flushTurnTimings, 1000);
    } else mark(name);
  }, markOnce(name: string, metrics?: Record<string, number>) {
    if (once.has(name)) return;
    once.add(name);
    mark(name, metrics);
  } };
}
