// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useMemo } from 'react';
import { IconPaperclip } from '../../../shared/ui/Icons';
import { SmallSpinner } from '../../../shared/ui/SmallSpinner';
import TabularPreview from './TabularPreview';
import {
  extractGoogleWorkspaceUrlFromDataUrl,
  isGoogleWorkspaceShortcutFileName,
  isGoogleWorkspaceShortcutMimeType,
} from '../utils/fileAttachments';
import { getOfficePreview } from '../utils/officePreview';
import type { TabularChartSeries } from '../utils/tabularPreview';

interface OfficeFileViewerProps {
  src?: string | null;
  variant: 'user' | 'assistant' | 'preview';
  compact?: boolean;
  fileName?: string | null;
  mimeType?: string | null;
  hasRemoteUri?: boolean;
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
  variant,
  compact = false,
  fileName,
  mimeType,
  hasRemoteUri = false,
}) => {
  const [previewText, setPreviewText] = React.useState<string | null>(null);
  const [previewNote, setPreviewNote] = React.useState<string | null>(null);
  const [previewRows, setPreviewRows] = React.useState<string[][] | null>(null);
  const [previewChart, setPreviewChart] = React.useState<TabularChartSeries | null>(null);
  const [isParsingPreview, setIsParsingPreview] = React.useState(false);

  const isUser = variant === 'user';
  const containerBg = isUser ? 'bg-user-msg-bg/20' : 'bg-ai-file-bg';
  const headerBg = isUser ? 'bg-user-msg-bg/40' : 'bg-ai-msg-bg/60';
  const textColor = isUser ? 'text-user-msg-text' : 'text-ai-file-text';
  const subtleText = isUser ? 'text-user-msg-text/70' : 'text-ai-file-text';

  const metaLabel = fileName || mimeType || 'office attachment';
  const attachmentLabel = getOfficeLabel(mimeType, fileName);

  const googleWorkspaceLink = useMemo(() => {
    const isGoogleShortcut = isGoogleWorkspaceShortcutFileName(fileName) || isGoogleWorkspaceShortcutMimeType(mimeType);
    if (!isGoogleShortcut) return null;
    return extractGoogleWorkspaceUrlFromDataUrl(src);
  }, [fileName, mimeType, src]);

  const openHref = googleWorkspaceLink || src || null;
  const openLabel = googleWorkspaceLink ? 'Open in Google Workspace' : 'Open file';
  const shouldUseDownload = Boolean(src && !googleWorkspaceLink && /^data:|^blob:/i.test(src));
  const downloadName = fileName || 'office-attachment';

  React.useEffect(() => {
    let cancelled = false;

    if (!src) {
      setPreviewText(null);
      setPreviewNote(hasRemoteUri ? 'Local preview unavailable. Reattach to open locally.' : 'Preview unavailable for this file.');
      setPreviewRows(null);
      setPreviewChart(null);
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
        setPreviewRows(result.tableRows || null);
        setPreviewChart(result.chartSeries || null);
      })
      .catch((error) => {
        if (cancelled) return;
        setPreviewText(null);
        setPreviewNote(error instanceof Error ? error.message : 'Failed to parse inline preview.');
        setPreviewRows(null);
        setPreviewChart(null);
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
    ? (hasRemoteUri ? 'Local preview unavailable. Reattach to open locally.' : 'Preview unavailable for this file.')
    : null);

  if (compact) {
    return (
      <div className={`w-full max-w-full min-w-0 rounded-lg overflow-hidden ${containerBg}`}>
        <div className={`px-2 py-1 text-[10px] font-mono truncate ${headerBg} ${textColor}`}>
          {metaLabel}
        </div>
        <div className="px-2 py-1.5 flex items-start gap-2">
          <IconPaperclip className={`w-4 h-4 shrink-0 mt-0.5 ${textColor}`} />
          <div className="min-w-0 flex-1">
            <p className={`text-[11px] font-semibold truncate ${textColor}`}>{attachmentLabel}</p>
            {isParsingPreview ? (
              <div className={`mt-1 inline-flex items-center gap-1 text-[10px] ${subtleText}`}>
                <SmallSpinner className="w-3 h-3" />
                Parsing preview...
              </div>
            ) : previewRows && previewRows.length > 0 ? (
              <TabularPreview
                rows={previewRows}
                chartSeries={previewChart}
                textColorClass={textColor}
                subtleTextClass={subtleText}
                compact
              />
            ) : compactPreviewSnippet ? (
              <pre className={`mt-1 text-[10px] leading-4 whitespace-pre-wrap break-words ${subtleText}`}>
                {compactPreviewSnippet}
              </pre>
            ) : statusText ? (
              <p className={`mt-1 text-[10px] ${subtleText}`}>{statusText}</p>
            ) : null}
            {openHref ? (
              <a
                href={openHref}
                target="_blank"
                rel="noopener noreferrer"
                download={shouldUseDownload ? downloadName : undefined}
                className={`text-[10px] underline ${subtleText}`}
              >
                {openLabel}
              </a>
            ) : null}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className={`w-full rounded-lg overflow-hidden ${containerBg}`}>
      <div className={`px-3 py-1.5 text-[11px] font-mono truncate ${headerBg} ${textColor}`}>
        {metaLabel}
      </div>
      <div className="p-3 flex items-start gap-3">
        <IconPaperclip className={`w-6 h-6 shrink-0 mt-0.5 ${textColor}`} />
        <div className="min-w-0 flex-1">
          <p className={`text-sm font-semibold ${textColor}`}>{attachmentLabel}</p>
          {isParsingPreview ? (
            <div className={`mt-2 inline-flex items-center gap-1.5 text-xs ${subtleText}`}>
              <SmallSpinner className="w-3.5 h-3.5" />
              Parsing inline preview...
            </div>
          ) : previewRows && previewRows.length > 0 ? (
            <>
              <TabularPreview
                rows={previewRows}
                chartSeries={previewChart}
                textColorClass={textColor}
                subtleTextClass={subtleText}
              />
              {previewText ? (
                <details className="mt-2">
                  <summary className={`text-xs cursor-pointer ${subtleText}`}>Raw extracted text</summary>
                  <pre className={`mt-1 p-2 text-xs leading-5 whitespace-pre-wrap break-words rounded border border-black/10 bg-black/5 ${subtleText}`}>
                    {previewText}
                  </pre>
                </details>
              ) : null}
            </>
          ) : previewText ? (
            <div className="mt-2 max-h-64 overflow-auto rounded border border-black/10 bg-black/5">
              <pre className={`p-2 text-xs leading-5 whitespace-pre-wrap break-words ${subtleText}`}>
                {previewText}
              </pre>
            </div>
          ) : (
            <p className={`mt-2 text-xs ${subtleText}`}>{statusText}</p>
          )}
          {openHref ? (
            <a
              href={openHref}
              target="_blank"
              rel="noopener noreferrer"
              download={shouldUseDownload ? downloadName : undefined}
              className={`mt-1 inline-block text-xs underline ${subtleText}`}
            >
              {openLabel}
            </a>
          ) : (
            <p className={`mt-1 text-xs ${subtleText}`}>{statusText}</p>
          )}
        </div>
      </div>
    </div>
  );
});

OfficeFileViewer.displayName = 'OfficeFileViewer';

export default OfficeFileViewer;
