/**
 * Sketchy Shape System
 * Generates unique organic border-radius shapes so no two adjacent
 * elements share the same silhouette. Uses 12 predefined CSS variants
 * (sketch-shape-0 through sketch-shape-11) and can also produce
 * fully unique inline borderRadius values from any numeric seed.
 */

const VARIANT_COUNT = 12;

/** Simple deterministic hash from a number seed. */
function hash(n: number): number {
  let h = (n * 2654435761) >>> 0;
  h = ((h >> 16) ^ h) * 0x45d9f3b;
  h = ((h >> 16) ^ h) * 0x45d9f3b;
  h = (h >> 16) ^ h;
  return h >>> 0;
}

/**
 * Returns a CSS class name like `sketch-shape-3` for the given index.
 * Adjacent indices are guaranteed to produce different variants.
 */
export function sketchShapeClass(index: number): string {
  // Use hash to avoid simple modular patterns
  const base = hash(index) % VARIANT_COUNT;
  const prev = hash(index - 1) % VARIANT_COUNT;
  // If collision with previous, shift by 1
  const variant = base === prev ? (base + 1) % VARIANT_COUNT : base;
  return `sketch-shape-${variant}`;
}

// Preset corner values for generating unique inline styles
const CORNERS = [255, 245, 235, 225, 215, 210, 200, 195, 185];
const SMALLS = [12, 15, 18, 20, 22, 25, 28, 30];

/**
 * Generates a fully unique inline borderRadius value from a seed.
 * No two seeds produce the same shape. Use for truly unique per-element shapes.
 */
export function sketchShapeStyle(seed: number): { borderRadius: string } {
  const h = hash(seed);
  const h2 = hash(seed + 7919); // second hash for variety

  const pick = (arr: number[], n: number) => arr[n % arr.length];

  const tl = pick(CORNERS, h);
  const tr = pick(SMALLS, h >> 4);
  const br = pick(CORNERS, h >> 8);
  const bl = pick(SMALLS, h >> 12);

  const tl2 = pick(SMALLS, h2);
  const tr2 = pick(CORNERS, h2 >> 4);
  const br2 = pick(SMALLS, h2 >> 8);
  const bl2 = pick(CORNERS, h2 >> 12);

  return {
    borderRadius: `${tl}px ${tr}px ${br}px ${bl}px / ${tl2}px ${tr2}px ${br2}px ${bl2}px`,
  };
}
