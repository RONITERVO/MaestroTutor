// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * useAutoSendOnSilence - auto-send STT transcript when stable.
 */

import { useCallback, useEffect, useRef } from 'react';
import type { MutableRefObject } from 'react';
import { useMaestroStore } from '../../../store';
import { selectIsSending, selectIsSpeaking } from '../../../store/slices/uiSlice';

export interface UseAutoSendOnSilenceConfig {
  settingsRef: MutableRefObject<{ stt: { enabled: boolean }; isSuggestionMode: boolean }>;
  transcript: string;
  attachedImageBase64: string | null;
  attachedImageMimeType: string | null;
  isSendingRef: MutableRefObject<boolean>;
  speechIsSpeakingRef: MutableRefObject<boolean>;
  clearTranscript: () => void;
  handleCreateSuggestion: (text: string) => void;
  handleSendMessageInternal: (text: string, imageBase64?: string, imageMimeType?: string, messageType?: 'user' | 'conversational-reengagement' | 'image-reengagement') => Promise<boolean>;
  stableMs: number;
}

export const useAutoSendOnSilence = ({
  transcript,
  attachedImageBase64,
  attachedImageMimeType,
  clearTranscript,
  handleCreateSuggestion,
  handleSendMessageInternal,
  stableMs,
}: UseAutoSendOnSilenceConfig) => {
  const autoSendTimerRef = useRef<number | null>(null);
  const autoSendSnapshotRef = useRef<string>('');

  const clearAutoSend = useCallback(() => {
    if (autoSendTimerRef.current) {
      clearTimeout(autoSendTimerRef.current);
      autoSendTimerRef.current = null;
    }
    autoSendSnapshotRef.current = '';
  }, []);

  useEffect(() => {
    const { settings } = useMaestroStore.getState();
    if (!settings.stt.enabled) {
      clearAutoSend();
      return;
    }

    const text = (transcript || '').trim();
    clearAutoSend();

    if (text.length < 2) {
      return;
    }

    autoSendSnapshotRef.current = text;
    autoSendTimerRef.current = window.setTimeout(() => {
      const snap = autoSendSnapshotRef.current;
      const current = (transcript || '').trim();
      const state = useMaestroStore.getState();
      const isSending = selectIsSending(state);
      const isSpeaking = selectIsSpeaking(state);
      if (state.settings.stt.enabled && !isSending && !isSpeaking && current.length >= 2 && current === snap) {
        clearTranscript();
        if (state.settings.isSuggestionMode) {
          handleCreateSuggestion(current);
        } else {
          handleSendMessageInternal(current, attachedImageBase64 || undefined, attachedImageMimeType || undefined, 'user');
        }
      }
      clearAutoSend();
    }, stableMs);

    return () => {
      clearAutoSend();
    };
  }, [
    transcript,
    attachedImageBase64,
    attachedImageMimeType,
    clearTranscript,
    handleCreateSuggestion,
    handleSendMessageInternal,
    stableMs,
    clearAutoSend,
  ]);

  return { clearAutoSend };
};

export default useAutoSendOnSilence;
