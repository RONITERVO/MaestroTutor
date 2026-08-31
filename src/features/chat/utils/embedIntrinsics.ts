// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Static derivation of an embed's layout box.
 *
 * The chat reserves space for a rich attachment *before* anything is mounted, so
 * that mounting, unmounting, freezing or never-running an embed can never move
 * the page. That is only possible if the box can be computed from the attachment
 * source text alone — no iframe, no canvas, no layout.
 *
 * We store an aspect ratio rather than a pixel height on purpose: a ratio is
 * viewport-independent, so it stays correct across rotation, keyboard show/hide,
 * split-screen and font scaling. A remembered pixel height is wrong the moment
 * any of those change, which is what forced a live re-measure (and a visible
 * jump) in the previous design.
 */

import type { EmbedBox, EmbedKind } from '../embeds/embedTypes';

/** Bump when the heuristics below change enough to invalidate stored boxes. */
export const EMBED_BOX_VERSION = 1;

/** Portrait/landscape extremes we refuse to reserve, to keep bubbles sane. */
export const MIN_EMBED_ASPECT_RATIO = 0.4;
export const MAX_EMBED_ASPECT_RATIO = 3;

export const DEFAULT_ASPECT_RATIO_BY_KIND: Record<EmbedKind, number> = {
  'mini-game': 4 / 3,
  pdf: 1 / Math.SQRT2, // A4 portrait
  office: 1 / Math.SQRT2,
  artifact: 1,
};

/** Scan cap: intrinsic declarations live in markup, not deep inside game logic. */
const MAX_SCAN_CHARS = 96 * 1024;

