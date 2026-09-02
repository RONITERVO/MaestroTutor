// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import {
  ALL_THEMES,
  DEFAULT_THEME_ID,
  THEME_CATALOGUE,
  THEME_IDS,
  isClearThemeId,
  solidThemeIdOf,
} from './themeCatalogue';
import { getThemePreset, THEME_PRESETS_BY_ID } from './themePresets';

describe('free theme catalogue', () => {
  it('offers a Clear and a solid variant of every theme', () => {
    expect(ALL_THEMES.map(theme => theme.themeId).sort()).toEqual(
      Object.values(THEME_IDS).sort(),
    );
    expect(ALL_THEMES).toHaveLength(THEME_CATALOGUE.length * 2);
  });

  it('maps every catalogue entry to a locally available preset', () => {
    for (const theme of ALL_THEMES) {
      expect(getThemePreset(theme.themeId), theme.themeId).not.toBeNull();
    }
  });

  it('lists the Clear variants first, since Clear is the default look', () => {
    const firstSolid = ALL_THEMES.findIndex(theme => !isClearThemeId(theme.themeId));
    const lastClear = ALL_THEMES.map(t => isClearThemeId(t.themeId)).lastIndexOf(true);
    expect(lastClear).toBeLessThan(firstSolid);
  });

  it('defaults to Clear Graphite, with the solid original still selectable', () => {
    expect(DEFAULT_THEME_ID).toBe(THEME_IDS.GRAPHITE_CLEAR);
    expect(isClearThemeId(DEFAULT_THEME_ID)).toBe(true);
    expect(solidThemeIdOf(DEFAULT_THEME_ID)).toBe(THEME_IDS.GRAPHITE);
    expect(getThemePreset(THEME_IDS.GRAPHITE)).not.toBeNull();
  });

  it('gives every preset a complete palette', () => {
    // Applying a preset clears every override first, so a gap would fall
    // through to the Clear default and come back transparent.
    const tokenCount = Object.keys(THEME_PRESETS_BY_ID[THEME_IDS.GRAPHITE].colors).length;
    for (const [themeId, preset] of Object.entries(THEME_PRESETS_BY_ID)) {
      expect(Object.keys(preset.colors).length, themeId).toBe(tokenCount);
    }
  });

  it('rejects inherited object keys as unknown theme IDs', () => {
    expect(getThemePreset('__proto__')).toBeNull();
    expect(getThemePreset('constructor')).toBeNull();
  });
});
