// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { Config } from 'tailwindcss';
import { COLOR_GROUPS } from './src/features/theme/config/colorRegistry';
import { tailwindColorValue } from './src/features/theme/utils/tokenValue';

// COLOR_GROUPS is the single source of truth for the theme tokens. The Vite
// colorTokensPlugin emits the matching `--token: value` declarations into
// :root, so deriving the Tailwind palette from the same registry keeps the
// utilities and the custom properties from drifting apart.
//
// Note: the tokens cannot be read back out of index.css, because that file only
// holds the `/* __COLOR_TOKENS__ */` marker until Vite expands it — and that
// expansion happens after PostCSS/Tailwind has already run.
//
// `tailwindColorValue` carries the `<alpha-value>` placeholder, which is what
// lets a user's per-token opacity and a developer's `/50` modifier multiply
// instead of overwriting each other. See utils/tokenValue.ts.
const colors = Object.fromEntries(
  COLOR_GROUPS.flatMap(group =>
    group.colors.map(color => [color.cssVar, tailwindColorValue(color.cssVar)]),
  ),
);

export default {
  content: ['./index.html', './delete-account.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      fontFamily: {
        sketch: ['"Caveat"', 'cursive'],
        hand: ['"Patrick Hand"', 'cursive'],
        architect: ['"Architects Daughter"', 'cursive'],
      },
      colors,
      borderRadius: {
        sketchy: '255px 15px 225px 15px / 15px 225px 15px 255px',
      },
      keyframes: {
        wobble: {
          '0%, 100%': { transform: 'rotate(-0.5deg)' },
          '25%': { transform: 'rotate(0.3deg)' },
          '50%': { transform: 'rotate(-0.2deg)' },
          '75%': { transform: 'rotate(0.4deg)' },
        },
        float: {
          '0%, 100%': { transform: 'translateY(0) rotate(0deg)' },
          '33%': { transform: 'translateY(-3px) rotate(0.5deg)' },
          '66%': { transform: 'translateY(1px) rotate(-0.3deg)' },
        },
        'pencil-scribble': { '0%': { width: '0%' }, '100%': { width: '100%' } },
        'fade-up': {
          '0%': { opacity: '0', transform: 'translateY(20px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        'sketch-in': {
          '0%': { opacity: '0', transform: 'scale(0.9) rotate(-1deg)' },
          '100%': { opacity: '1', transform: 'scale(1) rotate(0deg)' },
        },
      },
      animation: {
        wobble: 'wobble 3s ease-in-out infinite',
        float: 'float 4s ease-in-out infinite',
        'pencil-scribble': 'pencil-scribble 1.5s ease-in-out',
        'fade-up': 'fade-up 0.5s ease-out',
        'sketch-in': 'sketch-in 0.4s ease-out',
      },
    },
  },
  plugins: [],
} satisfies Config;
