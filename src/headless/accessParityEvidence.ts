// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { HeadlessAccessMode } from './access';
import { FIRST_LESSON_TOOL_KINDS } from './firstLessonCoverage';

type Evidence = Record<string, any>;

export interface HeadlessFirstLessonParityProof {
  accessMode: HeadlessAccessMode | null;
  passed: boolean;
  uploadGeneratedMedia: boolean;
  userTurnCount: number;
  coverageKeys: string[];
  failedCoverage: string[];
  turnKinds: string[];
  responseSourceCounts: Record<string, number>;
  generatedToolKinds: string[];
  uploadedToolKinds: string[];
}

export interface HeadlessAccessParityComparison {
  passed: boolean;
  mismatches: string[];
  managed: HeadlessFirstLessonParityProof;
  byok: HeadlessFirstLessonParityProof;
}

const asRecord = (value: unknown): Evidence => (
  value && typeof value === 'object' && !Array.isArray(value) ? value as Evidence : {}
);

const strings = (values: unknown[]): string[] => values
  .filter((value): value is string => typeof value === 'string' && Boolean(value.trim()))
  .map(value => value.trim());

const sameStrings = (left: string[], right: string[]): boolean => (
  left.length === right.length && left.every((value, index) => value === right[index])
);

export const buildHeadlessFirstLessonParityProof = (
  raw: unknown,
): HeadlessFirstLessonParityProof => {
  const result = asRecord(raw);
  const coverage = asRecord(result.coverage);
  const turns = Array.isArray(result.turns) ? result.turns.map(asRecord) : [];
  const aftersteps = Array.isArray(result.aftersteps) ? result.aftersteps.map(asRecord) : [];
  const accessMode = result.accessMode === 'managed' || result.accessMode === 'byok'
    ? result.accessMode
    : null;
  const responseSourceCounts: Record<string, number> = {};
  for (const afterstep of aftersteps) {
    const source = typeof afterstep.responseSource === 'string' ? afterstep.responseSource : 'missing';
    responseSourceCounts[source] = (responseSourceCounts[source] || 0) + 1;
  }
  const stableResponseSourceCounts = Object.fromEntries(
    Object.entries(responseSourceCounts).sort(([left], [right]) => left.localeCompare(right)),
  );

  return {
    accessMode,
    passed: result.passed === true,
    uploadGeneratedMedia: result.uploadGeneratedMedia === true,
    userTurnCount: Number(result.userTurnCount) || 0,
    coverageKeys: Object.keys(coverage).sort(),
    failedCoverage: Object.entries(coverage)
      .filter(([, value]) => value !== true)
      .map(([key]) => key)
      .sort(),
    turnKinds: strings(turns.map(turn => turn.kind)),
    responseSourceCounts: stableResponseSourceCounts,
    generatedToolKinds: [...new Set(strings(aftersteps.map(item => item.tool)))].sort(),
    uploadedToolKinds: [...new Set(strings(aftersteps
      .filter(item => item.uploaded === true)
      .map(item => item.tool)))].sort(),
  };
};

/**
 * Compare stable semantic evidence only. Provider wording and optional model
 * tool choices may differ, but both transports must execute the same ordered
 * journey, prove every coverage flag and upload each required generated type.
 */
export const compareHeadlessFirstLessonParity = (
  managedRaw: unknown,
  byokRaw: unknown,
): HeadlessAccessParityComparison => {
  const managed = buildHeadlessFirstLessonParityProof(managedRaw);
  const byok = buildHeadlessFirstLessonParityProof(byokRaw);
  const mismatches: string[] = [];
  if (managed.accessMode !== 'managed') mismatches.push('Managed proof is not labelled managed.');
  if (byok.accessMode !== 'byok') mismatches.push('BYOK proof is not labelled byok.');
  if (!managed.passed || managed.failedCoverage.length) mismatches.push('Managed first-lesson proof is incomplete.');
  if (!byok.passed || byok.failedCoverage.length) mismatches.push('BYOK first-lesson proof is incomplete.');
  if (!managed.uploadGeneratedMedia || !byok.uploadGeneratedMedia) {
    mismatches.push('Generated-media upload was not exercised in both access modes.');
  }
  if (!sameStrings(managed.coverageKeys, byok.coverageKeys)) {
    mismatches.push('Managed and BYOK coverage keys differ.');
  }
  if (!sameStrings(managed.turnKinds, byok.turnKinds)) {
    mismatches.push('Managed and BYOK journey turn order differs.');
  }
  if (managed.userTurnCount !== byok.userTurnCount) {
    mismatches.push('Managed and BYOK persistent user-turn counts differ.');
  }
  if (JSON.stringify(managed.responseSourceCounts) !== JSON.stringify(byok.responseSourceCounts)) {
    mismatches.push('Managed and BYOK suggestion source counts differ.');
  }
  for (const tool of FIRST_LESSON_TOOL_KINDS) {
    if (!managed.generatedToolKinds.includes(tool) || !byok.generatedToolKinds.includes(tool)) {
      mismatches.push(`The ${tool} provider path was not generated in both access modes.`);
    }
    if (!managed.uploadedToolKinds.includes(tool) || !byok.uploadedToolKinds.includes(tool)) {
      mismatches.push(`The ${tool} upload path was not proved in both access modes.`);
    }
  }
  return { passed: mismatches.length === 0, mismatches, managed, byok };
};
