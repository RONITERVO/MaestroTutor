// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useMemo } from 'react';
import { IconPaperclip } from '../../../shared/ui/Icons';
import { decodeTextFromDataUrl } from '../utils/fileAttachments';
import TabularPreview from './TabularPreview';
import { deriveChartSeriesListFromRows, parseDelimitedText } from '../utils/tabularPreview';
import MiniGameViewer from './MiniGameViewer';
import { isRunnableMiniGameAttachment } from '../utils/miniGameAttachment';

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
  const decodedText = useMemo(() => decodeTextFromDataUrl(src), [src]);
  const fileExt = useMemo(() => {
    const name = (fileName || '').trim().toLowerCase();
    const dotIndex = name.lastIndexOf('.');
    if (dotIndex < 0 || dotIndex >= name.length - 1) return '';
    return name.slice(dotIndex + 1);
  }, [fileName]);
  const isTabularTextFile = useMemo(() => {
    const normalizedMime = (mimeType || '').trim().toLowerCase();
    return (
      normalizedMime.includes('csv') ||
      fileExt === 'csv' ||
      fileExt === 'tsv'
    );
  }, [mimeType, fileExt]);
  const shouldRenderMiniGame = useMemo(() => {
    if (compact || !decodedText) return false;
    return isRunnableMiniGameAttachment({
      sourceCode: decodedText,
      fileName,
      mimeType,
    });
  }, [compact, decodedText, fileName, mimeType]);
  const tabularRows = useMemo(() => {
    if (!decodedText || !isTabularTextFile) return [];
    return parseDelimitedText(decodedText, fileExt === 'tsv' ? '\t' : undefined);
  }, [decodedText, isTabularTextFile, fileExt]);
  const chartSeriesList = useMemo(() => deriveChartSeriesListFromRows(tabularRows), [tabularRows]);
  const tabularSheets = useMemo(() => {
    if (!isTabularTextFile || tabularRows.length <= 1) return [];
    return [{
      name: fileName || 'Sheet 1',
      rows: tabularRows,
      chartSeriesList,
    }];
  }, [isTabularTextFile, tabularRows, chartSeriesList, fileName]);
  const previewSnippet = useMemo(() => {
    if (!decodedText) return '';
    if (decodedText.length <= 3200) return decodedText;
    return `${decodedText.slice(0, 3200)}\n...`;
  }, [decodedText]);

  const isUser = variant === 'user';
  const containerBg = isUser ? 'bg-user-msg-bg/20' : 'bg-ai-file-bg';
  const headerBg = isUser ? 'bg-user-msg-bg/40' : 'bg-ai-msg-bg/60';
  const textColor = isUser ? 'text-user-msg-text' : 'text-ai-file-text';
  const subtleText = isUser ? 'text-user-msg-text/70' : 'text-ai-file-text';
  const metaLabel = fileName || mimeType || 'text attachment';
  const effectiveBottomInset = !compact ? Math.max(0, Math.round(bottomInset)) : 0;

  if (!decodedText) {
    return (
      <div className={`flex flex-col items-center justify-center rounded-lg ${containerBg} ${compact ? 'h-24 w-full' : 'h-48 w-full'}`}>
        <IconPaperclip className={`w-8 h-8 ${textColor}`} />
        <p className={`mt-2 text-xs ${textColor}`}>Unable to decode text attachment.</p>
      </div>
    );
  }

  if (shouldRenderMiniGame) {
    return (
      <MiniGameViewer
        sourceCode={decodedText}
        variant={variant}
        fileName={fileName}
        mimeType={mimeType}
        bottomInset={effectiveBottomInset}
      />
    );
  }

  if (compact) {
    return (
      <div className={`w-full max-w-full min-w-0 rounded-lg overflow-hidden ${containerBg}`} style={{ contain: 'inline-size' }}>
        <div className={`px-2 py-1 text-[10px] font-mono truncate ${headerBg} ${textColor}`}>
          {metaLabel}
        </div>
        {tabularSheets.length > 0 ? (
          <div className="px-2 pb-2">
            <TabularPreview
              sheets={tabularSheets}
              textColorClass={textColor}
              subtleTextClass={subtleText}
              compact
            />
          </div>
        ) : (
          <div
            className="w-full max-w-full min-w-0 overflow-x-auto overflow-y-scroll"
            style={{
              maxHeight: '5.25rem',
              scrollbarGutter: 'stable',
              overscrollBehavior: 'contain',
              touchAction: 'pan-y',
              WebkitOverflowScrolling: 'touch' as any,
            }}
          >
            <pre className={`p-2 text-[10px] leading-4 font-mono whitespace-pre w-max min-w-full ${textColor}`}>
              {decodedText}
            </pre>
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="relative w-full max-w-full min-w-0 overflow-hidden">
      <div
        className={`w-full max-w-full min-w-0 overflow-x-auto overflow-y-scroll rounded-lg ${containerBg}`}
        style={{
          maxHeight: '60vh',
          scrollbarGutter: 'stable',
          overscrollBehavior: 'contain',
          touchAction: 'pan-y',
          WebkitOverflowScrolling: 'touch' as any,
          scrollPaddingBottom: `${effectiveBottomInset}px`,
        }}
      >
        <div className={`sticky top-0 z-10 px-3 py-1.5 text-[11px] font-mono truncate ${headerBg} ${textColor}`}>
          {metaLabel}
        </div>
        <div
          className="p-3"
          style={effectiveBottomInset > 0 ? { paddingBottom: `calc(0.75rem + ${effectiveBottomInset}px)` } : undefined}
        >
          {tabularSheets.length > 0 ? (
            <>
              <TabularPreview
                sheets={tabularSheets}
                textColorClass={textColor}
                subtleTextClass={subtleText}
              />
              <details className="mt-3">
                <summary className={`text-xs cursor-pointer ${subtleText}`}>Raw text</summary>
                <div
                  className="mt-1 max-h-72 overflow-auto rounded border border-black/10 bg-black/5"
                  style={{ overscrollBehavior: 'contain', touchAction: 'pan-y' }}
                >
                  <pre className={`p-2 text-[11px] leading-5 font-mono whitespace-pre w-max min-w-full ${textColor}`}>
                    {previewSnippet}
                  </pre>
                </div>
              </details>
            </>
          ) : (
            <pre className={`text-[11px] leading-5 font-mono whitespace-pre w-max min-w-full ${textColor}`}>
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
