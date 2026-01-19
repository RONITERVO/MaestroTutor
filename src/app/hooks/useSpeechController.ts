// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { useCallback, useRef, useEffect } from 'react';
import { 
  ChatMessage, 
  SpeechPart, 
  TtsAudioCacheEntry, 
  RecordedUtterance, 
  ReplySuggestion,
  LanguagePair 
} from '../../core/types';
import useBrowserSpeech from '../../features/speech/hooks/useBrowserSpeech';
import { getPrimaryCode } from '../../shared/utils/languageUtils';
import { 
  INLINE_CAP_AUDIO, 
  computeTtsCacheKey, 
  getCachedAudioForKey 
} from '../../features/chat/utils/persistence';

export interface UseSpeechControllerConfig {
  settingsRef: React.MutableRefObject<{
    stt: { enabled: boolean; language: string; provider?: 'browser' | 'gemini' };
    tts: { provider?: 'browser' | 'gemini'; speakNative: boolean };
    isSuggestionMode: boolean;
  }>;
  messagesRef: React.MutableRefObject<ChatMessage[]>;
  selectedLanguagePairRef: React.MutableRefObject<LanguagePair | undefined>;
  isSendingRef: React.MutableRefObject<boolean>;
  lastFetchedSuggestionsForRef: React.MutableRefObject<string | null>;
  /** Ref to suggestions - allows late binding after useMaestroController */
  replySuggestionsRef?: React.MutableRefObject<ReplySuggestion[]>;
  upsertMessageTtsCache: (messageId: string, entry: TtsAudioCacheEntry) => void;
  upsertSuggestionTtsCache: (messageId: string, suggestionIndex: number, entry: TtsAudioCacheEntry) => void;
  setMessages?: React.Dispatch<React.SetStateAction<ChatMessage[]>>;
}

export interface UseSpeechControllerReturn {
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
  startListening: (lang: string) => void;
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
 */
export const useSpeechController = (config: UseSpeechControllerConfig): UseSpeechControllerReturn => {
  const { 
    settingsRef, 
    messagesRef, 
    selectedLanguagePairRef,
    isSendingRef,
    lastFetchedSuggestionsForRef,
    replySuggestionsRef,
    upsertMessageTtsCache,
    upsertSuggestionTtsCache,
    setMessages,
  } = config;

  const speechIsSpeakingRef = useRef<boolean>(false);
  const recordedUtterancePendingRef = useRef<RecordedUtterance | null>(null);
  const pendingRecordedAudioMessageRef = useRef<string | null>(null);
  const sttInterruptedBySendRef = useRef<boolean>(false);

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
            startListening(settingsRef.current.stt.language);
          }
        }, 100);
      } else if (errorOccurred) {
        console.warn("STT cycle ended with error. STT will not automatically restart unless manually toggled.");
      }
    },
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
            startListening(settingsRef.current.stt.language);
            sttInterruptedBySendRef.current = false;
          }
        }, 100);
      }
    }, []),
    getTtsProvider: useCallback(() => settingsRef.current.tts?.provider || 'browser', []),
    getSttProvider: useCallback(() => settingsRef.current.stt?.provider || 'browser', []),
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
      if (pendingId && setMessages) {
        pendingRecordedAudioMessageRef.current = null;
        setMessages((prev) => prev.map((m) => (m.id === pendingId ? { ...m, recordedUtterance: utterance } : m)));
        recordedUtterancePendingRef.current = null;
      }
    }, [setMessages])
  });

  // Sync speaking state to ref
  useEffect(() => {
    speechIsSpeakingRef.current = isSpeaking;
  }, [isSpeaking]);

  const prepareSpeechPartsWithCache = useCallback((parts: SpeechPart[], defaultLang: string): SpeechPart[] => {
    const provider = settingsRef.current.tts?.provider || 'browser';
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
  }, [settingsRef, messagesRef, replySuggestionsRef, upsertMessageTtsCache, upsertSuggestionTtsCache, lastFetchedSuggestionsForRef]);

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
            if (pair.spanish && pair.spanish.trim()) {
              partsForTTS.push({ text: pair.spanish, langCode: targetLang, context: { source: 'message', messageId: message.id } });
            }
            if (pair.english && pair.english.trim()) {
              partsForTTS.push({ text: pair.english, langCode: nativeLang, context: { source: 'message', messageId: message.id } });
            }
          });
        } else {
          message.translations.forEach(pair => {
            if (pair.spanish && pair.spanish.trim()) {
              partsForTTS.push({ text: pair.spanish, langCode: targetLang, context: { source: 'message', messageId: message.id } });
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
    startListening,
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

export default useSpeechController;
