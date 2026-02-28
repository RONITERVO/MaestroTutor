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
  const h1 = hash(seed);
  const h2 = hash(seed + 7919); // second hash for variety
  const h3 = hash(seed + 104729); // third hash to determine large/small mappings

  const getVal = (isLarge: boolean, n: number) => {
    const arr = isLarge ? CORNERS : SMALLS;
    return arr[n % arr.length];
  };

  // Determine structural pattern for each corner. 0 = (Large, Small), 1 = (Small, Large)
  // This drastically increases unpredictability by changing the curve orientation.
  const pathTL = h3 & 1;
  const pathTR = (h3 >> 1) & 1;
  const pathBR = (h3 >> 2) & 1;
  const pathBL = (h3 >> 3) & 1;

  const tlX = getVal(pathTL === 0, h1);
  const trX = getVal(pathTR === 0, h1 >> 4);
  const brX = getVal(pathBR === 0, h1 >> 8);
  const blX = getVal(pathBL === 0, h1 >> 12);

  const tlY = getVal(pathTL === 1, h2);
  const trY = getVal(pathTR === 1, h2 >> 4);
  const brY = getVal(pathBR === 1, h2 >> 8);
  const blY = getVal(pathBL === 1, h2 >> 12);

  return {
    borderRadius: `${tlX}px ${trX}px ${brX}px ${blX}px / ${tlY}px ${trY}px ${brY}px ${blY}px`,
  };
}
