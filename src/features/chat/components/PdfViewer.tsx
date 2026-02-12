// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useState, useRef, useEffect, useCallback } from 'react';
import * as pdfjsLib from 'pdfjs-dist';
import { IconPaperclip } from '../../../shared/ui/Icons';
import { SmallSpinner } from '../../../shared/ui/SmallSpinner';

pdfjsLib.GlobalWorkerOptions.workerSrc = `${import.meta.env.BASE_URL}pdf.worker.min.mjs`;

/** Return the total number of pages in a PDF data-URL. */
export async function getPdfPageCount(src: string): Promise<number> {
  const base64Part = src.includes(',') ? src.split(',')[1] : src;
  const binaryString = atob(base64Part);
  const bytes = new Uint8Array(binaryString.length);
  for (let i = 0; i < binaryString.length; i++) {
    bytes[i] = binaryString.charCodeAt(i);
  }
  const pdf = await pdfjsLib.getDocument({ data: bytes }).promise;
  return pdf.numPages;
}

/** Render a single PDF page to a JPEG data-URL (re-usable outside PdfViewer). */
export async function renderPdfPageToImage(
  src: string,
  pageNum: number = 1,
  scale: number = 1.5,
): Promise<string> {
  const base64Part = src.includes(',') ? src.split(',')[1] : src;
  const binaryString = atob(base64Part);
  const bytes = new Uint8Array(binaryString.length);
  for (let i = 0; i < binaryString.length; i++) {
    bytes[i] = binaryString.charCodeAt(i);
  }
  const pdf = await pdfjsLib.getDocument({ data: bytes }).promise;
  const page = await pdf.getPage(Math.min(pageNum, pdf.numPages));
  const viewport = page.getViewport({ scale });
  const canvas = document.createElement('canvas');
  canvas.width = viewport.width;
  canvas.height = viewport.height;
  await page.render({ canvas, viewport }).promise;
  return canvas.toDataURL('image/jpeg', 0.92);
}

interface PdfViewerProps {
  src: string;
  variant: 'user' | 'assistant' | 'preview';
  compact?: boolean;
}

const PdfViewer: React.FC<PdfViewerProps> = React.memo(({ src, variant, compact = false }) => {
  const [pages, setPages] = useState<string[]>([]);
  const [pageCount, setPageCount] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [visiblePage, setVisiblePage] = useState(1);

  const scrollContainerRef = useRef<HTMLDivElement>(null);
  const pageRefsMap = useRef<Map<number, HTMLImageElement>>(new Map());

  useEffect(() => {
    let cancelled = false;

    const renderPdf = async () => {
      setIsLoading(true);
      setError(null);
      setPages([]);
      setPageCount(0);
      setVisiblePage(1);

      try {
        const base64Part = src.includes(',') ? src.split(',')[1] : src;
        const binaryString = atob(base64Part);
        const bytes = new Uint8Array(binaryString.length);
        for (let i = 0; i < binaryString.length; i++) {
          bytes[i] = binaryString.charCodeAt(i);
        }

        const pdf = await pdfjsLib.getDocument({ data: bytes }).promise;
        if (cancelled) return;

        const numPages = pdf.numPages;
        if (numPages === 0) {
          setError('PDF has no pages');
          setIsLoading(false);
          return;
        }

        setPageCount(numPages);
        const maxPages = compact ? 1 : numPages;
        const scale = compact ? 0.75 : 1.5;
        const renderedPages: string[] = [];

        for (let i = 1; i <= maxPages; i++) {
          if (cancelled) return;
          const page = await pdf.getPage(i);
          const viewport = page.getViewport({ scale });

          const canvas = document.createElement('canvas');
          canvas.width = viewport.width;
          canvas.height = viewport.height;

          await page.render({ canvas, viewport }).promise;
          renderedPages.push(canvas.toDataURL('image/jpeg', 0.85));
        }

        if (!cancelled) {
          setPages(renderedPages);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load PDF');
        }
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };

    renderPdf();
    return () => { cancelled = true; };
  }, [src, compact]);

  // Track visible page with IntersectionObserver
  useEffect(() => {
    if (compact || pages.length <= 1) return;
    const container = scrollContainerRef.current;
    if (!container) return;

    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            const pageNum = Number(entry.target.getAttribute('data-page'));
            if (pageNum) setVisiblePage(pageNum);
          }
        }
      },
      { root: container, threshold: 0.5 }
    );

    pageRefsMap.current.forEach((el) => observer.observe(el));
    return () => observer.disconnect();
  }, [pages, compact]);

  const setPageRef = useCallback((pageNum: number, el: HTMLImageElement | null) => {
    if (el) {
      pageRefsMap.current.set(pageNum, el);
    } else {
      pageRefsMap.current.delete(pageNum);
    }
  }, []);

  const isUser = variant === 'user';

  const containerBg = isUser ? 'bg-blue-600/20' : 'bg-gray-50';
  const indicatorBg = 'bg-black/60 text-white';
  const errorTextColor = isUser ? 'text-blue-100' : 'text-gray-500';
  const iconColor = isUser ? 'text-blue-100' : 'text-gray-400';

  if (isLoading) {
    return (
      <div className={`flex flex-col items-center justify-center rounded-lg ${containerBg} ${compact ? 'h-24 w-full' : 'h-48 w-full'}`}>
        <SmallSpinner className={`w-6 h-6 ${isUser ? 'text-white' : 'text-blue-500'}`} />
        <p className={`mt-2 text-xs ${errorTextColor}`}>Loading PDF...</p>
      </div>
    );
  }

  if (error || pages.length === 0) {
    return (
      <div className={`flex flex-col items-center justify-center rounded-lg ${containerBg} ${compact ? 'h-24 w-full' : 'h-48 w-full'}`}>
        <IconPaperclip className={`w-10 h-10 ${iconColor}`} />
        <p className={`mt-2 text-xs ${errorTextColor}`}>{error || 'Unable to display PDF'}</p>
      </div>
    );
  }

  if (compact) {
    return (
      <div className={`relative rounded overflow-hidden ${containerBg}`}>
        <img
          src={pages[0]}
          alt="PDF page 1"
          className="h-24 w-full object-cover"
        />
        {pageCount > 1 && (
          <div className={`absolute bottom-1 right-1 ${indicatorBg} text-[10px] px-1.5 py-0.5 rounded-full`}>
            {pageCount}p
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="relative w-full">
      <div
        ref={scrollContainerRef}
        className={`overflow-y-auto rounded-lg ${containerBg}`}
        style={{ maxHeight: '60vh' }}
      >
        <div className="flex flex-col gap-1 p-1">
          {pages.map((pageUrl, index) => (
            <img
              key={index}
              ref={(el) => setPageRef(index + 1, el)}
              data-page={index + 1}
              src={pageUrl}
              alt={`PDF page ${index + 1}`}
              className="w-full rounded shadow-sm"
            />
          ))}
        </div>
      </div>
      {pageCount > 1 && (
        <div className={`absolute bottom-2 right-2 ${indicatorBg} text-xs px-2 py-0.5 rounded-full pointer-events-none`}>
          {visiblePage} / {pageCount}
        </div>
      )}
    </div>
  );
});

PdfViewer.displayName = 'PdfViewer';

export default PdfViewer;
