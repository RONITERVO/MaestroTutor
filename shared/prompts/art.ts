// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/**
 * Soft art direction for generated artifacts.
 *
 * This rides along in the first user turn as reference material rather than in
 * the system instruction. Anything stated in the system instruction reads as
 * policy and gets obeyed literally for the rest of the session, which is how a
 * house style hardens into a template. Here the register is descriptive: this
 * is what the sketchbook has looked like, not what the next page must contain.
 *
 * Fragments from several different artifact kinds are shown together on
 * purpose. What they share is the style; what they differ on is the subject,
 * and that difference is the part that says "invent something new".
 */

export const ART_STYLE_REFERENCE_MAX_CHARS = 6_000;

const STYLE_NOTES = [
  'Feel: aivan kuin luonnostelisit kynällä riisipaperille - pencil worked into a notebook page, never a rendered product UI.',
  'Ground: warm paper (#fffef2, #faf6ee, #fdfaf2, #fcf6ea) sitting on a transparent body, with a low-opacity warm drop shadow under it.',
  'Edges: dashed or hand-wobbled borders (#7a5230, #9d754b, #d8be9b) at 10-16px radius, sometimes a tape strip rotated a degree or two.',
  'Ink: brown-black strokes (#3e2723, #3c2a1e, #4a4035) with round caps and joins at 2-4px, trembled by feTurbulence + feDisplacementMap.',
  'Accents, used sparingly: brick red (#d32f2f, #8b3a2b, #9c3814), leaf green (#4f6336, #2e7d32), muted brown for secondary text (#5d4037, #857458).',
  'Type: handwritten for labels (Segoe Print, Caveat, Comic Sans MS, cursive), brush-cut serif for target-language glyphs (KaiTi, Kaiti SC, STKaiti, Noto Sans SC).',
  'Motion: restrained and organic - strokes drawing themselves in, a 3-5s ease-in-out float or sway, labels fading in staggered. Nothing bouncy, nothing arcade.',
  'Glosses stack rather than sprawl: target glyph large, romanisation under it, native gloss under that, all in the handwritten face.',
  'No emoji anywhere. They do not belong on paper.',
].join('\n');

const FRAGMENTS = [
  `Pencil tremble applied to plain geometry:
<filter id="pencil"><feTurbulence type="fractalNoise" baseFrequency="0.5" numOctaves="3" result="noise"/><feDisplacementMap in="SourceGraphic" in2="noise" scale="1.5"/></filter>
.sketch-stroke { fill:none; stroke:#3e2723; stroke-width:3; stroke-linecap:round; stroke-linejoin:round; filter:url(#pencil); }
<circle cx="0" cy="-40" r="20" class="sketch-stroke"/>
<path d="M 0 -20 L 0 30 M -15 0 L 15 -10 M 0 30 L -15 70 M 0 30 L 15 70" class="sketch-stroke"/>`,

  `Strokes that draw themselves, labels that arrive after them:
.sketch-line { stroke:#3c2a1e; stroke-width:3; fill:none; stroke-linecap:round; stroke-dasharray:300; stroke-dashoffset:300; animation:drawLine 2s ease forwards; }
.label { font-family:'Comic Sans MS', cursive, sans-serif; fill:#9c3814; font-size:16px; opacity:0; animation:fadeInLabel 1s forwards; }
@keyframes drawLine { to { stroke-dashoffset:0; } }
@keyframes fadeInLabel { to { opacity:1; } }
<path d="M 50 40 C 50 100, 30 110, 20 130 M 50 40 C 50 100, 70 110, 80 130" class="sketch-line" style="animation-delay:0.5s"/>`,

  `A page that breathes, with paper grain on the border and a soft shadow under the card:
@keyframes gentleFloat { 0%,100% { transform:translateY(0) rotate(0deg); } 50% { transform:translateY(-4px) rotate(1deg); } }
<filter id="soft-shadow"><feDropShadow dx="2" dy="4" stdDeviation="3" flood-color="#423218" flood-opacity="0.15"/></filter>
<rect x="18" y="16" width="344" height="308" rx="14" fill="#fcf6ea" stroke="#e6dac1" stroke-width="1.5" filter="url(#soft-shadow)"/>
<path d="M28 26 L352 24 L350 314 L26 316 Z" fill="none" stroke="#8a7b63" stroke-width="1.8" stroke-dasharray="6 3" opacity="0.45" filter="url(#paper-sketch)"/>`,

  `The same paper in HTML, taped down at one corner:
body { background:transparent; }
.notebook-container { background:#fdfaf2; border:2px dashed #9d754b; border-radius:16px; box-shadow:2px 6px 18px rgba(65,40,15,0.12), inset 0 0 30px rgba(245,230,205,0.45); }
.tape-strip { position:absolute; top:-6px; right:32px; width:86px; height:20px; background:rgba(230,205,160,0.65); transform:rotate(3deg); }
.glyph-box { background:#fbf5e6; border:1.5px dashed #b58e65; border-radius:8px; }`,

  `Hand drawn on canvas: every line and circle carries a little jitter, so nothing lands mechanically straight.
const midX = (x1 + x2) / 2 + (Math.random() - 0.5) * 1.5;
ctx.quadraticCurveTo(midX, midY, x2, y2);
const wobble = Math.sin(angle * 3 + cx) * 0.8 + (Math.random() * 0.8 - 0.4);`,
].join('\n\n');

export const ART_STYLE_REFERENCE_TEXT = `Sketchbook reference - how artifacts in this app have tended to look:

${STYLE_NOTES}

${FRAGMENTS}

Shown for finish and feel, not as material to reuse. The next artifact should be its own idea, with its own subject and layout, that simply happens to live in the same sketchbook. Drifting within this family is welcome; landing outside it is what looks wrong.`;
