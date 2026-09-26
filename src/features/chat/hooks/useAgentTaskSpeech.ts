// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useCallback, useEffect, useRef } from 'react';
import type { ChatMessage } from '../../../core/types';
import { TaskSpeechQueue } from '../../../core-sdk/media/taskSpeechQueue';
import { useMaestroStore } from '../../../store';
import { sessionActivity } from '../../../platform/browser/sessionActivity';
import { isReengagementToken, TOKEN_CATEGORY } from '../../../core/config/activityTokens';
import { subscribeRoomTaskResults } from '../services/roomTaskResults';

export interface AgentTaskSpeechConfig {
  enabled: boolean;
  speakMessage(message: ChatMessage): void;
  stopSpeaking(): void | Promise<unknown>;
  hasPendingQueueItems(): boolean;
  pauseLiveForSpeech(): Promise<(() => void) | null>;
  pauseObserverForSpeech(): Promise<boolean>;
}
/** Idle local monitoring is interruptible; speech, provider work and explicit
 * user holds are not. Checking tokens as well as mode fences VAD/Whisper races. */
export function taskSpeechReady(reservation?: string): boolean {
  const state = useMaestroStore.getState();
  if (state.isLoadingHistory || !sessionActivity.isActive() || document.hidden) return false;
  if ([state.liveSessionState, state.silentObserverState].some(mode => mode === 'active' || mode === 'connecting')) return false;
  return [...state.activityTokens].every(token =>
    (token === reservation) || isReengagementToken(token)
    || ['agent:task', 'gen:suggestions', 'vad:listen', 'vad:observer-listen', 'whisper:loading', 'whisper:observer-loading'].includes(token));
}
export function useAgentTaskSpeech(config: AgentTaskSpeechConfig): () => void | Promise<void> {
  const latest = useRef(config); latest.current = config;
  const queue = useRef<TaskSpeechQueue | null>(null);
  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | undefined;
    let alive = true;
    const releases = new Set<ReturnType<typeof setTimeout>>();
    const reservations = new Set<string>();
    const controller = new TaskSpeechQueue({
      available: task => {
        const state = useMaestroStore.getState();
        return latest.current.enabled && state.settings.selectedLanguagePairId === task.conversationId && !state.isLoadingHistory
          && state.messages.some(message => message.id === task.id && message.agentTask?.id === task.id);
      },
      ready: () => taskSpeechReady() && !latest.current.hasPendingQueueItems(),
      claim: async () => {
        if (!taskSpeechReady() || latest.current.hasPendingQueueItems()) return null;
        const pairId = useMaestroStore.getState().settings.selectedLanguagePairId;
        const state = useMaestroStore.getState();
        const reservation = state.addActivityToken(TOKEN_CATEGORY.TTS, `agent-result-${crypto.randomUUID()}`);
        reservations.add(reservation);
        let resume: (() => void) | null = null;
        let released = false;
        const current = () => alive && latest.current.enabled && pairId === useMaestroStore.getState().settings.selectedLanguagePairId
          && sessionActivity.isActive() && !document.hidden && !useMaestroStore.getState().isLoadingHistory;
        const release = (restore: boolean) => {
          if (released) return; released = true;
          const finish = () => {
            if (restore && current() && latest.current.hasPendingQueueItems()) {
              const wait = setTimeout(() => { releases.delete(wait); finish(); }, 100); releases.add(wait); return;
            }
            reservations.delete(reservation);
            useMaestroStore.getState().removeActivityToken(reservation);
            if (restore && current()) resume?.();
          };
          finish();
        };
        try {
          resume = await latest.current.pauseLiveForSpeech();
          if (!resume || !current() || !await latest.current.pauseObserverForSpeech() || !taskSpeechReady(reservation)) { release(true); return null; }
          return { current, release, ready: () => taskSpeechReady(reservation) && !latest.current.hasPendingQueueItems() };
        } catch { release(true); return null; }
      },
      speak: task => {
        const message = useMaestroStore.getState().messages.find(item => item.id === task.id);
        if (message) latest.current.speakMessage(message);
      },
      playing: () => latest.current.hasPendingQueueItems(),
      stop: () => latest.current.stopSpeaking(),
    });
    queue.current = controller;
    const wake = () => {
      if (!alive || timer !== undefined || !controller.hasWork()) return;
      timer = setTimeout(() => {
        timer = undefined;
        if (!sessionActivity.isActive()) controller.cancel(false);
        void controller.tick().finally(wake);
      }, 100);
    };
    const unsubscribe = subscribeRoomTaskResults(task => { controller.enqueue(task); wake(); });
    const stopActivity = sessionActivity.subscribe(() => { if (!sessionActivity.isActive()) controller.cancel(false); wake(); });
    return () => {
      alive = false; unsubscribe(); stopActivity(); clearTimeout(timer); controller.cancel(false); queue.current = null;
      for (const wait of releases) clearTimeout(wait);
      for (const reservation of reservations) useMaestroStore.getState().removeActivityToken(reservation);
    };
  }, []);
  return useCallback(() => queue.current?.cancel(), []);
}
