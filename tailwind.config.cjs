// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
const { readFileSync } = require('node:fs');

// CSS variables are the theme source of truth. Deriving Tailwind colors from
// them prevents the runtime CDN configuration and the actual design tokens
// from drifting into two separate lists.
const css = readFileSync('./src/app/index.css', 'utf8');
const variableNames = [...css.matchAll(/--([a-z][a-z0-9-]+)\s*:/g)]
  .map(match => match[1])
  .filter(name => name !== 'radius');
const colors = Object.fromEntries(
  [...new Set(variableNames)].map(name => [name, `hsl(var(--${name}))`]),
);

/** @type {import('tailwindcss').Config} */
module.exports = {
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
};
