// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * useHardware - Hook for managing hardware access (camera, microphone).
 * 
 * Handles device enumeration, stream management, and snapshot capture.
 * Syncs key state to Zustand store for cross-component access.
 */

import { useState, useCallback, useRef, useEffect } from 'react';
import { CameraDevice } from '../../core/types';
import type { TranslationFunction } from './useTranslations';
import { IMAGE_GEN_CAMERA_ID } from '../../core/config/app';
import { getFacingModeFromLabel } from '../../features/vision';
import { useMaestroStore } from '../../store';

export interface UseHardwareConfig {
  t: TranslationFunction;
  /** Snapshot enabled - actual value for proper deps tracking */
  sendWithSnapshotEnabled: boolean;
  /** Visual context enabled - actual value for proper deps tracking */
  useVisualContext: boolean;
  /** Selected camera ID - actual value for proper deps tracking */
  selectedCameraId: string | null;
  settingsRef: React.MutableRefObject<{
    selectedCameraId: string | null;
    sendWithSnapshotEnabled: boolean;
    smartReengagement: { useVisualContext: boolean };
  }>;
}

export interface UseHardwareReturn {
  /** List of available cameras */
  availableCameras: CameraDevice[];
  availableCamerasRef: React.MutableRefObject<CameraDevice[]>;
  /** Current camera facing mode */
  currentCameraFacingMode: 'user' | 'environment' | 'unknown';
  /** Live video stream (for visual context or Live API) */
  liveVideoStream: MediaStream | null;
  setLiveVideoStream: React.Dispatch<React.SetStateAction<MediaStream | null>>;
  /** Video element ref for visual context */
  visualContextVideoRef: React.RefObject<HTMLVideoElement | null>;
  /** Stream ref for visual context */
  visualContextStreamRef: React.MutableRefObject<MediaStream | null>;
  /** Camera error message */
  visualContextCameraError: string | null;
  setVisualContextCameraError: React.Dispatch<React.SetStateAction<string | null>>;
  /** User snapshot error */
  snapshotUserError: string | null;
  setSnapshotUserError: React.Dispatch<React.SetStateAction<string | null>>;
  /** Capture a snapshot from the camera */
  captureSnapshot: (isForReengagement?: boolean) => Promise<{
    base64: string;
    mimeType: string;
    storageOptimizedBase64: string;
    storageOptimizedMimeType: string;
  } | null>;
  /** Fetch available cameras */
  fetchAvailableCameras: () => Promise<void>;
  /** Whether microphone API is available */
  microphoneApiAvailable: boolean;
}

/**
 * Hook for managing hardware access (camera, microphone).
 * Handles device enumeration, stream management, and snapshot capture.
 */
