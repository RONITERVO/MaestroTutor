// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useMemo } from 'react';
import { IconPaperclip } from '../../../shared/ui/Icons';
import { decodeTextFromDataUrl } from '../utils/fileAttachments';
import TabularPreview from './TabularPreview';
import { deriveTabularSheetsFromTextAttachment } from '../utils/tabularPreview';
import MiniGameViewer from './MiniGameViewer';
import MiniGameErrorBoundary from './MiniGameErrorBoundary';
import { isRunnableMiniGameAttachment } from '../utils/miniGameAttachment';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';

interface TextFileViewerProps {
  src: string;
  variant: 'user' | 'assistant' | 'preview';
  compact?: boolean;
  fileName?: string | null;
  mimeType?: string | null;
  bottomInset?: number;
}

const TextFileViewer: React.FC<TextFileViewerProps> = React.memo(({
  src,
  variant,
  compact = false,
  fileName,
  mimeType,
  bottomInset = 0,
}) => {
  const { t } = useAppTranslations();
  const decodedText = useMemo(() => decodeTextFromDataUrl(src), [src]);
  const shouldRenderMiniGame = useMemo(() => {
    if (compact || !decodedText) return false;
    return isRunnableMiniGameAttachment({
      sourceCode: decodedText,
      fileName,
      mimeType,
    });
  }, [compact, decodedText, fileName, mimeType]);
  const tabularSheets = useMemo(
    () => deriveTabularSheetsFromTextAttachment(decodedText, mimeType, fileName),
    [decodedText, fileName, mimeType]
  );
  const previewSnippet = useMemo(() => {
    if (!decodedText) return '';
    if (decodedText.length <= 3200) return decodedText;
    return `${decodedText.slice(0, 3200)}\n...`;
  }, [decodedText]);

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
          sourceCode={decodedText}
          variant={variant}
          fileName={fileName}
          mimeType={mimeType}
          bottomInset={effectiveBottomInset}
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
      <div className="notebook-attachment-paper paper-texture notebook-lines sketch-shape-4 w-full max-w-full min-w-0 overflow-hidden px-2 py-1.5" style={{ contain: 'inline-size' }}>
        <div className={`truncate font-architect text-[11px] font-semibold ${textColor}`}>
          {metaLabel}
        </div>
        <div
          className="notebook-attachment-scroll w-full max-w-full min-w-0 overflow-x-auto overflow-y-scroll"
          style={{
            maxHeight: '5.25rem',
          }}
        >
          <pre className="notebook-attachment-pre py-1 text-[11px] leading-4 whitespace-pre w-max min-w-full">
            {decodedText}
          </pre>
        </div>
      </div>
    );
  }

  return (
    <div className="relative w-full max-w-full min-w-0 overflow-hidden">
      <div
        className="notebook-attachment-paper paper-texture notebook-lines sketch-shape-4 w-full max-w-full min-w-0 overflow-hidden px-3 py-2"
        style={{
          scrollPaddingBottom: `${effectiveBottomInset}px`,
        }}
      >
        <div className={`truncate font-architect text-[14px] font-semibold ${textColor}`}>
          {metaLabel}
        </div>
        <div
          className="notebook-attachment-scroll w-full max-w-full min-w-0 overflow-x-auto overflow-y-scroll"
          style={{
            maxHeight: '60vh',
          }}
        >
          {tabularSheets.length > 0 ? (
            <>
              <TabularPreview
                sheets={tabularSheets}
                textColorClass={textColor}
                subtleTextClass={subtleText}
                surfaceClassName="bg-paper-surface/85"
                panelSurfaceClassName="bg-paper-stripe/35"
              />
              <details className="mt-3">
                <summary className={`text-xs cursor-pointer ${subtleText}`}>{t('textFile.rawText') || 'Raw text'}</summary>
                <div
                  className="notebook-attachment-scroll mt-1 max-h-72 overflow-auto border-t border-sketch-line/20"
                  style={{ overscrollBehavior: 'contain', touchAction: 'pan-y' }}
                >
                  <pre className="notebook-attachment-pre py-2 text-[11px] leading-5 whitespace-pre w-max min-w-full">
                    {previewSnippet}
                  </pre>
                </div>
              </details>
            </>
          ) : (
            <pre
              className="notebook-attachment-pre py-2 text-[12px] leading-5 whitespace-pre w-max min-w-full"
              style={effectiveBottomInset > 0 ? { paddingBottom: `calc(0.5rem + ${effectiveBottomInset}px)` } : undefined}
            >
              {decodedText}
            </pre>
          )}
        </div>
      </div>
    </div>
  );
});

TextFileViewer.displayName = 'TextFileViewer';

export default TextFileViewer;
