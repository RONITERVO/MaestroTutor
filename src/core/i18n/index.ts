
// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { enTranslations } from './en';
import { esTranslations } from './es';

export type TranslationReplacements = Record<string, string | number>;
export type Translations = Record<string, Record<string, string>>;

export const translations: Translations = {
  en: enTranslations,
  es: esTranslations,
  // Mapping other languages to English fallback for now
  fr: enTranslations,
  fi: enTranslations,
  de: enTranslations,
  it: enTranslations,
  ja: enTranslations,
  ko: enTranslations,
  pt: enTranslations,
  ru: enTranslations,
  zh: enTranslations,
  sv: enTranslations,
};
