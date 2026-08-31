// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React from 'react';
import { IconBookmark, IconTrash } from '../../../shared/ui/Icons';
import { TranslationReplacements } from '../../../core/i18n/index';

interface MessageSwipeTrayProps {
  messageId: string;
  isUser: boolean;
  isAssistant: boolean;
  isOpen: boolean;
  isBookmarked: boolean;
  canBookmark: boolean;
  t: (key: string, replacements?: TranslationReplacements) => string;
  onBookmark: (messageId: string) => void;
  onDelete: (messageId: string) => void;
}

/**
 * The delete/bookmark tray revealed by swiping a message.
 *
 * Extracted from ChatInterface's message loop purely so it can be memoised.
 * Inline in the loop it re-rendered — along with its icons — for every message
 * on every commit: device profiling counted 560 IconTrash and 280 IconBookmark
 * renders during a single scroll. It only actually changes when this message's
 * tray opens or its bookmark state moves.
 */
const MessageSwipeTray: React.FC<MessageSwipeTrayProps> = ({
  messageId,
  isUser,
  isAssistant,
  isOpen,
  isBookmarked,
  canBookmark,
  t,
  onBookmark,
  onDelete,
}) => (
  <div
    className={`absolute ${isUser ? 'right-0' : 'left-0'} top-1/2 -translate-y-1/2 flex flex-col items-center gap-2`}
    style={{
      width: 56,
      zIndex: 50,
      pointerEvents: isOpen ? 'auto' : 'none',
      opacity: isOpen ? 1 : 0,
      transform: `translateY(-50%) ${isOpen ? 'translateX(0)' : `translateX(${isUser ? '8px' : '-8px'})`}`,
      transition: 'opacity 120ms ease, transform 120ms ease',
      touchAction: 'none',
    }}
    onPointerDown={(e) => { e.stopPropagation(); }}
    aria-hidden={isOpen ? undefined : true}
  >
    {isAssistant && (isBookmarked || canBookmark) && (
      <button
        className="p-2 bg-save-sugg-bg text-save-sugg-text shadow sketchy-border-thin"
        onPointerDown={(e) => { e.stopPropagation(); }}
        onClick={(e) => { e.stopPropagation(); if (!isBookmarked) onBookmark(messageId); }}
        title={isBookmarked ? (t('chat.bookmark.isHere') || 'Bookmark is here') : (t('chat.bookmark.setHere') || 'Set bookmark here')}
        aria-pressed={isBookmarked}
      >
        <IconBookmark className={`w-5 h-5 ${isBookmarked ? 'opacity-100' : 'opacity-90'}`} />
      </button>
    )}
    <button
      className="p-2 bg-delete-msg-bg text-delete-msg-text shadow sketchy-border-thin"
      onPointerDown={(e) => { e.stopPropagation(); }}
      onClick={(e) => { e.stopPropagation(); onDelete(messageId); }}
      title={t('chat.deleteMessage') || 'Delete message'}
    >
      <IconTrash className="w-5 h-5" />
    </button>
  </div>
);

export default React.memo(MessageSwipeTray);
