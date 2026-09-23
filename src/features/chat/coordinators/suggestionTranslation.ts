// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { AppSettings, ChatMessage, LanguagePair, ReplySuggestion } from '../../../core/types';
import type { UseTutorConversationConfig, MutableValue } from './conversationContracts';
import type { translateText as translate } from '../../../api/gemini/generative';
import type { trackGeminiUsage as trackUsage } from '../../../shared/utils/costTracker';
import { getPrimarySubtag } from '../../../shared/utils/languageUtils';
import { getGeminiModels } from '../../../core-sdk/modelRegistry';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE, type TokenCategory } from '../../../core/config/activityTokens';
export interface SuggestionTranslationPorts extends Pick<UseTutorConversationConfig, 'addMessage' | 't' | 'setMessages' | 'setReplySuggestions' | 'handleToggleSuggestionModeRef'> {
  selectedLanguagePairRef: MutableValue<LanguagePair | undefined>;
  settingsRef: MutableValue<AppSettings>;
  messagesRef: MutableValue<ChatMessage[]>;
  lastFetchedSuggestionsForRef: MutableValue<string | null>;
  createSuggestionTokenRef: MutableValue<string | null>;
  addActivityToken(category: TokenCategory, subtype: string): string;
  removeActivityToken(token: string): void;
  translateText: typeof translate;
  trackGeminiUsage: typeof trackUsage;
}
/** Translate a learner-authored suggestion and preserve its block ownership and
 * mode-exit lifecycle independently of the automatic suggestion request. */
export function createSuggestionTranslation(ports: SuggestionTranslationPorts) {
  const { addMessage, t, selectedLanguagePairRef, settingsRef, lastFetchedSuggestionsForRef, messagesRef, setMessages, setReplySuggestions, handleToggleSuggestionModeRef, addActivityToken, removeActivityToken, createSuggestionTokenRef, translateText, trackGeminiUsage } = ports;
  return async (textToTranslate: string) => {

    if (!textToTranslate || !selectedLanguagePairRef.current) return;

    // Add token for creating suggestion
    createSuggestionTokenRef.current = addActivityToken(TOKEN_CATEGORY.GEN, TOKEN_SUBTYPE.CREATE_SUGGESTION);

    const sttLang = settingsRef.current.stt.language;
    const sttLangCode = getPrimarySubtag(sttLang);
    const targetLangCode = getPrimarySubtag(selectedLanguagePairRef.current.targetLanguageCode);

    let fromLangName: string;
    let toLangName: string;
    let originalTextIsTarget: boolean;

    if (sttLangCode === targetLangCode) {
      fromLangName = selectedLanguagePairRef.current.targetLanguageName;
      toLangName = selectedLanguagePairRef.current.nativeLanguageName;
      originalTextIsTarget = true;
    } else {
      fromLangName = selectedLanguagePairRef.current.nativeLanguageName;
      toLangName = selectedLanguagePairRef.current.targetLanguageName;
      originalTextIsTarget = false;
    }

    try {
      const { translatedText, usageMetadata, modelVersion, modelUsed } = await translateText(textToTranslate, fromLangName, toLangName);
      trackGeminiUsage({
        feature: 'translation',
        configuredModel: modelUsed || getGeminiModels().text.translation,
        modelVersion,
        usageMetadata,
      });
      const newSuggestion: ReplySuggestion = {
        target: originalTextIsTarget ? textToTranslate : translatedText,
        native: originalTextIsTarget ? translatedText : textToTranslate,
      };

      const isDuplicate = (s: ReplySuggestion) => s.target === newSuggestion.target && s.native === newSuggestion.native;

      setReplySuggestions(prev => {
        if (prev.some(isDuplicate)) return prev;
        return [newSuggestion, ...prev];
      });

      const targetMsgId = lastFetchedSuggestionsForRef.current ||
        messagesRef.current.slice().reverse().find(m => m.role === 'assistant' && !m.thinking)?.id;

      if (targetMsgId) {
        if (!lastFetchedSuggestionsForRef.current) {
          lastFetchedSuggestionsForRef.current = targetMsgId;
        }
        setMessages(prev => prev.map(m => {
          if (m.id === targetMsgId) {
            const existing = m.replySuggestions || [];
            if (existing.some(isDuplicate)) return m;
            return { ...m, replySuggestions: [newSuggestion, ...existing] };
          }
          return m;
        }));
      }

    } catch (error) {
      console.error("Failed to create suggestion via translation:", error);
      addMessage({ role: 'error', text: t('error.translationFailed') });
    } finally {
      // Remove creating suggestion token
      if (createSuggestionTokenRef.current) {
        removeActivityToken(createSuggestionTokenRef.current);
        createSuggestionTokenRef.current = null;
      }
      // Exit suggestion mode after creating suggestion (matches original behavior)
      if (handleToggleSuggestionModeRef?.current) {
        handleToggleSuggestionModeRef.current(false);
      }
    }
  };
}
