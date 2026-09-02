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
  // Clear variants: the same palette with its fills dropped so the sketch
  // outlines carry the design. Separate ids because the solid ones are
  // persisted in user settings and must keep meaning what they meant.
  OCEAN_BLUE_CLEAR: 'theme_ocean_blue_clear',
  SUNSET_GOLD_CLEAR: 'theme_sunset_gold_clear',
  DARK_NEON_CLEAR: 'theme_dark_neon_clear',
  SCHOLAR_CLEAR: 'theme_scholar_clear',
  PURE_LIGHT_CLEAR: 'theme_pure_light_clear',
  OBSIDIAN_CLEAR: 'theme_obsidian_clear',
  FOREST_CLEAR: 'theme_forest_clear',
  LAVENDER_CLEAR: 'theme_lavender_clear',
  SPECTRUM_CLEAR: 'theme_spectrum_clear',
  GRAPHITE_CLEAR: 'theme_graphite_clear',
  ORIGINAL_CLEAR: 'theme_original_clear',
} as const;

export type ThemeId = (typeof THEME_IDS)[keyof typeof THEME_IDS];

/**
 * The app-wide default can be rotated later by changing one local theme ID.
 */
export const DEFAULT_THEME_ID: ThemeId = THEME_IDS.GRAPHITE_CLEAR;

/** Catalogue of free themes with display metadata. */
export const THEME_CATALOGUE: ThemeCatalogueItem[] = [
  {
    themeId: THEME_IDS.OCEAN_BLUE,
    displayName: 'Ocean Blue Solid',
    description: 'Calm blues and aqua tones inspired by deep water.',
    icon: '🌊',
    previewColors: ['210 70% 45%', '195 80% 55%', '200 60% 30%', '190 50% 85%'],
  },
  {
    themeId: THEME_IDS.SUNSET_GOLD,
    displayName: 'Sunset Gold Solid',
    description: 'Warm golds, amber, and coral hues of the evening sky.',
    icon: '🌅',
    previewColors: ['38 90% 55%', '25 85% 60%', '15 80% 50%', '45 70% 90%'],
  },
  {
    themeId: THEME_IDS.DARK_NEON,
    displayName: 'Dark Neon Solid',
    description: 'High-contrast dark background with vibrant neon accents.',
    icon: '🌆',
    previewColors: ['0 0% 8%', '280 100% 65%', '165 100% 55%', '60 100% 60%'],
  },
  {
    themeId: THEME_IDS.SCHOLAR,
    displayName: 'Scholar Solid',
    description: 'Warm parchment with deep indigo ink and sky-blue accents.',
    icon: '📜',
    previewColors: ['39 37% 94%', '248 41% 27%', '199 84% 58%', '261 75% 63%'],
  },
  {
    themeId: THEME_IDS.PURE_LIGHT,
    displayName: 'Pure Light Solid',
    description: 'Minimal white and black with crisp electric-blue highlights for a clean studio feel.',
    icon: '☀️',
    previewColors: ['0 0% 98%', '0 0% 12%', '215 100% 50%', '120 60% 40%'],
  },
  {
    themeId: THEME_IDS.OBSIDIAN,
    displayName: 'Obsidian Solid',
    description: 'Velvety charcoal with warm ivory contrast and layered grayscale depth.',
    icon: '🌑',
    previewColors: ['220 8% 12%', '40 8% 85%', '220 8% 26%', '40 8% 90%'],
  },
  {
    themeId: THEME_IDS.FOREST,
    displayName: 'Forest Solid',
    description: 'Warm sage paper with deep woodland green ink and moss accents.',
    icon: '🌿',
    previewColors: ['80 15% 95%', '90 25% 20%', '140 55% 38%', '160 45% 45%'],
  },
  {
    themeId: THEME_IDS.LAVENDER,
    displayName: 'Lavender Solid',
    description: 'Soft lavender-white paper with deep plum ink and violet accents.',
    icon: '💜',
    previewColors: ['267 35% 97%', '262 52% 24%', '270 58% 56%', '255 48% 66%'],
  },
  {
    themeId: THEME_IDS.SPECTRUM,
    displayName: 'Spectrum Solid',
    description: 'The four iconic primaries - blue, red, yellow, and green - each given a dedicated role across the entire UI.',
    icon: '🌈',
    previewColors: ['214 89% 55%', '5 70% 52%', '45 96% 48%', '153 76% 38%'],
  },
  {
    themeId: THEME_IDS.GRAPHITE,
    displayName: 'Graphite Solid',
    description: 'Pure black on white - every element expressed in graphite grays, like a masterful pencil sketch with no color to distract.',
    icon: '✏️',
    previewColors: ['40 8% 97%', '220 8% 14%', '220 6% 28%', '220 5% 60%'],
  },
  {
    themeId: THEME_IDS.ORIGINAL,
    displayName: 'Original Solid',
    description: 'The long-running Maestro classic - bright paper, blue ink, watercolor accents, and the familiar notebook feel.',
    icon: '📘',
    previewColors: ['210 20% 97%', '220 30% 20%', '220 70% 55%', '190 60% 55%'],
  },
];

/**
 * The Clear variants, generated from the solid catalogue above so a new theme
 * only has to be written once. They lead the list: Clear is the default look,
 * and the solid originals sit behind them as the alternative.
 */
const CLEAR_SUFFIX = '_clear';

const CLEAR_CATALOGUE: ThemeCatalogueItem[] = THEME_CATALOGUE.map(theme => ({
  ...theme,
  themeId: `${theme.themeId}${CLEAR_SUFFIX}` as ThemeId,
  displayName: theme.displayName.replace(/ Solid$/, ''),
  description: `${theme.description.replace(/\.$/, '')} - drawn as outlines, with no fills.`,
}));

/** Every included theme, Clear variants first. */
export const ALL_THEMES: ThemeCatalogueItem[] = [...CLEAR_CATALOGUE, ...THEME_CATALOGUE];

export const isClearThemeId = (themeId: string): boolean => themeId.endsWith(CLEAR_SUFFIX);

/** The solid theme a Clear variant is derived from. */
export const solidThemeIdOf = (themeId: ThemeId): ThemeId =>
  themeId.slice(0, -CLEAR_SUFFIX.length) as ThemeId;
