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
];
