// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useState, useEffect } from 'react';
import { ReplySuggestion } from '../../../core/types';
import { TranslationReplacements } from '../../../core/i18n/index';
import { IconTranslate } from '../../../shared/ui/Icons';
import { SmallSpinner } from '../../../shared/ui/SmallSpinner';
import { useMaestroStore } from '../../../store';
import { selectReplySuggestions } from '../../../store/slices/chatSlice';
import { selectSettings } from '../../../store/slices/settingsSlice';
import { selectIsLoadingSuggestions, selectIsCreatingSuggestion, selectIsSpeaking } from '../../../store/slices/uiSlice';

interface SuggestionsListProps {
  t: (key: string, replacements?: TranslationReplacements) => string;
  onToggleSuggestionMode: () => void;
  onSuggestionClick: (suggestion: ReplySuggestion, langType: 'target' | 'native') => void;
  stopSpeaking: () => void;
}

const SuggestionsList: React.FC<SuggestionsListProps> = ({
  t,
  onToggleSuggestionMode,
  onSuggestionClick,
  stopSpeaking
}) => {
  const replySuggestions = useMaestroStore(selectReplySuggestions);
  const isLoadingSuggestions = useMaestroStore(selectIsLoadingSuggestions);
  const isCreatingSuggestion = useMaestroStore(selectIsCreatingSuggestion);
  const isSpeaking = useMaestroStore(selectIsSpeaking);
  const settings = useMaestroStore(selectSettings);
  const isSuggestionMode = settings.isSuggestionMode;
  const microphoneApiAvailable = useMaestroStore(state => state.microphoneApiAvailable);
  const isSttSupported = microphoneApiAvailable;

  const [clickTimeoutId, setClickTimeoutId] = useState<number | null>(null);
  const [lastClickedSuggestionInfo, setLastClickedSuggestionInfo] = useState<{ suggestion: ReplySuggestion, timestamp: number } | null>(null);
  const [doubleClickedSuggestionTarget, setDoubleClickedSuggestionTarget] = useState<string | null>(null);

  useEffect(() => {
    setDoubleClickedSuggestionTarget(null);
  }, [replySuggestions]);

  const handleSuggestionBubbleClick = (suggestion: ReplySuggestion) => {
    if (isSpeaking) {
      if (clickTimeoutId) {
        clearTimeout(clickTimeoutId);
        setClickTimeoutId(null);
      }
      setLastClickedSuggestionInfo(null);
      setDoubleClickedSuggestionTarget(null);
      stopSpeaking();
      return;
    }
     const now = Date.now();
 
     if (clickTimeoutId) {
         clearTimeout(clickTimeoutId);
         setClickTimeoutId(null);
     }
 
     if (lastClickedSuggestionInfo &&
         lastClickedSuggestionInfo.suggestion.target === suggestion.target &&
         (now - lastClickedSuggestionInfo.timestamp < 300)) {
 
         onSuggestionClick(suggestion, 'native');
         setDoubleClickedSuggestionTarget(suggestion.target); 
         setLastClickedSuggestionInfo(null);
     } else {
         setLastClickedSuggestionInfo({ suggestion, timestamp: now });
         const timeoutId = window.setTimeout(() => {
             onSuggestionClick(suggestion, 'target');
             setDoubleClickedSuggestionTarget(null); 
             setLastClickedSuggestionInfo(null);
             setClickTimeoutId(null);
         }, 300);
         setClickTimeoutId(timeoutId);
         if (doubleClickedSuggestionTarget !== null && doubleClickedSuggestionTarget !== suggestion.target) {
             setDoubleClickedSuggestionTarget(null);
         }
     }
   };

   // Always fully expanded, matching GitHub edition behavior.
   const suggestionsContainerClasses = "pt-2 max-w-2xl w-full";

  return (
    <div
      className={suggestionsContainerClasses}
      role="toolbar"
      aria-label={t('chat.suggestionsAriaLabel')}
      style={{
        // @ts-ignore
        containerType: 'inline-size'
      }}
    >
        <div className="flex flex-wrap gap-2 justify-end">
            {isLoadingSuggestions && (
              <span className="inline-block px-3 py-1.5 text-muted-foreground italic" style={{ fontSize: '2.8cqw' }}>{t('chat.loadingSuggestions')}</span>
            )}
            {!isLoadingSuggestions && replySuggestions.map((suggestion, index) => (
            <button
                key={index}
                onClick={(e) => { e.stopPropagation(); handleSuggestionBubbleClick(suggestion); }}
                className={`inline-block px-3 py-1.5 transition-colors text-foreground bg-secondary hover:bg-paper-dark sketchy-border-thin ${doubleClickedSuggestionTarget === suggestion.target ? 'focus:outline-none focus:ring-2 focus:ring-watercolor' : 'focus:outline-none focus:ring-2 focus:ring-accent'}`}
                style={{ fontSize: '3.1cqw' }}
                title={t('chat.suggestion.speak', { suggestion: suggestion.target })}
                aria-label={t('chat.suggestion.ariaLabel', { suggestion: suggestion.target })}
                tabIndex={0}
            >
                {suggestion.target}
            </button>
            ))}
            {!isLoadingSuggestions && isSttSupported && replySuggestions.length > 0 && (
                <button
                    onClick={(e) => { e.stopPropagation(); onToggleSuggestionMode(); }}
                    className={`inline-flex items-center justify-center w-[34px] h-[34px] text-sm transition-colors disabled:opacity-50 sketchy-border-thin
                        ${isSuggestionMode
                            ? 'bg-accent text-accent-foreground animate-pulse'
                            : 'text-foreground bg-secondary hover:bg-paper-dark'
                        }
                        focus:outline-none focus:ring-2 focus:ring-accent`}
                    style={{ fontSize: '3cqw' }}
                    title={t('chat.suggestion.toggleCreateMode')}
                    aria-label={t('chat.suggestion.toggleCreateMode')}
                    aria-pressed={isSuggestionMode}
                    disabled={isCreatingSuggestion}
                >
                    {isCreatingSuggestion ? <SmallSpinner className="w-5 h-5 text-accent-foreground/70" /> : <IconTranslate className="w-5 h-5" />}
                </button>
            )}
        </div>
    </div>
  );
};

export default SuggestionsList;
