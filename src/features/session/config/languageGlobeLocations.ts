// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import type { LanguageDefinition } from '../../../core/config/languages';

export interface GlobeLocation {
  lat: number;
  lng: number;
}

/**
 * Country anchors for the globe selector. Shared-flag languages are spread
 * within their country so each language remains individually selectable.
 */
export const LANGUAGE_GLOBE_LOCATIONS: Record<string, GlobeLocation> = {
  'en-US': { lat: 39.8, lng: -98.6 },
  'en-GB': { lat: 54.4, lng: -2.4 },
  'en-AU': { lat: -25.3, lng: 133.8 },
  'en-IN': { lat: 22.7, lng: 78.9 },

  'es-ES': { lat: 40.4, lng: -3.7 },
  'es-US': { lat: 23.6, lng: -102.5 },

  'fr-FR': { lat: 46.2, lng: 2.2 },
  'fr-CA': { lat: 56.1, lng: -106.3 },

  'de-DE': { lat: 51.2, lng: 10.4 },
  'it-IT': { lat: 42.8, lng: 12.5 },
  'pt-BR': { lat: -14.2, lng: -51.9 },
  'nl-NL': { lat: 52.1, lng: 5.3 },
  'pl-PL': { lat: 52.0, lng: 19.1 },
  'ru-RU': { lat: 61.5, lng: 105.3 },
  'tr-TR': { lat: 39.0, lng: 35.2 },
  'ar-XA': { lat: 23.9, lng: 45.1 },

  'hi-IN': { lat: 26.0, lng: 80.1 },
  'bn-IN': { lat: 23.4, lng: 88.4 },
  'gu-IN': { lat: 22.8, lng: 71.2 },
  'kn-IN': { lat: 14.8, lng: 76.4 },
  'ml-IN': { lat: 10.5, lng: 76.3 },
  'mr-IN': { lat: 19.7, lng: 75.7 },
  'ta-IN': { lat: 10.8, lng: 78.7 },
  'te-IN': { lat: 16.0, lng: 80.2 },

  'ja-JP': { lat: 36.2, lng: 138.3 },
  'ko-KR': { lat: 36.5, lng: 127.8 },
  'cmn-CN': { lat: 35.8, lng: 104.2 },
  'th-TH': { lat: 15.8, lng: 101.0 },
  'vi-VN': { lat: 16.2, lng: 107.8 },
  'id-ID': { lat: -2.5, lng: 118.0 },

  'fi-FI': { lat: 64.0, lng: 26.0 },
  'sv-SE': { lat: 62.0, lng: 16.0 },
};

const FALLBACK_LOCATION: GlobeLocation = { lat: 12, lng: 0 };

export function getLanguageGlobeLocation(lang: LanguageDefinition): GlobeLocation {
  return LANGUAGE_GLOBE_LOCATIONS[lang.langCode] ?? FALLBACK_LOCATION;
}
