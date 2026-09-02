// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/**
 * Derives the "Clear" variant of a palette: fills drop away and the sketch
 * outlines carry the design, which is the look a lot of people preferred before
 * the tokens gained opacity.
 *
 * The variants are computed rather than written out. A hand-maintained copy of
 * every palette would be ~3,400 lines of data that has to be revisited every
 * time a token is added, and would silently rot the first time someone forgot.
 * Deriving them means a new token gets a sensible clear value in all eleven
 * themes for free, and the rules below are the single place to argue with.
 *
 * Two things stop this from being "set every background to zero":
 *
 *   1. Some fills are the only thing making their content readable or
 *      noticeable - the Maestro flag, the translation highlight, the audio
 *      player's controls, the API key gate, the traffic log's header and cards,
 *      and anything drawn over photos or video. Those keep their fill.
 *   2. Text tuned for a fill is often invisible on the page once the fill goes.
 *      Graphite's user bubble is dark with near-white text; drop the bubble and
 *      that text lands on near-white paper. Every foreground whose surface went
 *      clear is re-checked against the page and walked back to a legible
 *      lightness, keeping its hue.
 */

import { COLOR_GROUPS } from './colorRegistry';
import { formatTokenValue, parseTokenValue } from '../utils/tokenValue';
import { contrastRatio, ensureContrast, formatHsl, parseHsl } from '../utils/contrast';

/**
 * Groups left exactly as they are. Their colours are either drawn over media
 * rather than over the page, or they *are* the outline the clear look depends
 * on, or the user called them out as needing to stay solid.
 */
const UNTOUCHED_GROUPS: ReadonlySet<string> = new Set([
  'Overlay Scrims',
  'Media Overlay',
  'Mini-game Overlay',
  'Message Tape Effect',
  'Notebook Marks',
  'Borders and Focus',
  'Voice Identity',
  'API Key Gate',
  'Translation Highlight',
  'Maestro Flag: Hold',
  'Maestro Flag: Speaking and Typing',
  'Maestro Flag: Listening, Observing, Idle',
]);

/** Surfaces that keep their fill even in a clear theme. */
const KEEP_FILLED: ReadonlySet<string> = new Set([
  // The canvas everything else is now read against.
  'page-bg',
  // The audio player's own controls; only its container goes clear.
  'audio-play-btn',
  'audio-bar',
  // The traffic log stays legible: only the sheet behind it turns see-through.
  'debug-header-bg',
  'debug-card-bg',
  'debug-btn-bg',
  // Recording and live state has to be unmistakable at a glance.
  'mic-record-bg',
  'mic-stt-bg',
  'live-badge-bg',
  'rec-error-bg',
  'top-live-active-bg',
  'top-live-error-bg',
  'overlay-live-error-bg',
  // Controls drawn on top of photos and video, where there is no page behind.
  'live-stop-bg',
  'vid-stop-bg',
  'remove-attach-bg',
  'overlay-live-error-hover',
  // A destructive action should never be a faint outline.
  'danger-btn-bg',
]);

/** Fills reduced to a wash instead of removed, so the state still registers. */
const TINTED: ReadonlyMap<string, number> = new Map([
  ['suggestion-active-bg', 0.18],
  ['stt-lang-selected-bg', 0.18],
  ['stt-lang-selected-sugg-bg', 0.18],
  ['input-error-bg', 0.16],
  ['snapshot-error-bg', 0.16],
  ['media-empty-bg', 0.08],
]);

/** Alpha for hover feedback, which would otherwise vanish entirely. */
const HOVER_ALPHA = 0.14;

/** Surfaces whose names do not end in `-bg`. */
const EXTRA_SURFACES: ReadonlySet<string> = new Set([
  'paper-surface',
  'paper-stripe',
  'ai-msg-placeholder',
  'theme-preset-btn',
]);

/** Foregrounds paired with a surface the name does not imply. */
const SURFACE_OF: Readonly<Record<string, string>> = {
  'audio-play-text': 'audio-play-btn',
  'audio-time-text': 'audio-player-bg',
  'bookmark-input-text': 'bookmark-input-bg',
  'history-peek-icon': 'history-peek-bg',
  'live-badge-dot': 'live-badge-bg',
  'live-stop-icon': 'live-stop-bg',
  'vid-stop-icon': 'vid-stop-bg',
  'remove-attach-icon': 'remove-attach-bg',
  'chat-input-icon': 'chat-input-bg',
  'sugg-input-icon': 'sugg-input-bg',
  'mic-record-icon': 'mic-record-bg',
  'mic-stt-icon': 'mic-stt-bg',
  'debug-btn-muted': 'debug-btn-bg',
  'debug-btn-text': 'debug-btn-bg',
};

