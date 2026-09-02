// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { PresetTheme } from './presetThemes';
import { THEME_CATALOGUE, THEME_IDS, type ThemeId } from './themeCatalogue';
import { makeTransparentPalette } from './transparentTheme';
import {
  OCEAN_BLUE_COLORS,
  SUNSET_GOLD_COLORS,
  DARK_NEON_COLORS,
  SCHOLAR_COLORS,
  PURE_LIGHT_COLORS,
  OBSIDIAN_COLORS,
  FOREST_COLORS,
  LAVENDER_COLORS,
  SPECTRUM_COLORS,
  GRAPHITE_COLORS,
  ORIGINAL_COLORS,
} from './themeColors';

const SOLID_PALETTES: Record<string, Record<string, string>> = {
  [THEME_IDS.OCEAN_BLUE]: OCEAN_BLUE_COLORS,
  [THEME_IDS.SUNSET_GOLD]: SUNSET_GOLD_COLORS,
  [THEME_IDS.DARK_NEON]: DARK_NEON_COLORS,
  [THEME_IDS.SCHOLAR]: SCHOLAR_COLORS,
  [THEME_IDS.PURE_LIGHT]: PURE_LIGHT_COLORS,
  [THEME_IDS.OBSIDIAN]: OBSIDIAN_COLORS,
  [THEME_IDS.FOREST]: FOREST_COLORS,
  [THEME_IDS.LAVENDER]: LAVENDER_COLORS,
  [THEME_IDS.SPECTRUM]: SPECTRUM_COLORS,
  [THEME_IDS.GRAPHITE]: GRAPHITE_COLORS,
  [THEME_IDS.ORIGINAL]: ORIGINAL_COLORS,
};

/**
 * Several palettes only list the tokens they actually change and lean on the
 * app default for the rest. That worked while the default was a solid theme;
 * with a Clear default those gaps would come back transparent, so every preset
 * is completed from the solid Graphite palette before it is published. A preset
 * is now always a full snapshot, which is also what makes it safe to apply one
 * by clearing every override first.
 */
const complete = (colors: Record<string, string>): Record<string, string> => ({
  ...GRAPHITE_COLORS,
  ...colors,
});

const buildPresets = (): Record<ThemeId, PresetTheme> => {
  const presets = {} as Record<ThemeId, PresetTheme>;

  for (const theme of THEME_CATALOGUE) {
    const solid = complete(SOLID_PALETTES[theme.themeId]);
    presets[theme.themeId] = {
      name: theme.displayName,
      description: 'Included theme',
      colors: solid,
    };
    presets[`${theme.themeId}_clear` as ThemeId] = {
      name: theme.displayName.replace(/ Solid$/, ''),
      description: 'Included theme',
      colors: makeTransparentPalette(solid),
    };
  }

  return presets;
};

export const THEME_PRESETS_BY_ID: Record<ThemeId, PresetTheme> = buildPresets();

export const getThemePreset = (themeId: string): PresetTheme | null => {
  if (!Object.prototype.hasOwnProperty.call(THEME_PRESETS_BY_ID, themeId)) {
    return null;
  }
  return THEME_PRESETS_BY_ID[themeId as ThemeId];
};
