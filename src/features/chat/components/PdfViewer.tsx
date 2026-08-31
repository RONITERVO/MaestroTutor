// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useState, useRef, useEffect, useCallback, useMemo } from 'react';
import type * as PdfJs from 'pdfjs-dist';
import { IconPaperclip } from '../../../shared/ui/Icons';
import { SmallSpinner } from '../../../shared/ui/SmallSpinner';
import AttachmentInteractionToggle from './AttachmentInteractionToggle';
import useChatResettingAttachmentMode from './useChatResettingAttachmentMode';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import { useMaestroStore, selectDeviceBudgets } from '../../../store';
import { useEmbedSlot } from '../embeds/useEmbedSlot';

/**
 * pdf.js is loaded on demand rather than at startup.
 *
 * It is one of the largest dependencies in the bundle and most sessions never
 * open a PDF, so eagerly importing it cost every launch the parse, compile and
 * retained module memory for code that would never run.
 */
let pdfjsPromise: Promise<typeof PdfJs> | null = null;

const loadPdfjs = (): Promise<typeof PdfJs> => {
  if (!pdfjsPromise) {
    pdfjsPromise = import('pdfjs-dist').then((lib) => {
      lib.GlobalWorkerOptions.workerSrc = `${import.meta.env.BASE_URL}pdf.worker.min.mjs`;
      return lib;
    });
  }
  return pdfjsPromise;
};

/** Decode a base64 data-URL (or raw base64 string) into a Uint8Array. */
export function decodeBase64ToUint8Array(src: string): Uint8Array {
  const base64Part = src.includes(',') ? src.substring(src.indexOf(',') + 1) : src;
  const binaryString = atob(base64Part);
  const bytes = new Uint8Array(binaryString.length);
  for (let i = 0; i < binaryString.length; i++) {
    bytes[i] = binaryString.charCodeAt(i);
  }
  return bytes;
}

// Simple cache for parsed PDFDocumentProxy objects keyed by src.
const pdfPromiseCache = new Map<string, Promise<PdfJs.PDFDocumentProxy>>();
const PDF_CACHE_MAX = 4;

/** Return a cached or freshly-loaded PDFDocumentProxy for the given src. */
export function getOrLoadPdf(src: string): Promise<PdfJs.PDFDocumentProxy> {
  const existing = pdfPromiseCache.get(src);
  if (existing) return existing;

  // Cached synchronously despite the async library load, so concurrent callers
  // for the same document still share one parse.
  const loadPromise = loadPdfjs().then((lib) => {
    const bytes = decodeBase64ToUint8Array(src);
    return lib.getDocument({ data: bytes }).promise;
  });

  if (pdfPromiseCache.size >= PDF_CACHE_MAX) {
    const oldestKey = pdfPromiseCache.keys().next().value;
    if (oldestKey !== undefined) pdfPromiseCache.delete(oldestKey);
  }

  pdfPromiseCache.set(src, loadPromise);
  loadPromise.catch(() => pdfPromiseCache.delete(src));

  return loadPromise;
}

/** Return the total number of pages in a PDF data-URL. */
export async function getPdfPageCount(src: string): Promise<number> {
  const pdf = await getOrLoadPdf(src);
  return pdf.numPages;
}

/** Render a single PDF page to a JPEG data-URL (re-usable outside PdfViewer). */
export async function renderPdfPageToImage(
  src: string,
  pageNum: number = 1,
  scale: number = 1.5,
): Promise<string> {
  if (pageNum < 1) throw new Error(`Invalid page number: ${pageNum}. Must be >= 1.`);
  const pdf = await getOrLoadPdf(src);
  const page = await pdf.getPage(Math.min(pageNum, pdf.numPages));
  const viewport = page.getViewport({ scale });
  const canvas = document.createElement('canvas');
  canvas.width = viewport.width;
  canvas.height = viewport.height;
  await page.render({ canvas, viewport }).promise;
  return canvas.toDataURL('image/jpeg', 0.92);
}

/**
 * Render one page into a blob URL sized for the box it will occupy.
 *
 * Two deliberate differences from the helper above, and they are the whole
 * reason PDFs used to blow the bitmap budget:
 *
 * - the scale is derived from the width the page is actually displayed at,
 *   rather than a fixed 1.5. Rasterizing an A4 page at 1.5 produces roughly
 *   892x1262 px — about 4.5 MB decoded — regardless of whether it is being
 *   shown in a 320 px-wide bubble.
 * - the result is a blob URL, not a base64 data URL. A blob lives outside the
 *   JS heap and `revokeObjectURL` actually frees it; a data URL is retained
 *   string bytes plus a decoded bitmap we have no way to release.
 */
