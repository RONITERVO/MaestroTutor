// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useCallback, useMemo } from 'react';
import { translations, TranslationReplacements } from '../../core/i18n/index';

export type TranslationFunction = (key: string, replacements?: TranslationReplacements) => string;

/**
 * Hook providing internationalization support.
 * Returns a translation function `t` based on the current language.
 * 
 * @param nativeLangCode - The user's native language code (e.g., 'en', 'es')
 * @returns Object containing the translation function
 */
export const useTranslations = (nativeLangCode: string) => {
  const lang = useMemo(() => nativeLangCode.substring(0, 2), [nativeLangCode]);

  const t = useCallback((key: string, replacements?: TranslationReplacements): string => {
    let translation = translations[lang]?.[key] || translations.en[key] || key;
    if (replacements) {
      Object.keys(replacements).forEach(rKey => {
        translation = translation.replace(`{${rKey}}`, String(replacements[rKey]));
      });
    }
    return translation;
  }, [lang]);

  return { t, currentLanguage: lang };
};

export default useTranslations;
