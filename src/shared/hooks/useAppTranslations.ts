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
      return getPrimaryCode(selectedLanguagePair.nativeLanguageCode);
    }
    const browserLang = (typeof navigator !== 'undefined' ? navigator.language : 'en').substring(0, 2);
    return browserLang || 'en';
  }, [selectedLanguagePair]);

  return useTranslations(nativeLangForTranslations);
};

export default useAppTranslations;
