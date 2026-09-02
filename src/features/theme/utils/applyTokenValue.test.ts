// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom

import { describe, it, expect } from 'vitest';
import { applyTokenValue, clearTokenValue } from './applyTokenValue';
import { hslStringToHexAlpha, hexAlphaToHslString } from './colorConversion';
import { parseTokenValue } from './tokenValue';

describe('applying a token to the DOM', () => {
  const element = () => document.createElement('div');

  it('splits a value across the channel and alpha properties', () => {
    const el = element();
    applyTokenValue(el, 'page-bg', '210 20% 97% / 0.5');

    expect(el.style.getPropertyValue('--page-bg')).toBe('210 20% 97%');
    expect(el.style.getPropertyValue('--page-bg-alpha')).toBe('0.5');
  });

  it('writes an explicit 1 for an opaque token', () => {
    // Falling back instead would let a translucent default theme show through
    // a token the user deliberately set back to opaque.
    const el = element();
    applyTokenValue(el, 'page-bg', '210 20% 97%');

    expect(el.style.getPropertyValue('--page-bg-alpha')).toBe('1');
  });

  it('clears both properties on reset', () => {
    const el = element();
    applyTokenValue(el, 'page-bg', '210 20% 97% / 0.25');
    clearTokenValue(el, 'page-bg');

    expect(el.style.getPropertyValue('--page-bg')).toBe('');
    expect(el.style.getPropertyValue('--page-bg-alpha')).toBe('');
  });
});

describe('picker hex conversion', () => {
  it('round-trips a translucent colour through the picker format', () => {
    const hex = hslStringToHexAlpha('210 20% 97% / 0.5');
    expect(hex).toMatch(/^#[0-9a-f]{8}$/i);

    const { alpha } = parseTokenValue(hexAlphaToHslString(hex));
    expect(alpha).toBeCloseTo(0.5, 2);
  });

  it('keeps an opaque colour in the familiar 6-digit form', () => {
    expect(hslStringToHexAlpha('210 20% 97%')).toMatch(/^#[0-9a-f]{6}$/i);
    expect(hexAlphaToHslString('#f4f6f8')).not.toContain('/');
  });

  it('reads back a 6-digit hex from the picker as opaque', () => {
    expect(parseTokenValue(hexAlphaToHslString('#000000')).alpha).toBe(1);
  });
});
