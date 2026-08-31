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
 * - Device performance tier and the embed/attachment budgets derived from it
 *
 * Note: MediaStream objects are non-serializable and should NOT be persisted.
 * These are marked as transient state.
 */

import type { StateCreator } from 'zustand';
import type { CameraDevice } from '../../core/types';
import type { MaestroStore } from '../maestroStore';

/**
 * How much simultaneous rich content this device can carry.
 *
 * Drives every memory budget in the chat. Play's memory and bitmap thresholds
 * are enforced against real devices, and the bottom of the Android range has
 * roughly an order of magnitude less headroom than the top, so a single fixed
 * budget is either unsafe on low-end or needlessly austere on high-end.
 */
export type DevicePerformanceTier = 'low' | 'mid' | 'high';

export interface DeviceBudgets {
  /** Simultaneously live embeds: real iframes / rendered documents. */
  maxLiveEmbeds: number;
  /** Retained poster bitmaps for frozen embeds. 0 = go straight to placeholder. */
  posterBudget: number;
  /** PDF pages rasterized around the viewport. */
  pdfWindowPages: number;
  /** Upper bound on the PDF render scale, before the device-pixel-ratio factor. */
  pdfScaleCap: number;
  /** Ceiling applied to the user's maxVisibleMessages setting. */
  maxVisibleMessagesCap: number;
}

export const DEVICE_BUDGETS: Record<DevicePerformanceTier, DeviceBudgets> = {
  low: { maxLiveEmbeds: 1, posterBudget: 0, pdfWindowPages: 1, pdfScaleCap: 1.0, maxVisibleMessagesCap: 20 },
  mid: { maxLiveEmbeds: 1, posterBudget: 4, pdfWindowPages: 2, pdfScaleCap: 1.25, maxVisibleMessagesCap: 35 },
  // maxLiveEmbeds stays at 1 on every tier. The arbiter supports N, but the
  // product requirement is one running artifact at a time, and holding a
  // second document on the strongest devices buys little while doubling the
  // worst case the Play memory thresholds are measured against.
  high: { maxLiveEmbeds: 1, posterBudget: 8, pdfWindowPages: 3, pdfScaleCap: 1.5, maxVisibleMessagesCap: 50 },
};

interface DeviceCapabilityProbe {
  deviceMemory?: number;
  hardwareConcurrency?: number;
}

/**
 * Classify the device once at startup.
 *
 * `navigator.deviceMemory` is absent on a number of Android WebViews and on
 * every iOS browser, so the unknown case must land somewhere safe rather than
 * optimistic: we default to 'mid', and let a single weak signal pull down to
 * 'low'. Over-estimating the tier is what gets an app terminated.
 */
export const detectDevicePerformanceTier = (probe?: DeviceCapabilityProbe): DevicePerformanceTier => {
  const nav = typeof navigator !== 'undefined' ? (navigator as Navigator & DeviceCapabilityProbe) : undefined;
  const deviceMemory = probe?.deviceMemory ?? nav?.deviceMemory;
  const cores = probe?.hardwareConcurrency ?? nav?.hardwareConcurrency;

  if ((deviceMemory !== undefined && deviceMemory <= 2) || (cores !== undefined && cores <= 4)) {
    return 'low';
  }
  if (deviceMemory === undefined) return 'mid';
  return deviceMemory <= 4 ? 'mid' : 'high';
};

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
  devicePerformanceTier: DevicePerformanceTier;

  // Capture state
  isCurrentlyPerformingVisualContextCapture: boolean;

  // Actions
  /** Override the detected tier (diagnostics panel, low-end QA passes). */
  setDevicePerformanceTier: (tier: DevicePerformanceTier) => void;
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
  devicePerformanceTier: detectDevicePerformanceTier(),

  // Capture state
  isCurrentlyPerformingVisualContextCapture: false,

  // Actions
  setDevicePerformanceTier: (tier: DevicePerformanceTier) => {
    set({ devicePerformanceTier: tier });
  },

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

/** Selectors — DEVICE_BUDGETS entries are stable references, safe for useStore. */
export const selectDevicePerformanceTier = (state: MaestroStore): DevicePerformanceTier =>
  state.devicePerformanceTier;

export const selectDeviceBudgets = (state: MaestroStore): DeviceBudgets =>
  DEVICE_BUDGETS[state.devicePerformanceTier] ?? DEVICE_BUDGETS.mid;
