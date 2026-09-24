// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** UI notifications must not interrupt capture ownership or resource teardown. */
export function notifyLiveConsumer(notify: () => void): void {
  try { notify(); } catch (error) { console.warn('Live callback failed:', error); }
}
