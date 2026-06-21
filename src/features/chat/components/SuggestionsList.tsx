// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useState, useEffect, useRef } from 'react';
import { ReplySuggestion } from '../../../core/types';
import { TranslationReplacements } from '../../../core/i18n/index';
import { IconTranslate, IconSpeaker, IconVolumeOff } from '../../../shared/ui/Icons';
import { SmallSpinner } from '../../../shared/ui/SmallSpinner';
import { useMaestroStore } from '../../../store';
import { selectReplySuggestions, selectSuggestionsLoadingStreamText } from '../../../store/slices/chatSlice';
import { selectSettings } from '../../../store/slices/settingsSlice';
import { selectIsLoadingSuggestions, selectIsCreatingSuggestion, selectIsSpeaking } from '../../../store/slices/uiSlice';

interface SuggestionsListProps {
  t: (key: string, replacements?: TranslationReplacements) => string;
  onToggleSuggestionMode: () => void;
  onSuggestionClick: (suggestion: ReplySuggestion, langType: 'target' | 'native') => void;
  stopSpeaking: () => void;
  onToggleSpeakNativeLang: () => void;
  speakNativeLang: boolean;
  onPracticeSuggestion: (suggestion: ReplySuggestion) => void;
  isPracticeDisabled: boolean;
}

const LONG_PRESS_DURATION_MS = 550;
const LONG_PRESS_MOVE_TOLERANCE_PX = 10;

