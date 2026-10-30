// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { ComponentProps } from 'react';
import paletteUrl from './assets/palette.svg';
import './AnimatedIcons.css';

type IconProps = ComponentProps<'span'>;
const stroke = { fill: 'none', stroke: 'currentColor', strokeWidth: 2.25, strokeLinecap: 'round', strokeLinejoin: 'round' } as const;

// Animate CSS boxes around static SVG layers. Transforming inner SVG geometry
// makes Chromium lay out the surrounding book every frame. Box transforms keep
// currentColor (including theme/hover alpha) and the original SVG draw order.
export const IconGlobe = ({ className = '', ...props }: IconProps) => (
  <span {...props} className={`maestro-animated-icon ${className}`}>
    <span className="maestro-globe-bounce">
      <svg className="maestro-icon-layer" viewBox="0 0 24 24" {...stroke}>
        <circle cx="12" cy="12" r="9" />
        <line x1="3" y1="12" x2="21" y2="12" />
        <line x1="12" y1="3" x2="12" y2="21" />
      </svg>
      <svg className="maestro-icon-layer maestro-globe-spin" viewBox="0 0 24 24" {...stroke}>
        <ellipse cx="12" cy="12" rx="5" ry="9" />
      </svg>
    </span>
  </span>
);

export const IconTarget = ({ className = '', ...props }: IconProps) => (
  <span {...props} className={`maestro-animated-icon ${className}`}>
    <svg className="maestro-icon-layer maestro-target-pulse" viewBox="0 0 24 24" fill="none" stroke="currentColor">
      <circle cx="12" cy="12" r="5" fill="currentColor" />
    </svg>
    <svg className="maestro-icon-layer" viewBox="0 0 24 24" fill="none" stroke="currentColor">
      <circle cx="12" cy="12" r="2.5" fill="currentColor" />
    </svg>
    <svg className="maestro-icon-layer maestro-target-lock" viewBox="0 0 24 24" {...stroke}>
      <circle cx="12" cy="12" r="7" />
      <path d="M 12 2 L 12 5" />
      <path d="M 12 19 L 12 22" />
      <path d="M 2 12 L 5 12" />
      <path d="M 19 12 L 22 12" />
    </svg>
  </span>
);

// This icon uses fixed paint colours; its self-contained image retains all four
// original animations without invalidating the surrounding document's layout.
export const IconPalette = ({ className = '', ...props }: ComponentProps<'img'>) => (
  <img {...props} className={`maestro-palette-icon ${className}`} src={paletteUrl} alt="Customize UI Colors" draggable={false} />
);
