// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * useAutoFetchSuggestions - fetch reply suggestions when TTS ends.
 */

import { useEffect, useRef } from 'react';
import type { MutableRefObject } from 'react';
import type { ChatMessage } from '../../core/types';

interface UseAutoFetchSuggestionsConfig {
  isSpeaking: boolean;
  messagesRef: MutableRefObject<ChatMessage[]>;
  lastFetchedSuggestionsForRef: MutableRefObject<string | null>;
  isLoadingSuggestionsRef: MutableRefObject<boolean>;
  fetchAndSetReplySuggestions: (assistantMessageId: string, lastTutorMessage: string, history: ChatMessage[]) => Promise<void>;
  getHistoryRespectingBookmark: (arr: ChatMessage[]) => ChatMessage[];
}

export const useAutoFetchSuggestions = ({
  isSpeaking,
  messagesRef,
  lastFetchedSuggestionsForRef,
  isLoadingSuggestionsRef,
  fetchAndSetReplySuggestions,
  getHistoryRespectingBookmark,
}: UseAutoFetchSuggestionsConfig) => {
  const wasSpeakingRef = useRef<boolean>(false);

  useEffect(() => {
    const wasSpeaking = wasSpeakingRef.current;
    wasSpeakingRef.current = isSpeaking;

    if (wasSpeaking && !isSpeaking) {
      if (isLoadingSuggestionsRef.current) {
        return;
      }
      const lastMessage = messagesRef.current.length > 0 ? messagesRef.current[messagesRef.current.length - 1] : null;
      if (lastMessage && lastMessage.role === 'assistant' && !lastMessage.thinking && lastMessage.id !== lastFetchedSuggestionsForRef.current) {
        const textForSuggestions = lastMessage.rawAssistantResponse || (lastMessage.translations?.find(t => t.spanish)?.spanish) || "";
        if (textForSuggestions.trim()) {
          // Set ref before fetch to prevent duplicate concurrent requests
          // Note: If fetch fails, we intentionally don't retry automatically.
          // The ref marks this message as "attempted" - the user can manually
          // trigger suggestions again or the next TTS end will try the new message.
          lastFetchedSuggestionsForRef.current = lastMessage.id;
          fetchAndSetReplySuggestions(lastMessage.id, textForSuggestions, getHistoryRespectingBookmark(messagesRef.current));
        }
      }
    }
  }, [isSpeaking, messagesRef, lastFetchedSuggestionsForRef, fetchAndSetReplySuggestions, getHistoryRespectingBookmark, isLoadingSuggestionsRef]);
};

export default useAutoFetchSuggestions;
