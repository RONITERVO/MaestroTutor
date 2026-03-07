// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React from 'react';
import { TranslationReplacements } from '../../../../core/i18n/index';
import { IconPencil } from '../../../../shared/ui/Icons';

interface ComposerProps {
  t: (key: string, replacements?: TranslationReplacements) => string;
  inputText: string;
  placeholder: string;
  isDisabled: boolean;
  isDrawDisabled: boolean;
  onChange: (e: React.ChangeEvent<HTMLTextAreaElement>) => void;
  onKeyDown: (e: React.KeyboardEvent<HTMLTextAreaElement>) => void;
  onPaste: (e: React.ClipboardEvent<HTMLTextAreaElement>) => void;
  onOpenDrawCanvas: () => void;
  bubbleTextAreaRef: React.RefObject<HTMLTextAreaElement | null>;
  prepDisplay: string | null;
  drawCanvasLabel: string;
  drawButtonClassName: string;
}

const Composer: React.FC<ComposerProps> = ({
  t,
  inputText,
  placeholder,
  isDisabled,
  isDrawDisabled,
  onChange,
  onKeyDown,
  onPaste,
  onOpenDrawCanvas,
  bubbleTextAreaRef,
  prepDisplay,
  drawCanvasLabel,
  drawButtonClassName,
}) => (
  <div className="relative w-full">
    <textarea
      ref={bubbleTextAreaRef}
      rows={1}
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
    <button
      type="button"
      onClick={onOpenDrawCanvas}
      disabled={isDrawDisabled}
      className={`absolute top-2 right-2 p-2 rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-input-focus-ring disabled:opacity-50 ${drawButtonClassName}`}
      title={drawCanvasLabel}
      aria-label={drawCanvasLabel}
    >
      <IconPencil className="w-4 h-4" />
    </button>
    {prepDisplay && <output className="sr-only" aria-live="polite">{prepDisplay}</output>}
  </div>
);

export default Composer;
