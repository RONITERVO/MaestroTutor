// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useEffect, useMemo, useState } from 'react';
import type { AiContentReportReason } from '../../../core/contracts/backend';
import type { ChatMessage } from '../../../core/types';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { IconFlag, IconXMark } from '../../../shared/ui/Icons';

interface AiContentReportModalProps {
  isOpen: boolean;
  message: ChatMessage | null;
  isSubmitting: boolean;
  errorMessage: string | null;
  onClose: () => void;
  onSubmit: (payload: { reason: AiContentReportReason; notes: string }) => Promise<void> | void;
}

const REPORT_REASONS: AiContentReportReason[] = [
  'sexual',
  'hate',
  'harassment',
  'self-harm',
  'violent',
  'deceptive',
  'spam',
  'other',
];

const buildPreviewText = (message: ChatMessage | null): string => {
  if (!message) return '';
  if (message.text?.trim()) return message.text.trim();
  if (message.translations?.length) {
    return message.translations
      .map((pair) => [pair.target, pair.native].filter(Boolean).join(' / '))
      .filter(Boolean)
      .join('\n');
  }
  if (message.rawAssistantResponse?.trim()) return message.rawAssistantResponse.trim();
  if (message.llmRawResponse?.trim()) return message.llmRawResponse.trim();
  if (message.maestroToolKind === 'image') return '[image response]';
  if (message.maestroToolKind === 'music') return '[music response]';
  if (message.maestroToolKind === 'audio-note') return '[audio response]';
  return '';
};

const AiContentReportModal: React.FC<AiContentReportModalProps> = ({
  isOpen,
  message,
  isSubmitting,
  errorMessage,
  onClose,
  onSubmit,
}) => {
  const { t } = useAppTranslations();
  const [reason, setReason] = useState<AiContentReportReason>('other');
  const [notes, setNotes] = useState('');

  useEffect(() => {
    if (!isOpen) return;
    setReason('other');
    setNotes('');
  }, [isOpen, message?.id]);

  const previewText = useMemo(() => buildPreviewText(message), [message]);

  if (!isOpen || !message) return null;

  return (
    <div className="fixed inset-0 z-[120] flex items-center justify-center bg-black/45 px-4 py-6">
      <div className="w-full max-w-lg bg-paper-surface text-page-text shadow-xl sketchy-border-thin">
        <div className="flex items-center justify-between border-b border-line-border px-4 py-3">
          <div className="flex items-center gap-2">
            <IconFlag className="h-5 w-5 text-red-700" />
            <div className="font-medium font-sketch">{t('chat.report.title')}</div>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={isSubmitting}
            className="p-1 text-page-text/70 hover:text-page-text"
            aria-label={t('chat.report.close')}
          >
            <IconXMark className="h-5 w-5" />
          </button>
        </div>

        <div className="space-y-4 px-4 py-4">
          <div className="space-y-1">
            <label className="block text-sm font-medium text-page-text" htmlFor="ai-report-reason">
              {t('chat.report.reasonLabel')}
            </label>
            <select
              id="ai-report-reason"
              value={reason}
              onChange={(event) => setReason(event.target.value as AiContentReportReason)}
              className="w-full border border-line-border bg-page-bg px-3 py-2 text-sm outline-none"
              disabled={isSubmitting}
            >
              {REPORT_REASONS.map((item) => (
                <option key={item} value={item}>
                  {t(`chat.report.reason.${item}`)}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-1">
            <div className="text-sm font-medium text-page-text">{t('chat.report.previewLabel')}</div>
            <div className="max-h-40 overflow-y-auto whitespace-pre-wrap break-words border border-line-border bg-page-bg px-3 py-2 text-xs text-page-text/80">
              {previewText || t('chat.report.noPreview')}
            </div>
          </div>

          <div className="space-y-1">
            <label className="block text-sm font-medium text-page-text" htmlFor="ai-report-notes">
              {t('chat.report.notesLabel')}
            </label>
            <textarea
              id="ai-report-notes"
              value={notes}
              onChange={(event) => setNotes(event.target.value)}
              className="min-h-24 w-full resize-y border border-line-border bg-page-bg px-3 py-2 text-sm outline-none"
              placeholder={t('chat.report.notesPlaceholder')}
              disabled={isSubmitting}
            />
          </div>

          {errorMessage && (
            <div className="rounded-md border border-red-300 bg-red-50 px-3 py-2 text-xs text-red-800">
              {errorMessage}
            </div>
          )}

          <div className="flex flex-wrap justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              disabled={isSubmitting}
              className="px-3 py-2 text-page-text hover:bg-page-bg sketchy-border-thin"
            >
              {t('chat.report.cancel')}
            </button>
            <button
              type="button"
              onClick={() => void onSubmit({ reason, notes })}
              disabled={isSubmitting}
              className="bg-red-700 px-3 py-2 text-white hover:bg-red-800 disabled:opacity-60 sketchy-border-thin"
            >
              {isSubmitting ? t('chat.report.submitting') : t('chat.report.submit')}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default AiContentReportModal;
