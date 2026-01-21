// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * Hardware Slice - manages camera and microphone hardware state
 * 
 * Responsibilities:
 * - Available cameras list
 * - Camera facing mode
 * - Live video stream (non-serializable, transient)
 * - Visual context stream (non-serializable, transient)
 * - Camera/snapshot error states
 * - Visual context capture state
 * 
 * Note: MediaStream objects are non-serializable and should NOT be persisted.
 * These are marked as transient state.
 */

import type { StateCreator } from 'zustand';
import type { CameraDevice } from '../../core/types';
import type { MaestroStore } from '../maestroStore';

export interface HardwareSlice {
  // State
  availableCameras: CameraDevice[];
  currentCameraFacingMode: 'user' | 'environment' | 'unknown';
  
  // Non-serializable / transient state (never persisted)
  liveVideoStream: MediaStream | null;
  visualContextStream: MediaStream | null;
  
  // Error states
  visualContextCameraError: string | null;
  snapshotUserError: string | null;
  
  // Capability detection
  microphoneApiAvailable: boolean;
  
  // Capture state
  isCurrentlyPerformingVisualContextCapture: boolean;
  
  // Actions
  setAvailableCameras: (cameras: CameraDevice[]) => void;
  setCurrentCameraFacingMode: (mode: 'user' | 'environment' | 'unknown') => void;
  setLiveVideoStream: (stream: MediaStream | null) => void;
  setVisualContextStream: (stream: MediaStream | null) => void;
  setVisualContextCameraError: (error: string | null) => void;
  setSnapshotUserError: (error: string | null) => void;
  setIsCurrentlyPerformingVisualContextCapture: (value: boolean) => void;
  
  // Utility - cleanup streams
  cleanupStreams: () => void;
}

export const createHardwareSlice: StateCreator<
  MaestroStore,
  [['zustand/subscribeWithSelector', never], ['zustand/devtools', never]],
  [],
  HardwareSlice
> = (set, get) => ({
  // Initial state
  availableCameras: [],
  currentCameraFacingMode: 'unknown',
  
  // Non-serializable (transient)
  liveVideoStream: null,
  visualContextStream: null,
  
  // Error states
  visualContextCameraError: null,
  snapshotUserError: null,
  
  // Capability detection
  microphoneApiAvailable: typeof window !== 'undefined' && 
    !!(navigator && navigator.mediaDevices && navigator.mediaDevices.getUserMedia),
  
  // Capture state
  isCurrentlyPerformingVisualContextCapture: false,
  
  // Actions
  setAvailableCameras: (cameras: CameraDevice[]) => {
    set({ availableCameras: cameras });
  },
  
  setCurrentCameraFacingMode: (mode: 'user' | 'environment' | 'unknown') => {
    set({ currentCameraFacingMode: mode });
  },
  
  setLiveVideoStream: (stream: MediaStream | null) => {
    // Stop existing tracks before replacing to avoid leaks
    const currentStream = get().liveVideoStream;
    if (currentStream) {
      currentStream.getTracks().forEach(track => track.stop());
    }
    set({ liveVideoStream: stream });
  },
  
  setVisualContextStream: (stream: MediaStream | null) => {
    // Stop existing tracks before replacing to avoid leaks
    const currentStream = get().visualContextStream;
    if (currentStream) {
      currentStream.getTracks().forEach(track => track.stop());
    }
    set({ visualContextStream: stream });
  },
  
  setVisualContextCameraError: (error: string | null) => {
    set({ visualContextCameraError: error });
  },
  
  setSnapshotUserError: (error: string | null) => {
    set({ snapshotUserError: error });
  },
  
  setIsCurrentlyPerformingVisualContextCapture: (value: boolean) => {
    set({ isCurrentlyPerformingVisualContextCapture: value });
  },
  
  // Cleanup all streams
  cleanupStreams: () => {
    const { liveVideoStream, visualContextStream } = get();
    
    if (liveVideoStream) {
      liveVideoStream.getTracks().forEach(track => track.stop());
    }
    if (visualContextStream) {
      visualContextStream.getTracks().forEach(track => track.stop());
    }
    
    set({ 
      liveVideoStream: null, 
      visualContextStream: null 
    });
  },
});
