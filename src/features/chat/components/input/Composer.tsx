// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React from 'react';
import { TranslationReplacements } from '../../../../core/i18n/index';

interface ComposerProps {
  t: (key: string, replacements?: TranslationReplacements) => string;
  inputText: string;
  placeholder: string;
  isDisabled: boolean;
  onChange: (e: React.ChangeEvent<HTMLTextAreaElement>) => void;
  onKeyDown: (e: React.KeyboardEvent<HTMLTextAreaElement>) => void;
  bubbleTextAreaRef: React.RefObject<HTMLTextAreaElement | null>;
  prepDisplay: string | null;
}

const Composer: React.FC<ComposerProps> = ({
  t,
  inputText,
  placeholder,
  isDisabled,
  onChange,
  onKeyDown,
  bubbleTextAreaRef,
  prepDisplay,
}) => (
  <div className="relative w-full">
    <textarea
      ref={bubbleTextAreaRef}
      rows={1}
      className="w-full py-3 px-4 bg-transparent border-none focus:ring-0 resize-none overflow-hidden placeholder-inherit min-h-[50px]"
      style={{ fontSize: '3.6cqw', lineHeight: 1.35 }}
      placeholder={placeholder}
      value={inputText}
      onChange={onChange}
      onKeyDown={onKeyDown}
      disabled={isDisabled}
      aria-label={t('chat.messageInputAriaLabel')}
    />
    {prepDisplay && <output className="sr-only" aria-live="polite">{prepDisplay}</output>}
  </div>
);

export default Composer;