export const useHardware = (config: UseHardwareConfig): UseHardwareReturn => {
  const { t, sendWithSnapshotEnabled, useVisualContext, selectedCameraId, settingsRef } = config;

  // Get store actions for syncing state
  const setStoreAvailableCameras = useMaestroStore(state => state.setAvailableCameras);
  const setStoreCurrentCameraFacingMode = useMaestroStore(state => state.setCurrentCameraFacingMode);
  const setStoreLiveVideoStream = useMaestroStore(state => state.setLiveVideoStream);
  const setStoreVisualContextCameraError = useMaestroStore(state => state.setVisualContextCameraError);
  const setStoreSnapshotUserError = useMaestroStore(state => state.setSnapshotUserError);

  const visualContextVideoRef = useRef<HTMLVideoElement>(null);
  const visualContextStreamRef = useRef<MediaStream | null>(null);
  const availableCamerasRef = useRef<CameraDevice[]>([]);

  const [availableCameras, setAvailableCameras] = useState<CameraDevice[]>([]);
  const [currentCameraFacingMode, setCurrentCameraFacingMode] = useState<'user' | 'environment' | 'unknown'>('unknown');
  const [liveVideoStream, setLiveVideoStream] = useState<MediaStream | null>(null);
  const [visualContextCameraError, setVisualContextCameraError] = useState<string | null>(null);
  const [snapshotUserError, setSnapshotUserError] = useState<string | null>(null);

  // Sync local state to store
  useEffect(() => { 
    availableCamerasRef.current = availableCameras; 
    setStoreAvailableCameras(availableCameras);
  }, [availableCameras, setStoreAvailableCameras]);

  useEffect(() => {
    setStoreCurrentCameraFacingMode(currentCameraFacingMode);
  }, [currentCameraFacingMode, setStoreCurrentCameraFacingMode]);

  useEffect(() => {
    setStoreLiveVideoStream(liveVideoStream);
  }, [liveVideoStream, setStoreLiveVideoStream]);

  useEffect(() => {
    setStoreVisualContextCameraError(visualContextCameraError);
  }, [visualContextCameraError, setStoreVisualContextCameraError]);

  useEffect(() => {
    setStoreSnapshotUserError(snapshotUserError);
  }, [snapshotUserError, setStoreSnapshotUserError]);

  // Update facing mode when camera selection changes
  useEffect(() => {
    const selectedId = settingsRef.current.selectedCameraId;
    if (!selectedId) {
      setCurrentCameraFacingMode('unknown');
      return;
    }
    const selected = availableCamerasRef.current.find(c => c.deviceId === selectedId);
    setCurrentCameraFacingMode(selected?.facingMode || 'unknown');
  }, [selectedCameraId]);

  const microphoneApiAvailable = typeof window !== 'undefined' && 
    !!(navigator && navigator.mediaDevices && navigator.mediaDevices.getUserMedia);

  const fetchAvailableCameras = useCallback(async () => {
    try {
      if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
        try {
          // Requesting stream triggers permission prompt if not granted
          const tempStream = await navigator.mediaDevices.getUserMedia({ video: true });
          tempStream.getTracks().forEach(track => track.stop());
        } catch (permError) {
          console.warn("Could not get temporary video stream for robust device enumeration:", permError);
        }
      }

      if (navigator.mediaDevices && navigator.mediaDevices.enumerateDevices) {
        const devices = await navigator.mediaDevices.enumerateDevices();
        const videoDevices = devices.filter(device => device.kind === 'videoinput');
        const cameraList: CameraDevice[] = videoDevices.map((device, index) => ({
          deviceId: device.deviceId,
          label: device.label || `Camera ${index + 1}`,
          facingMode: getFacingModeFromLabel(device.label)
        }));
        setAvailableCameras(cameraList);
        
        // Update facing mode if we have a selected camera
        const selectedId = settingsRef.current.selectedCameraId;
        if (selectedId) {
          const selected = cameraList.find(c => c.deviceId === selectedId);
          if (selected?.facingMode) {
            setCurrentCameraFacingMode(selected.facingMode);
          }
        }
      }
    } catch (error) {
      console.error("Error enumerating video devices:", error);
      setAvailableCameras([]);
    }
  }, [settingsRef]);

  // Fetch cameras on mount and device changes
  useEffect(() => {
    fetchAvailableCameras();
    if (navigator.mediaDevices) {
      navigator.mediaDevices.addEventListener('devicechange', fetchAvailableCameras);
    }
    return () => {
      if (navigator.mediaDevices) {
        navigator.mediaDevices.removeEventListener('devicechange', fetchAvailableCameras);
      }
    };
  }, [fetchAvailableCameras]);

  // Re-fetch cameras when enabling features that use the camera
  useEffect(() => {
    if (sendWithSnapshotEnabled || useVisualContext) {
      fetchAvailableCameras();
    }
  }, [sendWithSnapshotEnabled, useVisualContext, fetchAvailableCameras]);

  // Manage visual context stream
  useEffect(() => {
    const startVisualContextStream = async () => {
      if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        setVisualContextCameraError(t('error.cameraAccessNotSupported'));
        return;
      }
      try {
        if (visualContextStreamRef.current) {
          visualContextStreamRef.current.getTracks().forEach(track => track.stop());
        }

        const videoConstraints: MediaStreamConstraints['video'] = settingsRef.current.selectedCameraId
          ? { deviceId: { exact: settingsRef.current.selectedCameraId } }
          : true;
        const stream = await navigator.mediaDevices.getUserMedia({ video: videoConstraints });
        visualContextStreamRef.current = stream;
        setLiveVideoStream(stream);
        if (visualContextVideoRef.current) {
          visualContextVideoRef.current.srcObject = stream;
          visualContextVideoRef.current.muted = true;
          visualContextVideoRef.current.playsInline = true;
          visualContextVideoRef.current.play().catch(playError => {
            console.error("Error playing visual context video:", playError);
            setVisualContextCameraError(t('error.visualContextStreamPlayback', { details: playError.message }));
          });
        }
        setVisualContextCameraError(null);
      } catch (err) {
        console.error("Error accessing camera for visual context:", err);
        let message = t("error.cameraUnknown");
        if (err instanceof Error) {
          if (err.name === "NotAllowedError" || err.name === "PermissionDeniedError") message = t('error.cameraPermissionDenied');
          else if (err.name === "NotFoundError" || err.name === "DevicesNotFoundError") message = t('error.cameraNotFound');
          else if (err.name === "OverconstrainedError") message = t('error.cameraOverconstrained', { errorMessage: err.message });
          else message = t('error.visualContextCameraGeneric', { details: err.message });
        }
        setVisualContextCameraError(message);
        setLiveVideoStream(null);
      }
    };

    const stopVisualContextStream = () => {
      if (visualContextStreamRef.current) {
        visualContextStreamRef.current.getTracks().forEach(track => track.stop());
        visualContextStreamRef.current = null;
      }
      setLiveVideoStream(null);
      if (visualContextVideoRef.current && visualContextVideoRef.current.srcObject) {
        visualContextVideoRef.current.srcObject = null;
        visualContextVideoRef.current.load();
      }
    };

    const shouldStream = (useVisualContext || sendWithSnapshotEnabled) && 
      selectedCameraId !== IMAGE_GEN_CAMERA_ID;

    if (shouldStream) {
      startVisualContextStream();
    } else {
      stopVisualContextStream();
      setVisualContextCameraError(null);
    }

    return () => {
      stopVisualContextStream();
    };
  }, [
    useVisualContext, 
    sendWithSnapshotEnabled, 
    selectedCameraId, 
    t
  ]);

  const captureSnapshot = useCallback(async (isForReengagement = false): Promise<{
    base64: string;
    mimeType: string;
    storageOptimizedBase64: string;
    storageOptimizedMimeType: string;
  } | null> => {
    const errorSetter = isForReengagement ? setVisualContextCameraError : setSnapshotUserError;
    errorSetter(null);

    const videoElement = visualContextVideoRef.current;
    if (!videoElement) {
      errorSetter(isForReengagement ? t('error.visualContextVideoElementNotReady') : t('error.snapshotVideoElementNotReady'));
      return null;
    }

    const currentSettings = settingsRef.current;
    let streamForCapture: MediaStream | null = null;
    let streamWasTemporarilyStarted = false;

    try {
      // If a Live stream is active (for Gemini Live or Visual Context), reuse it
      let activeLiveStream = liveVideoStream && liveVideoStream.active ? liveVideoStream : (
        visualContextStreamRef.current && visualContextStreamRef.current.active ? visualContextStreamRef.current : null
      );

      if (activeLiveStream &&
        videoElement.srcObject === activeLiveStream &&
        videoElement.readyState >= HTMLMediaElement.HAVE_ENOUGH_DATA &&
        videoElement.videoWidth > 0 && videoElement.videoHeight > 0) {
        streamForCapture = activeLiveStream;
      } else {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
          errorSetter(isForReengagement ? t('error.visualContextCameraAccessNotSupported') : t('error.snapshotCameraAccessNotSupported'));
          return null;
        }
        const videoConstraints: MediaStreamConstraints['video'] = currentSettings.selectedCameraId
          ? { deviceId: { exact: currentSettings.selectedCameraId } }
          : true;

        streamForCapture = await navigator.mediaDevices.getUserMedia({ video: videoConstraints });
        streamWasTemporarilyStarted = true;
        videoElement.srcObject = streamForCapture;
        videoElement.muted = true;
        videoElement.playsInline = true;
        await videoElement.play();

        await new Promise((resolve, reject) => {
          const timeoutErrorKey = isForReengagement ? "error.visualContextTimeout" : "error.snapshotTimeout";
          const dimensionErrorKey = isForReengagement ? "error.visualContextVideoDimensionsZero" : "error.snapshotVideoDimensionsZero";
          const videoErrorKey = isForReengagement ? "error.visualContextVideoError" : "error.snapshotVideoError";

          const timeout = setTimeout(() => reject(new Error(t(timeoutErrorKey))), 3000);
          const onLoadedData = () => {
            clearTimeout(timeout);
            if (videoElement.videoWidth > 0 && videoElement.videoHeight > 0) {
              resolve(undefined);
            } else {
              reject(new Error(t(dimensionErrorKey)));
            }
          };
          videoElement.onloadeddata = onLoadedData;
          videoElement.onerror = () => {
            clearTimeout(timeout);
            reject(new Error(t(videoErrorKey)));
          };
          if (videoElement.readyState >= HTMLMediaElement.HAVE_ENOUGH_DATA && 
              videoElement.videoWidth > 0 && videoElement.videoHeight > 0) {
            clearTimeout(timeout);
            resolve(undefined);
          }
        });
      }

      const canvas = document.createElement('canvas');
      canvas.width = videoElement.videoWidth;
      canvas.height = videoElement.videoHeight;
      const context = canvas.getContext('2d');
      if (!context) throw new Error(isForReengagement ? t("error.visualContext2DContext") : t("error.snapshot2DContext"));

      context.drawImage(videoElement, 0, 0, canvas.width, canvas.height);
      const imageBase64 = canvas.toDataURL('image/jpeg', 0.9);
      return { base64: imageBase64, mimeType: 'image/jpeg', storageOptimizedBase64: imageBase64, storageOptimizedMimeType: 'image/jpeg' };

    } catch (err) {
      console.error(`Error capturing image (${isForReengagement ? 're-engagement' : 'snapshot'}):`, err);
      const message = err instanceof Error ? err.message : t("error.imageCaptureGeneric");
      const prefixKey = isForReengagement ? "error.visualContextCaptureFailed" : "error.snapshotCaptureFailed";

      if (message.includes("Permission") || message.includes("NotAllowedError")) {
        errorSetter(t(`${prefixKey}Permission`));
      } else if (message.includes("NotFoundError") || message.includes("DevicesNotFoundError")) {
        errorSetter(t(`${prefixKey}NotFound`));
      } else if (message.includes("Timeout") || message.includes("dimensions zero") || message.includes("Video element error")) {
        errorSetter(t(`${prefixKey}NotReady`, { details: message }));
      } else {
        errorSetter(t(`${prefixKey}Generic`, { details: message }));
      }
      return null;
    } finally {
      if (streamForCapture && streamWasTemporarilyStarted) {
        streamForCapture.getTracks().forEach(track => track.stop());
        if (videoElement.srcObject === streamForCapture && 
            !((settingsRef.current.smartReengagement.useVisualContext || liveVideoStream) && 
              visualContextStreamRef.current === streamForCapture)) {
          videoElement.srcObject = null;
          videoElement.load();
        }
      }
    }
  }, [t, liveVideoStream, settingsRef]);

  return {
    availableCameras,
    availableCamerasRef,
    currentCameraFacingMode,
    liveVideoStream,
    setLiveVideoStream,
    visualContextVideoRef,
    visualContextStreamRef,
    visualContextCameraError,
    setVisualContextCameraError,
    snapshotUserError,
    setSnapshotUserError,
    captureSnapshot,
    fetchAvailableCameras,
    microphoneApiAvailable,
  };
};

export default useHardware;
