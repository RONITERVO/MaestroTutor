// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React from 'react';
import { TranslationReplacements } from '../../../../core/i18n/index';
import { IconPencil, IconXMark } from '../../../../shared/ui/Icons';
import { getSuggestionPracticeProgress } from '../../utils/suggestionPractice';

interface ComposerProps {
  t: (key: string, replacements?: TranslationReplacements) => string;
  inputText: string;
  suggestionPracticeTarget: string | null;
  placeholder: string;
  isDisabled: boolean;
  isDrawDisabled: boolean;
  onChange: (e: React.ChangeEvent<HTMLTextAreaElement>) => void;
  onKeyDown: (e: React.KeyboardEvent<HTMLTextAreaElement>) => void;
  onPaste: (e: React.ClipboardEvent<HTMLTextAreaElement>) => void;
  onOpenDrawCanvas: () => void;
  onCancelSuggestionPractice: () => void;
  bubbleTextAreaRef: React.RefObject<HTMLTextAreaElement | null>;
  prepDisplay: string | null;
  drawCanvasLabel: string;
  drawButtonClassName: string;
}

const Composer: React.FC<ComposerProps> = ({
  t,
  inputText,
  suggestionPracticeTarget,
  placeholder,
  isDisabled,
  isDrawDisabled,
  onChange,
  onKeyDown,
  onPaste,
  onOpenDrawCanvas,
  onCancelSuggestionPractice,
  bubbleTextAreaRef,
  prepDisplay,
  drawCanvasLabel,
  drawButtonClassName,
}) => {
  const practiceProgress = suggestionPracticeTarget
    ? getSuggestionPracticeProgress(suggestionPracticeTarget, inputText)
    : null;
  const previewRemainder = practiceProgress?.state === 'following'
    ? practiceProgress.remainder
    : '';
  const practiceStatus = practiceProgress?.state === 'complete'
    ? t('chat.suggestion.practiceComplete')
    : suggestionPracticeTarget
      ? t('chat.suggestion.practiceActive', { suggestion: suggestionPracticeTarget })
      : '';
  const inputPaddingClass = suggestionPracticeTarget ? 'pr-20' : 'pr-12';
  const textLayoutStyle: React.CSSProperties = {
    fontSize: '3.6cqw',
    lineHeight: 1.35,
    overflowWrap: 'break-word',
    wordBreak: 'break-word',
  };

  return (
    <div className="relative w-full">
      {suggestionPracticeTarget ? (
        <div className="relative grid w-full">
          {[inputText, suggestionPracticeTarget].map((sizingText, index) => (
            <div
              key={index}
              aria-hidden="true"
              dir="auto"
              className={`invisible col-start-1 row-start-1 w-full min-h-[50px] py-3 px-4 ${inputPaddingClass} whitespace-pre-wrap`}
              style={textLayoutStyle}
            >
              {sizingText || '\u200b'}
            </div>
          ))}
          <textarea
            ref={bubbleTextAreaRef}
            rows={1}
            dir="auto"
            className={`absolute inset-0 z-[1] h-full w-full py-3 px-4 ${inputPaddingClass} bg-transparent border-none focus:ring-0 resize-none overflow-hidden placeholder-inherit min-h-[50px]`}
            style={textLayoutStyle}
            value={inputText}
            onChange={onChange}
            onKeyDown={onKeyDown}
            onPaste={onPaste}
            disabled={isDisabled}
            aria-label={t('chat.messageInputAriaLabel')}
            aria-describedby="composer-suggestion-practice-hint"
          />
          {previewRemainder && (
            <div
              aria-hidden="true"
              dir="auto"
              className={`absolute inset-0 py-3 px-4 ${inputPaddingClass} pointer-events-none whitespace-pre-wrap overflow-hidden`}
              style={textLayoutStyle}
            >
              <span className="invisible">{inputText}</span>
              <span className="opacity-35">{previewRemainder}</span>
            </div>
          )}
        </div>
      ) : (
        <textarea
          ref={bubbleTextAreaRef}
          rows={1}
          dir="auto"
          className="w-full py-3 px-4 pr-12 bg-transparent border-none focus:ring-0 resize-none overflow-hidden placeholder-inherit min-h-[50px]"
          style={{ fontSize: '3.6cqw', lineHeight: 1.35 }}
          placeholder={placeholder}
          value={inputText}
          onChange={onChange}
          onKeyDown={onKeyDown}
          onPaste={onPaste}
          disabled={isDisabled}
          aria-label={t('chat.messageInputAriaLabel')}
        />
      )}

      {suggestionPracticeTarget && (
        <span id="composer-suggestion-practice-hint" className="sr-only">
          {t('chat.suggestion.practiceHint')}
        </span>
      )}
      <div className="absolute z-[2] top-2 right-2 flex items-center gap-0.5">
        {suggestionPracticeTarget && (
          <button
            type="button"
            onClick={onCancelSuggestionPractice}
            className={`p-2 rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-input-focus-ring ${drawButtonClassName}`}
            title={t('chat.suggestion.cancelPractice')}
            aria-label={t('chat.suggestion.cancelPractice')}
          >
            <IconXMark className="w-4 h-4" />
          </button>
        )}
        <button
          type="button"
          onClick={onOpenDrawCanvas}
          disabled={isDrawDisabled}
          className={`p-2 rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-input-focus-ring disabled:opacity-50 ${drawButtonClassName}`}
          title={drawCanvasLabel}
          aria-label={drawCanvasLabel}
        >
          <IconPencil className="w-4 h-4" />
        </button>
      </div>
      {practiceStatus && <output className="sr-only" aria-live="polite">{practiceStatus}</output>}
      {prepDisplay && <output className="sr-only" aria-live="polite">{prepDisplay}</output>}
    </div>
  );
};

export default Composer;
