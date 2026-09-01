// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { makePlayAccountBinding, playPurchaseBelongsToAccount } from './playAccountBinding';

describe('Google Play account binding', () => {
  it('uses the documented 64-character SHA-256 identifier', () => {
    expect(makePlayAccountBinding('firebase-user-1')).toBe(
      '7b97a6ec8bda3b960e73d80f60f0b8bc76ace41bce6681db719a701c8a55d2dc',
    );
  });

  it('accepts only the signed-in Firebase account', () => {
    const binding = makePlayAccountBinding('firebase-user-1');
    expect(playPurchaseBelongsToAccount(binding, 'firebase-user-1')).toBe(true);
    expect(playPurchaseBelongsToAccount(binding, 'firebase-user-2')).toBe(false);
    expect(playPurchaseBelongsToAccount(null, 'firebase-user-1')).toBe(false);
  });
});
