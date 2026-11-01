// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, expect, it, vi } from 'vitest';
import { verifyAppAudit } from './verify-app-audit.mjs';

const clean = () => ({
  auditReportVersion: 2,
  metadata: { vulnerabilities: { info: 0, low: 0, moderate: 0, high: 0, critical: 0, total: 0 } },
  vulnerabilities: {},
});
const finding = (severity = 'high', url = 'https://github.com/advisories/GHSA-m9gg-hp2v-232j') => ({
  ...clean(),
  metadata: { vulnerabilities: { ...clean().metadata.vulnerabilities, [severity]: 2, total: 2 } },
  vulnerabilities: {
    '@firebase/firestore': { name: '@firebase/firestore', severity, nodes: ['node_modules/@firebase/firestore'], via: ['@grpc/grpc-js'] },
    '@grpc/grpc-js': { name: '@grpc/grpc-js', severity, nodes: ['node_modules/@grpc/grpc-js'], via: [{ name: '@grpc/grpc-js', dependency: '@grpc/grpc-js', severity, url }] },
  },
});
afterEach(() => vi.useRealTimers());

it('accepts a complete clean v2 report with npm dependency metadata', () => {
  const report = { ...clean(), metadata: { ...clean().metadata, dependencies: { prod: 45, dev: 70, total: 115 } } };
  expect(verifyAppAudit(report)).toEqual({ clean: true, reviewedPackages: [] });
});
it('accepts plain null-prototype dictionaries', () => {
  const report = clean();
  report.vulnerabilities = Object.create(null);
  expect(verifyAppAudit(report).clean).toBe(true);
});
it.each([undefined, null, [], 'audit', 2, {}, { ...clean(), auditReportVersion: 1 }, { ...clean(), auditReportVersion: '2' }])(
  'rejects a missing or malformed report %#', report => {
    expect(() => verifyAppAudit(report)).toThrow('Missing/invalid npm audit report');
  },
);
it.each([undefined, null, [], 'none', new Date(), Object.create({ hiddenFinding: true })])(
  'rejects a non-record vulnerabilities value %#', vulnerabilities => {
    expect(() => verifyAppAudit({ ...clean(), vulnerabilities })).toThrow('Missing/invalid npm audit report');
  },
);
it.each([undefined, null, [], { vulnerabilities: null }, { vulnerabilities: [] }])(
  'rejects missing or malformed metadata %#', metadata => {
    expect(() => verifyAppAudit({ ...clean(), metadata })).toThrow('Missing/invalid npm audit report');
  },
);
it.each([{ code: 'ENETWORK' }, 'registry unavailable', null, false, undefined])(
  'rejects an error report even if it also claims to be clean %#', error => {
    expect(() => verifyAppAudit({ ...clean(), error })).toThrow('Missing/invalid npm audit report');
  },
);
it.each(['info', 'low', 'moderate', 'high', 'critical', 'total'] as const)(
  'requires the %s counter', name => {
    const counters: Record<string, number> = { ...clean().metadata.vulnerabilities };
    delete counters[name];
    expect(() => verifyAppAudit({ ...clean(), metadata: { vulnerabilities: counters } })).toThrow('severity counters');
  },
);
it('rejects unknown severity counters rather than assuming a new severity is harmless', () => {
  const counters = { ...clean().metadata.vulnerabilities, unknown: 1 };
  expect(() => verifyAppAudit({ ...clean(), metadata: { vulnerabilities: counters } })).toThrow('severity counters');
});
it.each([-1, 0.5, '0', NaN, Infinity, Number.MAX_SAFE_INTEGER + 1])(
  'rejects an invalid total or severity count %#', value => {
    for (const name of ['total', 'low']) {
      const counters = { ...clean().metadata.vulnerabilities, [name]: value };
      expect(() => verifyAppAudit({ ...clean(), metadata: { vulnerabilities: counters } })).toThrow('severity counters');
    }
  },
);
it('rejects a claimed clean result with nonzero counters and a partial findings map', () => {
  const report = finding();
  expect(() => verifyAppAudit({ ...report, vulnerabilities: {} })).toThrow('Incomplete or inconsistent');
  delete (report.vulnerabilities as Record<string, unknown>)['@grpc/grpc-js'];
  expect(() => verifyAppAudit(report)).toThrow('Incomplete or inconsistent');
});
it('requires severity counts to match findings, not merely the total', () => {
  const report = finding();
  report.metadata.vulnerabilities.high = 0;
  report.metadata.vulnerabilities.low = 2;
  expect(() => verifyAppAudit(report)).toThrow('Incomplete or inconsistent');
});
it('rejects hidden findings under zero counters', () => {
  expect(() => verifyAppAudit({ ...clean(), vulnerabilities: finding().vulnerabilities })).toThrow('Incomplete or inconsistent');
});
it.each([null, [], { name: 'wrong', severity: 'high' }, { name: 'example', severity: 'unknown' }])(
  'rejects malformed finding entries %#', entry => {
    expect(() => verifyAppAudit({ ...clean(), vulnerabilities: { example: entry } })).toThrow('Missing/invalid npm audit finding');
  },
);
it.each(['info', 'low', 'moderate', 'high', 'critical'])(
  'rejects all %s findings, including historically reviewed advisories', severity => {
    expect(() => verifyAppAudit(finding(severity))).toThrow('no exceptions are allowed');
  },
);
it('rejects new advisories and newly vulnerable packages', () => {
  expect(() => verifyAppAudit(finding('high', 'https://github.com/advisories/new'))).toThrow('no exceptions are allowed');
  expect(() => verifyAppAudit({
    ...clean(), metadata: { vulnerabilities: { ...clean().metadata.vulnerabilities, moderate: 1, total: 1 } },
    vulnerabilities: { example: { name: 'example', severity: 'moderate', via: [], nodes: ['node_modules/example'] } },
  })).toThrow('no exceptions are allowed');
});
it.each(['2026-10-02T00:00:00Z', '2026-11-01T00:00:00Z', '2099-01-01T00:00:00Z'])(
  'has no date-based advisory exceptions at %s', timestamp => {
    vi.useFakeTimers({ now: new Date(timestamp) });
    expect(verifyAppAudit(clean()).clean).toBe(true);
    expect(() => verifyAppAudit(finding())).toThrow('no exceptions are allowed');
  },
);
