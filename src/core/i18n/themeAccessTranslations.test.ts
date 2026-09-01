// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { translations } from './index';

const REQUIRED_KEYS = [
  'header.aiAccessRequired',
  'themeGallery.title',
  'themeGallery.close',
  'themeGallery.included',
  'themeGallery.apply',
  'themeGallery.includedDescription',
  'themeGallery.footerNote',
  'themeCustomizer.galleryTitle',
  'themeCustomizer.galleryDescription',
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
