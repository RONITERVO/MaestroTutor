// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { useEffect, useRef, useState } from 'react';
import { useMaestroStore } from '../../../store';
import { createBrowserLiveRuntime } from '../live/browserRuntime';
import { createLiveConversationController } from '../live/controller';
import type { LiveSessionState, UseGeminiLiveConversationCallbacks } from '../live/types';
export type { LiveSessionState, LiveTurnTranscriptUpdateReason, LiveTurnTranscriptUpdate, UseGeminiLiveConversationCallbacks, StartLiveConversationOptions } from '../live/types';

/** React binding for one Live session owner. Provider/capture/playback callbacks
 * read the latest committed callback set without rebuilding the active session. */
export function useGeminiLiveConversation(callbacks: UseGeminiLiveConversationCallbacks = {}) {
  const [, setState] = useState<LiveSessionState>('idle');
  const addActivityToken = useMaestroStore(state => state.addActivityToken);
  const removeActivityToken = useMaestroStore(state => state.removeActivityToken);
  const controllerRef = useRef<ReturnType<typeof createLiveConversationController> | null>(null);
  if (!controllerRef.current) {
    controllerRef.current = createLiveConversationController(createBrowserLiveRuntime({
      setState, addActivityToken, removeActivityToken,
    }), callbacks);
  }
  const controller = controllerRef.current;
  useEffect(() => { controller.setCallbacks(callbacks); }, [callbacks, controller]);
  useEffect(() => () => { controller.dispose(); }, [controller]);
  return { start: controller.start, stop: controller.stop, updateVideoInput: controller.updateVideoInput };
}
