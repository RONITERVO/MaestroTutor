/**
 * Message Tape System
 * Generates deterministic random tape strip placements for messages,
 * giving the illusion that each message note is taped onto the page.
 *
 * Each tape has: position (side/corner), angle, length, width, offset,
 * and flags for wrinkled / poorly-attached states.
 */

// ── Types ──────────────────────────────────────────────────────
export interface TapeStrip {
  /** Placement area on the message */
  placement: 'top' | 'bottom' | 'left' | 'right' | 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right';
  /** Rotation in degrees */
  angle: number;
  /** Length in px */
  length: number;
  /** Width in px */
  width: number;
  /** Offset along the edge (0-1 fraction, 0 = start of edge, 1 = end) */
  offset: number;
  /** How far the tape overlaps outside the message (px) */
  overhang: number;
  /** Has a visible wrinkle / crease */
  wrinkled: boolean;
  /** Poorly attached — one end lifting */
  lifted: boolean;
}

export interface TapeLayout {
  tapes: TapeStrip[];
  /** Corners that have NO tape near them → eligible for corner-lift effect */
  liftedCorners: ('tl' | 'tr' | 'bl' | 'br')[];
}

// ── Deterministic hash (same as sketchyShape) ──────────────────
function hash(n: number): number {
  let h = (n * 2654435761) >>> 0;
  h = ((h >> 16) ^ h) * 0x45d9f3b;
  h = ((h >> 16) ^ h) * 0x45d9f3b;
  h = (h >> 16) ^ h;
  return h >>> 0;
}

/** Deterministic float 0..1 from two seeds */
function frac(a: number, b: number): number {
  return (hash(a * 7919 + b) % 10000) / 10000;
}

// ── All possible placements ────────────────────────────────────
const ALL_PLACEMENTS: TapeStrip['placement'][] = [
  'top', 'bottom', 'left', 'right',
  'top-left', 'top-right', 'bottom-left', 'bottom-right',
];

// Which corners a placement "covers" (holds down)
const PLACEMENT_CORNER_COVERAGE: Record<TapeStrip['placement'], ('tl' | 'tr' | 'bl' | 'br')[]> = {
  'top':          ['tl', 'tr'],
  'bottom':       ['bl', 'br'],
  'left':         ['tl', 'bl'],
  'right':        ['tr', 'br'],
  'top-left':     ['tl'],
  'top-right':    ['tr'],
  'bottom-left':  ['bl'],
  'bottom-right': ['br'],
};

/**
 * Generate a deterministic tape layout for a message at the given index.
 * Returns 1-8 tapes with varying properties, plus which corners are un-taped (lifted).
 */
export function generateTapeLayout(messageIndex: number): TapeLayout {
  const seed = hash(messageIndex + 42);

  // Number of tapes: 1-8, weighted toward 2-4
  const raw = frac(seed, 1);
  let count: number;
  if (raw < 0.08)      count = 1;
  else if (raw < 0.25) count = 2;
  else if (raw < 0.50) count = 3;
  else if (raw < 0.72) count = 4;
  else if (raw < 0.85) count = 5;
  else if (raw < 0.93) count = 6;
  else if (raw < 0.97) count = 7;
  else                 count = 8;

  // Pick which placements to use (shuffle deterministically, take first `count`)
  const order = ALL_PLACEMENTS.map((p, i) => ({ p, sort: frac(seed, i + 10) }));
  order.sort((a, b) => a.sort - b.sort);
  const chosen = order.slice(0, count).map(o => o.p);

  const coveredCorners = new Set<string>();
  const tapes: TapeStrip[] = chosen.map((placement, i) => {
    const idx = i + 1;
    // Angle: corners get ±30-55°, sides get ±0-15°
    const isCorner = placement.includes('-');
    const baseAngle = isCorner
      ? 30 + frac(seed, idx * 3) * 25
      : frac(seed, idx * 3) * 15;
    // Randomize sign
    const angle = frac(seed, idx * 5) > 0.5 ? baseAngle : -baseAngle;

    // Length: 40-90px
    const length = 40 + frac(seed, idx * 7) * 50;
    // Width: 14-26px
    const width = 14 + frac(seed, idx * 11) * 12;
    // Offset along edge: 0.15-0.85 for sides, 0.0-0.3 for corners
    const offset = isCorner
      ? frac(seed, idx * 13) * 0.3
      : 0.15 + frac(seed, idx * 13) * 0.7;
    // Overhang outside message
    const overhang = 8 + frac(seed, idx * 17) * 14;
    // 30% chance of wrinkle
    const wrinkled = frac(seed, idx * 19) < 0.30;
    // 20% chance of poorly attached
    const lifted = frac(seed, idx * 23) < 0.20;

    // Track which corners are covered
    for (const c of PLACEMENT_CORNER_COVERAGE[placement]) {
      coveredCorners.add(c);
    }

    return { placement, angle, length, width, offset, overhang, wrinkled, lifted };
  });

  // Corners not held by any tape → can lift
  const allCorners: ('tl' | 'tr' | 'bl' | 'br')[] = ['tl', 'tr', 'bl', 'br'];
  const liftedCorners = allCorners.filter(c => !coveredCorners.has(c));

  return { tapes, liftedCorners };
}

/**
 * Convert a TapeStrip into CSS inline styles for absolute positioning.
 */
export function tapeStripStyle(tape: TapeStrip): Record<string, string | number | undefined> {
  const style: Record<string, string | number | undefined> = {
    position: 'absolute',
    width: `${tape.length}px`,
    height: `${tape.width}px`,
    transform: `rotate(${tape.angle}deg)`,
    transformOrigin: 'center center',
  };

  const oh = tape.overhang;

  switch (tape.placement) {
    case 'top':
      style.top = `${-oh}px`;
      style.left = `${tape.offset * 100}%`;
      style.transform = `translateX(-50%) rotate(${tape.angle}deg)`;
      break;
    case 'bottom':
      style.bottom = `${-oh}px`;
      style.left = `${tape.offset * 100}%`;
      style.transform = `translateX(-50%) rotate(${tape.angle}deg)`;
      break;
    case 'left':
      style.left = `${-oh}px`;
      style.top = `${tape.offset * 100}%`;
      style.transform = `translateY(-50%) rotate(${tape.angle + 90}deg)`;
      break;
    case 'right':
      style.right = `${-oh}px`;
      style.top = `${tape.offset * 100}%`;
      style.transform = `translateY(-50%) rotate(${tape.angle + 90}deg)`;
      break;
    case 'top-left':
      style.top = `${-oh}px`;
      style.left = `${-oh * 0.5}px`;
      style.transform = `rotate(${tape.angle > 0 ? tape.angle : tape.angle - 45}deg)`;
      break;
    case 'top-right':
      style.top = `${-oh}px`;
      style.right = `${-oh * 0.5}px`;
      style.transform = `rotate(${tape.angle < 0 ? tape.angle : tape.angle + 45}deg)`;
      break;
    case 'bottom-left':
      style.bottom = `${-oh}px`;
      style.left = `${-oh * 0.5}px`;
      style.transform = `rotate(${tape.angle < 0 ? tape.angle : tape.angle + 45}deg)`;
      break;
    case 'bottom-right':
      style.bottom = `${-oh}px`;
      style.right = `${-oh * 0.5}px`;
      style.transform = `rotate(${tape.angle > 0 ? tape.angle : tape.angle - 45}deg)`;
      break;
  }

  return style;
}
