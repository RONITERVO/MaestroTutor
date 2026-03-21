// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/**
 * Describes a purchasable color theme product.
 * Product IDs must match those configured in Google Play Console
 * under Monetize → Products → In-app products.
 */
export interface ThemeProduct {
  /** Google Play product ID (e.g. "theme_ocean_blue"). */
  productId: string;
  /** User-facing display name. */
  displayName: string;
  /** Short description shown in the store. */
  description: string;
  /** Emoji or icon identifier used in the store card. */
  icon: string;
  /** HSL palette preview swatches (3–5 values) for the store UI. */
  previewColors: string[];
}

/** Theme product IDs — must match Google Play Console. */
export const THEME_PRODUCT_IDS = {
  OCEAN_BLUE: 'theme_ocean_blue',
  SUNSET_GOLD: 'theme_sunset_gold',
  DARK_NEON: 'theme_dark_neon',
  SCHOLAR: 'theme_scholar',
  PURE_LIGHT: 'theme_pure_light',
  OBSIDIAN: 'theme_obsidian',
  FOREST: 'theme_forest',
  LAVENDER: 'theme_lavender',
  SPECTRUM: 'theme_spectrum',
} as const;

export type ThemeProductId = (typeof THEME_PRODUCT_IDS)[keyof typeof THEME_PRODUCT_IDS];

/** Catalogue of purchasable themes with display metadata. */
export const THEME_PRODUCTS: ThemeProduct[] = [
  {
    productId: THEME_PRODUCT_IDS.OCEAN_BLUE,
    displayName: 'Ocean Blue',
    description: 'Calm blues and aqua tones inspired by deep water.',
    icon: '🌊',
    previewColors: ['210 70% 45%', '195 80% 55%', '200 60% 30%', '190 50% 85%'],
  },
  {
    productId: THEME_PRODUCT_IDS.SUNSET_GOLD,
    displayName: 'Sunset Gold',
    description: 'Warm golds, amber, and coral hues of the evening sky.',
    icon: '🌅',
    previewColors: ['38 90% 55%', '25 85% 60%', '15 80% 50%', '45 70% 90%'],
  },
  {
    productId: THEME_PRODUCT_IDS.DARK_NEON,
    displayName: 'Dark Neon',
    description: 'High-contrast dark background with vibrant neon accents.',
    icon: '🌆',
    previewColors: ['0 0% 8%', '280 100% 65%', '165 100% 55%', '60 100% 60%'],
  },
  {
    productId: THEME_PRODUCT_IDS.SCHOLAR,
    displayName: 'Scholar',
    description: 'Warm parchment with deep indigo ink and sky-blue accents.',
    icon: '📜',
    previewColors: ['39 37% 94%', '248 41% 27%', '199 84% 58%', '261 75% 63%'],
  },
  {
    productId: THEME_PRODUCT_IDS.PURE_LIGHT,
    displayName: 'Pure Light',
    description: 'Crisp white with deep navy and premium blue — elegant and timeless.',
    icon: '☀️',
    previewColors: ['210 25% 98%', '222 47% 20%', '214 87% 51%', '199 89% 40%'],
  },
  {
    productId: THEME_PRODUCT_IDS.OBSIDIAN,
    displayName: 'Obsidian',
    description: 'Deep navy-black with refined blue and teal — sophisticated darkness.',
    icon: '🌑',
    previewColors: ['222 38% 8%', '214 80% 46%', '174 68% 44%', '222 28% 17%'],
  },
  {
    productId: THEME_PRODUCT_IDS.FOREST,
    displayName: 'Forest',
    description: 'Warm sage paper with deep woodland green ink and moss accents.',
    icon: '🌿',
    previewColors: ['80 15% 95%', '90 25% 20%', '140 55% 38%', '160 45% 45%'],
  },
  {
    productId: THEME_PRODUCT_IDS.LAVENDER,
    displayName: 'Lavender',
    description: 'Soft lavender-white paper with deep plum ink and violet accents.',
    icon: '💜',
    previewColors: ['267 35% 97%', '262 52% 24%', '270 58% 56%', '255 48% 66%'],
  },
  {
    productId: THEME_PRODUCT_IDS.SPECTRUM,
    displayName: 'Spectrum',
    description: 'The four iconic primaries — blue, red, yellow, and green — each given a dedicated role across the entire UI.',
    icon: '🌈',
    previewColors: ['214 89% 55%', '5 70% 52%', '45 96% 48%', '153 76% 38%'],
  },
];
