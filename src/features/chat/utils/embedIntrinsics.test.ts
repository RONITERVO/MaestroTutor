// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import {
  EMBED_BOX_VERSION,
  MAX_EMBED_ASPECT_RATIO,
  MIN_EMBED_ASPECT_RATIO,
  clampAspectRatio,
  computeEmbedBoxHeight,
  deriveEmbedBox,
  parseAspectRatioValue,
  resolveEmbedBox,
  shouldCommitMeasuredBox,
} from './embedIntrinsics';
import type { EmbedBox } from '../../../core/types';

describe('parseAspectRatioValue', () => {
  it('reads ratio and decimal forms, and rejects the rest', () => {
    expect(parseAspectRatioValue('16/9')).toBeCloseTo(16 / 9, 5);
    expect(parseAspectRatioValue(' 4 / 3 ')).toBeCloseTo(4 / 3, 5);
    expect(parseAspectRatioValue('1.5')).toBe(1.5);
    expect(parseAspectRatioValue('auto')).toBe(0);
    expect(parseAspectRatioValue('')).toBe(0);
    expect(parseAspectRatioValue(null)).toBe(0);
    expect(parseAspectRatioValue('16/0')).toBe(0);
  });
});

describe('deriveEmbedBox', () => {
  it('prefers an explicit author hint over anything measurable', () => {
    const source = `
      <meta name="maestro-aspect" content="16/9" />
      <canvas width="400" height="400"></canvas>
    `;
    expect(deriveEmbedBox({ sourceCode: source, kind: 'mini-game' }).aspectRatio)
      .toBeCloseTo(16 / 9, 3);
  });

  it('falls back to a canvas backing-store size', () => {
    const source = '<body><canvas id="game" width="800" height="600"></canvas></body>';
    expect(deriveEmbedBox({ sourceCode: source, kind: 'mini-game' }).aspectRatio)
      .toBeCloseTo(4 / 3, 3);
  });

  it('reads an SVG viewBox', () => {
    const source = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1000 500"></svg>';
    expect(deriveEmbedBox({ sourceCode: source, kind: 'artifact' }).aspectRatio).toBe(2);
  });

  it('reads a comma-separated viewBox', () => {
    // SVG allows commas between viewBox values, not only whitespace.
    const source = '<svg viewBox="0,0,1000,500"></svg>';
    expect(deriveEmbedBox({ sourceCode: source, kind: 'artifact' }).aspectRatio).toBe(2);
  });

  it('reads SVG width/height when there is no viewBox', () => {
    const source = '<svg width="300" height="200"></svg>';
    expect(deriveEmbedBox({ sourceCode: source, kind: 'artifact' }).aspectRatio).toBe(1.5);
  });

  it('reads a literal aspect-ratio from the artifact CSS', () => {
    const source = '<style>#stage { aspect-ratio: 3 / 2; }</style><div id="stage"></div>';
    expect(deriveEmbedBox({ sourceCode: source, kind: 'mini-game' }).aspectRatio).toBe(1.5);
  });

  it('uses the per-kind default when the source declares nothing', () => {
    const source = '<div>hello</div>';
    expect(deriveEmbedBox({ sourceCode: source, kind: 'mini-game' }).aspectRatio)
      .toBeCloseTo(4 / 3, 3);
  });

  it('prefers a probed ratio (e.g. a PDF page viewport) over source parsing', () => {
    const box = deriveEmbedBox({ sourceCode: '<canvas width="100" height="100">', kind: 'pdf', probedAspectRatio: 0.75 });
    expect(box.aspectRatio).toBe(0.75);
  });

  it('clamps absurd declared ratios into the reservable range', () => {
    const wide = deriveEmbedBox({ sourceCode: '<canvas width="4000" height="100">', kind: 'mini-game' });
    const tall = deriveEmbedBox({ sourceCode: '<canvas width="100" height="4000">', kind: 'mini-game' });
    expect(wide.aspectRatio).toBe(MAX_EMBED_ASPECT_RATIO);
    expect(tall.aspectRatio).toBe(MIN_EMBED_ASPECT_RATIO);
  });

  it('is deterministic: the same source always reserves the same box', () => {
    const source = '<canvas width="640" height="480"></canvas>';
    const first = deriveEmbedBox({ sourceCode: source, kind: 'mini-game' });
    const second = deriveEmbedBox({ sourceCode: source, kind: 'mini-game' });
    expect(second).toEqual(first);
  });
});

describe('resolveEmbedBox', () => {
  it('keeps a stored box from the current schema version', () => {
    const stored: EmbedBox = { aspectRatio: 1.9, source: 'measured', v: EMBED_BOX_VERSION };
    const resolved = resolveEmbedBox(stored, { sourceCode: '<canvas width="100" height="100">', kind: 'mini-game' });
    expect(resolved.aspectRatio).toBe(1.9);
    expect(resolved.source).toBe('measured');
  });

  it('re-derives when the stored box predates the current schema', () => {
    const stored: EmbedBox = { aspectRatio: 1.9, source: 'measured', v: EMBED_BOX_VERSION - 1 };
    const resolved = resolveEmbedBox(stored, { sourceCode: '<canvas width="200" height="100">', kind: 'mini-game' });
    expect(resolved.aspectRatio).toBe(2);
    expect(resolved.source).toBe('static');
  });

  it('re-derives when nothing is stored, so first paint reserves the right box', () => {
    const resolved = resolveEmbedBox(undefined, { sourceCode: '<canvas width="200" height="100">', kind: 'mini-game' });
    expect(resolved.aspectRatio).toBe(2);
  });
});

describe('shouldCommitMeasuredBox', () => {
  const stored: EmbedBox = { aspectRatio: 1.5, source: 'static', v: EMBED_BOX_VERSION };

  it('ignores measurements that barely differ, so boots do not churn the layout', () => {
    expect(shouldCommitMeasuredBox(stored, 1.52)).toBe(false);
  });

  it('commits a materially different measurement', () => {
    expect(shouldCommitMeasuredBox(stored, 2.1)).toBe(true);
  });

  it('ignores a measurement of zero', () => {
    expect(shouldCommitMeasuredBox(stored, 0)).toBe(false);
  });

  it('commits when nothing has ever been stored', () => {
    expect(shouldCommitMeasuredBox(undefined, 1.5)).toBe(true);
  });
});

describe('computeEmbedBoxHeight', () => {
  it('derives height from width and ratio', () => {
    expect(computeEmbedBoxHeight(2, 600, null)).toBe(300);
  });

  it('respects the viewport cap', () => {
    expect(computeEmbedBoxHeight(0.5, 600, 400)).toBe(400);
  });

  it('never collapses below the minimum', () => {
    expect(computeEmbedBoxHeight(3, 300, 400)).toBe(220);
  });

  it('is viewport-independent in ratio terms: rotation changes height, not the box', () => {
    // Same stored ratio, two viewport widths — the box is still "correct" in
    // both, which is exactly what storing a pixel height failed to do.
    const portrait = computeEmbedBoxHeight(1.5, 360, 900);
    const landscape = computeEmbedBoxHeight(1.5, 720, 900);
    expect(landscape).toBe(portrait * 2);
  });
});

describe('clampAspectRatio', () => {
  it('rejects non-finite and non-positive values', () => {
    expect(clampAspectRatio(Number.NaN)).toBe(0);
    expect(clampAspectRatio(0)).toBe(0);
    expect(clampAspectRatio(-2)).toBe(0);
  });
});
