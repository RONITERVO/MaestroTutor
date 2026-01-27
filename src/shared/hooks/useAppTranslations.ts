// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useMemo } from 'react';
import { useTranslations } from '../../app/hooks/useTranslations';
import { useMaestroStore } from '../../store';
import { selectSelectedLanguagePair } from '../../store/slices/settingsSlice';
import { getPrimaryCode } from '../utils/languageUtils';

export const useAppTranslations = () => {
  const selectedLanguagePair = useMaestroStore(selectSelectedLanguagePair);

  const nativeLangForTranslations = useMemo(() => {
    if (selectedLanguagePair?.nativeLanguageCode) {
      // Use full BCP-47 code (e.g., "en-US") for translations
      return getPrimaryCode(selectedLanguagePair.nativeLanguageCode);
    }
    // Fall back to browser language or default to en-US
    const browserLang = typeof navigator !== 'undefined' ? navigator.language : 'en-US';
    return browserLang || 'en-US';
  }, [selectedLanguagePair]);

  return useTranslations(nativeLangForTranslations);
};

export default useAppTranslations;
