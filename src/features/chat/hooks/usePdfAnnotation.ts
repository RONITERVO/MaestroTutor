// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useState, useRef, useCallback } from 'react';
import { renderPdfPageToImage, getPdfPageCount } from '../components/PdfViewer';

interface UsePdfAnnotationOptions {
  editCanvasRef: React.RefObject<HTMLCanvasElement | null>;
  onStartAnnotation: (imageUrl: string) => void;
  setAnnotationSourceUrl: (url: string | null) => void;
  setUndoStack: React.Dispatch<React.SetStateAction<ImageData[]>>;
  isNewStrokeRef: React.MutableRefObject<boolean>;
}

interface UsePdfAnnotationReturn {
  pdfSrcRef: React.MutableRefObject<string | null>;
  pdfPageNum: number;
  pdfPageCount: number;
  startPdfAnnotation: (src: string) => Promise<void>;
  changePdfPage: (delta: number) => Promise<void>;
  resetPdfState: () => void;
}

export function usePdfAnnotation({
  editCanvasRef,
  onStartAnnotation,
  setAnnotationSourceUrl,
  setUndoStack,
  isNewStrokeRef,
}: UsePdfAnnotationOptions): UsePdfAnnotationReturn {
  const [pdfPageNum, setPdfPageNum] = useState(1);
  const [pdfPageCount, setPdfPageCount] = useState(0);
  const pdfSrcRef = useRef<string | null>(null);

  const startPdfAnnotation = useCallback(async (src: string) => {
    try {
      const [pageImage, count] = await Promise.all([
        renderPdfPageToImage(src),
        getPdfPageCount(src),
      ]);
      pdfSrcRef.current = src;
      setPdfPageNum(1);
      setPdfPageCount(count);
      onStartAnnotation(pageImage);
    } catch (err) {
      console.error('Failed to start PDF annotation:', err);
    }
  }, [onStartAnnotation]);

  const changePdfPage = useCallback(async (delta: number) => {
    const src = pdfSrcRef.current;
    if (!src || pdfPageCount <= 1) return;
    const next = Math.max(1, Math.min(pdfPageNum + delta, pdfPageCount));
    if (next === pdfPageNum) return;

    try {
      const pageImage = await renderPdfPageToImage(src, next);
      setPdfPageNum(next);
      setAnnotationSourceUrl(pageImage);
      setUndoStack([]);
      isNewStrokeRef.current = true;
      const canvas = editCanvasRef.current;
      const ctx = canvas?.getContext('2d');
      if (canvas && ctx) ctx.clearRect(0, 0, canvas.width, canvas.height);
    } catch (err) {
      console.error('Failed to change PDF page:', err);
    }
  }, [pdfPageNum, pdfPageCount, editCanvasRef, setAnnotationSourceUrl, setUndoStack, isNewStrokeRef]);

  const resetPdfState = useCallback(() => {
    setPdfPageNum(1);
    setPdfPageCount(0);
    pdfSrcRef.current = null;
  }, []);

  return {
    pdfSrcRef,
    pdfPageNum,
    pdfPageCount,
    startPdfAnnotation,
    changePdfPage,
    resetPdfState,
  };
}
