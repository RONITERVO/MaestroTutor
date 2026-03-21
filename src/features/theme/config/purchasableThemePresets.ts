// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { PresetTheme } from './presetThemes';
import { THEME_PRODUCT_IDS, type ThemeProductId } from './themeProducts';
import {
  OCEAN_BLUE_COLORS,
  SUNSET_GOLD_COLORS,
  DARK_NEON_COLORS,
  SCHOLAR_COLORS,
} from './themeColors';

export const PURCHASABLE_THEME_PRESETS: Record<ThemeProductId, PresetTheme> = {
  [THEME_PRODUCT_IDS.OCEAN_BLUE]: {
    name: 'Ocean Blue',
    description: 'Premium theme',
    colors: OCEAN_BLUE_COLORS,
  },
  [THEME_PRODUCT_IDS.SUNSET_GOLD]: {
    name: 'Sunset Gold',
    description: 'Premium theme',
    colors: SUNSET_GOLD_COLORS,
  },
  [THEME_PRODUCT_IDS.DARK_NEON]: {
    name: 'Dark Neon',
    description: 'Premium theme',
    colors: DARK_NEON_COLORS,
  },
  [THEME_PRODUCT_IDS.SCHOLAR]: {
    name: 'Scholar',
    description: 'Premium theme',
    colors: SCHOLAR_COLORS,
  },
};

export const getPurchasableThemePreset = (productId: string): PresetTheme | null =>
  PURCHASABLE_THEME_PRESETS[productId as ThemeProductId] ?? null;
