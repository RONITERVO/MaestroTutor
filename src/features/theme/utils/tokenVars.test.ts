// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, it, expect } from 'vitest';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { buildDoc, DOC_PATH } from '../../../../scripts/generate-design-system-docs';
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

  /**
   * Product UI has to be themeable end to end, so a colour from Tailwind's own
   * palette is never the right answer - a user cannot reach `bg-black/60` or
   * `text-red-800` from the customizer. The same goes for a literal colour
   * smuggled into an arbitrary value, e.g. `shadow-[0_2px_4px_rgba(0,0,0,.3)]`.
   *
   * Add a token to colorRegistry.ts instead; see docs/DESIGN_SYSTEM.md.
   *
   * Multi-colour illustrations (shared/ui/Icons.tsx), the canvas-generated
   * practice paper and the globe widget's own palette are deliberately outside
   * this rule: they are artwork rather than themeable chrome, and they are
   * `style`/SVG attributes rather than utility classes, so they do not match.
   */
  it('never styles product UI with a raw Tailwind palette colour', () => {
    const PALETTE = [
      'white', 'black', 'slate', 'gray', 'zinc', 'neutral', 'stone', 'red', 'orange',
      'amber', 'yellow', 'lime', 'green', 'emerald', 'teal', 'cyan', 'sky', 'blue',
      'indigo', 'violet', 'purple', 'fuchsia', 'pink', 'rose',
    ].join('|');
    const UTILITY = 'bg|text|border|ring|from|via|to|fill|stroke|shadow|divide|placeholder|outline|decoration|accent|caret';
    const paletteClass = new RegExp(
      `\\b(?:${UTILITY})-(?:${PALETTE})(?:-[0-9]{2,3})?(?:/(?:\\[[^\\]]*\\]|[0-9]+))?\\b`,
      'g',
    );
    // An arbitrary value carrying a literal colour, e.g. shadow-[0_1px_2px_#0003].
    const literalInArbitrary = new RegExp(
      `\\b(?:${UTILITY})-\\[[^\\]]*(?:rgba?\\(|hsla?\\(|#[0-9a-fA-F]{3,8})[^\\]]*\\]`,
      'g',
    );

    const offenders: string[] = [];
    for (const file of collectSourceFiles(SRC_ROOT)) {
      if (!/\.tsx?$/.test(file)) continue;
      const source = readFileSync(file, 'utf8');
      for (const re of [paletteClass, literalInArbitrary]) {
        for (const hit of source.matchAll(re)) {
          offenders.push(`${relative(REPO_ROOT, file)}: ${hit[0]}`);
        }
      }
    }

    expect(offenders).toEqual([]);
  });

  /**
   * A `-bg` token is a surface, and the Clear themes empty surfaces out. Paint
   * ink with one and it disappears the moment someone picks a Clear theme -
   * which is how the save/load confirmation panel lost its label and its
   * "type SAVE to confirm" prompt.
   *
   * Borders, rings and shadows are left out on purpose: a surface's own outline
   * or drop shadow is a real idiom, and it is correct for those to fade when
   * the fill they belong to does.
   */
  it('never paints ink with a surface token', () => {
    const INK = 'text|placeholder|caret|decoration|divide|fill|stroke';
    const inkFromSurface = new RegExp(`\\b(?:${INK})-([a-z0-9-]+-bg)(?:/[0-9]+)?\\b`, 'g');

    const offenders: string[] = [];
    for (const file of collectSourceFiles(SRC_ROOT)) {
      if (!/\.tsx?$/.test(file)) continue;
      for (const hit of readFileSync(file, 'utf8').matchAll(inkFromSurface)) {
        if (!TOKEN_NAMES.has(hit[1])) continue;
        offenders.push(`${relative(REPO_ROOT, file)}: ${hit[0]}`);
      }
    }

    expect(offenders).toEqual([]);
  });

  it('keeps the design system doc in step with the registry', () => {
    // Regenerating a current doc is a no-op, so any drift shows up as a diff.
    expect(buildDoc(), 'run `npm run docs:tokens`').toEqual(readFileSync(DOC_PATH, 'utf8'));
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
