// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useEffect, useState } from 'react';
import AttachmentInteractionToggle from './AttachmentInteractionToggle';

interface NotebookTextPreviewProps {
  title: string;
  text: string;
  subtitle?: string | null;
  compact?: boolean;
  bottomInset?: number;
  wrapText?: boolean;
  footer?: React.ReactNode;
}

const NotebookTextPreview: React.FC<NotebookTextPreviewProps> = React.memo(({
  title,
  text,
  subtitle = null,
  compact = false,
  bottomInset = 0,
  wrapText = false,
  footer = null,
}) => {
  const [isAttachmentScrollEnabled, setIsAttachmentScrollEnabled] = useState(false);
  const effectiveBottomInset = !compact ? Math.max(0, Math.round(bottomInset)) : 0;

  useEffect(() => {
    setIsAttachmentScrollEnabled(false);
  }, [text, title]);

  const scrollStyle: React.CSSProperties = compact
    ? {
        maxHeight: '5.25rem',
      }
    : {
        maxHeight: '60vh',
        overflowX: isAttachmentScrollEnabled ? 'auto' : 'hidden',
        overflowY: isAttachmentScrollEnabled ? 'auto' : 'hidden',
        overscrollBehavior: isAttachmentScrollEnabled ? 'contain' : 'auto',
        touchAction: isAttachmentScrollEnabled ? (wrapText ? 'pan-y' : 'pan-x pan-y') : 'pan-y',
        WebkitOverflowScrolling: 'touch' as any,
        scrollPaddingBottom: `${effectiveBottomInset}px`,
      };
  const compactScrollClass = compact ? 'overflow-x-auto overflow-y-auto' : '';
  const textLayoutClass = wrapText ? 'whitespace-pre-wrap break-words' : 'whitespace-pre w-max min-w-full';
  const textSizeClass = compact ? 'text-[11px] leading-4' : 'text-[12px] leading-5';

  return (
    <div
      className={`notebook-attachment-paper paper-texture notebook-lines sketch-shape-4 w-full max-w-full min-w-0 overflow-hidden ${compact ? 'px-2 py-1.5' : 'px-3 py-2'}`}
      style={compact ? { contain: 'inline-size' } : undefined}
    >
      <div className={`flex items-start justify-between gap-3 ${compact ? '' : 'px-0.5'}`}>
        <div className="min-w-0 flex-1">
          <p className={`truncate font-architect font-semibold text-deep-ink ${compact ? 'text-[11px]' : 'text-[14px]'}`}>
            {title}
          </p>
          {subtitle ? (
            <p className={`truncate font-semibold text-deep-ink/85 ${compact ? 'text-[10px]' : 'text-xs'}`}>
              {subtitle}
            </p>
          ) : null}
        </div>
        {!compact && (
          <AttachmentInteractionToggle
            isAttachmentModeEnabled={isAttachmentScrollEnabled}
            attachmentLabel="File scroll"
            attachmentTitle="Scroll attachment"
            groupLabel="Text attachment interaction mode"
            onToggle={() => setIsAttachmentScrollEnabled((prev) => !prev)}
          />
        )}
      </div>

      <div
        className={`notebook-attachment-scroll w-full max-w-full min-w-0 ${compactScrollClass} ${compact ? 'mt-1' : 'mt-2 border-t border-sketch-line/20'}`}
        style={scrollStyle}
        onWheel={isAttachmentScrollEnabled ? (event) => event.stopPropagation() : undefined}
        onTouchMove={isAttachmentScrollEnabled ? (event) => event.stopPropagation() : undefined}
      >
        <pre
          className={`notebook-attachment-pre ${compact ? 'py-1' : 'py-2'} ${textSizeClass} ${textLayoutClass}`}
          style={!compact && effectiveBottomInset > 0 ? { paddingBottom: `calc(0.5rem + ${effectiveBottomInset}px)` } : undefined}
        >
          {text}
        </pre>
      </div>

      {footer ? (
        <div className="mt-1 text-xs text-sketch-line">
          {footer}
        </div>
      ) : null}
    </div>
  );
});

NotebookTextPreview.displayName = 'NotebookTextPreview';

export default NotebookTextPreview;
