// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { IconEnableGameGestures, IconPaperclip, IconPlay, IconTerminal, IconUndo } from '../../../shared/ui/Icons';
import AttachmentInteractionToggle from './AttachmentInteractionToggle';
import { buildMiniGameSrcDoc } from '../utils/miniGameAttachment';
import { useAppTranslations } from '../../../shared/hooks/useAppTranslations';
import EmbedBox from '../embeds/EmbedBox';
import { useEmbedSlot } from '../embeds/useEmbedSlot';
import { POSTER_MAX_EDGE_PX, POSTER_QUALITY, dataUrlToBlobUrl } from '../embeds/posterStore';
import {
  EMBED_BOX_VERSION,
  clampAspectRatio,
  resolveEmbedBox,
  shouldCommitMeasuredBox,
} from '../utils/embedIntrinsics';
import type { EmbedBox as EmbedBoxValue } from '../../../core/types';

type MiniGameRuntimeState = 'booting' | 'ready' | 'error';
type MiniGameInteractionMode = 'scroll' | 'gestures';
type MiniGameForwardedPointerKind = 'pointerdown' | 'pointerup' | 'click';

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
  /** Stable identity for the activation slot — normally the message id. */
  embedId: string;
  sourceCode: string;
  variant: 'user' | 'assistant' | 'preview';
  fileName?: string | null;
  mimeType?: string | null;
  bottomInset?: number;
  /** Reserved box persisted on the message, if one has been stored. */
  embedBox?: EmbedBoxValue;
  /** Called only when a live run measured a materially different box. */
  onEmbedBoxChange?: (box: EmbedBoxValue) => void;
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

const TAP_SLOP_PX = 9;
/** First poster once the game has drawn something; then rarely, while live. */
const POSTER_FIRST_CAPTURE_MS = 1800;
const POSTER_REFRESH_MS = 8000;

