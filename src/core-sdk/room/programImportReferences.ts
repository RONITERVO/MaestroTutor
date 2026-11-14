// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { validModuleRecord } from '../../../shared/programModuleIdentity';
import type { ProgramModule } from '../../../shared/programSyntax';
import { strictProgramJson } from './programs';
const record = (value: unknown): value is Record<string, unknown> => value !== null && typeof value === 'object' && !Array.isArray(value);
/** Shared by visual authoring and agent drafts. Contents are verified, copied, never upgraded. */
export function copyVerifiedProgramModule(hash: string, definition: unknown): ProgramModule {
  if (!validModuleRecord(definition, hash)) throw new Error('The module contents do not match the inspected pin. Inspect it again.');
  return JSON.parse(JSON.stringify(definition)) as ProgramModule;
}
/** Explicit authoring shorthand only. Native/saved source always embeds full pins.
 * No lookup beyond the caller's observed definitions; no resource/signal grants. */
export function resolveProgramImportReferences(source: string, observed: { hash: string; definition: unknown }[]): string {
  if (source.length > 24000) throw new Error('Program source is too large.');
  const program = strictProgramJson(source);
  if (!record(program) || !Array.isArray(program.imports)) return source;
  if (program.imports.length > 4) throw new Error('Too many module imports.');
  let changed = false;
  for (const item of program.imports) {
    if (!record(item) || item.module !== null) continue;
    if (typeof item.hash !== 'string') throw new Error('An import reference needs an exact inspected hash.');
    const known = observed.find(candidate => candidate.hash === item.hash);
    if (!known) throw new Error('Inspect this exact module hash before using module:null.');
    item.module = copyVerifiedProgramModule(item.hash, known.definition); changed = true;
  }
  const expanded = changed ? JSON.stringify(program) : source;
  if (expanded.length > 24000) throw new Error('Expanded program source is too large.');
  return expanded;
}
