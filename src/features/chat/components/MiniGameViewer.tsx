// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { IconEnableGameGestures, IconPaperclip, IconTerminal, IconUndo } from '../../../shared/ui/Icons';
import AttachmentInteractionToggle from './AttachmentInteractionToggle';
import { buildMiniGameSrcDoc } from '../utils/miniGameAttachment';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';

type MiniGameRuntimeState = 'booting' | 'ready' | 'error';
type MiniGameInteractionMode = 'scroll' | 'gestures';
type MiniGameForwardedPointerKind = 'pointerdown' | 'pointerup' | 'click';

interface MiniGameFrameMetrics {
  width: number;
  height: number;
  aspectRatio: number;
}

interface MiniGamePointerGateState {
  pointerId: number | null;
  startClientX: number;
  startClientY: number;
  button: number;
  buttons: number;
  canForward: boolean;
  hasMoved: boolean;
}

interface MiniGameViewerProps {
  sourceCode: string;
  variant: 'user' | 'assistant' | 'preview';
  fileName?: string | null;
  mimeType?: string | null;
  bottomInset?: number;
}

interface RectLike {
  top: number;
  right: number;
  bottom: number;
  left: number;
  width: number;
  height: number;
}

interface MiniGameInteractionDeckToggleProps {
  gameGesturesEnabled: boolean;
  canUseGameGestures: boolean;
  compact?: boolean;
  groupLabel: string;
  gameGesturesLabel: string;
  gameGesturesTitle: string;
  returnToChatScrollLabel: string;
  gameGesturesUnavailableLabel: string;
  textColor: string;
  subtleText: string;
  lineColor: string;
  containerBg: string;
  padBtnBg: string;
  onSelectMode: (enabled: boolean, event: React.MouseEvent<HTMLButtonElement>) => void;
}

const FULL_VISIBILITY_TOLERANCE_PX = 3;
const TAP_SLOP_PX = 9;

const createEmptyPointerGateState = (): MiniGamePointerGateState => ({
  pointerId: null,
  startClientX: 0,
  startClientY: 0,
  button: 0,
  buttons: 0,
  canForward: false,
  hasMoved: false,
});

const isScrollableOverflow = (value: string): boolean => /(auto|scroll|overlay)/i.test(value);

const getNearestScrollContainer = (element: HTMLElement | null): HTMLElement | null => {
  if (!element || typeof window === 'undefined') return null;

  let parent = element.parentElement;
  while (parent) {
    const style = window.getComputedStyle(parent);
    if (isScrollableOverflow(style.overflowY || '')) {
      return parent;
    }
    parent = parent.parentElement;
  }

  return null;
};

const getViewportRect = (): RectLike => {
  const visualViewport = typeof window !== 'undefined' ? window.visualViewport : null;
  const width = visualViewport?.width ?? window.innerWidth;
  const height = visualViewport?.height ?? window.innerHeight;
  const left = visualViewport?.offsetLeft ?? 0;
  const top = visualViewport?.offsetTop ?? 0;
  return {
    top,
    left,
    right: left + width,
    bottom: top + height,
    width,
    height,
  };
};

const getVisibilityRootRect = (element: HTMLElement): RectLike => {
  const scrollContainer = getNearestScrollContainer(element);
  return scrollContainer ? scrollContainer.getBoundingClientRect() : getViewportRect();
};

const isFullyInsideRect = (elementRect: DOMRect, rootRect: RectLike): boolean => (
  elementRect.top >= rootRect.top - FULL_VISIBILITY_TOLERANCE_PX &&
  elementRect.left >= rootRect.left - FULL_VISIBILITY_TOLERANCE_PX &&
  elementRect.right <= rootRect.right + FULL_VISIBILITY_TOLERANCE_PX &&
  elementRect.bottom <= rootRect.bottom + FULL_VISIBILITY_TOLERANCE_PX
);