const META_HINT_RE = /<meta[^>]+name\s*=\s*["']maestro-aspect["'][^>]*content\s*=\s*["']([^"']+)["']/i;
const DATA_HINT_RE = /data-maestro-aspect\s*=\s*["']([^"']+)["']/i;
const CANVAS_RE = /<canvas\b[^>]*>/i;
const SVG_TAG_RE = /<svg\b[^>]*>/i;
const VIEWBOX_RE = /viewBox\s*=\s*["']\s*[-\d.eE+]+\s+[-\d.eE+]+\s+([-\d.eE+]+)\s+([-\d.eE+]+)\s*["']/i;
const CSS_ASPECT_RE = /aspect-ratio\s*:\s*([^;}"']+)/i;

const attributeNumber = (tag: string, name: string): number => {
  const match = tag.match(new RegExp(`\\b${name}\\s*=\\s*["']?([\\d.]+)`, 'i'));
  if (!match) return 0;
  const value = Number.parseFloat(match[1]);
  return Number.isFinite(value) && value > 0 ? value : 0;
};

/** Parse a CSS-style aspect-ratio value: "16/9", "1.777", "auto". */
export const parseAspectRatioValue = (value: string | null | undefined): number => {
  if (!value) return 0;
  const normalized = String(value).trim().toLowerCase();
  if (!normalized || normalized === 'auto') return 0;

  const slash = normalized.match(/^([\d.]+)\s*\/\s*([\d.]+)/);
  if (slash) {
    const width = Number.parseFloat(slash[1]);
    const height = Number.parseFloat(slash[2]);
    if (Number.isFinite(width) && Number.isFinite(height) && width > 0 && height > 0) {
      return width / height;
    }
    return 0;
  }

  const numeric = Number.parseFloat(normalized);
  return Number.isFinite(numeric) && numeric > 0 ? numeric : 0;
};

/** Clamp to the range we are willing to reserve, rounded for stable comparison. */
export const clampAspectRatio = (value: number): number => {
  if (!Number.isFinite(value) || value <= 0) return 0;
  const clamped = Math.min(MAX_EMBED_ASPECT_RATIO, Math.max(MIN_EMBED_ASPECT_RATIO, value));
  return Math.round(clamped * 1000) / 1000;
};

interface DeriveEmbedBoxOptions {
  sourceCode?: string | null;
  kind: EmbedKind;
  /** Known ratio from a cheap non-source probe (e.g. pdf.js page 1 viewport). */
  probedAspectRatio?: number | null;
}

/**
 * Derive an embed box from source text. Cheap enough to run at render time for
 * every message in the list; runs a handful of single-pass regexes over at most
 * MAX_SCAN_CHARS and touches no DOM.
 */
export const deriveEmbedBox = ({ sourceCode, kind, probedAspectRatio }: DeriveEmbedBoxOptions): EmbedBox => {
  const probed = clampAspectRatio(probedAspectRatio ?? 0);
  if (probed) {
    return { aspectRatio: probed, source: 'static', v: EMBED_BOX_VERSION };
  }

  const source = (sourceCode || '').slice(0, MAX_SCAN_CHARS);
  const fallback = (): EmbedBox => ({
    aspectRatio: clampAspectRatio(DEFAULT_ASPECT_RATIO_BY_KIND[kind]),
    source: 'static',
    v: EMBED_BOX_VERSION,
  });

  if (!source) return fallback();

  // 1. An explicit author hint always wins — this is what we ask artifacts to emit.
  const hint = source.match(META_HINT_RE) || source.match(DATA_HINT_RE);
  const hinted = clampAspectRatio(parseAspectRatioValue(hint?.[1]));
  if (hinted) return { aspectRatio: hinted, source: 'static', v: EMBED_BOX_VERSION };

  // 2. A canvas declares its own backing-store size, which is the game's box.
  const canvasTag = source.match(CANVAS_RE)?.[0];
  if (canvasTag) {
    const width = attributeNumber(canvasTag, 'width');
    const height = attributeNumber(canvasTag, 'height');
    const ratio = clampAspectRatio(width > 0 && height > 0 ? width / height : 0);
    if (ratio) return { aspectRatio: ratio, source: 'static', v: EMBED_BOX_VERSION };
  }

  // 3. An SVG viewBox (or explicit width/height) is authoritative for drawings.
  const svgTag = source.match(SVG_TAG_RE)?.[0];
  if (svgTag) {
    const viewBox = svgTag.match(VIEWBOX_RE);
    if (viewBox) {
      const width = Number.parseFloat(viewBox[1]);
      const height = Number.parseFloat(viewBox[2]);
      const ratio = clampAspectRatio(width > 0 && height > 0 ? width / height : 0);
      if (ratio) return { aspectRatio: ratio, source: 'static', v: EMBED_BOX_VERSION };
    }

    const width = attributeNumber(svgTag, 'width');
    const height = attributeNumber(svgTag, 'height');
    const ratio = clampAspectRatio(width > 0 && height > 0 ? width / height : 0);
    if (ratio) return { aspectRatio: ratio, source: 'static', v: EMBED_BOX_VERSION };
  }

  // 4. A literal aspect-ratio in the artifact's own CSS.
  const cssRatio = clampAspectRatio(parseAspectRatioValue(source.match(CSS_ASPECT_RE)?.[1]));
  if (cssRatio) return { aspectRatio: cssRatio, source: 'static', v: EMBED_BOX_VERSION };

  return fallback();
};

/**
 * Should a measurement taken from a live run replace the stored box?
 *
 * Deliberately strict: committing small deltas would make every boot nudge the
 * layout of the *next* session for no visible benefit.
 */
export const shouldCommitMeasuredBox = (stored: EmbedBox | undefined, measuredAspectRatio: number): boolean => {
  const measured = clampAspectRatio(measuredAspectRatio);
  if (!measured) return false;
  if (!stored || stored.v !== EMBED_BOX_VERSION) return true;
  if (stored.source === 'measured' && Math.abs(stored.aspectRatio - measured) < 0.04) return false;
  return Math.abs(stored.aspectRatio - measured) / stored.aspectRatio > 0.04;
};

/** Resolve a stored box, re-deriving when it is missing or from an older schema. */
export const resolveEmbedBox = (stored: EmbedBox | undefined, options: DeriveEmbedBoxOptions): EmbedBox => {
  if (stored && stored.v === EMBED_BOX_VERSION && clampAspectRatio(stored.aspectRatio)) {
    return { ...stored, aspectRatio: clampAspectRatio(stored.aspectRatio) };
  }
  return deriveEmbedBox(options);
};

/** The height the reserved box will occupy. Exported for tests and PDF windowing. */
export const computeEmbedBoxHeight = (
  aspectRatio: number,
  availableWidth: number,
  maxHeight: number | null | undefined,
  minHeight = 220,
): number => {
  const ratio = clampAspectRatio(aspectRatio) || DEFAULT_ASPECT_RATIO_BY_KIND.artifact;
  const natural = availableWidth > 0 ? availableWidth / ratio : minHeight;
  const capped = maxHeight && maxHeight > 0 ? Math.min(natural, maxHeight) : natural;
  return Math.max(minHeight, Math.round(capped));
};
