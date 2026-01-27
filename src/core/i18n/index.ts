// When adding new translations, please only update the en.ts file and index.ts if nessesary. Other language files will be updated by language experts of those languages.

// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { arTranslations } from './ar';
import { bnTranslations } from './bn';
import { deTranslations } from './de';
import { enTranslations } from './en';
import { esTranslations } from './es';
import { fiTranslations } from './fi';
import { frTranslations } from './fr';
import { guTranslations } from './gu';
import { hiTranslations } from './hi';
import { idTranslations } from './id';
import { itTranslations } from './it';
import { jaTranslations } from './ja';
import { knTranslations } from './kn';
import { koTranslations } from './ko';
import { mlTranslations } from './ml';
import { mrTranslations } from './mr';
import { nlTranslations } from './nl';
import { plTranslations } from './pl';
import { ptTranslations } from './pt';
import { ruTranslations } from './ru';
import { svTranslations } from './sv';
import { taTranslations } from './ta';
import { teTranslations } from './te';
import { thTranslations } from './th';
import { trTranslations } from './tr';
import { viTranslations } from './vi';
import { zhTranslations } from './zh';

export type TranslationReplacements = Record<string, string | number>;
export type Translations = Record<string, Record<string, string>>;

// Maps BCP-47 langCodes (e.g., "en-US") to translation objects
// Falls back to English for languages without dedicated translation files
export const translations: Translations = {
  // English variants
  "en-US": enTranslations,
  "en-GB": enTranslations,
  "en-AU": enTranslations,
  "en-IN": enTranslations,
  
  // Spanish variants
  "es-ES": esTranslations,
  "es-US": esTranslations,
  
  // Other languages
  "fr-FR": frTranslations,
  "fr-CA": frTranslations,
  "de-DE": deTranslations,
  "it-IT": itTranslations,
  "pt-BR": ptTranslations,
  "nl-NL": nlTranslations,
  "pl-PL": plTranslations,
  "ru-RU": ruTranslations,
  "tr-TR": trTranslations,
  "ar-XA": arTranslations,
  "hi-IN": hiTranslations,
  "bn-IN": bnTranslations,
  "gu-IN": guTranslations,
  "kn-IN": knTranslations,
  "ml-IN": mlTranslations,
  "mr-IN": mrTranslations,
  "ta-IN": taTranslations,
  "te-IN": teTranslations,
  "ja-JP": jaTranslations,
  "ko-KR": koTranslations,
  "cmn-CN": zhTranslations,
  "th-TH": thTranslations,
  "vi-VN": viTranslations,
  "id-ID": idTranslations,
  "fi-FI": fiTranslations,
  "sv-SE": svTranslations,
};
