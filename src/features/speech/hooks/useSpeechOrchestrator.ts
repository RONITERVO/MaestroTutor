// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * useSpeechOrchestrator - Hook for orchestrating speech (TTS/STT) functionality.
 * 
 * Wraps useBrowserSpeech and adds caching, message speaking, etc.
 * Manages activity tokens for unified busy state tracking via uiSlice.
 * 
 * Activity tokens used:
 * - 'tts:speak' - TTS is actively speaking
 * - 'stt:listen' - STT is actively listening
 */

import { useCallback, useRef, useEffect, useMemo } from 'react';
import { 
  ChatMessage, 
  SpeechPart, 
  TtsAudioCacheEntry, 
  RecordedUtterance 
} from '../../../core/types';
import useBrowserSpeech from './useBrowserSpeech';
import { getPrimaryCode } from '../../../shared/utils/languageUtils';
import { INLINE_CAP_AUDIO, computeTtsCacheKey, getCachedAudioForKey } from '../../chat';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE } from '../../../core/config/activityTokens';
import { useMaestroStore } from '../../../store';
import { selectIsSending } from '../../../store/slices/uiSlice';
import { selectSelectedLanguagePair } from '../../../store/slices/settingsSlice';
import { createSmartRef, createWritableSmartRef } from '../../../shared/utils/smartRef';

export interface UseSpeechOrchestratorConfig {
  upsertMessageTtsCache: (messageId: string, entry: TtsAudioCacheEntry) => void;
  upsertSuggestionTtsCache: (messageId: string, suggestionIndex: number, entry: TtsAudioCacheEntry) => void;
  setMessages?: React.Dispatch<React.SetStateAction<ChatMessage[]>>;
}

export interface UseSpeechOrchestratorReturn {
  // TTS State
  isSpeaking: boolean;
  speak: (textOrParts: string | SpeechPart[], defaultLang: string) => void;
  stopSpeaking: () => void;
  isSpeechSynthesisSupported: boolean;
  speakingUtteranceText: string | null;
  hasPendingQueueItems: () => boolean;
  
  // STT State
  isListening: boolean;
  transcript: string;
  startListening: (languageOrOptions?: string | { language?: string; lastAssistantMessage?: string; replySuggestions?: string[] }) => void;
  stopListening: () => void;
  sttError: string | null;
  isSpeechRecognitionSupported: boolean;
  clearTranscript: () => void;
  claimRecordedUtterance: () => RecordedUtterance | null;
  
  // Refs
  speechIsSpeakingRef: React.MutableRefObject<boolean>;
  recordedUtterancePendingRef: React.MutableRefObject<RecordedUtterance | null>;
  pendingRecordedAudioMessageRef: React.MutableRefObject<string | null>;
  sttInterruptedBySendRef: React.MutableRefObject<boolean>;
  
  // Utility functions
  prepareSpeechPartsWithCache: (parts: SpeechPart[], defaultLang: string) => SpeechPart[];
  speakMessage: (message: ChatMessage) => void;
  speakWrapper: (textOrParts: string | SpeechPart[], defaultLang: string) => void;
}

/**
 * Hook for orchestrating speech (TTS/STT) functionality.
 * Wraps useBrowserSpeech and adds caching, message speaking, etc.
 * Manages activity tokens for unified busy state tracking.
 */
