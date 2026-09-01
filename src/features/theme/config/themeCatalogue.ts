// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/**
 * Describes an included color theme. Theme IDs are local application
 * identifiers, never storefront products: every current and future color
 * theme is free and available without an account or network connection.
 */
export interface ThemeCatalogueItem {
  /** Stable local identifier used for presets and persisted migrations. */
  themeId: ThemeId;
  /** User-facing display name. */
  displayName: string;
  /** Short description shown in the gallery. */
  description: string;
  /** Emoji or icon identifier used in the gallery card. */
  icon: string;
  /** HSL palette preview swatches (3-5 values) for the gallery UI. */
  previewColors: string[];
}

/** Stable local theme identifiers. */
export const THEME_IDS = {
  OCEAN_BLUE: 'theme_ocean_blue',
  SUNSET_GOLD: 'theme_sunset_gold',
  DARK_NEON: 'theme_dark_neon',
  SCHOLAR: 'theme_scholar',
  PURE_LIGHT: 'theme_pure_light',
  OBSIDIAN: 'theme_obsidian',
  FOREST: 'theme_forest',
  LAVENDER: 'theme_lavender',
  SPECTRUM: 'theme_spectrum',
  GRAPHITE: 'theme_graphite',
  ORIGINAL: 'theme_original',
} as const;

export type ThemeId = (typeof THEME_IDS)[keyof typeof THEME_IDS];

/**
 * The app-wide default can be rotated later by changing one local theme ID.
 */
export const DEFAULT_THEME_ID: ThemeId = THEME_IDS.GRAPHITE;

/** Catalogue of free themes with display metadata. */
export const THEME_CATALOGUE: ThemeCatalogueItem[] = [
  {
    themeId: THEME_IDS.OCEAN_BLUE,
    displayName: 'Ocean Blue',
    description: 'Calm blues and aqua tones inspired by deep water.',
    icon: '🌊',
    previewColors: ['210 70% 45%', '195 80% 55%', '200 60% 30%', '190 50% 85%'],
  },
  {
    themeId: THEME_IDS.SUNSET_GOLD,
    displayName: 'Sunset Gold',
    description: 'Warm golds, amber, and coral hues of the evening sky.',
    icon: '🌅',
    previewColors: ['38 90% 55%', '25 85% 60%', '15 80% 50%', '45 70% 90%'],
  },
  {
    themeId: THEME_IDS.DARK_NEON,
    displayName: 'Dark Neon',
    description: 'High-contrast dark background with vibrant neon accents.',
    icon: '🌆',
    previewColors: ['0 0% 8%', '280 100% 65%', '165 100% 55%', '60 100% 60%'],
  },
  {
    themeId: THEME_IDS.SCHOLAR,
    displayName: 'Scholar',
    description: 'Warm parchment with deep indigo ink and sky-blue accents.',
    icon: '📜',
    previewColors: ['39 37% 94%', '248 41% 27%', '199 84% 58%', '261 75% 63%'],
  },
  {
    themeId: THEME_IDS.PURE_LIGHT,
    displayName: 'Pure Light',
    description: 'Minimal white and black with crisp electric-blue highlights for a clean studio feel.',
    icon: '☀️',
    previewColors: ['0 0% 98%', '0 0% 12%', '215 100% 50%', '120 60% 40%'],
  },
  {
    themeId: THEME_IDS.OBSIDIAN,
    displayName: 'Obsidian',
    description: 'Velvety charcoal with warm ivory contrast and layered grayscale depth.',
    icon: '🌑',
    previewColors: ['220 8% 12%', '40 8% 85%', '220 8% 26%', '40 8% 90%'],
  },
  {
    themeId: THEME_IDS.FOREST,
    displayName: 'Forest',
    description: 'Warm sage paper with deep woodland green ink and moss accents.',
    icon: '🌿',
    previewColors: ['80 15% 95%', '90 25% 20%', '140 55% 38%', '160 45% 45%'],
  },
  {
    themeId: THEME_IDS.LAVENDER,
    displayName: 'Lavender',
    description: 'Soft lavender-white paper with deep plum ink and violet accents.',
    icon: '💜',
    previewColors: ['267 35% 97%', '262 52% 24%', '270 58% 56%', '255 48% 66%'],
  },
  {
    themeId: THEME_IDS.SPECTRUM,
    displayName: 'Spectrum',
    description: 'The four iconic primaries - blue, red, yellow, and green - each given a dedicated role across the entire UI.',
    icon: '🌈',
    previewColors: ['214 89% 55%', '5 70% 52%', '45 96% 48%', '153 76% 38%'],
  },
  {
    themeId: THEME_IDS.GRAPHITE,
    displayName: 'Graphite',
    description: 'Pure black on white - every element expressed in graphite grays, like a masterful pencil sketch with no color to distract.',
    icon: '✏️',
    previewColors: ['40 8% 97%', '220 8% 14%', '220 6% 28%', '220 5% 60%'],
  },
  {
    themeId: THEME_IDS.ORIGINAL,
    displayName: 'Original',
    description: 'The long-running Maestro classic - bright paper, blue ink, watercolor accents, and the familiar notebook feel.',
    icon: '📘',
    previewColors: ['210 20% 97%', '220 30% 20%', '220 70% 55%', '190 60% 55%'],
  },
];

/**
 * The active default stays available as the reset state, so the gallery keeps
 * the previous UI shape by listing the other included themes.
 */
export const THEME_GALLERY_ITEMS: ThemeCatalogueItem[] = THEME_CATALOGUE.filter(
  theme => theme.themeId !== DEFAULT_THEME_ID,
);
