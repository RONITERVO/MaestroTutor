// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/**
 * Regenerates the counts in the Overview and the whole Full Token Inventory
 * section of docs/DESIGN_SYSTEM.md from the registry and the active default
 * palette. Run it after adding or renaming a token:
 *
 *   npm run docs:tokens
 *
 * `tokenVars.test.ts` fails if the committed doc does not match this output, so
 * the inventory cannot drift away from the code again.
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { COLOR_GROUPS, ALL_COLOR_VARS, type ColorGroup } from '../src/features/theme/config/colorRegistry';
import { DEFAULT_THEME_COLORS } from '../src/features/theme/config/defaultTheme';

const HERE = dirname(fileURLToPath(import.meta.url));
export const DOC_PATH = join(HERE, '..', 'docs', 'DESIGN_SYSTEM.md');

const INVENTORY_HEADING = '## Full Token Inventory';

const cell = (value: string | undefined): string => (value ?? '').replace(/\|/g, '\\|');

export function renderDoc(
  source: string,
  groups: ColorGroup[],
  defaults: Record<string, string>,
  tokenCount: number,
): string {
  const eol = source.includes('\r\n') ? '\r\n' : '\n';
  const lines: string[] = [
    INVENTORY_HEADING,
    '',
    'Generated from `colorRegistry.ts` by `npm run docs:tokens`. Do not edit by hand.',
    '',
    'Default HSL is the value in the active default palette; every other theme may override it.',
    '',
  ];

  for (const group of groups) {
    lines.push(`### ${group.groupName}`, '');
    if (group.groupDescription) lines.push(group.groupDescription, '');
    lines.push('| CSS Variable | Default HSL | Friendly Name | Description |');
    lines.push('|---|---|---|---|');
    for (const color of group.colors) {
      lines.push(
        `| \`--${color.cssVar}\` | \`${cell(defaults[color.cssVar])}\` | ` +
        `${cell(color.friendlyName)} | ${cell(color.description)} |`,
      );
    }
    lines.push('');
  }

  const start = source.indexOf(INVENTORY_HEADING);
  if (start < 0) throw new Error(`${INVENTORY_HEADING} not found in ${DOC_PATH}`);

  const preamble = source
    .slice(0, start)
    .replace(/^- Active color tokens: \d+$/m, `- Active color tokens: ${tokenCount}`)
    .replace(/^- Token groups: \d+$/m, `- Token groups: ${groups.length}`);

  return preamble + lines.join(eol).trimEnd() + eol;
}

/** The doc as it should be on disk. A current doc regenerates to itself. */
export function buildDoc(): string {
  return renderDoc(
    readFileSync(DOC_PATH, 'utf8'),
    COLOR_GROUPS,
    DEFAULT_THEME_COLORS,
    ALL_COLOR_VARS.length,
  );
}

// Only write when run directly; the test imports buildDoc to compare.
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  writeFileSync(DOC_PATH, buildDoc());
  console.log('docs/DESIGN_SYSTEM.md regenerated');
}