/** Decorative colours that are never meant to meet a contrast floor. */
const DECORATIVE = /(-glow|-shadow|-ring|-focus|-border|-line|-divider|-underline|-wash|-stroke|-pulse-outer|-pulse-inner|-inset|-crease|-wrinkle|-highlight)$/;

/** Roles that read as body text; everything else is treated as a UI mark. */
const TEXT_ROLE = /(-text|-link|-placeholder)$/;

const TEXT_CONTRAST = 4.5;
const UI_CONTRAST = 3;

const isSurface = (name: string): boolean => name.endsWith('-bg') || EXTRA_SURFACES.has(name);
const isHover = (name: string): boolean => name.endsWith('-hover') || name.includes('-hover-');

/** The surface a foreground is drawn on, if the token set has one. */
function surfaceOf(name: string, palette: Record<string, string>): string | null {
  if (SURFACE_OF[name]) return SURFACE_OF[name];
  const base = name.replace(/-(text|icon|accent|muted|link|placeholder|dot|spinner)$/, '');
  if (base === name) return null;
  const candidate = `${base}-bg`;
  return candidate in palette ? candidate : null;
}

/** The fill a hover state belongs to, so a kept fill keeps its hover. */
function hoverBaseOf(name: string): string {
  return `${name.replace(/-hover(-bg)?$/, '')}-bg`;
}

const groupOfToken = (() => {
  const map = new Map<string, string>();
  for (const group of COLOR_GROUPS) {
    for (const color of group.colors) map.set(color.cssVar, group.groupName);
  }
  return map;
})();

/** True when this token's fill is removed or reduced in a clear theme. */
function goesClear(name: string): boolean {
  const group = groupOfToken.get(name);
  if (group && UNTOUCHED_GROUPS.has(group)) return false;
  if (KEEP_FILLED.has(name)) return false;
  if (isHover(name)) return !KEEP_FILLED.has(hoverBaseOf(name));
  return isSurface(name) || TINTED.has(name);
}

/**
 * Build the clear variant of a palette.
 *
 * Every key of `base` is preserved, so the result is exactly as complete as
 * what it was derived from.
 */
export function makeTransparentPalette(base: Record<string, string>): Record<string, string> {
  const pageBg = parseHsl(base['page-bg'] ?? '0 0% 100%') ?? { h: 0, s: 0, l: 100 };
  const result: Record<string, string> = {};

  // Pass 1: thin out the fills.
  for (const [name, value] of Object.entries(base)) {
    if (!goesClear(name)) {
      result[name] = value;
      continue;
    }
    const { channels } = parseTokenValue(value);
    const alpha = TINTED.get(name) ?? (isHover(name) ? HOVER_ALPHA : 0);
    result[name] = formatTokenValue(channels, alpha);
  }

  // Pass 2: whatever sat on a fill that just disappeared now sits on the page.
  for (const [name, value] of Object.entries(base)) {
    const group = groupOfToken.get(name);
    if (group && UNTOUCHED_GROUPS.has(group)) continue;
    if (isSurface(name) || isHover(name) || DECORATIVE.test(name)) continue;

    const surface = surfaceOf(name, base);
    // Still sitting on a real fill, so its original tuning still holds.
    if (surface && !goesClear(surface)) continue;

    const hsl = parseHsl(value);
    if (!hsl) continue;

    const target = TEXT_ROLE.test(name) ? TEXT_CONTRAST : UI_CONTRAST;
    const fixed = ensureContrast(hsl, pageBg, target);
    if (fixed.l === hsl.l) continue;

    result[name] = formatTokenValue(formatHsl(fixed), parseTokenValue(value).alpha);
  }

  return result;
}

/** Reports tokens that still fail their contrast floor, for the test suite. */
export function unreadableTokens(
  palette: Record<string, string>,
): { cssVar: string; ratio: number; target: number }[] {
  const pageBg = parseHsl(palette['page-bg'] ?? '0 0% 100%');
  if (!pageBg) return [];

  const failures: { cssVar: string; ratio: number; target: number }[] = [];
  for (const [name, value] of Object.entries(palette)) {
    const group = groupOfToken.get(name);
    if (group && UNTOUCHED_GROUPS.has(group)) continue;
    if (isSurface(name) || isHover(name) || DECORATIVE.test(name)) continue;

    const surface = surfaceOf(name, palette);
    if (surface && parseTokenValue(palette[surface] ?? '').alpha > 0.5) continue;

    const hsl = parseHsl(value);
    if (!hsl) continue;

    const target = TEXT_ROLE.test(name) ? TEXT_CONTRAST : UI_CONTRAST;
    const ratio = contrastRatio(hsl, pageBg);
    if (ratio + 0.005 < target) failures.push({ cssVar: name, ratio, target });
  }
  return failures;
}
