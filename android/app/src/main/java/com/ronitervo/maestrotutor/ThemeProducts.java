// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

package com.ronitervo.maestrotutor;

import java.util.Arrays;
import java.util.List;

/**
 * Defines all purchasable color theme product IDs.
 * These must match the product IDs configured in Google Play Console
 * under Monetize → Products → In-app products.
 */
public final class ThemeProducts {

    private ThemeProducts() {}

    /** Calm ocean blues and aqua tones. */
    public static final String THEME_OCEAN_BLUE = "theme_ocean_blue";

    /** Warm sunset golds and amber hues. */
    public static final String THEME_SUNSET_GOLD = "theme_sunset_gold";

    /** High-contrast dark background with neon accents. */
    public static final String THEME_DARK_NEON = "theme_dark_neon";

    /** All purchasable theme product IDs. */
    public static final List<String> ALL_PRODUCT_IDS = Arrays.asList(
            THEME_OCEAN_BLUE,
            THEME_SUNSET_GOLD,
            THEME_DARK_NEON
    );
}
