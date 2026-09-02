// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/**
 * The grammar for a stored theme token value, and the CSS variable naming
 * contract built on top of it.
 *
 * A token value is `"<h> <s>% <l>%"` with an optional alpha: `"210 20% 97% / 0.5"`.
 * Omitting the alpha means fully opaque, so every theme written before opacity
 * existed keeps its exact meaning.
 *
 * Each token expands to three CSS variables:
 *
 *   --page-bg:        210 20% 97%                          <- channels only
 *   --page-bg-alpha:  0.5                                  <- emitted only when < 1
 *   --page-bg-color:  hsl(var(--page-bg) / var(--page-bg-alpha, 1))
 *
 * The channels and the alpha have to stay in separate variables. Tailwind
 * composes its own opacity modifier by injecting an alpha into the colour, so a
 * token that already carried one inline would produce `hsl(H S% L% / 0.5 / .3)`
 * for `bg-page-bg/30` - invalid CSS, silently dropped by the browser. Keeping
 * them apart lets the two multiply instead (see `tailwindColorValue`).
 *
 * Consumers pick by context:
 *   - Tailwind utility classes: `bg-page-bg`, `bg-page-bg/30`
 *   - hand-written CSS and inline styles: `var(--page-bg-color)`
 *
 * Writing `hsl(var(--page-bg))` by hand drops the user's opacity, so
 * `tokenVars.test.ts` fails the build if that shape appears in source.
 */

/** Suffix for the variable holding a token's user-set opacity. */
export const ALPHA_VAR_SUFFIX = '-alpha';

/** Suffix for the ready-to-use colour variable combining channels and alpha. */
export const COLOR_VAR_SUFFIX = '-color';

/** Used when a token has no value anywhere - a visible mid grey, not a crash. */
export const FALLBACK_CHANNELS = '0 0% 50%';

export interface TokenValue {
  /** Bare HSL channel triplet, e.g. `"210 20% 97%"`. Never carries alpha. */
  channels: string;
  /** Opacity in the 0..1 range. */
  alpha: number;
}

export const alphaVarName = (cssVar: string): string => `--${cssVar}${ALPHA_VAR_SUFFIX}`;
export const colorVarName = (cssVar: string): string => `--${cssVar}${COLOR_VAR_SUFFIX}`;

/**
 * The colour value registered with Tailwind for a token.
 *
 * `<alpha-value>` is Tailwind's placeholder: it becomes `1` for a bare `bg-x`
 * and `0.3` for `bg-x/30`. Multiplying it by the user's alpha means neither
 * control can override the other - a token the user set to 50% rendered through
 * `bg-x/30` lands at 15%, and `bg-x` alone is exactly the user's 50%.
 */
export const tailwindColorValue = (cssVar: string): string =>
  `hsl(var(--${cssVar}) / calc(var(--${cssVar}${ALPHA_VAR_SUFFIX}, 1) * <alpha-value>))`;

/** The `--x-color` definition, shared by the build-time generator. */
export const colorVarValue = (cssVar: string): string =>
  `hsl(var(--${cssVar}) / var(--${cssVar}${ALPHA_VAR_SUFFIX}, 1))`;

const clampAlpha = (value: number): number => {
  if (!Number.isFinite(value)) return 1;
  return Math.min(1, Math.max(0, value));
};

/**
 * Split a stored value into channels and alpha. Anything unparseable degrades
 * to opaque rather than throwing - a malformed imported theme should render
 * wrong, not break the app.
 */
export function parseTokenValue(raw: string | null | undefined): TokenValue {
  if (typeof raw !== 'string') {
    return { channels: FALLBACK_CHANNELS, alpha: 1 };
  }

  const [rawChannels, rawAlpha, ...extra] = raw.split('/');
  const channels = rawChannels.trim().replace(/\s+/g, ' ');

  // More than one slash is not a value this app ever wrote; treat the whole
  // thing as suspect and keep only the channels.
  if (!channels || extra.length > 0) {
    return { channels: channels || FALLBACK_CHANNELS, alpha: 1 };
  }
  if (rawAlpha === undefined) {
    return { channels, alpha: 1 };
  }

  const trimmed = rawAlpha.trim();
  const alpha = trimmed.endsWith('%')
    ? parseFloat(trimmed) / 100
    : parseFloat(trimmed);

  return { channels, alpha: clampAlpha(alpha) };
}

/**
 * Inverse of `parseTokenValue`. Opaque tokens serialise without an alpha, so
 * values round-trip byte-identically to what earlier versions stored and
 * exported theme files stay free of `/ 1` noise.
 */
export function formatTokenValue(channels: string, alpha: number): string {
  const normalised = channels.trim().replace(/\s+/g, ' ');
  const clamped = clampAlpha(alpha);
  if (clamped >= 1) return normalised;
  // Three decimals is finer than the 0-255 alpha the picker can produce.
  return `${normalised} / ${parseFloat(clamped.toFixed(3))}`;
}
