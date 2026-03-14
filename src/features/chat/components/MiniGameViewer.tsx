// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { IconPaperclip, IconTerminal, IconUndo } from '../../../shared/ui/Icons';
import { buildMiniGameSrcDoc } from '../utils/miniGameAttachment';

type MiniGameRuntimeState = 'booting' | 'ready' | 'error';

interface MiniGameViewerProps {
  sourceCode: string;
  variant: 'user' | 'assistant' | 'preview';
  fileName?: string | null;
  mimeType?: string | null;
  bottomInset?: number;
}

const MiniGameViewer: React.FC<MiniGameViewerProps> = React.memo(({
  sourceCode,
  variant,
  fileName,
  mimeType,
  bottomInset = 0,
}) => {
  const [showCode, setShowCode] = useState(false);
  const [runtimeState, setRuntimeState] = useState<MiniGameRuntimeState>('booting');
  const [runtimeError, setRuntimeError] = useState<string>('');
  const [reloadToken, setReloadToken] = useState(0);

  const [hasIntersected, setHasIntersected] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const iframeRef = useRef<HTMLIFrameElement>(null);

  useEffect(() => {
    const el = containerRef.current;
    if (!el || typeof IntersectionObserver === 'undefined') return;

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting && !hasIntersected) {
          setHasIntersected(true);
        }
      },
      { rootMargin: '600px' },
    );
    observer.observe(el);
    return () => { observer.disconnect(); };
  }, [hasIntersected]);

  const frameId = useMemo(
    () => `mini-game-${Math.random().toString(36).slice(2, 10)}-${reloadToken}`,
    [reloadToken]
  );

  const srcDoc = useMemo(
    () => buildMiniGameSrcDoc({ sourceCode, fileName, mimeType, frameId }),
    [sourceCode, fileName, mimeType, frameId]
  );

  const handleReload = useCallback(() => {
    setRuntimeState('booting');
    setReloadToken((n) => n + 1);
  }, []);

  const handleFullscreen = useCallback(() => {
    if (iframeRef.current) {
      if (iframeRef.current.requestFullscreen) {
        iframeRef.current.requestFullscreen();
      } else if ((iframeRef.current as any).webkitRequestFullscreen) {
        (iframeRef.current as any).webkitRequestFullscreen();
      }
    }
  }, []);

  useEffect(() => {
    if (!hasIntersected) return;

    setRuntimeState('booting');
    setRuntimeError('');

    const timeout = window.setTimeout(() => {
      setRuntimeState((prev) => (prev === 'booting' ? 'ready' : prev));
    }, 1800);

    const onMessage = (event: MessageEvent) => {
      const payload = event.data;
      if (!payload || payload.type !== 'maestro-mini-game-status' || payload.frameId !== frameId) return;

      if (payload.status === 'error') {
        setRuntimeState('error');
        setRuntimeError((payload.detail || 'Runtime error').slice(0, 220));
      } else if (payload.status === 'ready') {
        setRuntimeState('ready');
      }
    };

    window.addEventListener('message', onMessage);
    return () => {
      window.clearTimeout(timeout);
      window.removeEventListener('message', onMessage);
    };
  }, [frameId, srcDoc, hasIntersected]);

  const isUser = variant === 'user';
  const containerBg = isUser ? 'bg-user-msg-bg/20' : 'bg-ai-file-bg';
  const bubbleSurfaceBg = isUser ? 'bg-user-msg-bg/95' : 'bg-ai-msg-bg/95';
  const textColor = isUser ? 'text-user-msg-text' : 'text-ai-file-text';
  const subtleText = isUser ? 'text-user-msg-text/70' : 'text-ai-file-text/70';
  const lineColor = isUser ? 'border-user-msg-text/25' : 'border-ai-file-text/25';
  const padBtnBg = isUser ? 'bg-user-msg-bg/50 hover:bg-user-msg-bg/65' : 'bg-ai-msg-bg/55 hover:bg-ai-msg-bg/70';
  const statusBubbleBg = runtimeState === 'error' ? 'bg-red-900/80' : 'bg-black/70';
  const effectiveBottomInset = Math.max(0, Math.round(bottomInset));
  const controlsUnderOverlay = effectiveBottomInset > 0;
  const focusedShellHeight = Math.max(92, Math.min(Math.round(effectiveBottomInset * 0.45) + 32, 122));
  const wrapperBottomPadding = controlsUnderOverlay ? Math.max(72, focusedShellHeight - 10) : 8;
  // Vastly simplified standard responsive block. 
  // LLM handles making the game fit these dimensions.
  const gameScreenStyle: React.CSSProperties = {
    width: '100%',
    height: controlsUnderOverlay ? 'min(68vh, 520px)' : '480px',
    minHeight: '260px',
    resize: controlsUnderOverlay ? 'none' : 'vertical', // Allow user to make it taller if they want
    backgroundColor: 'transparent',
  };

  const actionButtonClass = 'p-2 bg-black/50 text-white rounded-full hover:bg-black/75 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-black/50 focus:ring-white transition-colors';

  return (
    <div ref={containerRef} className="w-full flex flex-col items-center">
      <div className="relative w-full max-w-[560px]" style={{ paddingBottom: `${wrapperBottomPadding}px` }}>

        <div
          className={`relative rounded-2xl overflow-hidden border ${lineColor} shadow-none ${controlsUnderOverlay && showCode ? 'z-30' : 'z-10'}`}
          style={gameScreenStyle}
        >
          {hasIntersected ? (
            <iframe
              ref={iframeRef}
              title={fileName ? `Mini game ${fileName}` : 'Mini game'}
              srcDoc={srcDoc}
              className="w-full h-full border-0 bg-transparent block"
              sandbox="allow-scripts allow-same-origin"
              allow="fullscreen"
              referrerPolicy="no-referrer"
              loading="lazy"
              style={{ backgroundColor: 'transparent' }}
            />
          ) : (
            <div className="w-full h-full" />
          )}

          {runtimeState !== 'ready' && hasIntersected && (
            <div className={`absolute left-2 right-2 top-2 z-10 rounded-lg px-2 py-1 text-[11px] ${statusBubbleBg} text-white`}>
              {runtimeState === 'error' ? `Mini-game error: ${runtimeError}` : 'Launching mini-game...'}
            </div>
          )}

          {controlsUnderOverlay && (
            <div className="absolute top-2 right-2 z-30 flex flex-col gap-2 pointer-events-auto">
              <button onClick={handleFullscreen} className={actionButtonClass} title="Fullscreen">
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 8V4m0 0h4M4 4l5 5m11-1V4m0 0h-4m4 0l-5 5M4 16v4m0 0h4m-4 0l5-5m11 5l-5-5m5 5v-4m0 4h-4" /></svg>
              </button>
              <button onClick={handleReload} className={actionButtonClass} title="Restart">
                <IconUndo className="w-4 h-4" />
              </button>
              <button onClick={() => setShowCode((prev) => !prev)} className={actionButtonClass} title="Code">
                <IconTerminal className="w-4 h-4" />
              </button>
            </div>
          )}

          {controlsUnderOverlay && showCode && (
            <div className={`absolute inset-2 z-20 rounded-xl border ${lineColor} ${bubbleSurfaceBg} overflow-hidden shadow-[0_12px_28px_rgba(2,6,23,0.28)] backdrop-blur-sm`}>
              <div className={`px-3 py-1.5 pr-12 text-[11px] font-mono truncate border-b ${lineColor} ${textColor}`}>
                {fileName || mimeType || 'mini-game source'}
              </div>
              <div
                className="max-h-full overflow-auto"
                style={{ height: 'calc(100% - 32px)', overscrollBehavior: 'contain', touchAction: 'pan-y', WebkitOverflowScrolling: 'touch' as any }}
              >
                <pre className={`p-3 text-[11px] leading-5 font-mono whitespace-pre w-max min-w-full ${textColor}`}>
                  {sourceCode}
                </pre>
              </div>
            </div>
          )}
        </div>

        {controlsUnderOverlay ? (
          <div
            className="absolute left-2 right-2 bottom-0 z-0 pointer-events-none bg-transparent"
            style={{
              height: `${focusedShellHeight}px`,
            }}
            aria-hidden
          />
        ) : (
          <div className="w-full mt-3 flex justify-center z-10 pointer-events-auto">
            <div className={`rounded-xl border ${lineColor} ${containerBg} px-4 py-2 backdrop-blur-sm pointer-events-auto shadow-sm flex items-center gap-4`}>
              <button onClick={handleFullscreen} className={`inline-flex items-center gap-1.5 rounded-full border ${lineColor} px-3 py-1 text-[10px] uppercase tracking-wider ${textColor} ${padBtnBg}`}>
                <svg className="w-3 h-3 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 8V4m0 0h4M4 4l5 5m11-1V4m0 0h-4m4 0l-5 5M4 16v4m0 0h4m-4 0l5-5m11 5l-5-5m5 5v-4m0 4h-4" /></svg>
                <span className="font-semibold">Fullscreen</span>
              </button>
              <button onClick={handleReload} className={`inline-flex items-center gap-1.5 rounded-full border ${lineColor} px-3 py-1 text-[10px] uppercase tracking-wider ${textColor} ${padBtnBg}`}>
                <IconUndo className="w-3 h-3 shrink-0" />
                <span className="font-semibold">Restart</span>
              </button>
              <button onClick={() => setShowCode((prev) => !prev)} className={`inline-flex items-center gap-1.5 rounded-full border ${lineColor} px-3 py-1 text-[10px] uppercase tracking-wider ${textColor} ${padBtnBg}`}>
                <IconTerminal className="w-3 h-3 shrink-0" />
                <span className="font-semibold">{showCode ? 'Hide Code' : 'Show Code'}</span>
              </button>
            </div>
          </div>
        )}
      </div>

      {!controlsUnderOverlay && showCode && (
        <div className={`mt-2 w-full max-w-[560px] rounded-xl border ${lineColor} ${containerBg} overflow-hidden`}>
          <div className={`px-3 py-1.5 text-[11px] font-mono truncate border-b ${lineColor} ${textColor}`}>
            {fileName || mimeType || 'mini-game source'}
          </div>
          <div className="max-h-56 overflow-auto" style={{ overscrollBehavior: 'contain', touchAction: 'pan-y', WebkitOverflowScrolling: 'touch' as any }}>
            <pre className={`p-3 text-[11px] leading-5 font-mono whitespace-pre w-max min-w-full ${textColor}`}>
              {sourceCode}
            </pre>
          </div>
        </div>
      )}

      {!sourceCode.trim() && (
        <div className={`mt-2 w-full max-w-[560px] rounded-lg border ${lineColor} ${containerBg} p-3 text-center`}>
          <IconPaperclip className={`w-6 h-6 mx-auto ${textColor}`} />
          <p className={`mt-1 text-xs ${subtleText}`}>Mini-game code is empty.</p>
        </div>
      )}
    </div>
  );
});

export default MiniGameViewer;
