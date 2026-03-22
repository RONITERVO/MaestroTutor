// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

package com.ronitervo.maestrotutor;

import java.util.List;

/**
 * Defines all purchasable color theme product IDs.
 * These must match the product IDs configured in Google Play Console
 * under Monetize → Products → In-app products.
 */
public final class ThemeProducts {

    private ThemeProducts() {
    }

    public static final String THEME_OCEAN_BLUE = "theme_ocean_blue";
    public static final String THEME_SUNSET_GOLD = "theme_sunset_gold";
    public static final String THEME_DARK_NEON = "theme_dark_neon";

    // Add your 7 new themes here:
    public static final String THEME_SCHOLAR = "theme_scholar";
    public static final String THEME_PURE_LIGHT = "theme_pure_light";
    public static final String THEME_OBSIDIAN = "theme_obsidian";
    public static final String THEME_FOREST = "theme_forest";
    public static final String THEME_LAVENDER = "theme_lavender";
    public static final String THEME_SPECTRUM = "theme_spectrum";
    public static final String THEME_GRAPHITE = "theme_graphite";

    public static final List<String> ALL_PRODUCT_IDS = List.of(
            THEME_OCEAN_BLUE,
            THEME_SUNSET_GOLD,
            THEME_DARK_NEON,
            THEME_SCHOLAR,
            THEME_PURE_LIGHT,
            THEME_OBSIDIAN,
            THEME_FOREST,
            THEME_LAVENDER,
            THEME_SPECTRUM,
            THEME_GRAPHITE);
}
