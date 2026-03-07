// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useMemo } from 'react';
import { IconPaperclip } from '../../../shared/ui/Icons';
import { decodeTextFromDataUrl } from '../utils/fileAttachments';

interface TextFileViewerProps {
  src: string;
  variant: 'user' | 'assistant' | 'preview';
  compact?: boolean;
  fileName?: string | null;
  mimeType?: string | null;
}

const TextFileViewer: React.FC<TextFileViewerProps> = React.memo(({
  src,
  variant,
  compact = false,
  fileName,
  mimeType,
}) => {
  const decodedText = useMemo(() => decodeTextFromDataUrl(src), [src]);

  const isUser = variant === 'user';
  const containerBg = isUser ? 'bg-user-msg-bg/20' : 'bg-ai-file-bg';
  const headerBg = isUser ? 'bg-user-msg-bg/40' : 'bg-ai-msg-bg/60';
  const textColor = isUser ? 'text-user-msg-text' : 'text-ai-file-text';
  const metaLabel = fileName || mimeType || 'text attachment';

  if (!decodedText) {
    return (
      <div className={`flex flex-col items-center justify-center rounded-lg ${containerBg} ${compact ? 'h-24 w-full' : 'h-48 w-full'}`}>
        <IconPaperclip className={`w-8 h-8 ${textColor}`} />
        <p className={`mt-2 text-xs ${textColor}`}>Unable to decode text attachment.</p>
      </div>
    );
  }

  if (compact) {
    return (
      <div className={`w-full rounded-lg overflow-hidden ${containerBg}`}>
        <div className={`px-2 py-1 text-[10px] font-mono truncate ${headerBg} ${textColor}`}>
          {metaLabel}
        </div>
        <div className="overflow-auto" style={{ maxHeight: '5.25rem' }}>
          <pre className={`p-2 text-[10px] leading-4 font-mono whitespace-pre min-w-max ${textColor}`}>
            {decodedText}
          </pre>
        </div>
      </div>
    );
  }

  return (
    <div className="relative w-full">
      <div className={`overflow-auto rounded-lg ${containerBg}`} style={{ maxHeight: '60vh' }}>
        <div className={`sticky top-0 z-10 px-3 py-1.5 text-[11px] font-mono truncate ${headerBg} ${textColor}`}>
          {metaLabel}
        </div>
        <pre className={`p-3 text-[11px] leading-5 font-mono whitespace-pre min-w-max ${textColor}`}>
          {decodedText}
        </pre>
      </div>
    </div>
  );
});

TextFileViewer.displayName = 'TextFileViewer';

export default TextFileViewer;