const MiniGameInteractionDeckToggle: React.FC<MiniGameInteractionDeckToggleProps> = ({
  gameGesturesEnabled,
  canUseGameGestures,
  compact = false,
  groupLabel,
  gameGesturesLabel,
  gameGesturesTitle,
  returnToChatScrollLabel,
  gameGesturesUnavailableLabel,
  textColor,
  subtleText,
  lineColor,
  containerBg,
  padBtnBg,
  onSelectMode,
}) => {
  return (
    <AttachmentInteractionToggle
      compact={compact}
      isAttachmentModeEnabled={gameGesturesEnabled}
      isAttachmentModeAvailable={canUseGameGestures}
      attachmentLabel={gameGesturesLabel}
      attachmentTitle={gameGesturesTitle}
      attachmentUnavailableTitle={gameGesturesUnavailableLabel}
      chatLabel={returnToChatScrollLabel}
      chatTitle={returnToChatScrollLabel}
      groupLabel={groupLabel}
      AttachmentIcon={IconEnableGameGestures}
      activeTextClassName={textColor}
      inactiveTextClassName={subtleText}
      activeSurfaceClassName={containerBg}
      inactiveSurfaceClassName={padBtnBg}
      borderClassName={lineColor}
      onSelectMode={onSelectMode}
    />
  );
};

