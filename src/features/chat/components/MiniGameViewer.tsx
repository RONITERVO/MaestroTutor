// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { IconPaperclip, IconTerminal, IconUndo } from '../../../shared/ui/Icons';
import { buildMiniGameSrcDoc } from '../utils/miniGameAttachment';

type MiniGameRuntimeState = 'booting' | 'ready' | 'error';

interface MiniGameContentMetrics {
  width: number;
  height: number;
}

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
  const [contentMetrics, setContentMetrics] = useState<MiniGameContentMetrics | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [isNearViewport, setIsNearViewport] = useState(true);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const el = containerRef.current;
    if (!el || typeof IntersectionObserver === 'undefined') return;

    const observer = new IntersectionObserver(
      ([entry]) => { setIsNearViewport(entry.isIntersecting); },
      { rootMargin: '600px' },
    );
    observer.observe(el);
    return () => { observer.disconnect(); };
  }, []);

  const frameId = useMemo(
    () => `mini-game-${Math.random().toString(36).slice(2, 10)}-${reloadToken}`,
    [reloadToken]
  );

  const srcDoc = useMemo(
    () => buildMiniGameSrcDoc({
      sourceCode,
      fileName,
      mimeType,
      frameId,
    }),
    [sourceCode, fileName, mimeType, frameId]
  );

  const handleReload = useCallback(() => setReloadToken((n) => n + 1), []);

  // Reset boot state when iframe remounts (reload or scroll back into view)
  useEffect(() => {
    if (!isNearViewport) return;

    setRuntimeState('booting');
    setRuntimeError('');
    setContentMetrics(null);

    const timeout = window.setTimeout(() => {
      setRuntimeState((prev) => (prev === 'booting' ? 'ready' : prev));
    }, 1800);

    const onMessage = (event: MessageEvent) => {
      const payload = event.data as
        | { type?: string; frameId?: string; status?: string; detail?: string; width?: number; height?: number }
        | null
        | undefined;
      if (!payload || payload.type !== 'maestro-mini-game-status' || payload.frameId !== frameId) {
        return;
      }
      if (
        payload.status === 'metrics' &&
        typeof payload.width === 'number' &&
        Number.isFinite(payload.width) &&
        payload.width > 0 &&
        typeof payload.height === 'number' &&
        Number.isFinite(payload.height) &&
        payload.height > 0
      ) {
        setContentMetrics({ width: payload.width, height: payload.height });
        return;
      }
      if (payload.status === 'error') {
        setRuntimeState('error');
        setRuntimeError((payload.detail || 'Runtime error').slice(0, 220));
        return;
      }
      if (payload.status === 'ready') {
        setRuntimeState('ready');
      }
    };

    window.addEventListener('message', onMessage);
    return () => {
      window.clearTimeout(timeout);
      window.removeEventListener('message', onMessage);
    };
  }, [frameId, srcDoc, isNearViewport]);

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
  const focusedAccentBottom = Math.max(14, Math.min(Math.round(focusedShellHeight * 0.28), 28));
  const shellBackground = isUser
    ? 'linear-gradient(180deg, hsl(var(--user-msg-bg) / 0.95) 0%, hsl(var(--user-msg-bg) / 0.88) 100%)'
    : 'linear-gradient(180deg, hsl(var(--ai-msg-bg)) 0%, hsl(var(--ai-msg-bg) / 0.94) 100%)';
  const shellHighlight = isUser
    ? 'linear-gradient(180deg, hsl(var(--user-msg-text) / 0.08), transparent)'
    : 'linear-gradient(180deg, hsl(var(--paper-surface) / 0.45), transparent)';
  const hasContentMetrics = Boolean(contentMetrics && contentMetrics.width > 0 && contentMetrics.height > 0);
  const contentAspectRatio = hasContentMetrics && contentMetrics
    ? `${contentMetrics.width} / ${contentMetrics.height}`
    : undefined;
  const gameScreenStyle: React.CSSProperties = hasContentMetrics
    ? {
        aspectRatio: contentAspectRatio,
        height: 'auto',
        minHeight: '220px',
        maxHeight: controlsUnderOverlay ? 'min(68vh, 520px)' : 'min(74vh, 560px)',
      }
    : { height: 'min(62vh, 480px)' };

  const actionButtonClass = 'p-2 bg-black/50 text-white rounded-full hover:bg-black/75 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-black/50 focus:ring-white transition-colors';

  return (
    <div ref={containerRef} className="w-full flex flex-col items-center">
      <div className="relative w-full max-w-[560px]" style={{ paddingBottom: `${wrapperBottomPadding}px` }}>

        {/* GAME SCREEN */}
        <div
          className={`relative w-full min-h-[220px] rounded-2xl overflow-hidden border ${lineColor} bg-black shadow-[0_14px_30px_rgba(2,6,23,0.38)] ${controlsUnderOverlay && showCode ? 'z-30' : 'z-10'}`}
          style={gameScreenStyle}
        >
          {isNearViewport ? (
            <iframe
              key={frameId}
              title={fileName ? `Mini game ${fileName}` : 'Mini game'}
              srcDoc={srcDoc}
              className="w-full h-full border-0 bg-black"
              sandbox="allow-scripts"
              referrerPolicy="no-referrer"
              loading="lazy"
            />
          ) : (
            <div className="w-full h-full bg-black" />
          )}
          {runtimeState !== 'ready' && isNearViewport && (
            <div className={`absolute left-2 right-2 top-2 z-10 rounded-lg px-2 py-1 text-[11px] ${statusBubbleBg} text-white`}>
              {runtimeState === 'error' ? `Mini-game error: ${runtimeError}` : 'Launching mini-game...'}
            </div>
          )}

          {controlsUnderOverlay && (
            <div className="absolute top-2 right-2 z-30 flex flex-col gap-2 pointer-events-auto">
              <button
                type="button"
                onClick={handleReload}
                className={actionButtonClass}
                title="Restart"
                aria-label="Restart mini-game"
              >
                <IconUndo className="w-4 h-4" />
              </button>
              <button
                type="button"
                onClick={() => setShowCode((prev) => !prev)}
                className={actionButtonClass}
                title={showCode ? 'Hide code' : 'Show code'}
                aria-label={showCode ? 'Hide mini-game code' : 'Show mini-game code'}
              >
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

        {/* Controls: outside overflow-hidden so the bottom peek is always visible */}
        {controlsUnderOverlay ? (
          <div
            className="absolute left-2 right-2 z-0 pointer-events-none overflow-hidden rounded-[28px]"
            style={{
              bottom: '0',
              height: `${focusedShellHeight}px`,
              border: '2px solid hsl(var(--pencil-stroke) / 0.28)',
              borderTop: 'none',
              background: shellBackground,
              boxShadow: '0 14px 30px hsl(var(--sketch-shadow) / 0.2), inset 0 1px 0 hsl(var(--paper-surface) / 0.55)',
            }}
            aria-hidden
          >
            <div
              className="absolute inset-x-0 top-0"
              style={{
                height: '34px',
                background: shellHighlight,
              }}
            />

            <div className="absolute left-1/2 top-4 -translate-x-1/2">
              <span
                className="text-[10px] uppercase tracking-[0.22em] font-bold select-none"
                style={{
                  color: 'hsl(var(--sketch-line) / 0.7)',
                  fontFamily: "'Patrick Hand', cursive",
                }}
              >
                MAESTRO
              </span>
            </div>

            <div
              className="absolute left-6 right-6 flex items-end justify-between"
              style={{ bottom: `${focusedAccentBottom}px` }}
            >
              <div className="relative shrink-0" style={{ width: '48px', height: '48px', opacity: 0.7 }}>
                <div className="absolute top-1/2 left-0 w-full" style={{ height: '14px', transform: 'translateY(-50%)', borderRadius: '2px', backgroundColor: 'hsl(var(--sketch-line) / 0.2)', border: '1px solid hsl(var(--sketch-line) / 0.15)' }} />
                <div className="absolute left-1/2 top-0 h-full" style={{ width: '14px', transform: 'translateX(-50%)', borderRadius: '2px', backgroundColor: 'hsl(var(--sketch-line) / 0.2)', border: '1px solid hsl(var(--sketch-line) / 0.15)' }} />
                <div className="absolute top-1/2 left-1/2 rounded-full" style={{ width: '8px', height: '8px', transform: 'translate(-50%, -50%)', backgroundColor: 'hsl(var(--sketch-line) / 0.35)' }} />
              </div>

              <div className="absolute left-1/2 -translate-x-1/2 flex items-center gap-3 opacity-45" style={{ bottom: '-2px' }}>
                <div className="rounded-full" style={{ width: '28px', height: '8px', backgroundColor: 'hsl(var(--pencil-stroke) / 0.16)', border: '1px solid hsl(var(--pencil-stroke) / 0.18)' }} />
                <div className="rounded-full" style={{ width: '28px', height: '8px', backgroundColor: 'hsl(var(--pencil-stroke) / 0.16)', border: '1px solid hsl(var(--pencil-stroke) / 0.18)' }} />
              </div>

              <div className="flex items-center gap-3 shrink-0" style={{ transform: 'rotate(-20deg)', opacity: 0.74 }}>
                <div className="flex flex-col items-center gap-0.5 mt-2">
                  <div className="rounded-full" style={{ width: '26px', height: '26px', backgroundColor: 'hsl(var(--pencil-stroke) / 0.15)', border: '1.5px solid hsl(var(--pencil-stroke) / 0.2)', boxShadow: 'inset 0 2px 4px hsl(var(--pencil-stroke) / 0.1)' }} />
                  <span className="text-[8px] font-bold select-none" style={{ color: 'hsl(var(--sketch-line) / 0.6)', fontFamily: "'Patrick Hand', cursive" }}>B</span>
                </div>
                <div className="flex flex-col items-center gap-0.5" style={{ marginTop: '-8px' }}>
                  <div className="rounded-full" style={{ width: '26px', height: '26px', backgroundColor: 'hsl(var(--pencil-stroke) / 0.15)', border: '1.5px solid hsl(var(--pencil-stroke) / 0.2)', boxShadow: 'inset 0 2px 4px hsl(var(--pencil-stroke) / 0.1)' }} />
                  <span className="text-[8px] font-bold select-none" style={{ color: 'hsl(var(--sketch-line) / 0.6)', fontFamily: "'Patrick Hand', cursive" }}>A</span>
                </div>
              </div>
            </div>
          </div>
        ) : (
          <div className="w-full mt-3 flex justify-center z-10 pointer-events-auto">
            <div className={`rounded-xl border ${lineColor} ${containerBg} px-4 py-2 backdrop-blur-sm pointer-events-auto shadow-sm flex items-center gap-4`}>
              <button
                type="button"
                onClick={handleReload}
                className={`inline-flex items-center gap-1.5 rounded-full border ${lineColor} px-3 py-1 text-[10px] uppercase tracking-wider ${textColor} ${padBtnBg} transition-transform active:scale-95`}
                title="Restart"
              >
                <IconUndo className="w-3 h-3 shrink-0" />
                <span className="font-semibold">Restart</span>
              </button>
              <button
                type="button"
                onClick={() => setShowCode((prev) => !prev)}
                className={`inline-flex items-center gap-1.5 rounded-full border ${lineColor} px-3 py-1 text-[10px] uppercase tracking-wider ${textColor} ${padBtnBg} transition-transform active:scale-95`}
                title={showCode ? 'Hide code' : 'Show code'}
              >
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
          <div
            className="max-h-56 overflow-auto"
            style={{ overscrollBehavior: 'contain', touchAction: 'pan-y', WebkitOverflowScrolling: 'touch' as any }}
          >
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

MiniGameViewer.displayName = 'MiniGameViewer';

export default MiniGameViewer;
