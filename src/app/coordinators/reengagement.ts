// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { ReengagementPorts } from './contracts';

/** Arbitrate one idle handoff: observer shutdown, visual capture and text fallback. */
export const createReengagementSequence = ({
  isLoadingHistoryRef, isSendingRef, speechIsSpeakingRef,
  isCurrentlyPerformingVisualContextCaptureRef, stopSilentObserverRef,
  settingsRef, visualContextStreamRef, captureSnapshot, handleSendMessageInternal,
  setReplySuggestions, setLastFetchedSuggestionsFor,
}: ReengagementPorts) => async () => {
  // Guard conditions - don't re-engage if busy
  if (isLoadingHistoryRef.current || isSendingRef.current || speechIsSpeakingRef.current || isCurrentlyPerformingVisualContextCaptureRef.current) {
    return;
  }
  await stopSilentObserverRef.current();

  setReplySuggestions([]);
  // Note: isLoadingSuggestions is now managed via tokens in useMaestroController
  // Clearing suggestions above is sufficient; token will be removed when generation completes
  setLastFetchedSuggestionsFor(null);

  let visualReengagementShown = false;
  const currentReengageSettings = settingsRef.current.smartReengagement;

  // Try visual re-engagement first if enabled and camera is active
  if (currentReengageSettings.useVisualContext && visualContextStreamRef.current && visualContextStreamRef.current.active) {
    isCurrentlyPerformingVisualContextCaptureRef.current = true;
    try {
      const imageResult = await captureSnapshot(true);
      if (imageResult && handleSendMessageInternal) {
        visualReengagementShown = await handleSendMessageInternal(
          '',
          imageResult.base64,
          imageResult.mimeType,
          'image-reengagement'
        );
      }
    } finally {
      isCurrentlyPerformingVisualContextCaptureRef.current = false;
    }
  }

  // Fallback to conversational re-engagement if visual didn't work
  if (!visualReengagementShown && handleSendMessageInternal) {
    await handleSendMessageInternal('', undefined, undefined, 'conversational-reengagement');
  }

};
