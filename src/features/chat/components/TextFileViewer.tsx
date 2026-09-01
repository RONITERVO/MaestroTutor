// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useMemo } from 'react';
import { IconPaperclip } from '../../../shared/ui/Icons';
import { decodeTextFromDataUrl } from '../../../core-sdk/chat/fileAttachments';
import TabularPreview from './TabularPreview';
import { deriveTabularSheetsFromTextAttachment } from '../utils/tabularPreview';
import MiniGameViewer from './MiniGameViewer';
import MiniGameErrorBoundary from './MiniGameErrorBoundary';
import NotebookTextPreview from './NotebookTextPreview';
import { isRunnableMiniGameAttachment } from '../utils/miniGameAttachment';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import type { EmbedBox } from '../../../core/types';

interface TextFileViewerProps {
  src: string;
  variant: 'user' | 'assistant' | 'preview';
  compact?: boolean;
  fileName?: string | null;
  mimeType?: string | null;
  bottomInset?: number;
  /** Stable identity for the embed activation slot — normally the message id. */
  embedId?: string;
  /** Reserved box persisted on the message, when the attachment is runnable. */
  embedBox?: EmbedBox;
  onEmbedBoxChange?: (box: EmbedBox) => void;
}

const TextFileViewer: React.FC<TextFileViewerProps> = React.memo(({
  src,
  variant,
  compact = false,
  fileName,
  mimeType,
  bottomInset = 0,
  embedId,
  embedBox,
  onEmbedBoxChange,
}) => {
  const { t } = useAppTranslations();
  const decodedText = useMemo(() => decodeTextFromDataUrl(src), [src]);
  const shouldRenderMiniGame = useMemo(() => {
    // Without a stable id the activation manager cannot arbitrate this embed,
    // so it would be the one iframe that escapes the budget. Fall through to
    // the static source preview instead.
    if (compact || !decodedText || !embedId) return false;
    return isRunnableMiniGameAttachment({
      sourceCode: decodedText,
      fileName,
      mimeType,
    });
  }, [compact, decodedText, embedId, fileName, mimeType]);
  const tabularSheets = useMemo(
    () => deriveTabularSheetsFromTextAttachment(decodedText, mimeType, fileName),
    [decodedText, fileName, mimeType]
  );
  const textColor = 'text-deep-ink';
  const subtleText = 'text-sketch-line';
  const metaLabel = fileName || mimeType || 'text attachment';
  const effectiveBottomInset = !compact ? Math.max(0, Math.round(bottomInset)) : 0;

  if (!decodedText) {
    return (
      <div className={`notebook-attachment-paper paper-texture notebook-lines sketch-shape-4 flex flex-col items-center justify-center overflow-hidden ${compact ? 'h-24 w-full' : 'h-48 w-full'}`}>
        <IconPaperclip className={`w-8 h-8 ${textColor}`} />
        <p className={`mt-2 text-xs ${subtleText}`}>{t('textFile.unableToDecode') || 'Unable to decode text attachment.'}</p>
      </div>
    );
  }

  if (shouldRenderMiniGame) {
    return (
      <MiniGameErrorBoundary
        failedText={t('miniGame.failedToRender') || 'Mini-game failed to render.'}
        retryText={t('miniGame.retry') || 'Retry'}
      >
        <MiniGameViewer
          embedId={embedId!}
          sourceCode={decodedText}
          variant={variant}
          fileName={fileName}
          mimeType={mimeType}
          bottomInset={effectiveBottomInset}
          embedBox={embedBox}
          onEmbedBoxChange={onEmbedBoxChange}
        />
      </MiniGameErrorBoundary>
    );
  }

  if (!compact && tabularSheets.length > 0) {
    return (
      <div className="relative w-full max-w-full min-w-0">
          <TabularPreview
            sheets={tabularSheets}
            textColorClass={textColor}
            subtleTextClass={subtleText}
            title={metaLabel}
            standalone
            bottomInset={effectiveBottomInset}
            surfaceClassName="bg-paper-surface/85"
            panelSurfaceClassName="bg-paper-stripe/35"
        />
      </div>
    );
  }

  if (compact) {
    if (tabularSheets.length > 0) {
      return (
        <div className="w-full max-w-full min-w-0 overflow-hidden" style={{ contain: 'inline-size' }}>
          <TabularPreview
            sheets={tabularSheets}
            textColorClass={textColor}
            subtleTextClass={subtleText}
            compact
            title={metaLabel}
            surfaceClassName="bg-paper-surface/85"
            panelSurfaceClassName="bg-paper-stripe/35"
          />
        </div>
      );
    }

    return (
      <NotebookTextPreview
        title={metaLabel}
        text={decodedText}
        compact
      />
    );
  }

  return (
    <div className="relative w-full max-w-full min-w-0 overflow-hidden">
      <NotebookTextPreview
        title={metaLabel}
        text={decodedText}
        bottomInset={effectiveBottomInset}
      />
    </div>
  );
});

TextFileViewer.displayName = 'TextFileViewer';

export default TextFileViewer;
