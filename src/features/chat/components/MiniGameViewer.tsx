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
  const [controlsExpanded, setControlsExpanded] = useState(false);
  const [runtimeState, setRuntimeState] = useState<MiniGameRuntimeState>('booting');
  const [runtimeError, setRuntimeError] = useState<string>('');
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

    const timeout = window.setTimeout(() => {
      setRuntimeState((prev) => (prev === 'booting' ? 'ready' : prev));
    }, 1800);

    const onMessage = (event: MessageEvent) => {
      const payload = event.data as
        | { type?: string; frameId?: string; status?: string; detail?: string }
        | null
        | undefined;
      if (!payload || payload.type !== 'maestro-mini-game-status' || payload.frameId !== frameId) {
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
  const textColor = isUser ? 'text-user-msg-text' : 'text-ai-file-text';
  const subtleText = isUser ? 'text-user-msg-text/70' : 'text-ai-file-text/70';
  const lineColor = isUser ? 'border-user-msg-text/25' : 'border-ai-file-text/25';
  const padBtnBg = isUser ? 'bg-user-msg-bg/50 hover:bg-user-msg-bg/65' : 'bg-ai-msg-bg/55 hover:bg-ai-msg-bg/70';
  const statusBubbleBg = runtimeState === 'error' ? 'bg-red-900/80' : 'bg-black/70';
  const effectiveBottomInset = Math.max(0, Math.round(bottomInset));

  return (
    <div
      ref={containerRef}
      className="w-full flex flex-col items-center"
      style={effectiveBottomInset > 0 ? { paddingBottom: `${effectiveBottomInset}px` } : undefined}
    >
      {/* Wrapper: pb-2 gives room for the 8px peek below the game frame */}
      <div className="relative w-full max-w-[560px]" style={{ paddingBottom: '8px' }}>
        <div
          className={`relative w-full min-h-[220px] rounded-2xl overflow-hidden border ${lineColor} bg-black shadow-[0_14px_30px_rgba(2,6,23,0.38)]`}
          style={{ height: 'min(62vh, 480px)' }}
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
        </div>

        {/* Controls: outside overflow-hidden so the bottom peek is always visible */}
        <div
          className="absolute left-2 right-2 z-20 pointer-events-none"
          style={{ bottom: '0' }}
        >
          {controlsExpanded ? (
            <div
              className={`w-full rounded-xl border ${lineColor} ${containerBg} px-3 py-1.5 backdrop-blur-sm pointer-events-auto`}
            >
              <div className="flex items-center justify-between gap-2">
                <div className="flex items-center gap-2 min-w-0">
                  <p className={`text-[10px] uppercase tracking-[0.18em] whitespace-nowrap ${subtleText}`}>Tap Gamepad</p>
                  <button
                    type="button"
                    onClick={() => setControlsExpanded(false)}
                    className={`inline-flex items-center justify-center rounded-full border ${lineColor} w-6 h-6 shrink-0 ${textColor} ${padBtnBg}`}
                    aria-label="Collapse gamepad controls"
                    title="Collapse controls"
                  >
                    <span className="text-[10px] leading-none">v</span>
                  </button>
                </div>
                <div className="flex items-center gap-2 shrink-0">
                  <button
                    type="button"
                    onClick={handleReload}
                    className={`inline-flex items-center gap-1 rounded-full border ${lineColor} px-2 py-1 text-[11px] whitespace-nowrap ${textColor} ${padBtnBg}`}
                  >
                    <IconUndo className="w-3 h-3 shrink-0" />
                    <span className="whitespace-nowrap">Restart</span>
                  </button>
                  <button
                    type="button"
                    onClick={() => setShowCode((prev) => !prev)}
                    className={`inline-flex items-center gap-1 rounded-full border ${lineColor} px-2 py-1 text-[11px] whitespace-nowrap ${textColor} ${padBtnBg}`}
                  >
                    <IconTerminal className="w-3 h-3 shrink-0" />
                    <span className="whitespace-nowrap">{showCode ? 'Hide code' : 'Show code'}</span>
                  </button>
                </div>
              </div>
            </div>
          ) : (
            <div className="flex justify-center">
              <button
                type="button"
                onClick={() => setControlsExpanded(true)}
                className={`pointer-events-auto inline-flex items-center justify-center rounded-full border ${lineColor} w-9 h-6 ${textColor} ${padBtnBg} backdrop-blur-sm shadow-[0_4px_12px_rgba(2,6,23,0.25)]`}
                aria-label="Expand gamepad controls"
                aria-expanded={false}
                title="Expand controls"
              >
                <span className="text-[10px] leading-none">^</span>
              </button>
            </div>
          )}
        </div>
      </div>

      {showCode && (
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

