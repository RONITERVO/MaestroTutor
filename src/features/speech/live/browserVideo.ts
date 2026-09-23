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
    if (frameIntervalRef.current !== null) {
      window.clearInterval(frameIntervalRef.current);
      frameIntervalRef.current = null;
    }
  };

  const detachCaptureVideo = () => {
    if (!captureVideoRef.current) return;
    try {
      // Only fully detach if we created this hidden element.
      if (captureVideoRef.current.parentElement === document.body && captureVideoRef.current.style.position === 'fixed') {
        captureVideoRef.current.pause();
        captureVideoRef.current.srcObject = null;
        document.body.removeChild(captureVideoRef.current);
      }
    } catch {
      // Ignore detach errors.
    }
    captureVideoRef.current = null;
  };

  const ensureVideoElementReady = async (stream: MediaStream, providedElement?: HTMLVideoElement | null) => {
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
      video.style.position = 'fixed';
      video.style.width = '0px';
      video.style.height = '0px';
      video.style.opacity = '0';
      document.body.appendChild(video);
    }
    await video.play().catch(() => undefined);
    if (video.videoWidth === 0 || video.videoHeight === 0) {
      await new Promise<void>((resolve) => {
        const handler = () => { video.removeEventListener('loadedmetadata', handler); resolve(); };
        video.addEventListener('loadedmetadata', handler);
      });
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

      const ctx = activeCanvas.getContext('2d');
      if (!ctx) return;
      const scale = Math.min(1, MAX_LIVE_FRAME_DIMENSION / Math.max(activeVideo.videoWidth, activeVideo.videoHeight));
      activeCanvas.width = Math.max(1, Math.round(activeVideo.videoWidth * scale));
      activeCanvas.height = Math.max(1, Math.round(activeVideo.videoHeight * scale));
      ctx.imageSmoothingEnabled = true;
      ctx.imageSmoothingQuality = 'medium';
      ctx.drawImage(activeVideo, 0, 0, activeCanvas.width, activeCanvas.height);

      videoFrameInFlightRef.current = true;
      activeCanvas.toBlob((blob) => {
        void (async () => {
          try {
            if (blob && sessionRef.current && currentSessionIdRef.current === sessionId) {
              const b64 = await blobToBase64(blob);
              if (currentSessionIdRef.current !== sessionId) return;
              if (!hasCameraConsent()) return;
              if (speechTurnBoundaryRef.current && !speechTurnBoundaryRef.current.isOpen) return;
              sessionRef.current.sendRealtimeInput({ video: { data: b64, mimeType: 'image/jpeg' } });
            }
          } finally {
            videoFrameInFlightRef.current = false;
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

    await ensureVideoElementReady(stream, providedElement);
    if (updateVersion !== videoUpdateVersionRef.current) {
      detachCaptureVideo();
      return;
    }

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
