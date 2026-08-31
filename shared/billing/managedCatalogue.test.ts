// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { appConfig, getJsonBodyLimitBytes, parseCreditPacks } from '../../functions/src/config';

describe('managed credit pack configuration', () => {
  it('preserves a valid catalogue in insertion order', () => {
    expect(parseCreditPacks('small:1000:299:play_small,large:6000:999')).toEqual([
      { id: 'small', credits: 1000, priceCents: 299, playProductId: 'play_small' },
      { id: 'large', credits: 6000, priceCents: 999 },
    ]);
  });

  it.each([
    'fractional-credits:1.5:299',
    'fractional-price:1000:2.99',
    `unsafe-credits:${Number.MAX_SAFE_INTEGER + 1}:299`,
    `unsafe-price:1000:${Number.MAX_SAFE_INTEGER + 1}`,
    'zero:0:299',
    'missing-fields:1000',
  ])('rejects an invalid entry instead of silently dropping or rounding it: %s', (value) => {
    expect(() => parseCreditPacks(value)).toThrow();
  });

  it('rejects duplicate pack ids', () => {
    expect(() => parseCreditPacks('same:1000:299,same:2000:499')).toThrow(/Duplicate credit pack id/);
  });

  it('rejects duplicate non-empty Play product ids', () => {
    expect(() => parseCreditPacks('a:1000:299:play_pack,b:2000:499:play_pack'))
      .toThrow(/Duplicate Google Play product id/);
  });

  it('rejects identifiers that collide across pack and Play namespaces', () => {
    expect(() => parseCreditPacks('a:1000:299:shared,b:2000:499:a'))
      .toThrow(/Ambiguous credit pack\/store id/);
    expect(() => parseCreditPacks('a:1000:299:shared,shared:2000:499:play_b'))
      .toThrow(/Ambiguous credit pack\/store id/);
  });

  it('keeps base64 uploads and their JSON envelope below the v2 request ceiling', () => {
    expect(appConfig.managedMaxUploadBytes).toBeLessThan(32 * 1024 * 1024);
    expect(getJsonBodyLimitBytes()).toBeLessThan(32 * 1024 * 1024);
  });
});
