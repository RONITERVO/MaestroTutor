// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { TaskSpeechResult } from '../../../core-sdk/media/taskSpeechQueue';
const listeners = new Set<(task: TaskSpeechResult) => void>();
/** Ephemeral completion notifications. Loading a saved task never replays audio. */
export const subscribeRoomTaskResults = (listener: (task: TaskSpeechResult) => void) => {
  listeners.add(listener); return () => { listeners.delete(listener); };
};
export const publishRoomTaskResult = (task: TaskSpeechResult) => {
  for (const listener of listeners) { try { listener(task); } catch { /* Chat remains the durable result. */ } }
};
