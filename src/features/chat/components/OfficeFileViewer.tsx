// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useMemo } from 'react';
import { IconPaperclip } from '../../../shared/ui/Icons';
import { SmallSpinner } from '../../../shared/ui/SmallSpinner';
import TabularPreview from './TabularPreview';
import NotebookTextPreview from './NotebookTextPreview';
import {
  extractGoogleWorkspaceUrlFromDataUrl,
  isGoogleWorkspaceShortcutFileName,
  isGoogleWorkspaceShortcutMimeType,
} from '../utils/fileAttachments';
import { getOfficePreview } from '../utils/officePreview';
import { isAttachmentOpenCancelError, openAttachmentFile } from '../utils/openAttachmentFile';
import type { TabularChartSeries, TabularSheetPreview } from '../utils/tabularPreview';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';

interface OfficeFileViewerProps {
  src?: string | null;
  variant: 'user' | 'assistant' | 'preview';
  compact?: boolean;
  fileName?: string | null;
  mimeType?: string | null;
  hasRemoteUri?: boolean;
  bottomInset?: number;
}

const WORD_EXTENSIONS = new Set(['doc', 'docx', 'docm', 'dot', 'dotx', 'dotm', 'rtf']);
const EXCEL_EXTENSIONS = new Set(['xls', 'xlsx', 'xlsm', 'xlsb', 'xltx', 'xltm']);
const POWERPOINT_EXTENSIONS = new Set(['ppt', 'pptx', 'pptm', 'pps', 'ppsx', 'pot', 'potx']);
const OPENDOCUMENT_EXTENSIONS = new Set(['odt', 'ods', 'odp']);

const getExtension = (fileName?: string | null): string => {
  const name = (fileName || '').trim().toLowerCase();
  const dotIndex = name.lastIndexOf('.');
  if (dotIndex < 0 || dotIndex >= name.length - 1) return '';
  return name.slice(dotIndex + 1);
};

const getOfficeLabel = (mimeType?: string | null, fileName?: string | null): string => {
  const mime = (mimeType || '').trim().toLowerCase();
  const ext = getExtension(fileName);

  if (
    ext === 'gdoc' ||
    mime.includes('google-apps.document')
  ) return 'Google Docs';
  if (
    ext === 'gsheet' ||
    mime.includes('google-apps.spreadsheet')
  ) return 'Google Sheets';
  if (
    ext === 'gslides' ||
    mime.includes('google-apps.presentation')
  ) return 'Google Slides';

  if (
    WORD_EXTENSIONS.has(ext) ||
    mime === 'application/msword' ||
    mime.includes('wordprocessingml') ||
    mime.includes('ms-word') ||
    mime.includes('rtf')
  ) return 'Microsoft Word';

  if (
    EXCEL_EXTENSIONS.has(ext) ||
    mime.includes('spreadsheetml') ||
    mime.includes('ms-excel')
  ) return 'Microsoft Excel';

  if (
    POWERPOINT_EXTENSIONS.has(ext) ||
    mime.includes('presentationml') ||
    mime.includes('ms-powerpoint')
  ) return 'Microsoft PowerPoint';

  if (
    OPENDOCUMENT_EXTENSIONS.has(ext) ||
    mime.includes('opendocument')
  ) return 'OpenDocument';

  return 'Office document';
};

