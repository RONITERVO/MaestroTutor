// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import {
  MAX_GLOBE_ZOOM,
  MIN_GLOBE_ZOOM,
  isPinWithinFrame,
  projectVector,
} from './LanguageSelectorGlobe';

/**
 * The globe's job as more countries arrive: zooming has to spread the flags
 * apart on screen *and* keep the number actually rendered bounded. Both are
 * geometry, so both are testable without mounting the component.
 */

const NO_ROTATION = { lng: 0, lat: 0 };
/** A point off the centre of the facing hemisphere. */
const OFF_CENTRE = { x: 0.3, y: 0.2, z: Math.sqrt(1 - 0.09 - 0.04) };

describe('globe projection under zoom', () => {
  it('leaves the globe centre fixed', () => {
    const centre = { x: 0, y: 0, z: 1 };
    for (const zoom of [MIN_GLOBE_ZOOM, 2, MAX_GLOBE_ZOOM]) {
      const p = projectVector(centre, NO_ROTATION, zoom);
      expect(p.screenX).toBeCloseTo(50, 6);
      expect(p.screenY).toBeCloseTo(50, 6);
    }
  });

  it('pushes points away from the centre in proportion to zoom', () => {
    const atOne = projectVector(OFF_CENTRE, NO_ROTATION, 1);
    const atThree = projectVector(OFF_CENTRE, NO_ROTATION, 3);
    // Pins keep their screen size, so tripling this offset is exactly what
    // triples the gap between neighbouring flags.
    expect(atThree.screenX - 50).toBeCloseTo((atOne.screenX - 50) * 3, 6);
    expect(atThree.screenY - 50).toBeCloseTo((atOne.screenY - 50) * 3, 6);
  });

  it('defaults to unzoomed so existing callers are unaffected', () => {
    expect(projectVector(OFF_CENTRE, NO_ROTATION)).toEqual(projectVector(OFF_CENTRE, NO_ROTATION, 1));
  });
});

describe('pin culling', () => {
  it('keeps pins inside the frame and a small margin around it', () => {
    expect(isPinWithinFrame(50, 50)).toBe(true);
    expect(isPinWithinFrame(0, 100)).toBe(true);
    expect(isPinWithinFrame(-5, 50)).toBe(true);
  });

  it('drops pins zoom has pushed well outside the window', () => {
    expect(isPinWithinFrame(-40, 50)).toBe(false);
    expect(isPinWithinFrame(50, 180)).toBe(false);
    expect(isPinWithinFrame(240, 240)).toBe(false);
  });

  it('culls more of a fixed set of points the further in you zoom', () => {
    // Points spread around the facing hemisphere.
    const points = Array.from({ length: 60 }, (_, i) => {
      const angle = (i / 60) * Math.PI * 2;
      const radius = 0.15 + (i % 6) * 0.14;
      const x = Math.cos(angle) * radius;
      const y = Math.sin(angle) * radius;
      return { x, y, z: Math.sqrt(Math.max(0, 1 - x * x - y * y)) };
    });

    const rendered = (zoom: number) => points
      .map((p) => projectVector(p, NO_ROTATION, zoom))
      .filter((p) => isPinWithinFrame(p.screenX, p.screenY))
      .length;

    expect(rendered(MIN_GLOBE_ZOOM)).toBe(points.length);
    expect(rendered(MAX_GLOBE_ZOOM)).toBeLessThan(rendered(MIN_GLOBE_ZOOM));
  });
});
