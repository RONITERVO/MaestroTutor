// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, it, expect } from 'vitest';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { ALL_COLOR_VARS } from '../config/colorRegistry';
import { DEFAULT_THEME_COLORS } from '../config/defaultTheme';
import {
  ALPHA_VAR_SUFFIX,
  COLOR_VAR_SUFFIX,
  formatTokenValue,
  parseTokenValue,
  tailwindColorValue,
} from './tokenValue';

const SRC_ROOT = join(__dirname, '..', '..', '..');
const REPO_ROOT = join(SRC_ROOT, '..');
const TOKEN_NAMES = new Set(ALL_COLOR_VARS.map(({ cssVar }) => cssVar));

/** Every source file that could reference a theme token. */
function collectSourceFiles(dir: string, acc: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      collectSourceFiles(full, acc);
    } else if (/\.(ts|tsx|css)$/.test(entry) && !/\.test\.tsx?$/.test(entry)) {
      acc.push(full);
    }
  }
  return acc;
}

/**
 * Extract the argument list of every `hsl(...)` call, matching parentheses so
 * nested `var()` and `calc()` do not truncate the match.
 */
function hslCalls(source: string): string[] {
  const calls: string[] = [];
  const opener = /hsl\(/g;
  let match: RegExpExecArray | null;

  while ((match = opener.exec(source)) !== null) {
    let depth = 1;
    let i = match.index + match[0].length;
    while (i < source.length && depth > 0) {
      if (source[i] === '(') depth++;
      else if (source[i] === ')') depth--;
      i++;
    }
    calls.push(source.slice(match.index + match[0].length, i - 1));
  }
  return calls;
}

describe('theme token CSS variables', () => {
  it('gives every registered token a default value', () => {
    const missing = ALL_COLOR_VARS
      .map(({ cssVar }) => cssVar)
      .filter(cssVar => DEFAULT_THEME_COLORS[cssVar] === undefined);

    expect(missing).toEqual([]);
  });

  it('keeps the -alpha and -color suffixes free of collisions', () => {
    const collisions = [...TOKEN_NAMES].filter(
      name => TOKEN_NAMES.has(name + ALPHA_VAR_SUFFIX) || TOKEN_NAMES.has(name + COLOR_VAR_SUFFIX),
    );

    expect(collisions).toEqual([]);
  });

  it('registers every token with Tailwind in the <alpha-value> form', () => {
    // Without the placeholder, Tailwind guesses at opacity modifiers and the
    // user's own alpha is dropped entirely.
    for (const { cssVar } of ALL_COLOR_VARS) {
      const value = tailwindColorValue(cssVar);
      expect(value).toContain('<alpha-value>');
      expect(value).toContain(`var(--${cssVar}${ALPHA_VAR_SUFFIX}, 1)`);
    }
  });

  /**
   * The guard that keeps user-set opacity working as the app grows: any hsl()
   * built on a theme token has to fold in that token's alpha. Both
   * `hsl(var(--x))` and `hsl(var(--x) / 0.5)` silently ignore what the user
   * chose in the customizer.
   *
   * Use `var(--x-color)` for the plain case, or
   * `hsl(var(--x) / calc(var(--x-alpha, 1) * 0.5))` to tint it further.
   */
  it('never reads a token colour without its alpha', () => {
    const offenders: string[] = [];

    for (const file of collectSourceFiles(SRC_ROOT)) {
      // tokenValue.ts is where the correct forms are defined.
      if (file.endsWith(join('theme', 'utils', 'tokenValue.ts'))) continue;

      for (const args of hslCalls(readFileSync(file, 'utf8'))) {
        for (const token of args.matchAll(/var\(--([a-z][a-z0-9-]*)[),]/g)) {
          const name = token[1];
          if (!TOKEN_NAMES.has(name)) continue;
          if (args.includes(`var(--${name}${ALPHA_VAR_SUFFIX}`)) continue;
          offenders.push(`${relative(REPO_ROOT, file)}: hsl(${args.trim()})`);
        }
      }
    }

    expect(offenders).toEqual([]);
  });
});

describe('token value grammar', () => {
  it('treats a value without an alpha as opaque', () => {
    expect(parseTokenValue('210 20% 97%')).toEqual({ channels: '210 20% 97%', alpha: 1 });
  });

  it('splits an alpha off the channels', () => {
    expect(parseTokenValue('210 20% 97% / 0.5')).toEqual({ channels: '210 20% 97%', alpha: 0.5 });
  });

  it('accepts a percentage alpha', () => {
    expect(parseTokenValue('0 0% 0% / 40%').alpha).toBeCloseTo(0.4);
  });

  it('clamps an out-of-range alpha', () => {
    expect(parseTokenValue('0 0% 0% / 5').alpha).toBe(1);
    expect(parseTokenValue('0 0% 0% / -1').alpha).toBe(0);
  });

  it('falls back to opaque on a malformed value', () => {
    expect(parseTokenValue('0 0% 0% / 1 / 2').alpha).toBe(1);
    expect(parseTokenValue('0 0% 0% / nope').alpha).toBe(1);
    expect(parseTokenValue(undefined)).toEqual({ channels: '0 0% 50%', alpha: 1 });
  });

  it('omits the alpha when opaque, so old themes round-trip unchanged', () => {
    expect(formatTokenValue('210 20% 97%', 1)).toBe('210 20% 97%');
  });

  it('round-trips a translucent value', () => {
    const value = formatTokenValue('210 20% 97%', 0.5);
    expect(value).toBe('210 20% 97% / 0.5');
    expect(parseTokenValue(value)).toEqual({ channels: '210 20% 97%', alpha: 0.5 });
  });

  it('parses every shipped default theme value', () => {
    for (const [cssVar, value] of Object.entries(DEFAULT_THEME_COLORS)) {
      const { channels } = parseTokenValue(value);
      expect(channels.split(/\s+/), `${cssVar} = "${value}"`).toHaveLength(3);
    }
  });
});
