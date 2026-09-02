// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { translations } from './index';

/**
 * Labels every locale must carry itself rather than falling back to English.
 * The theme gallery keys were dropped with the gallery: the customizer now
 * lists every included theme directly.
 */
const REQUIRED_KEYS = [
  'header.aiAccessRequired',
  'themeCustomizer.title',
  'themeCustomizer.quickThemes',
  'themeCustomizer.deletePreset',
] as const;

describe('theme and access translations', () => {
  it('defines the new user-facing labels in every supported locale', () => {
    for (const [locale, localeTranslations] of Object.entries(translations)) {
      for (const key of REQUIRED_KEYS) {
        expect(
          Object.prototype.hasOwnProperty.call(localeTranslations, key),
          `${locale} should define ${key} instead of falling back to English`,
        ).toBe(true);
        expect(localeTranslations[key]?.trim()).toBeTruthy();
      }
    }
  });
});
