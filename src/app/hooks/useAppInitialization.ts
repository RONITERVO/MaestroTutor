// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * useAppInitialization - Centralized app startup hook.
 *
 * Responsibilities:
 * - App lifecycle (title + splash removal)
 * - Asset hydration (avatar/loading gifs)
 * - Settings init + history load via store-backed hooks
 */

import { useEffect, useRef, type MutableRefObject } from 'react';
import { useMaestroStore } from '../../store';
import { useAppLifecycle } from './useAppLifecycle';
import { useAppAssets } from './useAppAssets';
import { useAppTranslations } from '../../shared/hooks/useAppTranslations';
import { selectSelectedLanguagePair } from '../../store/slices/settingsSlice';
import { selectIsLoadingSuggestions } from '../../store/slices/uiSlice';

export interface UseAppInitializationConfig {
  maestroAvatarUriRef: MutableRefObject<string | null>;
  maestroAvatarMimeTypeRef: MutableRefObject<string | null>;
}

export const useAppInitialization = ({
  maestroAvatarUriRef,
  maestroAvatarMimeTypeRef,
}: UseAppInitializationConfig) => {
  const { t } = useAppTranslations();

  useAppLifecycle(t);

  const setLoadingGifs = useMaestroStore(state => state.setLoadingGifs);
  const setMaestroAvatar = useMaestroStore(state => state.setMaestroAvatar);

  useAppAssets({
    setLoadingGifs,
    setMaestroAvatar,
    maestroAvatarUriRef,
    maestroAvatarMimeTypeRef,
  });

  const settings = useMaestroStore(state => state.settings);
  const selectedLanguagePair = useMaestroStore(selectSelectedLanguagePair);
  const messages = useMaestroStore(state => state.messages);
  const isLoadingHistory = useMaestroStore(state => state.isLoadingHistory);
  const replySuggestions = useMaestroStore(state => state.replySuggestions);
  const lastFetchedSuggestionsFor = useMaestroStore(state => state.lastFetchedSuggestionsFor);
  const isLoadingSuggestions = useMaestroStore(selectIsLoadingSuggestions);

  const initSettings = useMaestroStore(state => state.initSettings);
  const updateSetting = useMaestroStore(state => state.updateSetting);
  const setSettings = useMaestroStore(state => state.setSettings);
  const loadHistoryForPair = useMaestroStore(state => state.loadHistoryForPair);
  const addMessage = useMaestroStore(state => state.addMessage);
  const updateMessage = useMaestroStore(state => state.updateMessage);
  const deleteMessage = useMaestroStore(state => state.deleteMessage);
  const setMessages = useMaestroStore(state => state.setMessages);
  const getHistoryRespectingBookmark = useMaestroStore(state => state.getHistoryRespectingBookmark);
  const computeMaxMessagesForArray = useMaestroStore(state => state.computeMaxMessagesForArray);
  const upsertMessageTtsCache = useMaestroStore(state => state.upsertMessageTtsCache);
  const upsertSuggestionTtsCache = useMaestroStore(state => state.upsertSuggestionTtsCache);
  const setReplySuggestions = useMaestroStore(state => state.setReplySuggestions);

  const settingsRef = useRef(settings);
  const selectedLanguagePairRef = useRef(selectedLanguagePair);
  const messagesRef = useRef(messages);
  const isLoadingHistoryRef = useRef(isLoadingHistory);
  const replySuggestionsRef = useRef(replySuggestions);
  const lastFetchedSuggestionsForRef = useRef(lastFetchedSuggestionsFor);
  const isLoadingSuggestionsRef = useRef(isLoadingSuggestions);

  useEffect(() => { settingsRef.current = settings; }, [settings]);
  useEffect(() => { selectedLanguagePairRef.current = selectedLanguagePair; }, [selectedLanguagePair]);
  useEffect(() => { messagesRef.current = messages; }, [messages]);
  useEffect(() => { isLoadingHistoryRef.current = isLoadingHistory; }, [isLoadingHistory]);
  useEffect(() => { replySuggestionsRef.current = replySuggestions; }, [replySuggestions]);
  useEffect(() => { lastFetchedSuggestionsForRef.current = lastFetchedSuggestionsFor; }, [lastFetchedSuggestionsFor]);
  useEffect(() => { isLoadingSuggestionsRef.current = isLoadingSuggestions; }, [isLoadingSuggestions]);

  const prevPairIdRef = useRef<string | null>(null);

  useEffect(() => {
    if (!useMaestroStore.getState().isSettingsLoaded) {
      initSettings();
    }
  }, [initSettings]);

  useEffect(() => {
    const pairId = settings.selectedLanguagePairId;
    if (pairId && pairId !== prevPairIdRef.current) {
      prevPairIdRef.current = pairId;
      loadHistoryForPair(pairId, t);
    }
  }, [settings.selectedLanguagePairId, loadHistoryForPair, t]);

  return {
    t,
    settings,
    settingsRef,
    handleSettingsChange: updateSetting,
    setSettings,
    selectedLanguagePair,
    selectedLanguagePairRef,
    messagesRef,
    isLoadingHistory,
    isLoadingHistoryRef,
    addMessage,
    updateMessage,
    deleteMessage,
    setMessages,
    getHistoryRespectingBookmark,
    computeMaxMessagesForArray,
    upsertMessageTtsCache,
    upsertSuggestionTtsCache,
    lastFetchedSuggestionsForRef,
    replySuggestions,
    setReplySuggestions,
    replySuggestionsRef,
    isLoadingSuggestionsRef,
  };
};

export default useAppInitialization;
