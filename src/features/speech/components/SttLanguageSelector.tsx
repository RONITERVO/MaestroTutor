// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React from 'react';
import { LanguageDefinition } from '../../../core/config/languages';
import { TranslationReplacements } from '../../../core/i18n/index';

const getPrimaryCode = (codes: string): string => (codes || "").split(',')[0].trim();

interface SttLanguageSelectorProps {
  targetLang: LanguageDefinition;
  nativeLang: LanguageDefinition;
  currentSttLangCode: string;
  onSelectLang: (langCode: string) => void;
  t: (key: string, replacements?: TranslationReplacements) => string;
  isCollapsed: boolean;
  isInSuggestionMode: boolean;
}

const SttLanguageSelector: React.FC<SttLanguageSelectorProps> = React.memo(({ targetLang, nativeLang, currentSttLangCode, onSelectLang, t, isCollapsed, isInSuggestionMode }) => {
  const targetCode = getPrimaryCode(targetLang.code);
  const nativeCode = getPrimaryCode(nativeLang.code);

  const isTargetSelected = currentSttLangCode === targetCode || targetLang.code.includes(currentSttLangCode);
  const isNativeSelected = currentSttLangCode === nativeCode || nativeLang.code.includes(currentSttLangCode);
  
  const wrapperClass = isCollapsed ? (isInSuggestionMode ? 'p-0.5 bg-secondary/60 sketchy-border-thin' : 'p-0.5 bg-primary/60 sketchy-border-thin') : 'p-0.5 bg-secondary sketchy-border-thin';
  const buttonBase = isCollapsed ? 'p-1.5 sketchy-border-thin' : 'p-2 sketchy-border-thin';
  const flagBase = isCollapsed ? 'text-base leading-none' : 'text-lg leading-none';
  const selectedClassCollapsed = isInSuggestionMode ? 'bg-white/50' : 'bg-white/30';
  const selectedClassExpanded = isInSuggestionMode ? 'bg-primary' : 'bg-primary';
  const unselectedHoverCollapsed = isInSuggestionMode ? 'hover:bg-black/20' : 'hover:bg-white/20';

  return (
    <div className={`flex items-center space-x-0.5 ${wrapperClass}`}>
      <button
        onClick={() => onSelectLang(targetCode)}
        className={`${buttonBase} transition-colors ${isTargetSelected ? (isCollapsed ? selectedClassCollapsed : selectedClassExpanded) : (isCollapsed ? `opacity-70 hover:opacity-100 ${unselectedHoverCollapsed}` : 'hover:bg-secondary')}`}
        title={t('sttLang.selectLanguage', { language: targetLang.displayName })}
        aria-label={t('sttLang.selectLanguage', { language: targetLang.displayName })}
        aria-pressed={isTargetSelected}
      >
        <span className={`${flagBase} ${isTargetSelected && !isCollapsed ? 'text-white' : ''}`}>{targetLang.flag}</span>
      </button>
      <button
        onClick={() => onSelectLang(nativeCode)}
        className={`${buttonBase} transition-colors ${isNativeSelected ? (isCollapsed ? selectedClassCollapsed : selectedClassExpanded) : (isCollapsed ? `opacity-70 hover:opacity-100 ${unselectedHoverCollapsed}` : 'hover:bg-secondary')}`}
        title={t('sttLang.selectLanguage', { language: nativeLang.displayName })}
        aria-label={t('sttLang.selectLanguage', { language: nativeLang.displayName })}
        aria-pressed={isNativeSelected}
      >
        <span className={`${flagBase} ${isNativeSelected && !isCollapsed ? 'text-white' : ''}`}>{nativeLang.flag}</span>
      </button>
    </div>
  );
});
SttLanguageSelector.displayName = "SttLanguageSelector";

export default SttLanguageSelector;
