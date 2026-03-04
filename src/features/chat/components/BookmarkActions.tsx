// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useState } from 'react';
import { ChatMessage } from '../../../core/types';
import { IconPencil, IconCheck, IconXMark } from '../../../shared/ui/Icons';

interface BookmarkActionsProps {
  t: (k: string, vars?: any) => string;
  message: ChatMessage;
  maxVisibleMessages: number;
  onChangeMaxVisibleMessages: (n: number) => void;
  updateMessage?: (id: string, updates: Partial<ChatMessage>) => void;
}

const BookmarkActions: React.FC<BookmarkActionsProps> = ({ t, message, maxVisibleMessages, onChangeMaxVisibleMessages, updateMessage }) => {
  const [isEditing, setIsEditing] = useState(false);
  const [summaryText, setSummaryText] = useState('');

  const startEditing = (e: React.MouseEvent) => {
    e.stopPropagation();
    e.preventDefault();
    setSummaryText(message.chatSummary || '');
    setIsEditing(true);
  };

  const save = (e?: React.MouseEvent) => {
    e?.stopPropagation();
    e?.preventDefault();
    if (updateMessage) {
      updateMessage(message.id, { chatSummary: summaryText.trim() || undefined });
    }
    setIsEditing(false);
  };

  const cancel = (e: React.MouseEvent) => {
    e.stopPropagation();
    e.preventDefault();
    setIsEditing(false);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    e.stopPropagation();
    if (e.key === 'Enter') {
      e.preventDefault();
      save();
    } else if (e.key === 'Escape') {
      e.preventDefault();
      setIsEditing(false);
    }
  };

  const dec = (e: React.MouseEvent) => {
    e.stopPropagation();
    const n = Math.max(2, Math.min(100, (maxVisibleMessages || 2) - 2));
    if (n !== maxVisibleMessages) onChangeMaxVisibleMessages(n);
  };
  const inc = (e: React.MouseEvent) => {
    e.stopPropagation();
    const n = Math.max(2, Math.min(100, (maxVisibleMessages || 2) + 2));
    if (n !== maxVisibleMessages) onChangeMaxVisibleMessages(n);
  };

  const containerClasses = "flex items-center gap-1 px-1.5 py-0.5 bg-bookmark-bg/90 text-bookmark-text sketchy-border-thin shadow-sm";

  if (isEditing) {
    return (
      <div className={containerClasses} role="region" aria-label="Edit bookmark summary" onPointerDown={e => e.stopPropagation()} onClick={e => e.stopPropagation()}>
        <input
          type="text"
          value={summaryText}
          onChange={(e) => setSummaryText(e.target.value)}
          onKeyDown={handleKeyDown}
          className="bg-bookmark-input-bg/80 border border-bookmark-divider/30 sketch-shape-0 px-2 py-0.5 text-xs text-bookmark-input-text placeholder-bookmark-text/60 focus:outline-none focus:border-bookmark-bg focus:bg-bookmark-input-bg w-48 transition-colors font-hand"
          placeholder="Summary..."
          autoFocus
        />
        <button
          type="button"
          onClick={save}
          className="p-1 sketch-shape-2 hover:bg-bookmark-bg/70 text-bookmark-text transition-colors"
          title="Save"
        >
          <IconCheck className="w-3.5 h-3.5" />
        </button>
        <button
          type="button"
          onClick={cancel}
          className="p-1 sketch-shape-4 hover:bg-bookmark-bg/70 text-bookmark-text transition-colors"
          title="Cancel"
        >
          <IconXMark className="w-3.5 h-3.5" />
        </button>
      </div>
    );
  }

  return (
    <div
      className={containerClasses}
      role="region"
      aria-label={t('chat.bookmark.actionsRegionAria') || 'Bookmark actions'}
      onPointerDown={e => e.stopPropagation()}
      onClick={e => e.stopPropagation()}
    >
      <button
        type="button"
        onClick={dec}
        disabled={maxVisibleMessages <= 2}
        className="px-2 py-0.5 text-xs font-hand sketch-shape-6 hover:bg-bookmark-bg/50 disabled:opacity-50"
        aria-label={t('chat.bookmark.decrementAria') || 'Decrease maximum visible messages'}
        title={t('chat.bookmark.decrementTitle') || 'Decrease'}
      >
        âˆ’
      </button>
      <span
        className="px-2 py-0.5 text-xs font-semibold font-hand select-none min-w-[2.5rem] text-center"
        aria-live="polite"
        aria-atomic="true"
      >
        {maxVisibleMessages}
      </span>
      <button
        type="button"
        onClick={inc}
        disabled={maxVisibleMessages >= 100}
        className="px-2 py-0.5 text-xs font-hand sketch-shape-8 hover:bg-bookmark-bg/50 disabled:opacity-50"
        aria-label={t('chat.bookmark.incrementAria') || 'Increase maximum visible messages'}
        title={t('chat.bookmark.incrementTitle') || 'Increase'}
      >
        +
      </button>
      <div className="w-px h-4 bg-bookmark-divider/40 mx-1" aria-hidden />
      <button
        type="button"
        onClick={startEditing}
        className="px-1.5 py-0.5 sketch-shape-10 hover:bg-bookmark-bg/50"
        title="Edit bookmark summary"
        aria-label="Edit bookmark summary"
      >
        <IconPencil className="w-3.5 h-3.5" />
      </button>
    </div>
  );
};

export default BookmarkActions;

