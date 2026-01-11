import React from 'react';
import { ChatMessage } from '../../../types';
import { IconPencil } from '../../../constants';

interface BookmarkActionsProps {
  t: (k: string, vars?: any) => string;
  message: ChatMessage;
  maxVisibleMessages: number;
  onChangeMaxVisibleMessages: (n: number) => void;
  updateMessage?: (id: string, updates: Partial<ChatMessage>) => void;
}

const BookmarkActions: React.FC<BookmarkActionsProps> = ({ t, message, maxVisibleMessages, onChangeMaxVisibleMessages, updateMessage }) => {
  const onEditSummary = React.useCallback(async () => {
    if (!updateMessage) return;
    try {
      const current = (message.chatSummary || '').trim();
      const next = window.prompt('Edit bookmark summary', current);
      if (next !== null) {
        const trimmed = next.trim();
        updateMessage(message.id, { chatSummary: trimmed || undefined });
      }
    } catch {}
  }, [message.id, message.chatSummary, updateMessage]);

  const dec = () => {
    const n = Math.max(2, Math.min(100, (maxVisibleMessages || 2) - 2));
    if (n !== maxVisibleMessages) onChangeMaxVisibleMessages(n);
  };
  const inc = () => {
    const n = Math.max(2, Math.min(100, (maxVisibleMessages || 2) + 2));
    if (n !== maxVisibleMessages) onChangeMaxVisibleMessages(n);
  };

  return (
    <div
      className="flex items-center gap-1 px-1.5 py-0.5 bg-amber-500/90 text-white border border-amber-400 rounded-full shadow-md"
      role="region"
      aria-label={t('chat.bookmark.actionsRegionAria') || 'Bookmark actions'}
    >
      <button
        type="button"
        onClick={dec}
        disabled={maxVisibleMessages <= 2}
        className="px-2 py-0.5 text-xs rounded-full hover:bg-amber-400/30 disabled:opacity-50"
        aria-label={t('chat.bookmark.decrementAria') || 'Decrease maximum visible messages'}
        title={t('chat.bookmark.decrementTitle') || 'Decrease'}
      >
        −
      </button>
      <span
        className="px-2 py-0.5 text-xs font-semibold select-none min-w-[2.5rem] text-center"
        aria-live="polite"
        aria-atomic="true"
      >
        {maxVisibleMessages}
      </span>
      <button
        type="button"
        onClick={inc}
        disabled={maxVisibleMessages >= 100}
        className="px-2 py-0.5 text-xs rounded-full hover:bg-amber-400/30 disabled:opacity-50"
        aria-label={t('chat.bookmark.incrementAria') || 'Increase maximum visible messages'}
        title={t('chat.bookmark.incrementTitle') || 'Increase'}
      >
        +
      </button>
      <div className="w-px h-4 bg-white/40 mx-1" aria-hidden />
      <button
        type="button"
        onClick={onEditSummary}
        className="px-1.5 py-0.5 rounded-full hover:bg-amber-400/30"
        title="Edit bookmark summary"
        aria-label="Edit bookmark summary"
      >
        <IconPencil className="w-3.5 h-3.5" />
      </button>
    </div>
  );
};

export default BookmarkActions;
