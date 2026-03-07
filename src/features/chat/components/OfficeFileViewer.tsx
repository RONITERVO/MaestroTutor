// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useMemo } from 'react';
import { IconPaperclip } from '../../../shared/ui/Icons';
import {
  extractGoogleWorkspaceUrlFromDataUrl,
  isGoogleWorkspaceShortcutFileName,
  isGoogleWorkspaceShortcutMimeType,
} from '../utils/fileAttachments';

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

  const statusText = !openHref
    ? (hasRemoteUri ? 'Local preview unavailable. Reattach to open locally.' : 'Preview unavailable for this file.')
    : null;

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
            ) : (
              <p className={`text-[10px] ${subtleText}`}>{statusText}</p>
            )}
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
