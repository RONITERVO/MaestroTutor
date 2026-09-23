// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { LiveSessionData } from './state';
import { MAX_LIVE_FRAME_DIMENSION } from './types';

export function createBrowserLiveVideo(state: Pick<LiveSessionData,
  'sessionRef' | 'frameIntervalRef' | 'captureVideoRef'
  | 'canvasRef' | 'videoUpdateVersionRef' | 'videoFrameInFlightRef'
  | 'currentSessionIdRef' | 'speechTurnBoundaryRef'
>, ports: { hasCameraConsent(): boolean }) {
  const {
    sessionRef, frameIntervalRef, captureVideoRef,
    canvasRef, videoUpdateVersionRef, videoFrameInFlightRef,
    currentSessionIdRef, speechTurnBoundaryRef,
  } = state;
  const { hasCameraConsent } = ports;
  const ownedVideos = new Set<HTMLVideoElement>();
  let cancelPreparation: (() => void) | null = null;
  let frameOwner: symbol | null = null;
  const releaseOwnedVideo = (video: HTMLVideoElement) => {
    if (!ownedVideos.delete(video)) return;
    try { video.pause(); } catch { }
    video.srcObject = null;
    video.remove();
  };
  const blobToBase64 = (blob: Blob): Promise<string> => new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onloadend = () => {
      const result = reader.result as string;
      const base64 = result.substring(result.indexOf(',') + 1);
      resolve(base64 || '');
    };
    reader.onerror = reject;
    reader.readAsDataURL(blob);
  });

  const stopVideoFrameLoop = () => {
    frameOwner = null;
    videoFrameInFlightRef.current = false;
    if (frameIntervalRef.current !== null) {
      window.clearInterval(frameIntervalRef.current);
      frameIntervalRef.current = null;
    }
  };

  const detachCaptureVideo = () => {
    cancelPreparation?.();
    cancelPreparation = null;
    for (const video of ownedVideos) releaseOwnedVideo(video);
    captureVideoRef.current = null;
  };

  const ensureVideoElementReady = async (
    stream: MediaStream, providedElement?: HTMLVideoElement | null,
    updateVersion = ++videoUpdateVersionRef.current,
  ) => {
    stopVideoFrameLoop();
    detachCaptureVideo();
    if (
      providedElement &&
      providedElement.srcObject === stream &&
      providedElement.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA &&
      providedElement.videoWidth > 0 &&
      providedElement.videoHeight > 0
    ) {
      captureVideoRef.current = providedElement;
      return providedElement;
    }
    const video = providedElement ?? document.createElement('video');
    video.muted = true;
    video.playsInline = true;
    video.srcObject = stream;
    if (!providedElement) {
      ownedVideos.add(video);
      video.style.position = 'fixed';
      video.style.width = '0px';
      video.style.height = '0px';
      video.style.opacity = '0';
      document.body.appendChild(video);
    }
    // Camera metadata/playback are optional. A stalled browser must not prevent
    // the audio session from starting, and superseding updates cancel immediately.
    const ready = await new Promise<boolean>(resolve => {
      let finished = false;
      let playSettled = false;
      const finish = (usable: boolean) => {
        if (finished) return;
        finished = true;
        window.clearTimeout(timer);
        video.removeEventListener('loadedmetadata', check);
        video.removeEventListener('error', fail);
        if (cancelPreparation === cancel) cancelPreparation = null;
        resolve(usable);
      };
      const check = () => { if (playSettled && video.videoWidth > 0 && video.videoHeight > 0) finish(true); };
      const fail = () => finish(false);
      const cancel = () => finish(false);
      const timer = window.setTimeout(fail, 5000);
      cancelPreparation = cancel;
      video.addEventListener('loadedmetadata', check);
      video.addEventListener('error', fail);
      Promise.resolve().then(() => video.play()).catch(() => undefined).then(() => { playSettled = true; check(); });
    });
    if (!ready || updateVersion !== videoUpdateVersionRef.current) {
      releaseOwnedVideo(video);
      return null;
    }
    captureVideoRef.current = video;
    return video;
  };

  const startVideoFrameLoop = (sessionId: number) => {
    stopVideoFrameLoop();
    frameIntervalRef.current = window.setInterval(() => {
      // Check session is still valid
      if (currentSessionIdRef.current !== sessionId) return;

      // A gated session sends no paid video while nobody is speaking. A frame
      if (!hasCameraConsent()) return;
      // per second of an empty room buys nothing: what the model needs to see
      // is the person who just started talking, and the gate is open by then.
      const boundary = speechTurnBoundaryRef.current;
      if (boundary && !boundary.isOpen) return;

      const activeSession = sessionRef.current;
      const activeVideo = captureVideoRef.current;
      const activeCanvas = canvasRef.current;
      if (!activeSession || !activeVideo || !activeCanvas) return;
      if (activeVideo.videoWidth === 0) return;
      if (videoFrameInFlightRef.current) return;
      const updateVersion = videoUpdateVersionRef.current;
      const isCurrentFrame = () => currentSessionIdRef.current === sessionId
        && videoUpdateVersionRef.current === updateVersion
        && sessionRef.current === activeSession && captureVideoRef.current === activeVideo;

      const ctx = activeCanvas.getContext('2d');
      if (!ctx) return;
      const scale = Math.min(1, MAX_LIVE_FRAME_DIMENSION / Math.max(activeVideo.videoWidth, activeVideo.videoHeight));
      activeCanvas.width = Math.max(1, Math.round(activeVideo.videoWidth * scale));
      activeCanvas.height = Math.max(1, Math.round(activeVideo.videoHeight * scale));
      ctx.imageSmoothingEnabled = true;
      ctx.imageSmoothingQuality = 'medium';
      ctx.drawImage(activeVideo, 0, 0, activeCanvas.width, activeCanvas.height);

      videoFrameInFlightRef.current = true;
      const owner = Symbol('video-frame');
      frameOwner = owner;
      activeCanvas.toBlob((blob) => {
        void (async () => {
          try {
            if (blob && isCurrentFrame()) {
              const b64 = await blobToBase64(blob);
              if (!isCurrentFrame()) return;
              if (!hasCameraConsent()) return;
              if (speechTurnBoundaryRef.current && !speechTurnBoundaryRef.current.isOpen) return;
              activeSession.sendRealtimeInput({ video: { data: b64, mimeType: 'image/jpeg' } });
            }
          } catch (error) {
            if (isCurrentFrame()) console.warn('Live video frame encoding failed:', error);
          } finally {
            if (frameOwner === owner) {
              frameOwner = null;
              videoFrameInFlightRef.current = false;
            }
          }
        })();
      }, 'image/jpeg', 0.5);
    }, 1000);
  };

  const updateVideoInput = async (
    stream?: MediaStream | null,
    providedElement?: HTMLVideoElement | null
  ) => {
    const updateVersion = ++videoUpdateVersionRef.current;
    stopVideoFrameLoop();
    detachCaptureVideo();

    if (!stream || !stream.active) {
      return;
    }

    const video = await ensureVideoElementReady(stream, providedElement, updateVersion);
    if (!video || updateVersion !== videoUpdateVersionRef.current) return;

    if (!canvasRef.current) {
      canvasRef.current = document.createElement('canvas');
    }

    const activeSessionId = currentSessionIdRef.current;
    if (activeSessionId) {
      startVideoFrameLoop(activeSessionId);
    }
  };
  return { stopVideoFrameLoop, detachCaptureVideo, ensureVideoElementReady, startVideoFrameLoop, updateVideoInput };
}
