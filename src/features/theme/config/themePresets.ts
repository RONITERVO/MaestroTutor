// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { PresetTheme } from './presetThemes';
import { THEME_IDS, type ThemeId } from './themeCatalogue';
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

export const THEME_PRESETS_BY_ID: Record<ThemeId, PresetTheme> = {
  [THEME_IDS.OCEAN_BLUE]: {
    name: 'Ocean Blue',
    description: 'Included theme',
    colors: OCEAN_BLUE_COLORS,
  },
  [THEME_IDS.SUNSET_GOLD]: {
    name: 'Sunset Gold',
    description: 'Included theme',
    colors: SUNSET_GOLD_COLORS,
  },
  [THEME_IDS.DARK_NEON]: {
    name: 'Dark Neon',
    description: 'Included theme',
    colors: DARK_NEON_COLORS,
  },
  [THEME_IDS.SCHOLAR]: {
    name: 'Scholar',
    description: 'Included theme',
    colors: SCHOLAR_COLORS,
  },
  [THEME_IDS.PURE_LIGHT]: {
    name: 'Pure Light',
    description: 'Included theme',
    colors: PURE_LIGHT_COLORS,
  },
  [THEME_IDS.OBSIDIAN]: {
    name: 'Obsidian',
    description: 'Included theme',
    colors: OBSIDIAN_COLORS,
  },
  [THEME_IDS.FOREST]: {
    name: 'Forest',
    description: 'Included theme',
    colors: FOREST_COLORS,
  },
  [THEME_IDS.LAVENDER]: {
    name: 'Lavender',
    description: 'Included theme',
    colors: LAVENDER_COLORS,
  },
  [THEME_IDS.SPECTRUM]: {
    name: 'Spectrum',
    description: 'Included theme',
    colors: SPECTRUM_COLORS,
  },
  [THEME_IDS.GRAPHITE]: {
    name: 'Graphite',
    description: 'Included theme',
    colors: GRAPHITE_COLORS,
  },
  [THEME_IDS.ORIGINAL]: {
    name: 'Original',
    description: 'Included theme',
    colors: ORIGINAL_COLORS,
  },
};

export const getThemePreset = (themeId: string): PresetTheme | null =>
  THEME_PRESETS_BY_ID[themeId as ThemeId] ?? null;
