// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * useChatPersistence - Handles autosave and bookmark management.
 */

import { useEffect } from 'react';
import { shallow } from 'zustand/shallow';
import { safeSaveChatHistoryDB, setChatMetaDB } from '../services/chatHistory';
import { setAppSettingsDB } from '../../session/services/settings';
import { isRealChatMessage } from '../../../shared/utils/common';
import { subscribeToStore, useMaestroStore, MAX_VISIBLE_MESSAGES_DEFAULT } from '../../../store';
import type { ChatMessage } from '../../../core/types';

export const useChatPersistence = () => {
  useEffect(() => {
    const unsubscribe = subscribeToStore(
      (state) => ({
        messages: state.messages,
        selectedLanguagePairId: state.settings.selectedLanguagePairId,
        isLoadingHistory: state.isLoadingHistory,
        historyBookmarkMessageId: state.settings.historyBookmarkMessageId,
        maxVisibleMessages: state.settings.maxVisibleMessages,
      }),
      (current, previous) => {
        const pairId = current.selectedLanguagePairId;

        if (pairId && !current.isLoadingHistory && current.messages !== previous?.messages) {
          safeSaveChatHistoryDB(pairId, current.messages).catch(() => {});
        }

        const shouldRecalc =
          current.messages !== previous?.messages ||
          current.historyBookmarkMessageId !== previous?.historyBookmarkMessageId ||
          current.maxVisibleMessages !== previous?.maxVisibleMessages ||
          current.isLoadingHistory !== previous?.isLoadingHistory ||
          current.selectedLanguagePairId !== previous?.selectedLanguagePairId;

        if (!shouldRecalc) return;
        if (!pairId || current.isLoadingHistory) return;

        const arr = current.messages;
        if (!arr || arr.length === 0) return;

        const eligibleIndices: number[] = [];
        for (let i = 0; i < arr.length; i++) {
          if (isRealChatMessage(arr[i] as ChatMessage)) eligibleIndices.push(i);
        }

        const maxVisible = (current.maxVisibleMessages ?? MAX_VISIBLE_MESSAGES_DEFAULT) + 2;
        if (eligibleIndices.length <= maxVisible) return;

        const bmId = current.historyBookmarkMessageId;
        let bmIndex = -1;
        if (bmId) {
          bmIndex = arr.findIndex(m => m.id === bmId);
        }

        const startForCount = bmIndex >= 0 ? bmIndex : 0;
        let currentVisibleIgnoringCtx = 0;
        for (let i = startForCount; i < arr.length; i++) {
          if (isRealChatMessage(arr[i] as ChatMessage)) currentVisibleIgnoringCtx++;
        }
        if (currentVisibleIgnoringCtx <= maxVisible) return;

        const cutoffPosInEligible = Math.max(0, eligibleIndices.length - maxVisible);
        const firstEligibleIdx = eligibleIndices[cutoffPosInEligible];

        let desiredBookmarkIdx = -1;
        for (let i = firstEligibleIdx; i < arr.length; i++) {
          if (arr[i].role === 'assistant' && !arr[i].thinking) {
            desiredBookmarkIdx = i;
            break;
          }
        }
        if (desiredBookmarkIdx === -1) return;

        const desiredBookmarkId = arr[desiredBookmarkIdx].id;
        if ((current.historyBookmarkMessageId || null) === (desiredBookmarkId || null)) return;

        const store = useMaestroStore.getState();
        store.setSettings(prev => ({ ...prev, historyBookmarkMessageId: desiredBookmarkId }));
        const fullSettings = store.settings;
        const updatedSettings = { ...fullSettings, historyBookmarkMessageId: desiredBookmarkId };
        setAppSettingsDB(updatedSettings).catch(() => {});
        setChatMetaDB(pairId, { bookmarkMessageId: desiredBookmarkId }).catch(() => {});
      },
      {
        equalityFn: shallow,
      }
    );

    return () => unsubscribe();
  }, []);
};

export default useChatPersistence;
