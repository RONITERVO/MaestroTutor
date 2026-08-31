// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, render, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

/**
 * PDFs were the app's largest single source of decoded bitmaps: every page of
 * every open document was rasterized at a fixed scale and retained as a base64
 * data URL. These tests hold the two properties that fixed it — only the pages
 * near the reader are rasterized, and the document still reserves its full
 * height so paging the window never moves the scroll position.
 */

const PAGE_WIDTH = 595;
const PAGE_HEIGHT = 842;
const PAGE_COUNT = 20;

const renderedPages: number[] = [];

/** Landscape every third page, so page 1's shape cannot stand in for the rest. */
const isLandscape = (pageNum: number) => pageNum % 3 === 0;

/** Lets a test hold a page mid-rasterization while the document is replaced. */
let renderGate: (() => void) | null = null;

const makePdf = () => ({
  numPages: PAGE_COUNT,
  getPage: vi.fn(async (pageNum: number) => ({
    getViewport: ({ scale }: { scale: number }) => ({
      width: (isLandscape(pageNum) ? PAGE_HEIGHT : PAGE_WIDTH) * scale,
      height: (isLandscape(pageNum) ? PAGE_WIDTH : PAGE_HEIGHT) * scale,
    }),
    render: () => {
      renderedPages.push(pageNum);
      return {
        promise: renderGate
          ? new Promise<void>((resolve) => { renderGate = () => resolve(); })
          : Promise.resolve(),
      };
    },
  })),
});

vi.mock('pdfjs-dist', () => ({
  GlobalWorkerOptions: { workerSrc: '' },
  getDocument: vi.fn(() => ({ promise: Promise.resolve(makePdf()) })),
}));

// A stable object identity, as the real hook returns — an unstable one would
// mask effect-dependency bugs rather than expose them.
const translations = { t: (key: string) => key };
vi.mock('../../../shared/hooks/useAppTranslations', () => ({
  useAppTranslations: () => translations,
  default: () => translations,
}));

const budgets = { maxLiveEmbeds: 1, posterBudget: 4, pdfWindowPages: 2, pdfScaleCap: 1.25, maxVisibleMessagesCap: 35 };

vi.mock('../../../store', () => ({
  useMaestroStore: (selector: (state: unknown) => unknown) => selector({ devicePerformanceTier: 'mid' }),
  selectDeviceBudgets: () => budgets,
}));

import PdfViewer from './PdfViewer';

const SRC = 'data:application/pdf;base64,QUJDRA==';
// A distinct, still-valid base64 payload: the src is decoded before use.
const SRC_REPLACEMENT = 'data:application/pdf;base64,RUZHSA==';

describe('PdfViewer page windowing', () => {
  beforeEach(() => {
    renderedPages.length = 0;
    renderGate = null;

    // jsdom has no canvas backend; the viewer only needs the calls to succeed.
    HTMLCanvasElement.prototype.getContext = vi.fn(() => ({ drawImage: vi.fn() })) as never;
    HTMLCanvasElement.prototype.toBlob = function toBlob(callback: BlobCallback) {
      callback(new Blob(['x'], { type: 'image/jpeg' }));
    } as never;

    let counter = 0;
    vi.stubGlobal('URL', {
      ...URL,
      createObjectURL: () => `blob:page-${(counter += 1)}`,
      revokeObjectURL: vi.fn(),
    });
    vi.stubGlobal('IntersectionObserver', class {
      observe() {}
      unobserve() {}
      disconnect() {}
      takeRecords() { return []; }
    });
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it('reserves a slot for every page but rasterizes only the window around the reader', async () => {
    const { container } = render(<PdfViewer src={SRC} variant="assistant" />);

    await waitFor(() => {
      expect(container.querySelectorAll('[data-page]').length).toBe(PAGE_COUNT);
    });
    await act(async () => { await Promise.resolve(); });

    // The full document's height is reserved from the first frame, so the
    // scrollbar is honest and moving the render window cannot shift the page.
    expect(container.querySelectorAll('[data-page]')).toHaveLength(PAGE_COUNT);

    await waitFor(() => {
      expect(renderedPages.length).toBeGreaterThan(0);
    });

    // pdfWindowPages: 2 => the visible page plus one either side.
    expect(renderedPages.length).toBeLessThanOrEqual(3);
    expect(new Set(renderedPages).size).toBeLessThanOrEqual(3);
  });

  it('reserves each page slot at the document page ratio before it is rasterized', async () => {
    const { container } = render(<PdfViewer src={SRC} variant="assistant" />);

    await waitFor(() => {
      expect(container.querySelectorAll('[data-page]').length).toBe(PAGE_COUNT);
    });

    const lastSlot = container.querySelector<HTMLElement>('[data-page="20"]')!;
    // Read the raw attribute: jsdom's CSSStyleDeclaration does not expose
    // aspect-ratio as a typed property.
    const declaredRatio = /aspect-ratio:\s*([\d.]+)/.exec(lastSlot.getAttribute('style') ?? '')?.[1];
    expect(Number(declaredRatio)).toBeCloseTo(PAGE_WIDTH / PAGE_HEIGHT, 3);
    // Nothing has been rendered into it, and it still occupies its space.
    expect(lastSlot.querySelector('img')).toBeNull();
  });

  it('reserves each page at its own shape rather than page one shape', async () => {
    const { container } = render(<PdfViewer src={SRC} variant="assistant" />);

    await waitFor(() => {
      const slot = container.querySelector<HTMLElement>('[data-page="3"]');
      const ratio = /aspect-ratio:\s*([\d.]+)/.exec(slot?.getAttribute('style') ?? '')?.[1];
      // Page 3 is landscape; stamping page 1's portrait ratio here would put
      // every later page at the wrong offset.
      expect(Number(ratio)).toBeCloseTo(PAGE_HEIGHT / PAGE_WIDTH, 3);
    });

    const portraitSlot = container.querySelector<HTMLElement>('[data-page="2"]')!;
    const portraitRatio = /aspect-ratio:\s*([\d.]+)/.exec(portraitSlot.getAttribute('style') ?? '')?.[1];
    expect(Number(portraitRatio)).toBeCloseTo(PAGE_WIDTH / PAGE_HEIGHT, 3);
  });

  it('still renders a page of the new document when the old one left work in flight', async () => {
    renderGate = () => {};
    const { rerender } = render(<PdfViewer src={SRC} variant="assistant" />);

    await waitFor(() => { expect(renderedPages.length).toBeGreaterThan(0); });
    const strandedPage = renderedPages[0];

    // Swap the document while that rasterization is still outstanding. Keyed
    // only by page number, the stranded entry would make the same page of the
    // new document look already handled and it would never render.
    renderGate = null;
    renderedPages.length = 0;
    rerender(<PdfViewer src={SRC_REPLACEMENT} variant="assistant" />);

    await waitFor(() => {
      expect(renderedPages).toContain(strandedPage);
    });
  });

  it('renders only the first page for a compact thumbnail', async () => {
    const { container } = render(<PdfViewer src={SRC} variant="preview" compact />);

    await waitFor(() => {
      expect(renderedPages.length).toBeGreaterThan(0);
    });
    await act(async () => { await Promise.resolve(); });

    expect(new Set(renderedPages)).toEqual(new Set([1]));
    expect(container.textContent).toContain('20p');
  });
});
