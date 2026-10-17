// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { expect, it } from 'vitest';
import { verifyAppAudit } from './verify-app-audit.mjs';
const now = new Date('2026-10-02T00:00:00Z');
const finding = () => ({ auditReportVersion: 2, metadata: { vulnerabilities: { total: 2 } }, vulnerabilities: {
  '@firebase/firestore': { name: '@firebase/firestore', severity: 'high', nodes: ['node_modules/@firebase/firestore'], via: ['@grpc/grpc-js'] },
  '@grpc/grpc-js': { name: '@grpc/grpc-js', severity: 'high', nodes: ['node_modules/@grpc/grpc-js'], via: [{ name: '@grpc/grpc-js', dependency: '@grpc/grpc-js', severity: 'high', url: 'https://github.com/advisories/GHSA-m9gg-hp2v-232j' }] },
} });
it('accepts clean results and the exact reviewed advisory path before expiry', () => {
  expect(verifyAppAudit({ auditReportVersion: 2, metadata: { vulnerabilities: { total: 0 } }, vulnerabilities: {} }, now).clean).toBe(true);
  expect(verifyAppAudit(finding(), now).clean).toBe(false);
});
it('rejects a new advisory even on the same package', () => {
  const report = finding(); report.vulnerabilities['@grpc/grpc-js'].via[0].url = 'https://github.com/advisories/new';
  expect(() => verifyAppAudit(report, now)).toThrow('Unreviewed advisory');
});
it('rejects a newly vulnerable package, nested copy or critical escalation', () => {
  const unknown = finding(); unknown.vulnerabilities['@firebase/firestore'].name = 'another-package';
  expect(() => verifyAppAudit(unknown, now)).toThrow();
  const nested = finding(); nested.vulnerabilities['@grpc/grpc-js'].nodes = ['node_modules/another/node_modules/@grpc/grpc-js'];
  expect(() => verifyAppAudit(nested, now)).toThrow();
  const critical = finding(); critical.vulnerabilities['@grpc/grpc-js'].severity = 'critical';
  expect(() => verifyAppAudit(critical, now)).toThrow();
});
it('rejects registry errors, incomplete reports and missing advisory targets', () => {
  expect(() => verifyAppAudit({ error: { code: 'ENETWORK' } }, now)).toThrow();
  const partial = finding(); partial.metadata.vulnerabilities.total = 3;
  expect(() => verifyAppAudit(partial, now)).toThrow();
  const dangling = finding(); dangling.vulnerabilities['@firebase/firestore'].via = ['missing'];
  expect(() => verifyAppAudit(dangling, now)).toThrow();
});
it('requires the exception to be reviewed again after its deadline', () => {
  expect(() => verifyAppAudit(finding(), new Date('2026-11-01T00:00:00Z'))).toThrow('expired');
});
