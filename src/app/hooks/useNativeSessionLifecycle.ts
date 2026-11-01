// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useEffect, useRef } from 'react';
import { sessionActivity } from '../../platform/browser/sessionActivity';

interface SessionStops {
  cancelReengagement: () => void;
  stopSpeaking: () => void;
  stopListening: () => void | Promise<void>;
  stopSilentObserver: () => Promise<void>;
  handleStopLiveSession: (options: { scheduleReengagement: boolean }) => Promise<void>;
  clearVideo: () => void;
}

export function useNativeSessionLifecycle(stops: SessionStops) {
  const latest = useRef(stops); latest.current = stops;
  useEffect(() => {
    const pauseElement = (event: Event) => {
      if (!sessionActivity.isActive() && event.target instanceof HTMLMediaElement) event.target.pause();
    };
    document.addEventListener('play', pauseElement, true);
    const unregister = sessionActivity.onSuspend(async () => {
      const value = latest.current;
      value.cancelReengagement();
      value.stopSpeaking();
      document.querySelectorAll('audio,video').forEach(element => (element as HTMLMediaElement).pause());
      value.clearVideo();
      await Promise.all([
        value.stopListening(), value.stopSilentObserver(),
        value.handleStopLiveSession({ scheduleReengagement: false }),
      ]);
    });
    return () => { unregister(); document.removeEventListener('play', pauseElement, true); };
  }, []);
}
