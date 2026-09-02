// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/**
 * WCAG contrast maths over the app's HSL token values.
 *
 * Used when deriving the transparent theme variants: once a surface stops
 * painting a fill, whatever sat on it is suddenly read against the page, and
 * text tuned for the old fill can end up invisible. `ensureContrast` walks a
 * colour's lightness until it is legible again, leaving hue and saturation
 * alone so the theme still looks like itself.
 */

import { parseTokenValue } from './tokenValue';

export interface Hsl {
  h: number;
  s: number;
  l: number;
}

/** Parse the channel triplet of a token value. Alpha is ignored. */
export function parseHsl(value: string): Hsl | null {
  const parts = parseTokenValue(value).channels.split(/\s+/);
  if (parts.length < 3) return null;

  const h = parseFloat(parts[0]);
  const s = parseFloat(parts[1]);
  const l = parseFloat(parts[2]);
  if ([h, s, l].some(Number.isNaN)) return null;

  return { h, s, l };
}

export const formatHsl = ({ h, s, l }: Hsl): string =>
  `${Math.round(h)} ${Math.round(s)}% ${Math.round(l)}%`;

function hueToRgb(p: number, q: number, t: number): number {
  let x = t;
  if (x < 0) x += 1;
  if (x > 1) x -= 1;
  if (x < 1 / 6) return p + (q - p) * 6 * x;
  if (x < 1 / 2) return q;
  if (x < 2 / 3) return p + (q - p) * (2 / 3 - x) * 6;
  return p;
}

/** sRGB channels in the 0..1 range. */
export function hslToRgb({ h, s, l }: Hsl): [number, number, number] {
  const hn = (((h % 360) + 360) % 360) / 360;
  const sn = Math.min(1, Math.max(0, s / 100));
  const ln = Math.min(1, Math.max(0, l / 100));

  if (sn === 0) return [ln, ln, ln];

  const q = ln < 0.5 ? ln * (1 + sn) : ln + sn - ln * sn;
  const p = 2 * ln - q;
  return [hueToRgb(p, q, hn + 1 / 3), hueToRgb(p, q, hn), hueToRgb(p, q, hn - 1 / 3)];
}

/** WCAG 2.x relative luminance. */
export function relativeLuminance(hsl: Hsl): number {
  const [r, g, b] = hslToRgb(hsl).map(channel =>
    channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4,
  );
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** WCAG contrast ratio, 1 (identical) to 21 (black on white). */
export function contrastRatio(a: Hsl, b: Hsl): number {
  const la = relativeLuminance(a);
  const lb = relativeLuminance(b);
  const [light, dark] = la > lb ? [la, lb] : [lb, la];
  return (light + 0.05) / (dark + 0.05);
}

/**
 * Nudge `foreground`'s lightness until it clears `target` contrast against
 * `background`, keeping hue and saturation.
 *
 * Steps away from the background rather than picking a direction up front, so a
 * mid-grey on a mid-grey page resolves toward whichever end actually has the
 * headroom. Returns the input untouched when it already passes, and the best
 * lightness found when the hue simply cannot reach the target (a saturated
 * yellow on white tops out around 2:1).
 */
export function ensureContrast(foreground: Hsl, background: Hsl, target: number): Hsl {
  if (contrastRatio(foreground, background) >= target) return foreground;

  const towardsDark = relativeLuminance(background) > 0.18;
  const candidates: Hsl[] = [];

  for (let step = 1; step <= 100; step++) {
    const l = towardsDark ? foreground.l - step : foreground.l + step;
    if (l < 0 || l > 100) break;
    candidates.push({ ...foreground, l });
  }
  // If walking one way runs out of room, try the other before giving up.
  for (let step = 1; step <= 100; step++) {
    const l = towardsDark ? foreground.l + step : foreground.l - step;
    if (l < 0 || l > 100) break;
    candidates.push({ ...foreground, l });
  }

  let best = foreground;
  let bestRatio = contrastRatio(foreground, background);
  for (const candidate of candidates) {
    const ratio = contrastRatio(candidate, background);
    if (ratio >= target) return candidate;
    if (ratio > bestRatio) {
      best = candidate;
      bestRatio = ratio;
    }
  }
  return best;
}
