// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * Live Session Slice - manages Gemini Live conversation session state
 * 
 * Responsibilities:
 * - Live session state (idle, connecting, active, error)
 * - Live session errors
 * - Session lifecycle tracking
 */

import type { StateCreator } from 'zustand';
import type { MaestroStore } from '../maestroStore';

export type LiveSessionState = 'idle' | 'connecting' | 'active' | 'error';

export interface LiveSessionSlice {
  // State
  liveSessionState: LiveSessionState;
  liveSessionError: string | null;
  silentObserverState: LiveSessionState;
  silentObserverError: string | null;
  
  // Actions
  setLiveSessionState: (state: LiveSessionState) => void;
  setLiveSessionError: (error: string | null) => void;
  setSilentObserverState: (state: LiveSessionState) => void;
  setSilentObserverError: (error: string | null) => void;
  resetLiveSession: () => void;
  resetSilentObserver: () => void;
}

export const createLiveSessionSlice: StateCreator<
  MaestroStore,
  [['zustand/subscribeWithSelector', never], ['zustand/devtools', never]],
  [],
  LiveSessionSlice
> = (set) => ({
  // Initial state
  liveSessionState: 'idle',
  liveSessionError: null,
  silentObserverState: 'idle',
  silentObserverError: null,
  
  // Actions
  setLiveSessionState: (state: LiveSessionState) => {
    // Single atomic update to avoid transient inconsistent state
    set((prev) => ({
      ...prev,
      liveSessionState: state,
      liveSessionError: state === 'connecting' ? null : prev.liveSessionError
    }));
  },
  
  setLiveSessionError: (error: string | null) => {
    set({ liveSessionError: error });
  },

  setSilentObserverState: (state: LiveSessionState) => {
    set((prev) => ({
      ...prev,
      silentObserverState: state,
      silentObserverError: state === 'connecting' ? null : prev.silentObserverError,
    }));
  },

  setSilentObserverError: (error: string | null) => {
    set({ silentObserverError: error });
  },
  
  resetLiveSession: () => {
    set({ 
      liveSessionState: 'idle', 
      liveSessionError: null 
    });
  },

  resetSilentObserver: () => {
    set({
      silentObserverState: 'idle',
      silentObserverError: null,
    });
  },
});