async function renderPageToBlobUrl(
  pdf: PdfJs.PDFDocumentProxy,
  pageNum: number,
  targetCssWidth: number,
  scaleCap: number,
): Promise<{ url: string; aspectRatio: number } | null> {
  const page = await pdf.getPage(pageNum);
  const baseViewport = page.getViewport({ scale: 1 });
  if (baseViewport.width <= 0 || baseViewport.height <= 0) return null;

  const dpr = typeof window !== 'undefined' ? Math.min(window.devicePixelRatio || 1, 2) : 1;
  const fitScale = targetCssWidth > 0 ? (targetCssWidth * dpr) / baseViewport.width : scaleCap;
  const scale = Math.max(0.4, Math.min(scaleCap * dpr, fitScale));
  const viewport = page.getViewport({ scale });

  const canvas = document.createElement('canvas');
  canvas.width = Math.max(1, Math.round(viewport.width));
  canvas.height = Math.max(1, Math.round(viewport.height));

  try {
    await page.render({ canvas, viewport }).promise;
    const blob = await new Promise<Blob | null>((resolve) => {
      canvas.toBlob(resolve, 'image/jpeg', 0.85);
    });
    if (!blob) return null;
    return { url: URL.createObjectURL(blob), aspectRatio: baseViewport.width / baseViewport.height };
  } finally {
    // Drop the backing store immediately rather than waiting for GC; on a
    // low-end device the collector can lag well behind a burst of renders.
    canvas.width = 0;
    canvas.height = 0;
  }
}

interface PdfViewerProps {
  src: string;
  variant: 'user' | 'assistant' | 'preview';
  compact?: boolean;
  bottomInset?: number;
  /** Stable identity for the activation slot — normally the message id. */
  embedId?: string;
}