const MiniGameViewer: React.FC<MiniGameViewerProps> = React.memo(({
  sourceCode,
  variant,
  fileName,
  mimeType,
  bottomInset = 0,
}) => {
  const { t } = useAppTranslations();
  const [showCode, setShowCode] = useState(false);
  const [runtimeState, setRuntimeState] = useState<MiniGameRuntimeState>('booting');
  const [runtimeError, setRuntimeError] = useState<string>('');
  const [reloadToken, setReloadToken] = useState(0);
  const [frameMetrics, setFrameMetrics] = useState<MiniGameFrameMetrics | null>(null);

  const [hasIntersected, setHasIntersected] = useState(false);
  const [isFrameFullyVisible, setIsFrameFullyVisible] = useState(false);
  const [frameHeightCap, setFrameHeightCap] = useState<number | null>(null);
  const [gameGesturesEnabled, setGameGesturesEnabled] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const frameShellRef = useRef<HTMLDivElement>(null);
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const pointerGateRef = useRef<MiniGamePointerGateState>(createEmptyPointerGateState());

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

  const resetPointerGate = useCallback(() => {
    pointerGateRef.current = createEmptyPointerGateState();
  }, []);

  const postMiniGameMessage = useCallback((payload: Record<string, unknown>) => {
    const targetWindow = iframeRef.current?.contentWindow;
    if (!targetWindow) return;
    targetWindow.postMessage({ ...payload, frameId }, '*');
  }, [frameId]);

  const postMiniGameMode = useCallback((mode: MiniGameInteractionMode) => {
    postMiniGameMessage({ type: 'maestro-mini-game-mode', mode });
  }, [postMiniGameMessage]);

  const getIframePoint = useCallback((clientX: number, clientY: number) => {
    const iframe = iframeRef.current;
    if (!iframe) return null;
    const rect = iframe.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) return null;

    return {
      x: Math.max(0, Math.min(rect.width, clientX - rect.left)),
      y: Math.max(0, Math.min(rect.height, clientY - rect.top)),
    };
  }, []);

  const postPointerInput = useCallback((
    kind: MiniGameForwardedPointerKind,
    event: React.PointerEvent<HTMLDivElement>,
    clientX = event.clientX,
    clientY = event.clientY,
    overrides?: Partial<Pick<MiniGamePointerGateState, 'button' | 'buttons'>>,
  ) => {
    const point = getIframePoint(clientX, clientY);
    if (!point) return;

    postMiniGameMessage({
      type: 'maestro-mini-game-input',
      kind,
      x: point.x,
      y: point.y,
      pointerId: event.pointerId,
      pointerType: event.pointerType || 'mouse',
      button: overrides?.button ?? event.button,
      buttons: overrides?.buttons ?? event.buttons,
      isPrimary: event.isPrimary,
      altKey: event.altKey,
      ctrlKey: event.ctrlKey,
      metaKey: event.metaKey,
      shiftKey: event.shiftKey,
    });
  }, [getIframePoint, postMiniGameMessage]);

  const handleReload = useCallback(() => {
    setGameGesturesEnabled(false);
    resetPointerGate();
    setRuntimeState('booting');
    setReloadToken((n) => n + 1);
  }, [resetPointerGate]);

  const handleToggleCode = useCallback(() => {
    setGameGesturesEnabled(false);
    resetPointerGate();
    setShowCode((prev) => !prev);
  }, [resetPointerGate]);

  useEffect(() => {
    setGameGesturesEnabled(false);
    setIsFrameFullyVisible(false);
    resetPointerGate();
    postMiniGameMode('scroll');
  }, [frameId, sourceCode, fileName, mimeType, resetPointerGate, postMiniGameMode]);

  useEffect(() => {
    if (!hasIntersected) return;

    setRuntimeState('booting');
    setRuntimeError('');
    setFrameMetrics(null);

    const timeout = window.setTimeout(() => {
      setRuntimeState((prev) => (prev === 'booting' ? 'ready' : prev));
    }, 1800);

    const onMessage = (event: MessageEvent) => {
      const payload = event.data;
      if (!payload || payload.type !== 'maestro-mini-game-status' || payload.frameId !== frameId) return;

      if (payload.status === 'metrics' && payload.metrics) {
        const nextWidth = typeof payload.metrics.width === 'number' && Number.isFinite(payload.metrics.width) ? payload.metrics.width : 0;
        const nextHeight = typeof payload.metrics.height === 'number' && Number.isFinite(payload.metrics.height) ? payload.metrics.height : 0;
        const nextAspectRatio = typeof payload.metrics.aspectRatio === 'number' && Number.isFinite(payload.metrics.aspectRatio) ? payload.metrics.aspectRatio : 0;

        if (nextWidth >= 16 && nextHeight >= 16) {
          setFrameMetrics((prev) => {
            if (
              prev &&
              Math.abs(prev.width - nextWidth) < 2 &&
              Math.abs(prev.height - nextHeight) < 2 &&
              Math.abs(prev.aspectRatio - nextAspectRatio) < 0.02
            ) {
              return prev;
            }

            return {
              width: nextWidth,
              height: nextHeight,
              aspectRatio: nextAspectRatio > 0 ? nextAspectRatio : nextWidth / nextHeight,
            };
          });
        }
        return;
      }

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
  const textColor = isUser ? 'text-user-attachment-game-text' : 'text-ai-file-text';
  const subtleText = isUser ? 'text-user-attachment-game-text/70' : 'text-ai-file-text/70';
  const lineColor = isUser ? 'border-user-attachment-game-text/25' : 'border-ai-file-text/25';
  const padBtnBg = isUser ? 'bg-user-msg-bg/50 hover:bg-user-msg-bg/65' : 'bg-ai-msg-bg/55 hover:bg-ai-msg-bg/70';
  const statusBubbleBg = runtimeState === 'error' ? 'bg-red-900/80' : 'bg-black/70';
  const effectiveBottomInset = Math.max(0, Math.round(bottomInset));
  const controlsUnderOverlay = effectiveBottomInset > 0;
  const focusedShellHeight = Math.max(92, Math.min(Math.round(effectiveBottomInset * 0.45) + 32, 122));
  const wrapperBottomPadding = controlsUnderOverlay ? Math.max(72, focusedShellHeight - 10) : 8;
  const fallbackFrameHeight = controlsUnderOverlay ? 520 : 480;
  const updateFrameAvailability = useCallback(() => {
    const frameShell = frameShellRef.current;
    if (!frameShell || typeof window === 'undefined') return;

    const rootRect = getVisibilityRootRect(frameShell);
    const reservedHeight = controlsUnderOverlay ? 28 : 92;
    const nextFrameHeightCap = Math.max(220, Math.floor(rootRect.height - reservedHeight));
    setFrameHeightCap((prev) => (prev === nextFrameHeightCap ? prev : nextFrameHeightCap));

    const frameRect = frameShell.getBoundingClientRect();
    const nextIsFullyVisible = isFullyInsideRect(frameRect, rootRect);
    setIsFrameFullyVisible((prev) => (prev === nextIsFullyVisible ? prev : nextIsFullyVisible));
  }, [controlsUnderOverlay]);
  const effectiveFrameHeightCap = frameHeightCap ?? 960;
  const resolvedFrameHeight = Math.max(
    220,
    Math.min(
      Math.round(frameMetrics?.height || fallbackFrameHeight),
      960,
      effectiveFrameHeightCap,
    ),
  );
  const gameScreenStyle: React.CSSProperties = {
    width: '100%',
    height: `${resolvedFrameHeight}px`,
    minHeight: '220px',
    backgroundColor: 'transparent',
  };

  const overlayIconShadowStyle: React.CSSProperties = {
    filter: 'drop-shadow(0 1px 2px rgba(0,0,0,0.72))',
  };
  const actionButtonClass = 'p-2 rounded-full text-white/90 opacity-85 transition-all duration-200 hover:text-white hover:opacity-100 focus:outline-none focus:ring-2 focus:ring-white/40 active:scale-95';
  const translateOrFallback = useCallback((key: string, fallback: string) => {
    const result = t(key);
    return result === key ? fallback : result;
  }, [t]);
  const canUseGameGestures = hasIntersected && runtimeState === 'ready' && isFrameFullyVisible && !showCode;
  const useGameGesturesLabel = translateOrFallback('miniGame.useGameGestures', 'Game swipes');
  const useGameGesturesTitle = translateOrFallback('miniGame.useGameGesturesTitle', 'Use game swipes');
  const returnToChatScrollLabel = translateOrFallback('miniGame.returnToChatScroll', 'Chat scroll');
  const gameGesturesUnavailableLabel = translateOrFallback('miniGame.gameGesturesUnavailable', 'Fully show game to use swipes');
  const interactionModeGroupLabel = translateOrFallback('miniGame.interactionMode', 'Mini-game interaction mode');

  useEffect(() => {
    if (!hasIntersected) return;
    const animationFrame = window.requestAnimationFrame(updateFrameAvailability);
    return () => { window.cancelAnimationFrame(animationFrame); };
  }, [hasIntersected, resolvedFrameHeight, showCode, updateFrameAvailability]);

  useEffect(() => {
    if (!hasIntersected) return;
    const frameShell = frameShellRef.current;
    if (!frameShell || typeof window === 'undefined') return;

    let animationFrame = 0;
    const scheduleAvailabilityUpdate = () => {
      if (animationFrame) {
        window.cancelAnimationFrame(animationFrame);
      }
      animationFrame = window.requestAnimationFrame(() => {
        animationFrame = 0;
        updateFrameAvailability();
      });
    };

    const handleScroll = () => {
      setGameGesturesEnabled(false);
      resetPointerGate();
      scheduleAvailabilityUpdate();
    };

    const scrollContainer = getNearestScrollContainer(frameShell);
    scrollContainer?.addEventListener('scroll', handleScroll, { passive: true });
    window.addEventListener('scroll', handleScroll, { passive: true });
    window.addEventListener('resize', scheduleAvailabilityUpdate, { passive: true });

    const resizeObserver = typeof ResizeObserver !== 'undefined'
      ? new ResizeObserver(scheduleAvailabilityUpdate)
      : null;
    resizeObserver?.observe(frameShell);
    if (scrollContainer) resizeObserver?.observe(scrollContainer);

    scheduleAvailabilityUpdate();

    return () => {
      if (animationFrame) window.cancelAnimationFrame(animationFrame);
      scrollContainer?.removeEventListener('scroll', handleScroll);
      window.removeEventListener('scroll', handleScroll);
      window.removeEventListener('resize', scheduleAvailabilityUpdate);
      resizeObserver?.disconnect();
    };
  }, [hasIntersected, resetPointerGate, updateFrameAvailability]);

  useEffect(() => {
    if (!canUseGameGestures && gameGesturesEnabled) {
      setGameGesturesEnabled(false);
      resetPointerGate();
    }
  }, [canUseGameGestures, gameGesturesEnabled, resetPointerGate]);

  useEffect(() => {
    if (!hasIntersected) return;
    postMiniGameMode(gameGesturesEnabled ? 'gestures' : 'scroll');
  }, [gameGesturesEnabled, hasIntersected, postMiniGameMode]);

  const handleSelectGameGestureMode = useCallback((nextEnabled: boolean, event: React.MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();
    event.stopPropagation();
    resetPointerGate();

    if (nextEnabled === gameGesturesEnabled) {
      return;
    }

    if (nextEnabled && !canUseGameGestures) return;

    setGameGesturesEnabled(nextEnabled);
    if (nextEnabled) {
      window.setTimeout(() => {
        iframeRef.current?.focus();
      }, 0);
    }
  }, [canUseGameGestures, gameGesturesEnabled, resetPointerGate]);

  const handleGatePointerDown = useCallback((event: React.PointerEvent<HTMLDivElement>) => {
    if (!event.isPrimary || event.button !== 0 || gameGesturesEnabled || showCode) return;

    event.stopPropagation();
    pointerGateRef.current = {
      pointerId: event.pointerId,
      startClientX: event.clientX,
      startClientY: event.clientY,
      button: event.button,
      buttons: event.buttons || 1,
      canForward: canUseGameGestures,
      hasMoved: false,
    };
  }, [canUseGameGestures, gameGesturesEnabled, showCode]);

  const handleGatePointerMove = useCallback((event: React.PointerEvent<HTMLDivElement>) => {
    const state = pointerGateRef.current;
    if (state.pointerId !== event.pointerId || !event.isPrimary) return;

    event.stopPropagation();

    const deltaX = event.clientX - state.startClientX;
    const deltaY = event.clientY - state.startClientY;
    if (Math.abs(deltaX) > TAP_SLOP_PX || Math.abs(deltaY) > TAP_SLOP_PX) {
      state.hasMoved = true;
    }
  }, []);

  const handleGatePointerUp = useCallback((event: React.PointerEvent<HTMLDivElement>) => {
    const state = pointerGateRef.current;
    if (state.pointerId !== event.pointerId || !event.isPrimary) return;

    event.stopPropagation();

    const deltaX = event.clientX - state.startClientX;
    const deltaY = event.clientY - state.startClientY;
    const isTap = Math.abs(deltaX) <= TAP_SLOP_PX && Math.abs(deltaY) <= TAP_SLOP_PX;

    if (state.canForward && isTap && !state.hasMoved) {
      event.preventDefault();
      postPointerInput('pointerdown', event, state.startClientX, state.startClientY, {
        button: state.button,
        buttons: state.buttons || 1,
      });
      postPointerInput('pointerup', event, event.clientX, event.clientY, {
        button: state.button,
        buttons: 0,
      });
      postPointerInput('click', event, event.clientX, event.clientY, {
        button: state.button,
        buttons: 0,
      });
    }

    try {
      if (event.currentTarget.hasPointerCapture(event.pointerId)) {
        event.currentTarget.releasePointerCapture(event.pointerId);
      }
    } catch (e) {
    }
    resetPointerGate();
  }, [resetPointerGate]);

  const handleGatePointerCancel = useCallback((event: React.PointerEvent<HTMLDivElement>) => {
    const state = pointerGateRef.current;
    if (state.pointerId !== event.pointerId || !event.isPrimary) return;

    event.stopPropagation();

    try {
      if (event.currentTarget.hasPointerCapture(event.pointerId)) {
        event.currentTarget.releasePointerCapture(event.pointerId);
      }
    } catch (e) {
    }
    resetPointerGate();
  }, [postPointerInput, resetPointerGate]);

  return (
    <div ref={containerRef} className="w-full flex flex-col items-center">
      <div className="relative w-full max-w-[560px]" style={{ paddingBottom: `${wrapperBottomPadding}px` }}>

        <div
          ref={frameShellRef}
          className={`relative rounded-2xl overflow-hidden border ${lineColor} shadow-none ${controlsUnderOverlay && showCode ? 'z-30' : 'z-10'}`}
          style={gameScreenStyle}
        >
          {hasIntersected ? (
            <iframe
              ref={iframeRef}
              title={fileName ? t('miniGame.titleWithFile', { fileName }) || `Mini game ${fileName}` : t('miniGame.title') || 'Mini game'}
              srcDoc={srcDoc}
              className="w-full h-full border-0 bg-transparent block"
              sandbox="allow-scripts allow-same-origin"
              referrerPolicy="no-referrer"
              loading="lazy"
              style={{
                backgroundColor: 'transparent',
                pointerEvents: gameGesturesEnabled ? 'auto' : 'none',
                touchAction: gameGesturesEnabled ? 'none' : 'pan-y',
              }}
            />
          ) : (
            <div className="w-full h-full" />
          )}

          {hasIntersected && !showCode && !gameGesturesEnabled && (
            <div
              className="absolute inset-0 z-20 bg-transparent"
              style={{ touchAction: 'pan-y' }}
              onPointerDown={handleGatePointerDown}
              onPointerMove={handleGatePointerMove}
              onPointerUp={handleGatePointerUp}
              onPointerCancel={handleGatePointerCancel}
              onClick={(event) => { event.stopPropagation(); }}
              aria-hidden
            />
          )}

          {runtimeState !== 'ready' && hasIntersected && (
            <div className={`absolute left-2 right-2 top-2 z-10 rounded-lg px-2 py-1 text-[11px] ${statusBubbleBg} text-white`}>
              {runtimeState === 'error' ? t('miniGame.runtimeError', { error: runtimeError }) || `Mini-game error: ${runtimeError}` : t('miniGame.launching') || 'Launching mini-game...'}
            </div>
          )}

          {controlsUnderOverlay && (
            <div className="absolute top-2 right-2 z-30 flex flex-col gap-2 pointer-events-auto">
              <MiniGameInteractionDeckToggle
                compact
                gameGesturesEnabled={gameGesturesEnabled}
                canUseGameGestures={canUseGameGestures}
                groupLabel={interactionModeGroupLabel}
                gameGesturesLabel={useGameGesturesLabel}
                gameGesturesTitle={useGameGesturesTitle}
                returnToChatScrollLabel={returnToChatScrollLabel}
                gameGesturesUnavailableLabel={gameGesturesUnavailableLabel}
                textColor="text-white"
                subtleText="text-white/80"
                lineColor="border-white/25"
                containerBg="bg-black/55"
                padBtnBg="bg-black/35 hover:bg-black/50"
                onSelectMode={handleSelectGameGestureMode}
              />
              <button onClick={handleReload} className={actionButtonClass} title={t('miniGame.restart') || 'Restart'}>
                <span style={overlayIconShadowStyle}>
                  <IconUndo className="w-4 h-4" />
                </span>
              </button>
              <button onClick={handleToggleCode} className={actionButtonClass} title={showCode ? t('miniGame.hideCode') || 'Hide Code' : t('miniGame.showCode') || 'Show Code'}>
                <span style={overlayIconShadowStyle}>
                  <IconTerminal className="w-4 h-4" />
                </span>
              </button>
            </div>
          )}

          {controlsUnderOverlay && showCode && (
            <div className="notebook-source-paper paper-texture notebook-lines sketch-shape-4 absolute inset-2 z-20 overflow-hidden border border-sketch-line/30 shadow-[0_12px_28px_rgba(2,6,23,0.18)]">
              <div className="px-3 py-1.5 pr-12 font-architect text-[12px] font-semibold truncate text-deep-ink border-b border-sketch-line/20">
                {fileName || mimeType || 'mini-game source'}
              </div>
              <div
                className="max-h-full overflow-auto"
                style={{ height: 'calc(100% - 32px)', overscrollBehavior: 'contain', touchAction: 'pan-y', WebkitOverflowScrolling: 'touch' as any }}
              >
                <pre className="notebook-source-pre p-3 text-[11px] leading-5 whitespace-pre w-max min-w-full">
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
              <MiniGameInteractionDeckToggle
                gameGesturesEnabled={gameGesturesEnabled}
                canUseGameGestures={canUseGameGestures}
                groupLabel={interactionModeGroupLabel}
                gameGesturesLabel={useGameGesturesLabel}
                gameGesturesTitle={useGameGesturesTitle}
                returnToChatScrollLabel={returnToChatScrollLabel}
                gameGesturesUnavailableLabel={gameGesturesUnavailableLabel}
                textColor={textColor}
                subtleText={subtleText}
                lineColor={lineColor}
                containerBg={containerBg}
                padBtnBg={padBtnBg}
                onSelectMode={handleSelectGameGestureMode}
              />
              <button onClick={handleReload} className={`inline-flex items-center gap-1.5 rounded-full border ${lineColor} px-3 py-1 text-[10px] uppercase tracking-wider ${textColor} ${padBtnBg}`}>
                <IconUndo className="w-3 h-3 shrink-0" />
                <span className="font-semibold">{t('miniGame.restart') || 'Restart'}</span>
              </button>
              <button onClick={handleToggleCode} className={`inline-flex items-center gap-1.5 rounded-full border ${lineColor} px-3 py-1 text-[10px] uppercase tracking-wider ${textColor} ${padBtnBg}`}>
                <IconTerminal className="w-3 h-3 shrink-0" />
                <span className="font-semibold">{showCode ? t('miniGame.hideCode') || 'Hide Code' : t('miniGame.showCode') || 'Show Code'}</span>
              </button>
            </div>
          </div>
        )}
      </div>

      {!controlsUnderOverlay && showCode && (
        <div className="notebook-source-paper paper-texture notebook-lines sketch-shape-4 mt-2 w-full max-w-[560px] overflow-hidden border border-sketch-line/30">
          <div className="px-3 py-1.5 font-architect text-[12px] font-semibold truncate border-b border-sketch-line/20 text-deep-ink">
            {fileName || mimeType || 'mini-game source'}
          </div>
          <div className="max-h-56 overflow-auto" style={{ overscrollBehavior: 'contain', touchAction: 'pan-y', WebkitOverflowScrolling: 'touch' as any }}>
            <pre className="notebook-source-pre p-3 text-[11px] leading-5 whitespace-pre w-max min-w-full">
              {sourceCode}
            </pre>
          </div>
        </div>
      )}

      {!sourceCode.trim() && (
        <div className={`mt-2 w-full max-w-[560px] rounded-lg border ${lineColor} ${containerBg} p-3 text-center`}>
          <IconPaperclip className={`w-6 h-6 mx-auto ${textColor}`} />
          <p className={`mt-1 text-xs ${subtleText}`}>{t('miniGame.codeEmpty') || 'Mini-game code is empty.'}</p>
        </div>
      )}
    </div>
  );
});

export default MiniGameViewer;
