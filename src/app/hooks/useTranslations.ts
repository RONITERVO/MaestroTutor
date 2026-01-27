// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useCallback, useMemo } from 'react';
import { translations, TranslationReplacements } from '../../core/i18n/index';

export type TranslationFunction = (key: string, replacements?: TranslationReplacements) => string;

const escapeRegExp = (value: string) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/**
 * Hook providing internationalization support.
 * Returns a translation function `t` based on the current language.
 * 
 * @param nativeLangCode - The user's native language code (BCP-47 format e.g., 'en-US', 'es-ES')
 * @returns Object containing the translation function
 */
export const useTranslations = (nativeLangCode: string) => {
  // Use full BCP-47 code for lookup (e.g., "en-US")
  const fullCode = useMemo(() => nativeLangCode || 'en-US', [nativeLangCode]);

  const t = useCallback((key: string, replacements?: TranslationReplacements): string => {
    // Try full BCP-47 code first, then fall back to en-US
    let translation = translations[fullCode]?.[key] || translations["en-US"]?.[key] || key;
    if (replacements) {
      Object.keys(replacements).forEach(rKey => {
        const escapedRKey = escapeRegExp(rKey);
        translation = translation.replace(new RegExp(`\\{${escapedRKey}\\}`, 'g'), String(replacements[rKey]));
      });
    }
    return translation;
  }, [fullCode]);

  return { t, currentLanguage: fullCode };
};

export default useTranslations;
