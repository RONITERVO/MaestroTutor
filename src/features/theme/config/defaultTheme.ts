// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { getThemePreset } from './themePresets';
import { DEFAULT_THEME_ID, THEME_CATALOGUE } from './themeCatalogue';

const defaultThemePreset = getThemePreset(DEFAULT_THEME_ID);
if (!defaultThemePreset) {
  throw new Error(`Missing preset for default theme: ${DEFAULT_THEME_ID}`);
}

const defaultTheme = THEME_CATALOGUE.find(
  theme => theme.themeId === DEFAULT_THEME_ID,
);
if (!defaultTheme) {
  throw new Error(`Missing catalogue metadata for default theme: ${DEFAULT_THEME_ID}`);
}

export const DEFAULT_THEME_PRESET = defaultThemePreset;
export const DEFAULT_THEME_COLORS = defaultThemePreset.colors;
export const DEFAULT_THEME = defaultTheme;
