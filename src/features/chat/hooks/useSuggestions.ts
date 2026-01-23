// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * useSuggestions - fetch reply suggestions when TTS ends.
 */

import { useEffect, useRef } from 'react';
import type { MutableRefObject } from 'react';
import type { ChatMessage } from '../../../core/types';
import { useMaestroStore } from '../../../store';
import { selectIsLoadingSuggestions } from '../../../store/slices/uiSlice';

export interface UseSuggestionsConfig {
  isSpeaking: boolean;
  messagesRef: MutableRefObject<ChatMessage[]>;
  lastFetchedSuggestionsForRef: MutableRefObject<string | null>;
  isLoadingSuggestionsRef: MutableRefObject<boolean>;
  fetchAndSetReplySuggestions: (assistantMessageId: string, lastTutorMessage: string, history: ChatMessage[]) => Promise<void>;
  getHistoryRespectingBookmark: (arr: ChatMessage[]) => ChatMessage[];
}

export const useSuggestions = ({
  isSpeaking,
  fetchAndSetReplySuggestions,
  getHistoryRespectingBookmark,
}: UseSuggestionsConfig) => {
  const wasSpeakingRef = useRef<boolean>(false);
  const setLastFetchedSuggestionsFor = useMaestroStore(state => state.setLastFetchedSuggestionsFor);

  useEffect(() => {
    const wasSpeaking = wasSpeakingRef.current;
    wasSpeakingRef.current = isSpeaking;

    if (wasSpeaking && !isSpeaking) {
      const state = useMaestroStore.getState();
      if (selectIsLoadingSuggestions(state)) {
        return;
      }
      const messages = state.messages;
      const lastMessage = messages.length > 0 ? messages[messages.length - 1] : null;
      if (lastMessage && lastMessage.role === 'assistant' && !lastMessage.thinking && lastMessage.id !== state.lastFetchedSuggestionsFor) {
        const textForSuggestions = lastMessage.rawAssistantResponse || (lastMessage.translations?.find(t => t.spanish)?.spanish) || "";
        if (textForSuggestions.trim()) {
          setLastFetchedSuggestionsFor(lastMessage.id);
          fetchAndSetReplySuggestions(lastMessage.id, textForSuggestions, getHistoryRespectingBookmark(messages));
        }
      }
    }
  }, [isSpeaking, fetchAndSetReplySuggestions, getHistoryRespectingBookmark, setLastFetchedSuggestionsFor]);
};

export default useSuggestions;
