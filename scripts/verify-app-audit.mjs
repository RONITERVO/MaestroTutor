// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

const parentEdges = {
  '@capacitor-firebase/app-check': ['firebase'],
  '@capacitor-firebase/authentication': ['firebase'],
  'firebase': ['@firebase/firestore', '@firebase/firestore-compat'],
  '@firebase/firestore-compat': ['@firebase/firestore'],
  '@firebase/firestore': ['@grpc/grpc-js'],
  '@grpc/grpc-js': [],
};
const reviewed = new Set([
  'https://github.com/advisories/GHSA-m9gg-hp2v-232j',
  'https://github.com/advisories/GHSA-f596-whhp-79r4',
]);

/** This exception is only for the unused root Firebase Node gRPC server path.
 * Functions and Live gateway are audited separately without this exception. */
export function verifyAppAudit(report, now = new Date()) {
  if (report?.auditReportVersion !== 2 || report.error || !report.vulnerabilities
    || !Number.isInteger(report.metadata?.vulnerabilities?.total)) throw new Error('Missing/invalid npm audit report');
  const entries = Object.entries(report.vulnerabilities);
  if (entries.length !== report.metadata.vulnerabilities.total) throw new Error('Incomplete npm audit report');
  if (!entries.length) return { clean: true, reviewedPackages: [] };
  if (!Number.isFinite(now.getTime()) || now >= new Date('2026-11-01T00:00:00Z')) throw new Error('Review the temporary Firebase gRPC exception again; it expired');
  for (const [name, value] of entries) {
    if (!Object.hasOwn(parentEdges, name) || value.name !== name || value.severity === 'critical'
      || !Array.isArray(value.via) || !value.via.length || !Array.isArray(value.nodes)
      || value.nodes.length !== 1 || value.nodes[0] !== `node_modules/${name}`) throw new Error(`Unreviewed dependency finding: ${name}`);
    for (const via of value.via) {
      if (typeof via === 'string') {
        if (!parentEdges[name].includes(via) || !report.vulnerabilities[via]) throw new Error(`Unreviewed advisory path: ${name}`);
      } else if (name !== '@grpc/grpc-js' || via?.name !== name || via.dependency !== name
        || !reviewed.has(via.url) || !['low', 'high'].includes(via.severity)) throw new Error(`Unreviewed advisory: ${name}`);
    }
    if (name === '@grpc/grpc-js' && value.via.some(via => typeof via === 'string')) throw new Error('Missing direct gRPC advisory');
  }
  return { clean: false, reviewedPackages: entries.map(([name]) => name), expires: '2026-11-01', reason: 'Root app imports Firebase Auth/App Check only, no Firestore or gRPC server. See docs/QUEST_DEPENDENCY_REVIEW.md.' };
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  if (!process.argv[2]) throw new Error('Pass the npm audit --omit=dev --json report path');
  console.log(JSON.stringify(verifyAppAudit(JSON.parse(readFileSync(process.argv[2], 'utf8').replace(/^\uFEFF/, ''))), null, 2));
}