const SuggestionsList: React.FC<SuggestionsListProps> = ({
  t,
  onToggleSuggestionMode,
  onSuggestionClick,
  stopSpeaking,
  onToggleSpeakNativeLang,
  speakNativeLang,
  onPracticeSuggestion,
  isPracticeDisabled,
}) => {
  const replySuggestions = useMaestroStore(selectReplySuggestions);
  const suggestionsLoadingStreamText = useMaestroStore(selectSuggestionsLoadingStreamText);
  const isLoadingSuggestions = useMaestroStore(selectIsLoadingSuggestions);
  const isCreatingSuggestion = useMaestroStore(selectIsCreatingSuggestion);
  const isSpeaking = useMaestroStore(selectIsSpeaking);
  const settings = useMaestroStore(selectSettings);
  const isSuggestionMode = settings.isSuggestionMode;
  const microphoneApiAvailable = useMaestroStore(state => state.microphoneApiAvailable);
  const isSttSupported = microphoneApiAvailable;

  const [expandedSuggestionTarget, setExpandedSuggestionTarget] = useState<string | null>(null);
  const [nativeFlashTarget, setNativeFlashTarget] = useState<string | null>(null);
  const [nativeFlashIsOn, setNativeFlashIsOn] = useState<boolean>(false);
  const nativeFlashTimeoutRef = useRef<number | null>(null);
  const pointerDownPosRef = useRef<{ x: number; y: number } | null>(null);
  const suggestionPressRef = useRef<{
    pointerId: number;
    startX: number;
    startY: number;
    button: HTMLButtonElement;
  } | null>(null);
  const suggestionLongPressTimerRef = useRef<number | null>(null);
  const suggestionLongPressTriggeredRef = useRef(false);
  const suppressSuggestionClickRef = useRef(false);
  const suppressSuggestionClickTimerRef = useRef<number | null>(null);

  useEffect(() => {
    setExpandedSuggestionTarget(null);
    setNativeFlashTarget(null);
  }, [replySuggestions]);

  useEffect(() => {
    return () => {
      if (nativeFlashTimeoutRef.current) clearTimeout(nativeFlashTimeoutRef.current);
      if (suggestionLongPressTimerRef.current) clearTimeout(suggestionLongPressTimerRef.current);
      if (suppressSuggestionClickTimerRef.current) clearTimeout(suppressSuggestionClickTimerRef.current);
    };
  }, []);

  const clearSuggestionPress = (pointerId?: number) => {
    const press = suggestionPressRef.current;
    if (pointerId !== undefined && press?.pointerId !== pointerId) return;
    if (suggestionLongPressTimerRef.current) {
      clearTimeout(suggestionLongPressTimerRef.current);
      suggestionLongPressTimerRef.current = null;
    }
    if (press?.button.hasPointerCapture(press.pointerId)) {
      press.button.releasePointerCapture(press.pointerId);
    }
    suggestionPressRef.current = null;
  };

  const suppressNextSuggestionClick = () => {
    suppressSuggestionClickRef.current = true;
    if (suppressSuggestionClickTimerRef.current) clearTimeout(suppressSuggestionClickTimerRef.current);
    suppressSuggestionClickTimerRef.current = window.setTimeout(() => {
      suppressSuggestionClickRef.current = false;
      suppressSuggestionClickTimerRef.current = null;
    }, 1000);
  };

  const triggerSuggestionPractice = (suggestion: ReplySuggestion) => {
    if (isPracticeDisabled) return;
    onPracticeSuggestion(suggestion);
  };

  const handleSuggestionPointerDown = (
    e: React.PointerEvent<HTMLButtonElement>,
    suggestion: ReplySuggestion
  ) => {
    if (isPracticeDisabled || !e.isPrimary || (e.pointerType === 'mouse' && e.button !== 0)) return;
    clearSuggestionPress();
    suggestionLongPressTriggeredRef.current = false;
    e.currentTarget.setPointerCapture(e.pointerId);
    suggestionPressRef.current = {
      pointerId: e.pointerId,
      startX: e.clientX,
      startY: e.clientY,
      button: e.currentTarget,
    };
    suggestionLongPressTimerRef.current = window.setTimeout(() => {
      suggestionLongPressTimerRef.current = null;
      suggestionLongPressTriggeredRef.current = true;
      triggerSuggestionPractice(suggestion);
    }, LONG_PRESS_DURATION_MS);
  };

  const handleSuggestionPointerMove = (e: React.PointerEvent<HTMLButtonElement>) => {
    const press = suggestionPressRef.current;
    if (!press || press.pointerId !== e.pointerId) return;
    const movedTooFar = Math.abs(e.clientX - press.startX) > LONG_PRESS_MOVE_TOLERANCE_PX
      || Math.abs(e.clientY - press.startY) > LONG_PRESS_MOVE_TOLERANCE_PX;
    if (movedTooFar) {
      clearSuggestionPress(e.pointerId);
      suggestionLongPressTriggeredRef.current = false;
      suppressNextSuggestionClick();
    }
  };

  const handleSuggestionPointerEnd = (e: React.PointerEvent<HTMLButtonElement>) => {
    const shouldSuppressClick = e.type === 'pointerup'
      && suggestionPressRef.current?.pointerId === e.pointerId
      && suggestionLongPressTriggeredRef.current;
    clearSuggestionPress(e.pointerId);
    suggestionLongPressTriggeredRef.current = false;
    if (shouldSuppressClick) suppressNextSuggestionClick();
  };

  const handleSuggestionBubbleClick = (suggestion: ReplySuggestion) => {
    if (isSpeaking) {
      stopSpeaking();
      return;
    }
    setExpandedSuggestionTarget(suggestion.target);
    onSuggestionClick(suggestion, 'target');
  };

  const handleNativePointerDown = (e: React.PointerEvent) => {
    pointerDownPosRef.current = { x: e.clientX, y: e.clientY };
  };

  const handleNativePointerUp = (e: React.PointerEvent, suggestion: ReplySuggestion) => {
    if (pointerDownPosRef.current) {
      const dx = Math.abs(e.clientX - pointerDownPosRef.current.x);
      const dy = Math.abs(e.clientY - pointerDownPosRef.current.y);
      if (dx < 10 && dy < 10) {
        e.preventDefault();
        const next = !speakNativeLang;
        setNativeFlashTarget(suggestion.target);
        setNativeFlashIsOn(next);
        if (nativeFlashTimeoutRef.current) clearTimeout(nativeFlashTimeoutRef.current);
        nativeFlashTimeoutRef.current = window.setTimeout(() => { setNativeFlashTarget(null); }, 900);
        onToggleSpeakNativeLang();
      }
    }
    pointerDownPosRef.current = null;
  };

  const handleNativePointerLeave = () => {
    pointerDownPosRef.current = null;
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
        <style>{`
        @keyframes pop-fade-speak { 0% { transform: scale(0.85); opacity: 0; } 20% { transform: scale(1.15); opacity: 1; } 80% { transform: scale(1.0); opacity: 1; } 100% { transform: scale(0.95); opacity: 0; } }
        .animate-speak-flash { animation: pop-fade-speak 900ms ease-out both; }
        `}</style>
        <div className="flex flex-wrap gap-2 justify-end">
            {isLoadingSuggestions && (
              <span
                className="inline-flex items-center gap-1.5 px-3 py-1.5 text-page-text/70 italic max-w-full"
                style={{
                  fontSize: '2.8cqw',
                  lineHeight: 1.35,
                  textAlign: 'right',
                  marginLeft: 'auto',
                }}
              >
                <span className="inline-block w-1.5 h-1.5 rounded-full bg-current opacity-60 animate-pulse flex-shrink-0" />
                <span className="truncate">
                  {suggestionsLoadingStreamText
                    ? suggestionsLoadingStreamText.length > 50
                      ? `${suggestionsLoadingStreamText.slice(0, 50)}\u2026`
                      : suggestionsLoadingStreamText
                    : t('chat.loadingSuggestions')}
                </span>
              </span>
            )}
            {!isLoadingSuggestions && replySuggestions.map((suggestion, index) => {
              const isExpanded = expandedSuggestionTarget === suggestion.target;
              return (
                <div key={index} className="inline-block">
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      if (suppressSuggestionClickRef.current) {
                        suppressSuggestionClickRef.current = false;
                        if (suppressSuggestionClickTimerRef.current) {
                          clearTimeout(suppressSuggestionClickTimerRef.current);
                          suppressSuggestionClickTimerRef.current = null;
                        }
                        e.preventDefault();
                        return;
                      }
                      handleSuggestionBubbleClick(suggestion);
                    }}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' && e.shiftKey && !isPracticeDisabled) {
                        e.preventDefault();
                        e.stopPropagation();
                        triggerSuggestionPractice(suggestion);
                      }
                    }}
                    onPointerDown={(e) => handleSuggestionPointerDown(e, suggestion)}
                    onPointerMove={handleSuggestionPointerMove}
                    onPointerUp={handleSuggestionPointerEnd}
                    onPointerCancel={handleSuggestionPointerEnd}
                    className="px-3 py-1.5 transition-colors text-page-text bg-suggestion-bg hover:bg-suggestion-hover sketchy-border-thin focus:outline-none focus:ring-2 focus:ring-suggestion-ring"
                    style={{
                      fontSize: '3.1cqw',
                      userSelect: 'none',
                      WebkitUserSelect: 'none',
                      WebkitTouchCallout: 'none',
                      touchAction: 'manipulation',
                    }}
                    title={isPracticeDisabled
                      ? t('chat.suggestion.practiceUnavailableLive')
                      : t('chat.suggestion.speakOrPractice', { suggestion: suggestion.target })}
                    aria-label={isPracticeDisabled
                      ? t('chat.suggestion.ariaLabel', { suggestion: suggestion.target })
                      : t('chat.suggestion.speakOrPractice', { suggestion: suggestion.target })}
                    aria-keyshortcuts={isPracticeDisabled ? undefined : 'Shift+Enter'}
                    tabIndex={0}
                  >
                    {suggestion.target}
                  </button>
                  <div
                    style={{
                      maxHeight: isExpanded ? '80px' : '0px',
                      opacity: isExpanded ? 1 : 0,
                      overflow: 'hidden',
                      transition: 'max-height 300ms ease-out, opacity 200ms ease-out',
                    }}
                  >
                    {suggestion.native && (
                      <p
                        className="italic mt-0.5 whitespace-pre-wrap pl-2 border-l-2 rounded-sm px-1 text-ai-file-text border-line-border cursor-pointer"
                        style={{ fontSize: '2.8cqw', lineHeight: 1.3 }}
                        onPointerDown={handleNativePointerDown}
                        onPointerUp={(e) => handleNativePointerUp(e, suggestion)}
                        onPointerLeave={handleNativePointerLeave}
                      >
                        {suggestion.native}
                        {nativeFlashTarget === suggestion.target && (
                          <span className="ml-1 inline-block align-middle animate-speak-flash">
                            {nativeFlashIsOn ? <IconSpeaker className="w-3 h-3 inline" /> : <IconVolumeOff className="w-3 h-3 inline" />}
                          </span>
                        )}
                      </p>
                    )}
                  </div>
                </div>
              );
            })}
            {!isLoadingSuggestions && isSttSupported && replySuggestions.length > 0 && (
                <button
                    onClick={(e) => { e.stopPropagation(); onToggleSuggestionMode(); }}
                    className={`inline-flex items-center justify-center w-[34px] h-[34px] text-sm transition-colors disabled:opacity-50 sketchy-border-thin
                        ${isSuggestionMode
                            ? 'bg-suggestion-active-bg text-suggestion-active-text animate-pulse'
                            : 'text-page-text bg-suggestion-bg hover:bg-suggestion-hover'
                        }
                        focus:outline-none focus:ring-2 focus:ring-suggestion-ring`}
                    style={{ fontSize: '3cqw' }}
                    title={t('chat.suggestion.toggleCreateMode')}
                    aria-label={t('chat.suggestion.toggleCreateMode')}
                    aria-pressed={isSuggestionMode}
                    disabled={isCreatingSuggestion}
                >
                    {isCreatingSuggestion ? <SmallSpinner className="w-5 h-5 text-suggestion-active-text/70" /> : <IconTranslate className="w-5 h-5" />}
                </button>
            )}
        </div>
    </div>
  );
};

export default SuggestionsList;