const OfficeFileViewer: React.FC<OfficeFileViewerProps> = React.memo(({
  src,
  compact = false,
  fileName,
  mimeType,
  hasRemoteUri = false,
  bottomInset = 0,
}) => {
  const { t } = useAppTranslations();
  const [previewText, setPreviewText] = React.useState<string | null>(null);
  const [previewNote, setPreviewNote] = React.useState<string | null>(null);
  const [previewSheets, setPreviewSheets] = React.useState<TabularSheetPreview[]>([]);
  const [isParsingPreview, setIsParsingPreview] = React.useState(false);
  const [isOpeningFile, setIsOpeningFile] = React.useState(false);

  const textColor = 'text-deep-ink';
  const subtleText = 'text-sketch-line';

  const metaLabel = fileName || mimeType || 'office attachment';
  const attachmentLabel = getOfficeLabel(mimeType, fileName);
  const effectiveBottomInset = !compact ? Math.max(0, Math.round(bottomInset)) : 0;

  const googleWorkspaceLink = useMemo(() => {
    const isGoogleShortcut = isGoogleWorkspaceShortcutFileName(fileName) || isGoogleWorkspaceShortcutMimeType(mimeType);
    if (!isGoogleShortcut) return null;
    return extractGoogleWorkspaceUrlFromDataUrl(src);
  }, [fileName, mimeType, src]);

  const openHref = googleWorkspaceLink || src || null;
  const openLabel = googleWorkspaceLink ? t('officeFile.openInGoogleWorkspace') || 'Open in Google Workspace' : t('officeFile.openFile') || 'Open file';
  const downloadName = fileName || 'office-attachment';

  React.useEffect(() => {
    let cancelled = false;

    if (!src) {
      setPreviewText(null);
      setPreviewNote(hasRemoteUri ? t('officeFile.localPreviewUnavailable') || 'Local preview unavailable. Reattach to open locally.' : t('officeFile.previewUnavailable') || 'Preview unavailable for this file.');
      setPreviewSheets([]);
      setIsParsingPreview(false);
      return () => { cancelled = true; };
    }

    setIsParsingPreview(true);
    setPreviewNote(null);
    getOfficePreview(src, mimeType, fileName)
      .then((result) => {
        if (cancelled) return;
        setPreviewText(result.text);
        setPreviewNote(result.note || null);
        if (Array.isArray(result.sheets) && result.sheets.length > 0) {
          setPreviewSheets(result.sheets);
        } else if (Array.isArray(result.tableRows) && result.tableRows.length > 0) {
          const list: TabularChartSeries[] = result.chartSeries ? [result.chartSeries] : [];
          setPreviewSheets([{ name: 'Sheet 1', rows: result.tableRows, chartSeriesList: list }]);
        } else {
          setPreviewSheets([]);
        }
      })
      .catch((error) => {
        if (cancelled) return;
        setPreviewText(null);
        setPreviewNote(error instanceof Error ? error.message : t('officeFile.failedToParse') || 'Failed to parse inline preview.');
        setPreviewSheets([]);
      })
      .finally(() => {
        if (!cancelled) setIsParsingPreview(false);
      });

    return () => { cancelled = true; };
  }, [src, mimeType, fileName, hasRemoteUri]);

  const compactPreviewSnippet = useMemo(() => {
    if (!previewText) return null;
    const lines = previewText.split('\n').slice(0, 4);
    const joined = lines.join('\n');
    if (joined.length <= 240) return joined;
    return `${joined.slice(0, 240)}...`;
  }, [previewText]);

  const statusText = previewNote || (!openHref
    ? (hasRemoteUri ? t('officeFile.localPreviewUnavailable') || 'Local preview unavailable. Reattach to open locally.' : t('officeFile.previewUnavailable') || 'Preview unavailable for this file.')
    : null);

  const handleOpenFile = React.useCallback(async (event: React.MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();
    event.stopPropagation();
    if (!openHref || isOpeningFile) return;

    setIsOpeningFile(true);
    try {
      await openAttachmentFile({
        url: openHref,
        fileName: downloadName,
        mimeType,
      });
    } catch (error) {
      if (!isAttachmentOpenCancelError(error)) {
        console.error('Failed to open office attachment:', error);
        if (typeof window !== 'undefined') {
          window.alert(t('officeFile.openFailed'));
        }
      }
    } finally {
      setIsOpeningFile(false);
    }
  }, [downloadName, isOpeningFile, mimeType, openHref, t]);

  const renderOpenFileButton = (className: string) => openHref ? (
    <button
      type="button"
      onPointerDown={(event) => event.stopPropagation()}
      onPointerUp={(event) => event.stopPropagation()}
      onPointerCancel={(event) => event.stopPropagation()}
      onClick={handleOpenFile}
      disabled={isOpeningFile}
      className={`${className} bg-transparent p-0 text-left disabled:cursor-wait disabled:opacity-60`}
      aria-busy={isOpeningFile}
    >
      {openLabel}
    </button>
  ) : null;

  const openFileFooter = renderOpenFileButton('underline text-sketch-line');

  if (!isParsingPreview && previewSheets.length > 0) {
    return (
      <div className="relative w-full max-w-full min-w-0 overflow-hidden" style={compact ? { contain: 'inline-size' } : undefined}>
        <TabularPreview
          sheets={previewSheets}
          textColorClass={textColor}
          subtleTextClass={subtleText}
          compact={compact}
          title={metaLabel}
          standalone={!compact}
          bottomInset={effectiveBottomInset}
          surfaceClassName="bg-paper-surface/85"
          panelSurfaceClassName="bg-paper-stripe/35"
        />
        {openHref ? (
          <div className={compact ? 'mt-1 px-2 text-[10px]' : 'mt-1 px-1 text-xs'}>
            {renderOpenFileButton(`underline ${subtleText}`)}
          </div>
        ) : null}
      </div>
    );
  }

  if (compact) {
    if (!isParsingPreview && compactPreviewSnippet) {
      return (
        <NotebookTextPreview
          title={metaLabel}
          subtitle={attachmentLabel}
          text={compactPreviewSnippet}
          compact
          wrapText
          footer={openFileFooter}
        />
      );
    }

    return (
      <div className="notebook-attachment-paper paper-texture notebook-lines sketch-shape-4 w-full max-w-full min-w-0 overflow-hidden px-2 py-1.5">
        <div className="flex items-start gap-2">
          <IconPaperclip className={`w-4 h-4 shrink-0 mt-0.5 ${textColor}`} />
          <div className="min-w-0 flex-1">
            <p className={`truncate font-architect text-[11px] font-semibold ${textColor}`}>{metaLabel}</p>
            <p className={`text-[11px] font-semibold truncate ${textColor}`}>{attachmentLabel}</p>
            {isParsingPreview ? (
              <div className={`mt-1 inline-flex items-center gap-1 text-[10px] ${subtleText}`}>
                <SmallSpinner className="w-3 h-3" />
                {t('officeFile.parsingPreview') || 'Parsing preview...'}
              </div>
            ) : statusText ? (
              <p className={`mt-1 text-[10px] ${subtleText}`}>{statusText}</p>
            ) : null}
            {renderOpenFileButton(`text-[10px] underline ${subtleText}`)}
          </div>
        </div>
      </div>
    );
  }

  if (!isParsingPreview && previewText) {
    return (
      <div className="relative w-full max-w-full min-w-0 overflow-hidden">
        <NotebookTextPreview
          title={metaLabel}
          subtitle={attachmentLabel}
          text={previewText}
          bottomInset={effectiveBottomInset}
          wrapText
          footer={openFileFooter}
        />
      </div>
    );
  }

  return (
    <div
      className="notebook-attachment-paper paper-texture notebook-lines sketch-shape-4 w-full overflow-hidden px-3 py-2"
      style={effectiveBottomInset > 0 ? { paddingBottom: `calc(0.5rem + ${effectiveBottomInset}px)` } : undefined}
    >
      <div className="flex items-start gap-3">
        <IconPaperclip className={`w-6 h-6 shrink-0 mt-0.5 ${textColor}`} />
        <div className="min-w-0 flex-1">
          <p className={`truncate font-architect text-[14px] font-semibold ${textColor}`}>{metaLabel}</p>
          <p className={`text-sm font-semibold ${textColor}`}>{attachmentLabel}</p>
          {isParsingPreview ? (
            <div className={`mt-2 inline-flex items-center gap-1.5 text-xs ${subtleText}`}>
              <SmallSpinner className="w-3.5 h-3.5" />
              {t('officeFile.parsingInlinePreview') || 'Parsing inline preview...'}
            </div>
          ) : (
            <p className={`mt-2 text-xs ${subtleText}`}>{statusText}</p>
          )}
          {renderOpenFileButton(`mt-1 inline-block text-xs underline ${subtleText}`)}
        </div>
      </div>
    </div>
  );
});

OfficeFileViewer.displayName = 'OfficeFileViewer';

export default OfficeFileViewer;