export const useSpeechOrchestrator = (config: UseSpeechOrchestratorConfig): UseSpeechOrchestratorReturn => {
  const { 
    upsertMessageTtsCache,
    upsertSuggestionTtsCache,
    setMessages,
  } = config;

  // Get store actions for activity token management
  const setStoreTranscript = useMaestroStore(state => state.setTranscript);
  const setStoreSttError = useMaestroStore(state => state.setSttError);
  const setStoreSpeakingUtteranceText = useMaestroStore(state => state.setSpeakingUtteranceText);
  const addActivityToken = useMaestroStore(state => state.addActivityToken);
  const removeActivityToken = useMaestroStore(state => state.removeActivityToken);

  const speechIsSpeakingRef = useRef<boolean>(false);
  const storeSetMessages = useMaestroStore(state => state.setMessages);
  const setRecordedUtterancePending = useMaestroStore(state => state.setRecordedUtterancePending);
  const setPendingRecordedAudioMessageId = useMaestroStore(state => state.setPendingRecordedAudioMessageId);
  const setSttInterruptedBySend = useMaestroStore(state => state.setSttInterruptedBySend);
  const applySetMessages = setMessages || storeSetMessages;

  // Smart refs - always return fresh state from store (no stale closures)
  const settingsRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.settings), []);
  const messagesRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.messages), []);
  const selectedLanguagePairRef = useMemo(() => createSmartRef(useMaestroStore.getState, selectSelectedLanguagePair), []);
  const isSendingRef = useMemo(() => createSmartRef(useMaestroStore.getState, selectIsSending), []);
  const replySuggestionsRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.replySuggestions), []);

  // Smart refs with setters - store-backed read/write access
  const lastFetchedSuggestionsForRef = useMemo(
    () => createWritableSmartRef(
      useMaestroStore.getState,
      state => state.lastFetchedSuggestionsFor,
      value => useMaestroStore.getState().setLastFetchedSuggestionsFor(value)
    ),
    []
  );

  const recordedUtterancePendingRef = useMemo(
    () => createWritableSmartRef(
      useMaestroStore.getState,
      state => state.recordedUtterancePending,
      setRecordedUtterancePending
    ),
    [setRecordedUtterancePending]
  );

  const pendingRecordedAudioMessageRef = useMemo(
    () => createWritableSmartRef(
      useMaestroStore.getState,
      state => state.pendingRecordedAudioMessageId,
      setPendingRecordedAudioMessageId
    ),
    [setPendingRecordedAudioMessageId]
  );

  const sttInterruptedBySendRef = useMemo(
    () => createWritableSmartRef(
      useMaestroStore.getState,
      state => state.sttInterruptedBySend,
      setSttInterruptedBySend
    ),
    [setSttInterruptedBySend]
  );
  
  // Track activity tokens for unified busy state management
  const speakingTokenRef = useRef<string | null>(null);
  const listeningTokenRef = useRef<string | null>(null);

  const {
    isSpeaking, 
    speak, 
    stopSpeaking, 
    isSpeechSynthesisSupported,
    isListening, 
    transcript, 
    startListening, 
    stopListening, 
    sttError, 
    isSpeechRecognitionSupported, 
    clearTranscript,
    speakingUtteranceText,
    claimRecordedUtterance,
    hasPendingQueueItems,
  } = useBrowserSpeech({
    onEngineCycleEnd: (errorOccurred: boolean) => {
      if (
        settingsRef.current.stt.enabled &&
        !errorOccurred &&
        !settingsRef.current.isSuggestionMode &&
        !isSendingRef.current &&
        !speechIsSpeakingRef.current
      ) {
        setTimeout(() => {
          if (settingsRef.current.stt.enabled) {
            startListeningWithContext(settingsRef.current.stt.language);
          }
        }, 100);
      } else if (errorOccurred) {
        console.warn("STT cycle ended with error. STT will not automatically restart unless manually toggled.");
      }
    },
    // Note: Empty dependency arrays are intentional and safe here because these refs
    // are store-backed proxies where .current always reads fresh state from useMaestroStore.getState().
    // This pattern ensures callbacks remain stable while always accessing current state.
    isGlobalSttEnabled: useCallback(() => settingsRef.current.stt.enabled, []),
    getGlobalSttLanguage: useCallback(() => settingsRef.current.stt.language, []),
    onSpeechQueueCompleted: useCallback(() => {
      speechIsSpeakingRef.current = false;
      if (
        sttInterruptedBySendRef.current &&
        settingsRef.current.stt.enabled &&
        !isSendingRef.current
      ) {
        setTimeout(() => {
          if (settingsRef.current.stt.enabled && !isSendingRef.current) {
            startListeningWithContext(settingsRef.current.stt.language);
            sttInterruptedBySendRef.current = false;
          }
        }, 100);
      }
    }, []),
    onRecordedUtteranceReady: useCallback((utterance: RecordedUtterance) => {
      if (!utterance || typeof utterance.dataUrl !== 'string' || 
          utterance.dataUrl.length === 0 || utterance.dataUrl.length > INLINE_CAP_AUDIO) {
        recordedUtterancePendingRef.current = null;
        pendingRecordedAudioMessageRef.current = null;
        return;
      }
      recordedUtterancePendingRef.current = utterance;
      // If there's a pending message waiting for this audio, attach it
      const pendingId = pendingRecordedAudioMessageRef.current;
      if (pendingId) {
        pendingRecordedAudioMessageRef.current = null;
        applySetMessages((prev) => prev.map((m) => (m.id === pendingId ? { ...m, recordedUtterance: utterance } : m)));
        recordedUtterancePendingRef.current = null;
      }
    }, [applySetMessages])
  });

  const getSttContext = useCallback(() => {
    const messages = messagesRef.current || [];
    let lastAssistantMessage: string | undefined;
    for (let i = messages.length - 1; i >= 0; i -= 1) {
      if (messages[i]?.role === 'assistant') {
        lastAssistantMessage = messages[i]?.text || messages[i]?.rawAssistantResponse || '';
        break;
      }
    }
    const replySuggestions = (replySuggestionsRef.current || [])
      .map(s => s?.target || s?.native)
      .filter(Boolean) as string[];
    return { lastAssistantMessage, replySuggestions };
  }, []);

  const startListeningWithContext = useCallback(
    (languageOrOptions?: string | { language?: string; lastAssistantMessage?: string; replySuggestions?: string[] }) => {
      const context = getSttContext();
      if (typeof languageOrOptions === 'string' || languageOrOptions === undefined) {
        startListening({ language: languageOrOptions, ...context });
        return;
      }
      startListening({ ...context, ...languageOrOptions });
    },
    [getSttContext, startListening]
  );

  // Sync speech state to refs and manage activity tokens
  useEffect(() => {
    speechIsSpeakingRef.current = isSpeaking;
    // Manage speaking token for unified busy state tracking
    if (isSpeaking && !speakingTokenRef.current) {
      speakingTokenRef.current = addActivityToken(TOKEN_CATEGORY.TTS, TOKEN_SUBTYPE.SPEAK);
    } else if (!isSpeaking && speakingTokenRef.current) {
      removeActivityToken(speakingTokenRef.current);
      speakingTokenRef.current = null;
    }
  }, [isSpeaking, addActivityToken, removeActivityToken]);

  useEffect(() => {
    // Manage listening token for unified busy state tracking
    if (isListening && !listeningTokenRef.current) {
      listeningTokenRef.current = addActivityToken(TOKEN_CATEGORY.STT, TOKEN_SUBTYPE.LISTEN);
    } else if (!isListening && listeningTokenRef.current) {
      removeActivityToken(listeningTokenRef.current);
      listeningTokenRef.current = null;
    }
  }, [isListening, addActivityToken, removeActivityToken]);

  useEffect(() => {
    setStoreTranscript(transcript);
  }, [transcript, setStoreTranscript]);

  useEffect(() => {
    setStoreSttError(sttError);
  }, [sttError, setStoreSttError]);

  useEffect(() => {
    setStoreSpeakingUtteranceText(speakingUtteranceText);
  }, [speakingUtteranceText, setStoreSpeakingUtteranceText]);

  const prepareSpeechPartsWithCache = useCallback((parts: SpeechPart[], defaultLang: string): SpeechPart[] => {
    const provider = 'gemini-live';
    return parts.map((part) => {
      const cleanedText = (part.text || '').replace(/\*/g, '').trim();
      const lang = part.langCode || defaultLang;
      const context = part.context;
      let cacheKey = part.cacheKey;
      let cachedAudio = part.cachedAudio;
      let onAudioCached = part.onAudioCached;
      const voiceName = part.voiceName;

      if (!cleanedText) {
        return {
          ...part,
          text: cleanedText,
          langCode: lang,
        };
      }

      if (!cacheKey && context && context.source === 'message' && context.messageId) {
        cacheKey = computeTtsCacheKey(cleanedText, lang, provider, voiceName);
        const message = messagesRef.current.find(m => m.id === context.messageId);
        cachedAudio = cachedAudio || getCachedAudioForKey(message?.ttsAudioCache, cacheKey);
        if (!onAudioCached) {
          const messageId = context.messageId;
          onAudioCached = (audioDataUrl) => {
            if (!cacheKey) return;
            upsertMessageTtsCache(messageId, {
              key: cacheKey,
              langCode: lang,
              provider,
              audioDataUrl,
              updatedAt: Date.now(),
              voiceName,
            });
          };
        }
      } else if (!cacheKey && context && context.source === 'suggestion' && 
                 context.messageId && typeof context.suggestionIndex === 'number') {
        const suggestionIndex = context.suggestionIndex;
        if (suggestionIndex >= 0) {
          cacheKey = computeTtsCacheKey(cleanedText, lang, provider, voiceName);
          const message = messagesRef.current.find(m => m.id === context.messageId);
          cachedAudio = cachedAudio || getCachedAudioForKey(message?.replySuggestions?.[suggestionIndex]?.ttsAudioCache, cacheKey);
          if (!cachedAudio && lastFetchedSuggestionsForRef.current === context.messageId) {
            const localSuggestion = replySuggestionsRef?.current?.[suggestionIndex];
            cachedAudio = cachedAudio || getCachedAudioForKey(localSuggestion?.ttsAudioCache, cacheKey);
          }
          if (!onAudioCached) {
            const messageId = context.messageId;
            onAudioCached = (audioDataUrl) => {
              if (!cacheKey) return;
              upsertSuggestionTtsCache(messageId, suggestionIndex, {
                key: cacheKey,
                langCode: lang,
                provider,
                audioDataUrl,
                updatedAt: Date.now(),
                voiceName,
              });
            };
          }
        }
      }

      return {
        ...part,
        text: cleanedText,
        langCode: lang,
        cacheKey,
        cachedAudio,
        onAudioCached,
      };
    });
  }, [upsertMessageTtsCache, upsertSuggestionTtsCache, lastFetchedSuggestionsForRef]);

  const speakMessage = useCallback((message: ChatMessage) => {
    const selectedLanguagePair = selectedLanguagePairRef.current;
    if (!selectedLanguagePair) return;

    if (message.role === 'assistant') {
      const partsForTTS: SpeechPart[] = [];
      const targetLang = getPrimaryCode(selectedLanguagePair.targetLanguageCode);
      const nativeLang = getPrimaryCode(selectedLanguagePair.nativeLanguageCode);
      let defaultLangForSpeakText = targetLang || 'es';

      if (message.translations && message.translations.length > 0) {
        if (settingsRef.current.tts.speakNative) {
          message.translations.forEach(pair => {
            if (pair.target && pair.target.trim()) {
              partsForTTS.push({ text: pair.target, langCode: targetLang, context: { source: 'message', messageId: message.id } });
            }
            if (pair.native && pair.native.trim()) {
              partsForTTS.push({ text: pair.native, langCode: nativeLang, context: { source: 'message', messageId: message.id } });
            }
          });
        } else {
          message.translations.forEach(pair => {
            if (pair.target && pair.target.trim()) {
              partsForTTS.push({ text: pair.target, langCode: targetLang, context: { source: 'message', messageId: message.id } });
            }
          });
        }
      } else if (message.rawAssistantResponse) {
        let textToSay = message.rawAssistantResponse;
        let langToUse = targetLang;
        const mightBeNative = !textToSay.match(/[¡¿ñáéíóú]/i) && nativeLang.startsWith('en') && textToSay.match(/[a-zA-Z]/);
        if (mightBeNative && settingsRef.current.tts.speakNative) {
          langToUse = nativeLang;
        }
        if (textToSay.trim()) {
          partsForTTS.push({ text: textToSay.trim(), langCode: langToUse, context: { source: 'message', messageId: message.id } });
          defaultLangForSpeakText = langToUse;
        }
      } else if (message.text) {
        let textToSay = message.text;
        let langToUse = targetLang;
        const mightBeNative = !textToSay.match(/[¡¿ñáéíóú]/i) && nativeLang.startsWith('en') && textToSay.match(/[a-zA-Z]/);
        if (mightBeNative && settingsRef.current.tts.speakNative) {
          langToUse = nativeLang;
        }
        if (textToSay.trim()) {
          partsForTTS.push({ text: textToSay.trim(), langCode: langToUse, context: { source: 'message', messageId: message.id } });
          defaultLangForSpeakText = langToUse;
        }
      }

      if (partsForTTS.length > 0) {
        const preparedParts = prepareSpeechPartsWithCache(partsForTTS, defaultLangForSpeakText);
        speak(preparedParts, defaultLangForSpeakText);
      }

    } else if (message.text && (message.role === 'error' || message.role === 'status')) {
      const textToSay = message.text;
      const langToUse = getPrimaryCode(selectedLanguagePair.nativeLanguageCode) || 'en';
      if (textToSay.trim()) {
        const preparedParts = prepareSpeechPartsWithCache([
          { text: textToSay.trim(), langCode: langToUse, context: { source: 'adHoc' } },
        ], langToUse);
        speak(preparedParts, langToUse);
      }
    }
  }, [prepareSpeechPartsWithCache, speak, settingsRef, selectedLanguagePairRef]);

  const speakWrapper = useCallback(
    (textOrParts: string | SpeechPart[], defaultLang: string) => {
      const baseParts: SpeechPart[] = typeof textOrParts === 'string'
        ? [{ text: textOrParts, langCode: defaultLang, context: { source: 'adHoc' } }]
        : textOrParts;
      const preparedParts = prepareSpeechPartsWithCache(baseParts, defaultLang);
      speak(preparedParts, defaultLang);
    },
    [prepareSpeechPartsWithCache, speak]
  );

  return {
    // TTS
    isSpeaking,
    speak,
    stopSpeaking,
    isSpeechSynthesisSupported,
    speakingUtteranceText,
    hasPendingQueueItems,
    
    // STT
    isListening,
    transcript,
    startListening: startListeningWithContext,
    stopListening,
    sttError,
    isSpeechRecognitionSupported,
    clearTranscript,
    claimRecordedUtterance,
    
    // Refs
    speechIsSpeakingRef,
    recordedUtterancePendingRef,
    pendingRecordedAudioMessageRef,
    sttInterruptedBySendRef,
    
    // Utilities
    prepareSpeechPartsWithCache,
    speakMessage,
    speakWrapper,
  };
};

export default useSpeechOrchestrator;
