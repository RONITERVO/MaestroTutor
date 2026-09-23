// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { createTurnTimingRecorder } from '../../core-sdk/turnTiming';

const timing = createTurnTimingRecorder({ storage: () => {
  try { return typeof localStorage === 'undefined' ? undefined : localStorage; }
  catch { return undefined; }
} });

export const { beginTurnTiming, exportTurnTimings, clearTurnTimings, flushTurnTimings } = timing;

if (typeof window !== 'undefined') {
  window.addEventListener('pagehide', flushTurnTimings);
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') flushTurnTimings();
  });
}
