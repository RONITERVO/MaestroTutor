// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { appendFile, readFile } from 'node:fs/promises';
import { compareHeadlessFirstLessonParity } from '../src/headless/accessParityEvidence';

const [managedPath, byokPath] = process.argv.slice(2);
if (!managedPath || !byokPath) {
  throw new Error('Usage: tsx scripts/compare-headless-access-parity.ts <managed.json> <byok.json>');
}

const [managed, byok] = await Promise.all([
  readFile(managedPath, 'utf8').then(JSON.parse),
  readFile(byokPath, 'utf8').then(JSON.parse),
]);
const comparison = compareHeadlessFirstLessonParity(managed, byok);
console.log(JSON.stringify(comparison, null, 2));

if (process.env.GITHUB_STEP_SUMMARY) {
  await appendFile(
    process.env.GITHUB_STEP_SUMMARY,
    `### Managed/BYOK semantic parity\n\n\`\`\`json\n${JSON.stringify(comparison, null, 2)}\n\`\`\`\n`,
    'utf8',
  );
}

if (!comparison.passed) process.exitCode = 1;
