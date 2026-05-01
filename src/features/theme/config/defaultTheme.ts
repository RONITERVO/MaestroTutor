// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { getPurchasableThemePreset } from './purchasableThemePresets';
import { DEFAULT_THEME_PRODUCT_ID, THEME_PRODUCTS } from './themeProducts';

const defaultThemePreset = getPurchasableThemePreset(DEFAULT_THEME_PRODUCT_ID);
if (!defaultThemePreset) {
  throw new Error(`Missing preset for default theme product: ${DEFAULT_THEME_PRODUCT_ID}`);
}

const defaultThemeProduct = THEME_PRODUCTS.find(
  product => product.productId === DEFAULT_THEME_PRODUCT_ID,
);
if (!defaultThemeProduct) {
  throw new Error(`Missing store metadata for default theme product: ${DEFAULT_THEME_PRODUCT_ID}`);
}

export const DEFAULT_THEME_PRESET = defaultThemePreset;
export const DEFAULT_THEME_COLORS = defaultThemePreset.colors;
export const DEFAULT_THEME_PRODUCT = defaultThemeProduct;