const createEmptyPointerGateState = (): MiniGamePointerGateState => ({
  pointerId: null,
  startClientX: 0,
  startClientY: 0,
  button: 0,
  buttons: 0,
  canForward: false,
  hasMoved: false,
});

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
  embedId,
  sourceCode,
  variant,
  fileName,
  mimeType,
  bottomInset = 0,
  embedBox,
  onEmbedBoxChange,
}) => {
  const { t } = useAppTranslations();
  const [showCode, setShowCode] = useState(false);
  const [runtimeState, setRuntimeState] = useState<MiniGameRuntimeState>('booting');
  const [runtimeError, setRuntimeError] = useState<string>('');
  const [reloadToken, setReloadToken] = useState(0);
  const [gameGesturesEnabled, setGameGesturesEnabled] = useState(false);

  const iframeRef = useRef<HTMLIFrameElement>(null);
  const pointerGateRef = useRef<MiniGamePointerGateState>(createEmptyPointerGateState());
  /** Latest measurement from the live run; committed on the way out, never live. */
  const measuredAspectRatioRef = useRef(0);
  /** A captured poster we own until the manager takes it. */
  const pendingPosterRef = useRef<string | null>(null);

  const slot = useEmbedSlot({ id: embedId, kind: 'mini-game' });
  const { setRef, isLive, isFullyVisible, poster, pin, unpin, publishPoster, postersEnabled } = slot;

  /**
   * The reserved box. Derived from the source text when nothing is stored, so
   * the very first paint — before any iframe exists — already occupies the
   * right amount of space.
   */
  const resolvedBox = useMemo(
    () => resolveEmbedBox(embedBox, { sourceCode, kind: 'mini-game' }),
    [embedBox, sourceCode],
  );

  const frameId = useMemo(
    () => `mini-game-${embedId.replace(/[^\w-]/g, '').slice(0, 24)}-${reloadToken}`,
    [embedId, reloadToken],
  );

  const srcDoc = useMemo(
    () => buildMiniGameSrcDoc({ sourceCode, fileName, mimeType, frameId }),
    [sourceCode, fileName, mimeType, frameId],
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

  // ---------------------------------------------------------------- lifecycle

  useEffect(() => {
    setGameGesturesEnabled(false);
    resetPointerGate();
  }, [frameId, sourceCode, fileName, mimeType, resetPointerGate]);

  /**
   * Commit a measured box on the way out rather than while running.
   *
   * A live commit would resize the message under the user's finger and shift
   * everything below it — the feedback loop this whole design exists to remove.
   * Committing on teardown means the corrected ratio applies to the *next*
   * mount, at a moment when nothing is animating.
   */
  const commitMeasuredBox = useCallback(() => {
    const measured = measuredAspectRatioRef.current;
    measuredAspectRatioRef.current = 0;
    if (!onEmbedBoxChange || !shouldCommitMeasuredBox(embedBox, measured)) return;
    onEmbedBoxChange({
      aspectRatio: clampAspectRatio(measured),
      source: 'measured',
      v: EMBED_BOX_VERSION,
    });
  }, [embedBox, onEmbedBoxChange]);

  useEffect(() => {
    if (isLive) return;

    // Left the live phase: hand over the poster and the measurement, then let
    // React unmount the iframe. Both are already in hand, so neither step needs
    // to talk to a frame that is about to disappear.
    setRuntimeState('booting');
    setGameGesturesEnabled(false);
    resetPointerGate();

    const pending = pendingPosterRef.current;
    if (pending) {
      pendingPosterRef.current = null;
      publishPoster(pending);
    }
    commitMeasuredBox();
  }, [isLive, commitMeasuredBox, publishPoster, resetPointerGate]);

  useEffect(() => () => {
    // Unmounted without passing through a demotion (message deleted, chat
    // cleared): drop the poster we still own so the blob is not orphaned.
    const pending = pendingPosterRef.current;
    pendingPosterRef.current = null;
    if (pending) URL.revokeObjectURL(pending);
  }, []);

  useEffect(() => {
    if (!isLive) return;

    setRuntimeState('booting');
    setRuntimeError('');

    const bootTimeout = window.setTimeout(() => {
      setRuntimeState((prev) => (prev === 'booting' ? 'ready' : prev));
    }, 1800);

    const onMessage = (event: MessageEvent) => {
      const payload = event.data;
      if (!payload || payload.type !== 'maestro-mini-game-status' || payload.frameId !== frameId) return;

      if (payload.status === 'metrics' && payload.metrics) {
        // Advisory only. Never touches the box that is currently on screen.
        const ratio = Number(payload.metrics.aspectRatio);
        if (Number.isFinite(ratio) && ratio > 0) measuredAspectRatioRef.current = ratio;
        return;
      }

      if (payload.status === 'poster') {
        const dataUrl = typeof payload.poster === 'string' ? payload.poster : '';
        if (!dataUrl) return;
        const blobUrl = dataUrlToBlobUrl(dataUrl);
        if (!blobUrl) return;
        const previous = pendingPosterRef.current;
        pendingPosterRef.current = blobUrl;
        if (previous) URL.revokeObjectURL(previous);
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
      window.clearTimeout(bootTimeout);
      window.removeEventListener('message', onMessage);
    };
  }, [frameId, isLive]);

  /** Keep a recent still on hand, so a demotion never waits on a round trip. */
  useEffect(() => {
    if (!isLive || !postersEnabled || runtimeState !== 'ready') return;

    const requestPoster = () => postMiniGameMessage({
      type: 'maestro-mini-game-poster-request',
      maxEdge: POSTER_MAX_EDGE_PX,
      quality: POSTER_QUALITY,
    });

    const first = window.setTimeout(requestPoster, POSTER_FIRST_CAPTURE_MS);
    const refresh = window.setInterval(requestPoster, POSTER_REFRESH_MS);
    return () => {
      window.clearTimeout(first);
      window.clearInterval(refresh);
    };
  }, [isLive, postersEnabled, runtimeState, postMiniGameMessage]);

  // ------------------------------------------------------------------- styling

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

  const overlayIconShadowStyle: React.CSSProperties = {
    filter: 'drop-shadow(0 1px 2px rgba(0,0,0,0.72))',
  };
  const actionButtonClass = 'p-2 rounded-full text-white/90 opacity-85 transition-all duration-200 hover:text-white hover:opacity-100 focus:outline-none focus:ring-2 focus:ring-white/40 active:scale-95';
  const translateOrFallback = useCallback((key: string, fallback: string) => {
    const result = t(key);
    return result === key ? fallback : result;
  }, [t]);

  const canUseGameGestures = isLive && runtimeState === 'ready' && isFullyVisible && !showCode;
  const useGameGesturesLabel = translateOrFallback('miniGame.useGameGestures', 'Game swipes');
  const useGameGesturesTitle = translateOrFallback('miniGame.useGameGesturesTitle', 'Use game swipes');
  const returnToChatScrollLabel = translateOrFallback('miniGame.returnToChatScroll', 'Chat scroll');
  const gameGesturesUnavailableLabel = translateOrFallback('miniGame.gameGesturesUnavailable', 'Fully show game to use swipes');
  const interactionModeGroupLabel = translateOrFallback('miniGame.interactionMode', 'Mini-game interaction mode');
  const resumeLabel = translateOrFallback('miniGame.tapToRun', 'Tap to run');

  // ------------------------------------------------------------------ gestures

  useEffect(() => {
    if (!canUseGameGestures && gameGesturesEnabled) {
      setGameGesturesEnabled(false);
      resetPointerGate();
    }
  }, [canUseGameGestures, gameGesturesEnabled, resetPointerGate]);

  useEffect(() => {
    if (!isLive) return;
    postMiniGameMode(gameGesturesEnabled ? 'gestures' : 'scroll');
  }, [gameGesturesEnabled, isLive, postMiniGameMode]);

  /**
   * Playing pins the slot: an in-progress game must not be evicted just because
   * another embed drifted closer to the centre of the viewport.
   */
  useEffect(() => {
    if (gameGesturesEnabled) pin();
    else unpin();
  }, [gameGesturesEnabled, pin, unpin]);

  const handleSelectGameGestureMode = useCallback((nextEnabled: boolean, event: React.MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();
    event.stopPropagation();
    resetPointerGate();

    if (nextEnabled === gameGesturesEnabled) return;
    if (nextEnabled && !canUseGameGestures) return;

    setGameGesturesEnabled(nextEnabled);
    if (nextEnabled) {
      window.setTimeout(() => {
        iframeRef.current?.focus();
      }, 0);
    }
  }, [canUseGameGestures, gameGesturesEnabled, resetPointerGate]);

  /** Tapping a resting embed asks for the live slot explicitly. */
  const handleActivateFromRest = useCallback(() => {
    pin();
    // The pin only needs to survive arbitration, not the session; it is released
    // when the embed scrolls out of view or the user starts a different one.
    window.setTimeout(() => unpin(), 1200);
  }, [pin, unpin]);

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
    } catch {
      /* the pointer may already have been released */
    }
    resetPointerGate();
  }, [postPointerInput, resetPointerGate]);

  const handleGatePointerCancel = useCallback((event: React.PointerEvent<HTMLDivElement>) => {
    const state = pointerGateRef.current;
    if (state.pointerId !== event.pointerId || !event.isPrimary) return;

    event.stopPropagation();

    try {
      if (event.currentTarget.hasPointerCapture(event.pointerId)) {
        event.currentTarget.releasePointerCapture(event.pointerId);
      }
    } catch {
      /* the pointer may already have been released */
    }
    resetPointerGate();
  }, [resetPointerGate]);

  // -------------------------------------------------------------------- render

  return (
    <div className="w-full flex flex-col items-center">
      <div className="relative w-full max-w-[560px]" style={{ paddingBottom: `${wrapperBottomPadding}px` }}>

        <EmbedBox
          aspectRatio={resolvedBox.aspectRatio}
          boxRef={setRef}
          className={`rounded-2xl border ${lineColor} shadow-none ${controlsUnderOverlay && showCode ? 'z-30' : 'z-10'}`}
        >
          {isLive ? (
            <iframe
              ref={iframeRef}
              title={fileName ? t('miniGame.titleWithFile', { fileName }) || `Mini game ${fileName}` : t('miniGame.title') || 'Mini game'}
              srcDoc={srcDoc}
              sandbox="allow-scripts allow-same-origin"
              referrerPolicy="no-referrer"
              style={{
                backgroundColor: 'transparent',
                pointerEvents: gameGesturesEnabled ? 'auto' : 'none',
                touchAction: gameGesturesEnabled ? 'none' : 'pan-y',
              }}
            />
          ) : poster ? (
            <img
              src={poster}
              alt=""
              aria-hidden
              className="embed-fill embed-poster"
              draggable={false}
            />
          ) : (
            <div className={`embed-fill embed-placeholder ${containerBg}`} aria-hidden />
          )}

          {!isLive && (
            <button
              type="button"
              onClick={handleActivateFromRest}
              className="absolute inset-0 z-20 flex items-center justify-center bg-transparent"
              title={resumeLabel}
              aria-label={resumeLabel}
            >
              <span className={`embed-rest-hint flex items-center gap-1.5 rounded-full border ${lineColor} ${containerBg} px-3 py-1.5 text-[10px] uppercase tracking-wider ${textColor}`}>
                <IconPlay className="w-3 h-3 shrink-0" />
                <span className="font-semibold">{resumeLabel}</span>
              </span>
            </button>
          )}

          {isLive && !showCode && !gameGesturesEnabled && (
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

          {runtimeState !== 'ready' && isLive && (
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
        </EmbedBox>

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

MiniGameViewer.displayName = 'MiniGameViewer';

export default MiniGameViewer;