const PdfViewer: React.FC<PdfViewerProps> = React.memo(({ src, compact = false, bottomInset = 0, embedId }) => {
  const { t } = useAppTranslations();
  const budgets = useMaestroStore(selectDeviceBudgets);

  const [pageCount, setPageCount] = useState(0);
  const [pageAspectRatio, setPageAspectRatio] = useState(1 / Math.SQRT2);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [visiblePage, setVisiblePage] = useState(1);
  /** Only the pages inside the current window are ever rasterized. */
  const [renderedPages, setRenderedPages] = useState<Record<number, string>>({});

  const {
    rootRef,
    isAttachmentModeEnabled: isPdfScrollEnabled,
    setIsAttachmentModeEnabled: setIsPdfScrollEnabled,
  } = useChatResettingAttachmentMode<HTMLDivElement>();

  const scrollContainerRef = useRef<HTMLDivElement>(null);
  const pageRefsMap = useRef<Map<number, HTMLElement>>(new Map());
  const renderedPagesRef = useRef<Record<number, string>>({});
  const inFlightRef = useRef<Set<number>>(new Set());

  // A PDF is an embed like any other: when it is not the live one, every
  // rasterized page is released. Compact thumbnails stay outside the system —
  // they are a single small bitmap and are never the memory problem.
  const slot = useEmbedSlot({
    id: embedId ?? '',
    kind: 'pdf',
    enabled: !compact && !!embedId,
  });
  // Without an id there is nothing to arbitrate, so fall back to always-live
  // rather than silently rendering a blank document.
  const isLive = compact || !embedId || slot.isLive;

  const { pin, setRef: setSlotRef } = slot;

  /**
   * Must be stable: React detaches and reattaches an inline ref callback on
   * every render, which would look to the slot like the box element changing
   * each time and re-register in a loop.
   */
  const attachScrollContainer = useCallback((el: HTMLDivElement | null) => {
    scrollContainerRef.current = el;
    setSlotRef(el);
  }, [setSlotRef]);

  /**
   * Scrolling the pages is an explicit engagement; hold the live slot. Only
   * ever pins: page scrolling is switched back off by any chat scroll, and
   * dropping the pin there would release the document while the reader is
   * still looking at it. The pin ends when the PDF scrolls away or the user
   * engages a different embed.
   */
  useEffect(() => {
    if (compact || !embedId) return;
    if (isPdfScrollEnabled) pin();
  }, [compact, embedId, isPdfScrollEnabled, pin]);

  const releaseRenderedPages = useCallback((keep?: Set<number>) => {
    const current = renderedPagesRef.current;
    const next: Record<number, string> = {};
    for (const [key, url] of Object.entries(current)) {
      const pageNum = Number(key);
      if (keep?.has(pageNum)) {
        next[pageNum] = url;
        continue;
      }
      URL.revokeObjectURL(url);
    }
    renderedPagesRef.current = next;
    setRenderedPages(next);
  }, []);

  // Document metadata only: page count and page 1's shape, so the viewer can
  // reserve the right height without rasterizing anything.
  useEffect(() => {
    let cancelled = false;

    const loadMetadata = async () => {
      setIsLoading(true);
      setError(null);
      setPageCount(0);
      setVisiblePage(1);
      setIsPdfScrollEnabled(false);

      try {
        const pdf = await getOrLoadPdf(src);
        if (cancelled) return;

        if (pdf.numPages === 0) {
          setError('PDF has no pages');
          setIsLoading(false);
          return;
        }

        const firstPage = await pdf.getPage(1);
        if (cancelled) return;
        const viewport = firstPage.getViewport({ scale: 1 });

        setPageCount(pdf.numPages);
        if (viewport.width > 0 && viewport.height > 0) {
          setPageAspectRatio(viewport.width / viewport.height);
        }
      } catch (err) {
        // Store the raw reason and translate at render time: a loading effect
        // must not depend on the translator, or a new locale object identity
        // would re-trigger the whole document load.
        if (!cancelled) setError(err instanceof Error ? err.message : '');
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };

    loadMetadata();
    return () => { cancelled = true; };
  }, [src, setIsPdfScrollEnabled]);

  // A new document means every page we hold belongs to the old one.
  useEffect(() => () => releaseRenderedPages(), [src, releaseRenderedPages]);

  const windowRadius = compact ? 0 : Math.max(0, budgets.pdfWindowPages - 1);

  const desiredPages = useMemo(() => {
    if (!pageCount) return [] as number[];
    if (compact) return [1];
    const first = Math.max(1, visiblePage - windowRadius);
    const last = Math.min(pageCount, visiblePage + windowRadius);
    const pages: number[] = [];
    for (let page = first; page <= last; page += 1) pages.push(page);
    return pages;
  }, [compact, pageCount, visiblePage, windowRadius]);

  // Render the window, release everything outside it.
  useEffect(() => {
    if (!pageCount) return;

    if (!isLive) {
      releaseRenderedPages();
      return;
    }

    let cancelled = false;
    const keep = new Set(desiredPages);
    releaseRenderedPages(keep);

    const renderWindow = async () => {
      const container = scrollContainerRef.current ?? rootRef.current;
      const targetWidth = container?.clientWidth || 320;

      const pdf = await getOrLoadPdf(src);
      if (cancelled) return;

      for (const pageNum of desiredPages) {
        if (cancelled) return;
        if (renderedPagesRef.current[pageNum] || inFlightRef.current.has(pageNum)) continue;

        inFlightRef.current.add(pageNum);
        try {
          const rendered = await renderPageToBlobUrl(pdf, pageNum, targetWidth, budgets.pdfScaleCap);
          if (!rendered) continue;
          // The window may have moved on, or the embed been demoted, while this
          // page was rasterizing; do not resurrect a page nobody wants.
          if (cancelled || !keep.has(pageNum)) {
            URL.revokeObjectURL(rendered.url);
            continue;
          }
          renderedPagesRef.current = { ...renderedPagesRef.current, [pageNum]: rendered.url };
          setRenderedPages(renderedPagesRef.current);
        } catch {
          /* a single unrenderable page should not take down the viewer */
        } finally {
          inFlightRef.current.delete(pageNum);
        }
      }
    };

    renderWindow();
    return () => { cancelled = true; };
  }, [budgets.pdfScaleCap, desiredPages, isLive, pageCount, releaseRenderedPages, rootRef, src]);

  // Track the visible page so the render window follows the reader.
  useEffect(() => {
    if (compact || pageCount <= 1) return;
    const container = scrollContainerRef.current;
    if (!container || typeof IntersectionObserver === 'undefined') return;

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
  }, [compact, pageCount]);

  const setPageRef = useCallback((pageNum: number, el: HTMLElement | null) => {
    if (el) {
      pageRefsMap.current.set(pageNum, el);
    } else {
      pageRefsMap.current.delete(pageNum);
    }
  }, []);

  const containerBg = 'notebook-native-paper sketch-shape-4';
  const indicatorBg = 'bg-black/60 text-white';
  const errorTextColor = 'text-sketch-line';
  const iconColor = 'text-deep-ink';
  const effectiveBottomInset = !compact ? Math.max(0, Math.round(bottomInset)) : 0;
  const pdfScrollStyle: React.CSSProperties = {
    maxHeight: '60vh',
    overflowY: isPdfScrollEnabled ? 'auto' : 'hidden',
    overscrollBehavior: isPdfScrollEnabled ? 'contain' : 'auto',
    touchAction: 'pan-y',
    WebkitOverflowScrolling: 'touch' as any,
    scrollPaddingBottom: `${effectiveBottomInset}px`,
  };

  if (isLoading) {
    return (
      <div className={`flex flex-col items-center justify-center rounded-lg ${containerBg} ${compact ? 'h-24 w-full' : 'h-48 w-full'}`}>
        <SmallSpinner className="w-6 h-6 text-deep-ink" />
        <p className={`mt-2 text-xs ${errorTextColor}`}>{t('chat.pdf.loading') || 'Loading PDF...'}</p>
      </div>
    );
  }

  if (error !== null || pageCount === 0) {
    return (
      <div className={`flex flex-col items-center justify-center rounded-lg ${containerBg} ${compact ? 'h-24 w-full' : 'h-48 w-full'}`}>
        <IconPaperclip className={`w-10 h-10 ${iconColor}`} />
        <p className={`mt-2 text-xs ${errorTextColor}`}>{error || t('chat.pdf.error') || 'Unable to display PDF'}</p>
      </div>
    );
  }

  if (compact) {
    const firstPage = renderedPages[1];
    return (
      <div className={`relative rounded overflow-hidden ${containerBg}`}>
        {firstPage ? (
          <img src={firstPage} alt="PDF page 1" className="h-24 w-full object-cover" />
        ) : (
          <div className="h-24 w-full flex items-center justify-center">
            <SmallSpinner className="w-5 h-5 text-deep-ink" />
          </div>
        )}
        {pageCount > 1 && (
          <div className={`absolute bottom-1 right-1 ${indicatorBg} text-[10px] px-1.5 py-0.5 rounded-full`}>
            {pageCount}p
          </div>
        )}
      </div>
    );
  }

  return (
    <div ref={rootRef} className="relative w-full">
      <div
        ref={attachScrollContainer}
        className={`rounded-lg ${containerBg}`}
        style={pdfScrollStyle}
        onWheel={isPdfScrollEnabled ? (event) => event.stopPropagation() : undefined}
        onTouchMove={isPdfScrollEnabled ? (event) => event.stopPropagation() : undefined}
      >
        <div
          className="flex flex-col gap-1 p-1"
          style={effectiveBottomInset > 0 ? { paddingBottom: `${effectiveBottomInset + 4}px` } : undefined}
        >
          {/*
            Every page occupies its reserved slot whether or not it has been
            rasterized, so the scroll height is the full document from the first
            frame and paging the render window never moves the scroll position.
          */}
          {Array.from({ length: pageCount }, (_, index) => index + 1).map((pageNum) => {
            const pageUrl = renderedPages[pageNum];
            return (
              <div
                key={pageNum}
                ref={(el) => setPageRef(pageNum, el)}
                data-page={pageNum}
                className="w-full rounded shadow-sm bg-black/[0.03]"
                style={{ aspectRatio: `${pageAspectRatio}` }}
              >
                {pageUrl && (
                  <img
                    src={pageUrl}
                    alt={`PDF page ${pageNum}`}
                    className="w-full h-full object-contain"
                    draggable={false}
                  />
                )}
              </div>
            );
          })}
        </div>
      </div>
      <div className="absolute left-2 top-2 z-20 pointer-events-auto">
        <AttachmentInteractionToggle
          isAttachmentModeEnabled={isPdfScrollEnabled}
          attachmentLabel="PDF scroll"
          attachmentTitle="Scroll PDF pages"
          groupLabel="PDF interaction mode"
          onToggle={() => setIsPdfScrollEnabled((prev) => !prev)}
        />
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
