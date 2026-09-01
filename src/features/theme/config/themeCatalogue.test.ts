// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import {
  DEFAULT_THEME_ID,
  THEME_CATALOGUE,
  THEME_GALLERY_ITEMS,
  THEME_IDS,
} from './themeCatalogue';
import { getThemePreset } from './themePresets';

describe('free theme catalogue', () => {
  it('maps every catalogue entry to a locally available preset', () => {
    expect(THEME_CATALOGUE.length).toBeGreaterThan(0);
    expect(THEME_CATALOGUE.map(theme => theme.themeId).sort()).toEqual(
      Object.values(THEME_IDS).sort(),
    );
    for (const theme of THEME_CATALOGUE) {
      expect(getThemePreset(theme.themeId)).not.toBeNull();
    }
  });

  it('keeps the reset default out of the gallery while exposing every alternative', () => {
    expect(THEME_GALLERY_ITEMS).toEqual(
      THEME_CATALOGUE.filter(theme => theme.themeId !== DEFAULT_THEME_ID),
    );
  });

  it('rejects inherited object keys as unknown theme IDs', () => {
    expect(getThemePreset('__proto__')).toBeNull();
    expect(getThemePreset('constructor')).toBeNull();
  });
});
