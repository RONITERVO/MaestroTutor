// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

const severities = ['info', 'low', 'moderate', 'high', 'critical'];
const isRecord = value => value !== null && typeof value === 'object'
  && [Object.prototype, null].includes(Object.getPrototypeOf(value));

/** Shipped app dependencies must have a complete, clean npm audit v2 report. */
export function verifyAppAudit(report) {
  if (!isRecord(report) || !Object.hasOwn(report, 'auditReportVersion')
    || report.auditReportVersion !== 2 || Object.hasOwn(report, 'error')
    || !isRecord(report.vulnerabilities) || !isRecord(report.metadata)
    || !isRecord(report.metadata.vulnerabilities)) throw new Error('Missing/invalid npm audit report');
  const counters = report.metadata.vulnerabilities;
  const counterNames = [...severities, 'total'];
  if (Object.keys(counters).length !== counterNames.length
    || counterNames.some(name => !Object.hasOwn(counters, name)
      || !Number.isSafeInteger(counters[name]) || counters[name] < 0)) {
    throw new Error('Missing/invalid npm audit severity counters');
  }
  const entries = Object.entries(report.vulnerabilities);
  const observed = Object.fromEntries(severities.map(name => [name, 0]));
  for (const [name, finding] of entries) {
    if (!isRecord(finding) || finding.name !== name || !severities.includes(finding.severity)) {
      throw new Error('Missing/invalid npm audit finding');
    }
    observed[finding.severity] += 1;
  }
  if (entries.length !== counters.total
    || severities.some(name => observed[name] !== counters[name])) {
    throw new Error('Incomplete or inconsistent npm audit report');
  }
  if (entries.length) {
    throw new Error(`App dependency audit found ${entries.length} vulnerable package(s); no exceptions are allowed`);
  }
  return { clean: true, reviewedPackages: [] };
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  if (!process.argv[2]) throw new Error('Pass the npm audit --omit=dev --json report path');
  console.log(JSON.stringify(verifyAppAudit(JSON.parse(readFileSync(process.argv[2], 'utf8').replace(/^\uFEFF/, ''))), null, 2));
}
