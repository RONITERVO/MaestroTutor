
// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import React, { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import ChatInterface from './src/components/ChatInterface';
import { AppSettings, ChatMessage, GroundingChunk, CameraDevice, ReplySuggestion, MaestroActivityStage, LanguagePair, RecordedUtterance, TtsAudioCacheEntry, SpeechPart, ChatMeta } from './types';
import { generateGeminiResponse, generateImage, translateText, ApiError, sanitizeHistoryWithVerifiedUris, uploadMediaToFiles, checkFileStatuses } from './src/services/geminiService';
import { getLoadingGifsDB, setLoadingGifsDB, getMaestroProfileImageDB, setMaestroProfileImageDB } from './src/services/assets';
import { getAppSettingsDB, setAppSettingsDB } from './src/services/settings';
import { getGlobalProfileDB, setGlobalProfileDB } from './src/services/globalProfile';
import useBrowserSpeech from './hooks/useBrowserSpeech';
import { useGeminiLiveConversation, LiveSessionState } from './hooks/speech/useGeminiLiveConversation';
import { translations, TranslationReplacements } from './translations/index';
import {
  APP_TITLE_KEY,
  LOCAL_STORAGE_SETTINGS_KEY,
  DEFAULT_TEXT_MODEL_ID,
  IMAGE_GEN_CAMERA_ID,
  STT_LANGUAGES,
  ALL_LANGUAGES,
  DEFAULT_NATIVE_LANG_CODE,
  DEFAULT_TARGET_LANG_CODE,
  MAX_MEDIA_TO_KEEP,
  DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE,
  IMAGE_GEN_SYSTEM_INSTRUCTION,
  IMAGE_GEN_USER_PROMPT_TEMPLATE,
  composeMaestroSystemInstruction,
} from './constants';

import { uniq, isRealChatMessage, fetchDefaultAvatarBlob } from './src/utils/common';
import { INLINE_CAP_AUDIO, upsertTtsCacheEntries, getCachedAudioForKey, computeTtsCacheKey } from './src/utils/persistence';
import { getChatHistoryDB, safeSaveChatHistoryDB, readBackupForPair, getChatMetaDB, setChatMetaDB, getAllChatHistoriesDB, clearAndSaveAllHistoriesDB, getAllChatMetasDB, deriveHistoryForApi } from './src/services/historyService';
import { generateAllLanguagePairs, getPrimaryCode, getShortLangCodeForPrompt } from './src/utils/languageUtils';
import { createKeyframeFromVideoDataUrl } from './src/utils/mediaUtils';
import { processMediaForUpload } from './src/services/mediaOptimizationService';
import Header from './src/components/Header';
import { useSmartReengagement } from './src/hooks/useSmartReengagement';
import DebugLogPanel from './src/components/DebugLogPanel';

const AUX_TEXT_MODEL_ID = 'gemini-3-flash-preview';

const allGeneratedLanguagePairs = generateAllLanguagePairs();
const DEFAULT_LANGUAGE_PAIR_ID = `${DEFAULT_TARGET_LANG_CODE}-${DEFAULT_NATIVE_LANG_CODE}`;

const initialSettings: AppSettings = {
  selectedLanguagePairId: null,
  selectedCameraId: null,
  sendWithSnapshotEnabled: false,
  tts: {
    provider: 'browser', // Default to browser TTS for stability/cost
    speakNative: true,
  },
  stt: {
    enabled: false,
    language: getPrimaryCode(ALL_LANGUAGES.find(l => l.langCode === DEFAULT_NATIVE_LANG_CODE)?.code || STT_LANGUAGES[0].code),
    provider: 'browser',
  },
  smartReengagement: {
    thresholdSeconds: 45,
    useVisualContext: false,
  },
  enableGoogleSearch: true,
  imageGenerationModeEnabled: true,
  imageFocusedModeEnabled: true,
  isSuggestionMode: false,
  historyBookmarkMessageId: null,
  maxVisibleMessages: undefined,
};

const MAX_VISIBLE_MESSAGES_DEFAULT = 50;

const loadFromLocalStorage = <T,>(key: string, defaultValue: T): T => {
  try {
    const item = window.localStorage.getItem(key);
    if (item) {
        const parsed = JSON.parse(item);
        if (key === LOCAL_STORAGE_SETTINGS_KEY && typeof defaultValue === 'object' && defaultValue !== null) {
            const mergedSettings = { ...defaultValue } as any;
            for (const k in parsed) {
                if (parsed.hasOwnProperty(k)) {
                    if (typeof parsed[k] === 'object' && parsed[k] !== null && !Array.isArray(parsed[k]) && k in mergedSettings) {
                        mergedSettings[k] = { ...mergedSettings[k], ...parsed[k] };
                    } else {
                        mergedSettings[k] = parsed[k];
                    }
                }
            }
            Object.keys(initialSettings).forEach(initialKey => {
                if (!(initialKey in mergedSettings)) {
                    mergedSettings[initialKey] = (initialSettings as any)[initialKey];
                }
                if (typeof mergedSettings.imageGenerationModeEnabled === 'undefined') {
                    mergedSettings.imageGenerationModeEnabled = initialSettings.imageGenerationModeEnabled;
                }
                if (typeof mergedSettings.imageFocusedModeEnabled === 'undefined') {
                    mergedSettings.imageFocusedModeEnabled = initialSettings.imageFocusedModeEnabled;
                }
                if (typeof mergedSettings.maxVisibleMessages === 'undefined' || mergedSettings.maxVisibleMessages === null) {
                  mergedSettings.maxVisibleMessages = MAX_VISIBLE_MESSAGES_DEFAULT;
                }
            });
      ['tts', 'stt', 'smartReengagement'].forEach(nestedKey => {
                if (mergedSettings[nestedKey] && (initialSettings as any)[nestedKey]) {
                    Object.keys((initialSettings as any)[nestedKey]).forEach(subKey => {
                        if (!((mergedSettings as any)[nestedKey] as any).hasOwnProperty(subKey)) {
                            (mergedSettings as any)[nestedKey][subKey] = (initialSettings as any)[nestedKey][subKey];
                        }
                    });
                }
            });
      if (!mergedSettings.tts || !mergedSettings.tts.provider) {
        mergedSettings.tts = { ...(mergedSettings.tts || {}), provider: 'browser' };
      }
      if (!mergedSettings.stt || !mergedSettings.stt.provider) {
        mergedSettings.stt = { ...(mergedSettings.stt || {}), provider: 'browser' };
      }
            if (mergedSettings.selectedLanguagePairId && !allGeneratedLanguagePairs.some(p => p.id === mergedSettings.selectedLanguagePairId)) {
                mergedSettings.selectedLanguagePairId = null;
            }
            const activePairForStt = allGeneratedLanguagePairs.find(p => p.id === mergedSettings.selectedLanguagePairId) || allGeneratedLanguagePairs.find(p => p.id === DEFAULT_LANGUAGE_PAIR_ID)!;
            mergedSettings.stt.language = mergedSettings.stt?.language || getPrimaryCode(activePairForStt.targetLanguageCode) || getPrimaryCode(activePairForStt.nativeLanguageCode) || STT_LANGUAGES[0].code;

            if (typeof mergedSettings.enableGoogleSearch === 'undefined') {
                mergedSettings.enableGoogleSearch = initialSettings.enableGoogleSearch;
            }

            return mergedSettings as T;
        }
        return parsed;
    }
    return defaultValue;
  } catch (error) {
    console.warn(`Error reading localStorage key "${key}":`, error);
    return defaultValue;
  }
};

const getFacingModeFromLabel = (label: string): 'user' | 'environment' | 'unknown' => {
    const lowerLabel = label.toLowerCase();
    if (lowerLabel.includes('front') || lowerLabel.includes('user')) return 'user';
    if (lowerLabel.includes('back') || lowerLabel.includes('rear') || lowerLabel.includes('environment')) return 'environment';
    return 'unknown';
};

const useTranslations = (nativeLangCode: string) => {
  const lang = nativeLangCode.substring(0,2);

  const t = useCallback((key: string, replacements?: TranslationReplacements): string => {
    let translation = translations[lang]?.[key] || translations.en[key] || key;
    if (replacements) {
      Object.keys(replacements).forEach(rKey => {
        translation = translation.replace(`{${rKey}}`, String(replacements[rKey]));
      });
    }
    return translation;
  }, [lang]);

  return { t };
};

const App: React.FC = () => {
  // --- Refs (Hoisted) ---
  const isLoadingHistoryRef = useRef<boolean>(true);
  const bubbleWrapperRefs = useRef<Map<string, HTMLDivElement>>(new Map());
  const uiBusyTokensRef = useRef<Set<string>>(new Set());
  const externalUiTaskCountRef = useRef<number>(0);
  const liveUiTokenRef = useRef<string | null>(null);
  const settingsRef = useRef(initialSettings);
  const messagesRef = useRef<ChatMessage[]>([]);
  const isSendingRef = useRef(false);
  const sendWithFileUploadInProgressRef = useRef(false);
  const availableCamerasRef = useRef<CameraDevice[]>([]);
  const selectedLanguagePairRef = useRef<LanguagePair | undefined>(undefined);
  const isLoadingSuggestionsRef = useRef(false);
  const lastFetchedSuggestionsForRef = useRef<string | null>(null);
  const visualContextVideoRef = useRef<HTMLVideoElement>(null);
  const visualContextStreamRef = useRef<MediaStream | null>(null);
  const liveSessionShouldRestoreSttRef = useRef(false);
  const liveSessionCaptureRef = useRef<{ stream: MediaStream; created: boolean } | null>(null);
  const isCurrentlyPerformingVisualContextCaptureRef = useRef(false);
  const speechIsSpeakingRef = useRef(false);
  const recordedUtterancePendingRef = useRef<RecordedUtterance | null>(null);
  const pendingRecordedAudioMessageRef = useRef<string | null>(null);
  const autoSendTimerRef = useRef<number | null>(null);
  const autoSendSnapshotRef = useRef<string>('');
  const sttInterruptedBySendRef = useRef(false);
  const _handleSendMessageInternalRef = useRef<ReturnType<typeof useCallback<(...args: any[]) => Promise<boolean>>>>((...args) => Promise.resolve(false));
  const sendPrepRef = useRef<{ active: boolean; label: string; done?: number; total?: number; etaMs?: number } | null>(null);
  const maestroAvatarUriRef = useRef<string | null>(null);
  const maestroAvatarMimeTypeRef = useRef<string | null>(null);
  const prevIsListeningRef = useRef<boolean>(false);
  const userActivityTimerRef = useRef<number | null>(null);
  const isUserActiveRef = useRef<boolean>(false);

  // --- State ---
  const [languagePairs] = useState<LanguagePair[]>(allGeneratedLanguagePairs);
  const [settings, setSettings] = useState<AppSettings>(initialSettings);
  
  const [isLanguageSelectionOpen, setIsLanguageSelectionOpen] = useState(false);
  const [tempNativeLangCode, setTempNativeLangCode] = useState<string | null>(null);
  const [tempTargetLangCode, setTempTargetLangCode] = useState<string | null>(null);

  const microphoneApiAvailable = useMemo(() => {
      if (typeof window === 'undefined') return false;
      try {
        return !!(navigator && navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
      } catch {
        return false;
      }
    }, []);
  const [loadingGifs, setLoadingGifs] = useState<string[]>([]);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isLoadingHistory, setIsLoadingHistory] = useState(true);
  
  // Sync Refs
  useEffect(() => { isLoadingHistoryRef.current = isLoadingHistory; }, [isLoadingHistory]);
  
  const [isSending, setIsSending] = useState(false);
  const [isCreatingSuggestion, setIsCreatingSuggestion] = useState(false);
  const [imageLoadDurations, setImageLoadDurations] = useState<number[]>([]);
  const [transitioningImageId, setTransitioningImageId] = useState<string | null>(null);

  const [isTopbarOpen, setIsTopbarOpen] = useState(false);
  const [showDebugLogs, setShowDebugLogs] = useState(false);
  
  const selectedLanguagePair = languagePairs.find(p => p.id === settings.selectedLanguagePairId);
  
  const nativeLangForTranslations = useMemo(() => {
    if (selectedLanguagePair) {
        return getPrimaryCode(selectedLanguagePair.nativeLanguageCode);
    }
    const browserLang = (typeof navigator !== 'undefined' ? navigator.language : 'en').substring(0,2);
    return allGeneratedLanguagePairs.find(p => p.nativeLanguageCode.startsWith(browserLang))
      ? browserLang
      : 'en';
  }, [selectedLanguagePair]);

  const { t } = useTranslations(nativeLangForTranslations);

  const [currentSystemPromptText, setCurrentSystemPromptText] = useState('');
  const [currentReplySuggestionsPromptText, setCurrentReplySuggestionsPromptText] = useState('');

  const [latestGroundingChunks, setLatestGroundingChunks] = useState<GroundingChunk[] | undefined>(undefined);
  const [visualContextCameraError, setVisualContextCameraError] = useState<string | null>(null);
  const [snapshotUserError, setSnapshotUserError] = useState<string | null>(null);
  const [availableCameras, setAvailableCameras] = useState<CameraDevice[]>([]);
  const [currentCameraFacingMode, setCurrentCameraFacingMode] = useState<'user' | 'environment' | 'unknown'>('unknown');

  const [attachedImageBase64, setAttachedImageBase64] = useState<string | null>(null);
  const [attachedImageMimeType, setAttachedImageMimeType] = useState<string | null>(null);

  const [replySuggestions, setReplySuggestions] = useState<ReplySuggestion[]>([]);
  const [isLoadingSuggestions, setIsLoadingSuggestions] = useState(false);
  const [maestroActivityStage, setMaestroActivityStage] = useState<MaestroActivityStage>('idle');
  
  const [isUserActive, setIsUserActive] = useState(false);
  useEffect(() => { isUserActiveRef.current = isUserActive; }, [isUserActive]);

  const [wasSpeaking, setWasSpeaking] = useState(false);

  const [uiBusyTaskTags, setUiBusyTaskTags] = useState<string[]>([]);
  const [externalUiTaskCount, setExternalUiTaskCount] = useState<number>(0);

  const recomputeUiBusyState = useCallback(() => {
    const tags: string[] = [];
    let nonReengagementCount = 0;
    uiBusyTokensRef.current.forEach(tok => {
      if (typeof tok !== 'string') return;
      const tag = tok.split(':')[0];
      if (tag) tags.push(tag);
      if (!tok.startsWith('reengage-')) nonReengagementCount++;
    });
    const seen = new Set<string>();
    const uniqTags = tags.filter(t => (seen.has(t) ? false : (seen.add(t), true)));
    setUiBusyTaskTags(uniqTags);
    setExternalUiTaskCount(nonReengagementCount);
  }, []);

  useEffect(() => {
    externalUiTaskCountRef.current = externalUiTaskCount;
  }, [externalUiTaskCount]);

  const addUiBusyToken = useCallback((token: string): string => {
    uiBusyTokensRef.current.add(token);
    recomputeUiBusyState();
    return token;
  }, [recomputeUiBusyState]);

  const removeUiBusyToken = useCallback((token?: string | null) => {
    if (!token) return;
    uiBusyTokensRef.current.delete(token);
    recomputeUiBusyState();
  }, [recomputeUiBusyState]);

  const clearUiBusyTokens = useCallback(() => {
    uiBusyTokensRef.current.clear();
    recomputeUiBusyState();
  }, [recomputeUiBusyState]);

  // Sync refs with state
  useEffect(() => { settingsRef.current = settings; }, [settings]);
  useEffect(() => { messagesRef.current = messages; }, [messages]);
  useEffect(() => { isSendingRef.current = isSending; }, [isSending]);
  useEffect(() => { availableCamerasRef.current = availableCameras; }, [availableCameras]);
  useEffect(() => { selectedLanguagePairRef.current = selectedLanguagePair; }, [selectedLanguagePair]);
  useEffect(() => { isLoadingSuggestionsRef.current = isLoadingSuggestions; }, [isLoadingSuggestions]);

  const handleSettingsChange = useCallback(<K extends keyof AppSettings>(key: K, value: AppSettings[K]) => {
    setSettings(prev => {
      const next = { ...prev, [key]: value };
      settingsRef.current = next;
      setAppSettingsDB(next).catch(() => {});
      return next;
    });
  }, []);

  const handleSetAttachedImage = useCallback((base64: string | null, mimeType: string | null) => {
    setAttachedImageBase64(base64);
    setAttachedImageMimeType(mimeType);
  }, []);

  const [liveVideoStream, setLiveVideoStream] = useState<MediaStream | null>(null);
  const [liveSessionState, setLiveSessionState] = useState<LiveSessionState>('idle');
  const [liveSessionError, setLiveSessionError] = useState<string | null>(null);

  const releaseLiveSessionCapture = useCallback(() => {
    if (liveSessionCaptureRef.current) {
      const { stream, created } = liveSessionCaptureRef.current;
      if (created && stream) {
        stream.getTracks().forEach(t => t.stop());
      }
      liveSessionCaptureRef.current = null;
    }
    setLiveVideoStream(null);
  }, []);

  const parseGeminiResponse = useCallback((responseText: string | undefined): Array<{ spanish: string; english: string }> => {
    if (typeof responseText !== 'string' || !responseText.trim() || !selectedLanguagePairRef.current) {
      return [];
    }
    const lines = responseText.split('\n').map(line => line.trim()).filter(line => line);
    const translations: Array<{ spanish: string; english: string }> = [];
    const nativeLangPrefix = `[${getShortLangCodeForPrompt(selectedLanguagePairRef.current.nativeLanguageCode)}]`;

    for (let i = 0; i < lines.length; i++) {
      const currentLine = lines[i];
      if (!currentLine.startsWith(nativeLangPrefix)) {
        let targetContent = currentLine;
        let nativeContent = "";
        if (i + 1 < lines.length && lines[i+1].startsWith(nativeLangPrefix)) {
          nativeContent = lines[i+1].substring(nativeLangPrefix.length).trim();
          i++;
        }
        translations.push({ spanish: targetContent, english: nativeContent });
      } else {
        translations.push({ spanish: "", english: currentLine.substring(nativeLangPrefix.length).trim() });
      }
    }

    if (translations.length === 0 && responseText.trim()) {
        translations.push({ spanish: responseText.trim(), english: "" });
    }
    return translations;
  }, []);

  useEffect(() => {
    if (isLoadingHistoryRef.current) return;
    const pairId = settingsRef.current.selectedLanguagePairId;
    if (!pairId) return;

    const arr = messagesRef.current;
    if (!arr || arr.length === 0) return;

  const isEligible = (m: ChatMessage) => isRealChatMessage(m);
    const eligibleIndices: number[] = [];
    for (let i = 0; i < arr.length; i++) {
      if (isEligible(arr[i])) eligibleIndices.push(i);
    }
  const maxVisible = (settingsRef.current.maxVisibleMessages ?? MAX_VISIBLE_MESSAGES_DEFAULT) + 2; 
  if (eligibleIndices.length <= maxVisible) return;

    const bmId = settingsRef.current.historyBookmarkMessageId;
    let bmIndex = -1;
    if (bmId) {
      bmIndex = arr.findIndex(m => m.id === bmId);
      if (bmIndex === -1) bmIndex = -1; 
    }

    const startForCount = bmIndex >= 0 ? bmIndex : 0;
    let currentVisibleIgnoringCtx = 0;
    for (let i = startForCount; i < arr.length; i++) {
      if (isEligible(arr[i])) currentVisibleIgnoringCtx++;
    }
  if (currentVisibleIgnoringCtx <= maxVisible) return;

  const cutoffPosInEligible = Math.max(0, eligibleIndices.length - maxVisible);
    const firstEligibleIdx = eligibleIndices[cutoffPosInEligible];

    let desiredBookmarkIdx = -1;
    for (let i = firstEligibleIdx; i < arr.length; i++) {
      if (arr[i].role === 'assistant' && !arr[i].thinking) { desiredBookmarkIdx = i; break; }
    }
    if (desiredBookmarkIdx === -1) {
      return;
    }

    const desiredBookmarkId = arr[desiredBookmarkIdx].id;
    if ((settingsRef.current.historyBookmarkMessageId || null) === (desiredBookmarkId || null)) return;

    setSettings(prev => {
      const next = { ...prev, historyBookmarkMessageId: desiredBookmarkId } as typeof prev;
      settingsRef.current = next;
      setAppSettingsDB(next).catch(() => {});
      return next;
    });
    (async () => { try { await setChatMetaDB(pairId, { bookmarkMessageId: desiredBookmarkId }); } catch {} })();
  }, [messages, settings.selectedLanguagePairId]);

  const trimHistoryByBookmark = useCallback((arr: ChatMessage[]): ChatMessage[] => {
    const s = settingsRef.current;
    const bm = s.historyBookmarkMessageId;
    if (!bm) return arr;
    if (!messagesRef.current.some(m => m.id === bm)) return arr;
    const idx = arr.findIndex(m => m.id === bm && !m.thinking);
    if (idx === -1) return arr;
    return arr.slice(idx + 1);
  }, []);

  const getHistoryRespectingBookmark = useCallback((arr: ChatMessage[]): ChatMessage[] => {
    if (settingsRef.current.historyBookmarkMessageId) {
      return trimHistoryByBookmark(arr);
    }
    return arr;
  }, [trimHistoryByBookmark]);


  const addMessage = useCallback((message: Omit<ChatMessage, 'id' | 'timestamp'>): string => {
    const newMessage = { ...message, id: crypto.randomUUID(), timestamp: Date.now() };
    setMessages(prevMessages => [...prevMessages, newMessage]);
    if (message.role === 'user' && message.text && message.text.length >= 2) {
      // handleUserInputActivity called inside useSmartReengagement now
    }
    return newMessage.id;
  }, []);

  useEffect(() => {
    (async () => {
      try {
        const fromDb = await getAppSettingsDB();
        let effective = fromDb || loadFromLocalStorage(LOCAL_STORAGE_SETTINGS_KEY, initialSettings);
        if (!effective.selectedLanguagePairId || !allGeneratedLanguagePairs.some(p => p.id === effective.selectedLanguagePairId)) {
          effective = { ...effective, selectedLanguagePairId: null };
          const browserLangCode = (typeof navigator !== 'undefined' && navigator.language || 'en').substring(0, 2);
          const defaultNative = ALL_LANGUAGES.find(l => l.langCode === browserLangCode) || ALL_LANGUAGES.find(l => l.langCode === DEFAULT_NATIVE_LANG_CODE)!;
          
          setTempNativeLangCode(defaultNative.langCode);
          setTempTargetLangCode(null);
          setIsLanguageSelectionOpen(true);
          
          setIsLoadingHistory(false);
        }
  setSettings(effective);
  settingsRef.current = effective;
        try { await setAppSettingsDB(effective); } catch {}
      } catch (e) {
        setSettings(initialSettings);
      }
    })();
  }, []);

  useEffect(() => {
    const loadHistoryForPair = async (pairId: string) => {
        setIsLoadingHistory(true);
        try {
            let history = await getChatHistoryDB(pairId);
            if (!history || history.length === 0) {
              const backup = readBackupForPair(pairId);
              if (backup && backup.length > 0) {
                history = backup;
                safeSaveChatHistoryDB(pairId, backup);
              }
            }
            const cleanedHistory = (history || []).map(msg => {
                if (msg.isGeneratingImage || msg.thinking) {
                    const newMsg = { ...msg };
                    if (newMsg.isGeneratingImage) {
                        newMsg.isGeneratingImage = false;
                        newMsg.imageGenError = t('chat.error.imageGenInterrupted');
                    }
                    if (newMsg.thinking) {
                        newMsg.thinking = false;
                        if (!newMsg.text && !newMsg.translations?.length && !newMsg.rawAssistantResponse) {
                            newMsg.role = 'error';
                            newMsg.text = t('chat.error.thinkingInterrupted');
                        }
                    }
                    return newMsg;
                }
                return msg;
            });
            
            const wasCleaned = (history || []).length > 0 && JSON.stringify(history) !== JSON.stringify(cleanedHistory);
            setMessages(cleanedHistory);
            if (wasCleaned) await safeSaveChatHistoryDB(pairId, cleanedHistory);
            try {
              const meta = await getChatMetaDB(pairId);
              if (meta && (meta.bookmarkMessageId)) {
                setSettings(prev => {
                  const next = {
                    ...prev,
                    historyBookmarkMessageId: meta.bookmarkMessageId ?? null,
                  } as typeof prev;
                  settingsRef.current = next;
                  setAppSettingsDB(next).catch(() => {});
                  return next;
                });
              } else {
                setSettings(prev => {
                  const next = { ...prev, historyBookmarkMessageId: null } as typeof prev;
                  settingsRef.current = next;
                  setAppSettingsDB(next).catch(() => {});
                  return next;
                });
              }
            } catch (e) {
            }
        } catch (error) {
            console.error("Failed to load history from IndexedDB", error);
            setMessages([]);
        } finally {
            setIsLoadingHistory(false);
        }
    };
  if (settings.selectedLanguagePairId) {
    setMessages([]);
    loadHistoryForPair(settings.selectedLanguagePairId);
  }
  }, [settings.selectedLanguagePairId, t]);

  useEffect(() => {
    if (!isLoadingHistory && settings.selectedLanguagePairId) {
        safeSaveChatHistoryDB(settings.selectedLanguagePairId, messages);
    }
  }, [messages, settings.selectedLanguagePairId, isLoadingHistory]);

  const handleReengagementThresholdChange = useCallback((newThreshold: number) => {
    handleSettingsChange('smartReengagement', {
      ...settingsRef.current.smartReengagement,
      thresholdSeconds: newThreshold,
    });
  }, [handleSettingsChange]);

  const onUiTaskStart = useCallback((token?: string) => {
    const actualToken = token && typeof token === 'string' ? token : crypto.randomUUID();
    addUiBusyToken(actualToken);
    return actualToken;
  }, [addUiBusyToken]);

  const onUiTaskEnd = useCallback((token?: string) => {
    if (token) {
      removeUiBusyToken(token);
    } else {
      clearUiBusyTokens();
    }
  }, [removeUiBusyToken, clearUiBusyTokens]);

  const fetchAndSetReplySuggestions = useCallback(async (assistantMessageId: string, lastTutorMessage: string, history: ChatMessage[]) => {
  if (isLoadingSuggestionsRef.current) {
    setReplySuggestions([]);
    setIsLoadingSuggestions(false);
    return;
  }
    if (!lastTutorMessage.trim() || !selectedLanguagePairRef.current) {
        setReplySuggestions([]);
        return;
    }

    {
      const allMsgs = messagesRef.current;
      const targetIdx = allMsgs.findIndex(m => m.id === assistantMessageId);
      if (targetIdx !== -1) {
        const target = allMsgs[targetIdx];
        if (target && Array.isArray((target as any).replySuggestions) && (target as any).replySuggestions.length > 0) {
          setReplySuggestions((target as any).replySuggestions as ReplySuggestion[]);
          setIsLoadingSuggestions(false);
          return;
        }
      }
    }

    setIsLoadingSuggestions(true);
    setReplySuggestions([]);

  const historyForPrompt = getHistoryRespectingBookmark(history)
        .filter(msg => msg.role === 'user' || msg.role === 'assistant')
        .slice(-6)
        .map(msg => {
            if (msg.role === 'user') {
                return `User: ${msg.text || '(sent an image)'}`;
            }
            return `Tutor: ${msg.translations?.[0]?.spanish || msg.rawAssistantResponse || msg.text || '(sent an image)'}`;
        })
        .join('\n');

  const allMsgs = messagesRef.current;
  let previousChatSummary = '';
  {
    const idx = allMsgs.findIndex(m => m.id === assistantMessageId);
    const searchEnd = idx === -1 ? allMsgs.length - 1 : idx - 1;
    for (let i = searchEnd; i >= 0; i--) {
      const m = allMsgs[i];
      if (m.role === 'assistant' && typeof m.chatSummary === 'string' && m.chatSummary.trim()) {
        previousChatSummary = m.chatSummary.trim();
        break;
      }
    }
  }

  let suggestionPrompt = currentReplySuggestionsPromptText
    .replace("{tutor_message_placeholder}", lastTutorMessage)
    .replace("{conversation_history_placeholder}", historyForPrompt || "No history yet.")
    .replace("{previous_chat_summary_placeholder}", previousChatSummary || "");


    const MAX_RETRIES = 2;
    let success = false;

    for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
        try {
      const response = await generateGeminiResponse(
        AUX_TEXT_MODEL_ID,
                suggestionPrompt,
                [],
                undefined,
                undefined,
                undefined,
                undefined,
                false,
                { responseMimeType: "application/json" }
            );

            let jsonStr = response.text.trim();
            const fenceRegex = /^```(\w*)?\s*\n?(.*?)\n?\s*```$/s;
            const fenceMatch = jsonStr.match(fenceRegex);

            if (fenceMatch && fenceMatch[2]) {
                jsonStr = fenceMatch[2].trim();
            } else {
                const firstBrace = jsonStr.indexOf('{');
                const lastBrace = jsonStr.lastIndexOf('}');
                if (firstBrace !== -1 && lastBrace !== -1) {
                    jsonStr = jsonStr.substring(firstBrace, lastBrace + 1);
                }
            }
            
            const parsedResponse = JSON.parse(jsonStr);

      if (Array.isArray(parsedResponse.suggestions) &&
        parsedResponse.suggestions.every(s => typeof s === 'object' && s !== null && 'target' in s && 'native' in s && typeof s.target === 'string' && typeof s.native === 'string')) {
        const suggestions = parsedResponse.suggestions as ReplySuggestion[];
        setReplySuggestions(suggestions);
        updateMessage(assistantMessageId, { replySuggestions: suggestions });
        try { const pid = settingsRef.current.selectedLanguagePairId; if (pid) { await safeSaveChatHistoryDB(pid, messagesRef.current); } } catch {}
      } else {
                console.warn("Parsed suggestions not in expected format:", parsedResponse.suggestions);
                setReplySuggestions([]);
            }
            
            if (typeof parsedResponse.reengagementSeconds === 'number' && parsedResponse.reengagementSeconds >= 5) {
                 handleReengagementThresholdChange(parsedResponse.reengagementSeconds);
            }

            try {
              const newChatSummary = typeof (parsedResponse as any).chatSummary === 'string' ? (parsedResponse as any).chatSummary.trim() : '';
              if (newChatSummary) {
                updateMessage(assistantMessageId, { chatSummary: newChatSummary });
                const existing = (await getGlobalProfileDB())?.text || '';
                const mergePrompt = `You are consolidating a language learner's global profile for tutoring. Merge the existing profile and the new input into one concise profile. Deduplicate, keep what's durable, prefer newer details, avoid PII. No headings or categories. Output only the merged profile text, max 1200 characters.\n\nExisting Profile:\n${existing || '(none)'}\n\nNew Input (chat summary):\n${newChatSummary}`;
                const mergeRes = await generateGeminiResponse(
                  AUX_TEXT_MODEL_ID,
                  mergePrompt,
                  [],
                  undefined,
                  undefined,
                  undefined,
                  undefined,
                  false,
                  { responseMimeType: 'text/plain', temperature: 0.1 }
                );
                const merged = (mergeRes.text || '').trim().slice(0, 10000);
                if (merged) await setGlobalProfileDB(merged);
              }
            } catch (e) {
              console.warn('Failed to update global profile from chat summary:', e);
            }

            success = true;
            break;

        } catch (error) {
            console.error(`Error fetching reply suggestions (attempt ${attempt + 1}/${MAX_RETRIES + 1}):`, error);
            if (attempt < MAX_RETRIES) {
                await new Promise(resolve => setTimeout(resolve, 500 * (attempt + 1)));
            } else {
                setReplySuggestions([]);
            }
        }
    }

    setIsLoadingSuggestions(false);
  }, [currentReplySuggestionsPromptText, handleReengagementThresholdChange]);

  const {
    isSpeaking, speak, stopSpeaking, isSpeechSynthesisSupported,
    isListening, transcript, startListening, stopListening, sttError, isSpeechRecognitionSupported, clearTranscript,
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
      }, [])
      ,
        getTtsProvider: useCallback(() => settingsRef.current.tts?.provider || 'browser', []),
        getSttProvider: useCallback(() => settingsRef.current.stt?.provider || 'browser', []),
        onRecordedUtteranceReady: useCallback((utterance: RecordedUtterance) => {
          if (!utterance || typeof utterance.dataUrl !== 'string' || utterance.dataUrl.length === 0 || utterance.dataUrl.length > INLINE_CAP_AUDIO) {
            recordedUtterancePendingRef.current = null;
            pendingRecordedAudioMessageRef.current = null;
            return;
          }
          recordedUtterancePendingRef.current = utterance;
          const pendingId = pendingRecordedAudioMessageRef.current;
          if (pendingId) {
            pendingRecordedAudioMessageRef.current = null;
            setMessages((prev) => prev.map((m) => (m.id === pendingId ? { ...m, recordedUtterance: utterance } : m)));
            recordedUtterancePendingRef.current = null;
          }
        }, [setMessages])
    });

  const restoreSttAfterLiveSession = useCallback(() => {
    if (liveSessionShouldRestoreSttRef.current) {
      const lang = settingsRef.current.stt.language;
      setSettings(prev => {
        const next = { ...prev, stt: { ...prev.stt, enabled: true } };
        settingsRef.current = next;
        return next;
      });
      liveSessionShouldRestoreSttRef.current = false;
      setTimeout(() => {
          if (settingsRef.current.stt.enabled) {
              startListening(lang);
          }
      }, 100);
    }
  }, [startListening]);

  useEffect(() => {
    if (!selectedLanguagePair) return;
    setCurrentSystemPromptText(selectedLanguagePair.baseSystemPrompt);
    setCurrentReplySuggestionsPromptText(selectedLanguagePair.baseReplySuggestionsPrompt);
  }, [selectedLanguagePair]);

  useEffect(() => {
    document.title = t(APP_TITLE_KEY);
  }, [t]);

  useEffect(() => {
    const splashScreen = document.getElementById('splash-screen');
    if (splashScreen) {
        setTimeout(() => {
            splashScreen.classList.add('fade-out');
            splashScreen.addEventListener('transitionend', () => {
                splashScreen.remove();
            });
        }, 200);
    }
  }, []);

  const STT_STABLE_NO_TEXT_MS = 4000; 
  
  const stripBracketedContent = useCallback((input: string | undefined | null): string => {
    if (typeof input !== 'string') return '';
    const without = input.replace(/\[[^\]]*\]/g, ' ');
    return without.replace(/\s+/g, ' ').trim();
  }, []);
  
  
  const handleDeleteMessage = useCallback((messageId: string) => {
    setMessages(prev => prev.filter(m => m.id !== messageId));
  }, []);

  const updateMessage = (messageId: string, updates: Partial<ChatMessage>) => {
    setMessages(prev => prev.map(m => m.id === messageId ? { ...m, ...updates, timestamp: Date.now() } : m));
  };

  const upsertMessageTtsCache = useCallback((messageId: string, entry: TtsAudioCacheEntry) => {
    if (!entry || typeof entry.audioDataUrl !== 'string' || entry.audioDataUrl.length === 0 || entry.audioDataUrl.length > INLINE_CAP_AUDIO) {
      return;
    }
    setMessages(prev => prev.map(m => {
      if (m.id !== messageId) return m;
      const nextCache = upsertTtsCacheEntries(m.ttsAudioCache, entry);
      return { ...m, ttsAudioCache: nextCache };
    }));
  }, []);

  const upsertSuggestionTtsCache = useCallback((messageId: string, suggestionIndex: number, entry: TtsAudioCacheEntry) => {
    if (!entry || typeof entry.audioDataUrl !== 'string' || entry.audioDataUrl.length === 0 || entry.audioDataUrl.length > INLINE_CAP_AUDIO) {
      return;
    }
    setMessages(prev => prev.map(m => {
      if (m.id !== messageId || !Array.isArray(m.replySuggestions)) return m;
      const nextSuggestions = m.replySuggestions.map((suggestion, idx) => {
        if (idx !== suggestionIndex) return suggestion;
        const nextCache = upsertTtsCacheEntries(suggestion.ttsAudioCache, entry);
        return { ...suggestion, ttsAudioCache: nextCache };
      });
      return { ...m, replySuggestions: nextSuggestions };
    }));

    if (lastFetchedSuggestionsForRef.current === messageId) {
      setReplySuggestions(prev => prev.map((suggestion, idx) => {
        if (idx !== suggestionIndex) return suggestion;
        const nextCache = upsertTtsCacheEntries(suggestion.ttsAudioCache, entry);
        return { ...suggestion, ttsAudioCache: nextCache };
      }));
    }
  }, [setReplySuggestions]);

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
      } else if (!cacheKey && context && context.source === 'suggestion' && context.messageId && typeof context.suggestionIndex === 'number') {
        const suggestionIndex = context.suggestionIndex;
        if (suggestionIndex >= 0) {
          cacheKey = computeTtsCacheKey(cleanedText, lang, provider, voiceName);
          const message = messagesRef.current.find(m => m.id === context.messageId);
          cachedAudio = cachedAudio || getCachedAudioForKey(message?.replySuggestions?.[suggestionIndex]?.ttsAudioCache, cacheKey);
          if (!cachedAudio && lastFetchedSuggestionsForRef.current === context.messageId) {
            const localSuggestion = replySuggestions[suggestionIndex];
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
  }, [replySuggestions, upsertMessageTtsCache, upsertSuggestionTtsCache]);

  const resolveBookmarkContextSummary = useCallback((): string | null => {
    const bm = settingsRef.current.historyBookmarkMessageId;
  if (!bm) return null;
    const full = messagesRef.current;
    const bmIndex = full.findIndex(m => m.id === bm);
    if (bmIndex === -1) return null;
    let summary: string | undefined = (full[bmIndex] && typeof full[bmIndex].chatSummary === 'string')
      ? full[bmIndex].chatSummary!.trim()
      : undefined;
    if (!summary) {
      for (let i = bmIndex; i >= 0; i--) {
        const m = full[i];
        if (m.role === 'assistant' && typeof m.chatSummary === 'string' && m.chatSummary.trim()) { summary = m.chatSummary.trim(); break; }
      }
    }
    if (!summary || !summary.trim()) return null;
    return summary.trim();
  }, []);

  const fetchAvailableCameras = useCallback(async () => {
    try {
      if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
        try {
            // Requesting stream triggers permission prompt if not granted
            const tempStream = await navigator.mediaDevices.getUserMedia({ video: true });
            tempStream.getTracks().forEach(track => track.stop());
        } catch (permError) {
            console.warn("Could not get temporary video stream for robust device enumeration:", permError);
        }
      }

      if (navigator.mediaDevices && navigator.mediaDevices.enumerateDevices) {
        const devices = await navigator.mediaDevices.enumerateDevices();
        const videoDevices = devices.filter(device => device.kind === 'videoinput');
        const cameraList: CameraDevice[] = videoDevices.map((device, index) => ({
            deviceId: device.deviceId,
            label: device.label || `Camera ${index + 1}`,
            facingMode: getFacingModeFromLabel(device.label)
        }));
        setAvailableCameras(cameraList);
      }
    } catch (error) {
      console.error("Error enumerating video devices:", error);
      setAvailableCameras([]);
    }
  }, []);

  useEffect(() => {
    fetchAvailableCameras();
    if (navigator.mediaDevices) {
        navigator.mediaDevices.addEventListener('devicechange', fetchAvailableCameras);
    }
    return () => {
        if (navigator.mediaDevices) {
            navigator.mediaDevices.removeEventListener('devicechange', fetchAvailableCameras);
        }
    };
  }, [fetchAvailableCameras]);
  
  // Re-fetch cameras when enabling features that use the camera to ensure list is fresh and permitted
  useEffect(() => {
    if (settings.sendWithSnapshotEnabled || settings.smartReengagement.useVisualContext) {
        fetchAvailableCameras();
    }
  }, [settings.sendWithSnapshotEnabled, settings.smartReengagement.useVisualContext, fetchAvailableCameras]);

  const captureSnapshot = useCallback(async (isForReengagement = false): Promise<{ base64: string; mimeType: string; llmBase64: string; llmMimeType: string } | null> => {
    const errorSetter = isForReengagement ? setVisualContextCameraError : setSnapshotUserError;
    errorSetter(null);

    const videoElement = visualContextVideoRef.current;
    if (!videoElement) {
        errorSetter(isForReengagement ? t('error.visualContextVideoElementNotReady') : t('error.snapshotVideoElementNotReady'));
        return null;
    }

    const currentSettings = settingsRef.current;
    let streamForCapture: MediaStream | null = null;
    let streamWasTemporarilyStarted = false;

    try {
        if (currentSettings.smartReengagement.useVisualContext &&
            visualContextStreamRef.current &&
            visualContextStreamRef.current.active &&
            videoElement.srcObject === visualContextStreamRef.current &&
            videoElement.readyState >= HTMLMediaElement.HAVE_ENOUGH_DATA &&
            videoElement.videoWidth > 0 && videoElement.videoHeight > 0) {
            streamForCapture = visualContextStreamRef.current;
        } else {
            if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                 errorSetter(isForReengagement ? t('error.visualContextCameraAccessNotSupported') : t('error.snapshotCameraAccessNotSupported'));
                 return null;
            }
            const videoConstraints: MediaStreamConstraints['video'] = currentSettings.selectedCameraId
                ? { deviceId: { exact: currentSettings.selectedCameraId } }
                : true;

            streamForCapture = await navigator.mediaDevices.getUserMedia({ video: videoConstraints });
            streamWasTemporarilyStarted = true;
            videoElement.srcObject = streamForCapture;
            videoElement.muted = true;
            videoElement.playsInline = true;
            await videoElement.play();

            await new Promise((resolve, reject) => {
                const timeoutErrorKey = isForReengagement ? "error.visualContextTimeout" : "error.snapshotTimeout";
                const dimensionErrorKey = isForReengagement ? "error.visualContextVideoDimensionsZero" : "error.snapshotVideoDimensionsZero";
                const videoErrorKey = isForReengagement ? "error.visualContextVideoError" : "error.snapshotVideoError";

                const timeout = setTimeout(() => reject(new Error(t(timeoutErrorKey))), 3000);
                const onLoadedData = () => {
                    clearTimeout(timeout);
                    if (videoElement.videoWidth > 0 && videoElement.videoHeight > 0) {
                        resolve(undefined);
                    } else {
                        reject(new Error(t(dimensionErrorKey)));
                    }
                };
                videoElement.onloadeddata = onLoadedData;
                videoElement.onerror = () => {
                    clearTimeout(timeout);
                    reject(new Error(t(videoErrorKey)));
                };
                if (videoElement.readyState >= HTMLMediaElement.HAVE_ENOUGH_DATA && videoElement.videoWidth > 0 && videoElement.videoHeight > 0) {
                     clearTimeout(timeout);
                     resolve(undefined);
                }
            });
        }

        const canvas = document.createElement('canvas');
        canvas.width = videoElement.videoWidth;
        canvas.height = videoElement.videoHeight;
        const context = canvas.getContext('2d');
        if (!context) throw new Error(isForReengagement ? t("error.visualContext2DContext") : t("error.snapshot2DContext"));

        context.drawImage(videoElement, 0, 0, canvas.width, canvas.height);
  const imageBase64 = canvas.toDataURL('image/jpeg', 0.9);
  return { base64: imageBase64, mimeType: 'image/jpeg', llmBase64: imageBase64, llmMimeType: 'image/jpeg' };

    } catch (err) {
        console.error(`Error capturing image (${isForReengagement ? 're-engagement' : 'snapshot'}):`, err);
        const message = err instanceof Error ? err.message : t("error.imageCaptureGeneric");
        const prefixKey = isForReengagement ? "error.visualContextCaptureFailed" : "error.snapshotCaptureFailed";

        if (message.includes("Permission") || message.includes("NotAllowedError")) {
             errorSetter(t(`${prefixKey}Permission`));
        } else if (message.includes("NotFoundError") || message.includes("DevicesNotFoundError")) {
             errorSetter(t(`${prefixKey}NotFound`));
        } else if (message.includes("Timeout") || message.includes("dimensions zero") || message.includes("Video element error")) {
             errorSetter(t(`${prefixKey}NotReady`, {details: message}));
        } else {
            errorSetter(t(`${prefixKey}Generic`, {details: message}));
        }
        return null;
    } finally {
        if (streamForCapture && streamWasTemporarilyStarted) {
            streamForCapture.getTracks().forEach(track => track.stop());
            if (videoElement.srcObject === streamForCapture && !(settingsRef.current.smartReengagement.useVisualContext && visualContextStreamRef.current === streamForCapture)) {
                videoElement.srcObject = null;
                videoElement.load();
            }
        }
    }
  }, [t]); 
  
  const triggerReengagementSequence = useCallback(async () => {
    // cancelReengagement() implicitly via setPhase
    if (isLoadingHistoryRef.current || isSendingRef.current || speechIsSpeakingRef.current || isCurrentlyPerformingVisualContextCaptureRef.current) {
      return;
    }

    setReplySuggestions([]);
    setIsLoadingSuggestions(false);
    lastFetchedSuggestionsForRef.current = null;

    let visualReengagementShown = false;
    const currentReengageSettings = settingsRef.current.smartReengagement;
    if (currentReengageSettings.useVisualContext && visualContextStreamRef.current && visualContextStreamRef.current.active) {
      isCurrentlyPerformingVisualContextCaptureRef.current = true;
      try {
        const imageResult = await captureSnapshot(true);
        if (imageResult) {
          visualReengagementShown = await _handleSendMessageInternalRef.current(
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

    if (!visualReengagementShown) {
      await _handleSendMessageInternalRef.current('', undefined, undefined, 'conversational-reengagement');
    }

  }, [captureSnapshot]);

  const {
    reengagementPhase,
    scheduleReengagement,
    cancelReengagement,
    isReengagementToken,
    handleUserActivity,
    setReengagementPhase
  } = useSmartReengagement({
    settings,
    isLoadingHistory,
    selectedLanguagePairId: settings.selectedLanguagePairId,
    isSending,
    isSpeaking,
    isVisualContextActive: isCurrentlyPerformingVisualContextCaptureRef.current,
    externalUiTaskCount,
    triggerReengagementSequence,
    addUiBusyToken,
    removeUiBusyToken
  });

  const handleUserInputActivity = useCallback(() => {
    setIsUserActive(true);
    isUserActiveRef.current = true;
    if (autoSendTimerRef.current) {
      clearTimeout(autoSendTimerRef.current);
      autoSendTimerRef.current = null;
      autoSendSnapshotRef.current = '';
    }
    if (userActivityTimerRef.current) clearTimeout(userActivityTimerRef.current);
    userActivityTimerRef.current = window.setTimeout(() => {
        setIsUserActive(false);
        isUserActiveRef.current = false;
    }, 3000); 

    setMaestroActivityStage('listening');

    handleUserActivity();
  }, [handleUserActivity]);

  useEffect(() => {
    speechIsSpeakingRef.current = isSpeaking;
  }, [isSpeaking]);

  useEffect(() => {
    if (wasSpeaking && !isSpeaking) {
      if (isLoadingSuggestionsRef.current) {
        return;
      }
      const lastMessage = messagesRef.current.length > 0 ? messagesRef.current[messagesRef.current.length - 1] : null;

      if (lastMessage && lastMessage.role === 'assistant' && !lastMessage.thinking && lastMessage.id !== lastFetchedSuggestionsForRef.current) {
        const textForSuggestions = lastMessage.rawAssistantResponse || (lastMessage.translations?.find(t => t.spanish)?.spanish) || "";
        if (textForSuggestions.trim()) {
          fetchAndSetReplySuggestions(lastMessage.id, textForSuggestions, getHistoryRespectingBookmark(messagesRef.current));
          lastFetchedSuggestionsForRef.current = lastMessage.id;
        }
      }
    }
    setWasSpeaking(isSpeaking);
  }, [isSpeaking, wasSpeaking, fetchAndSetReplySuggestions]);

  // computeMaxMessagesForArray used by deriveHistory
  const computeMaxMessagesForArray = useCallback((arr: ChatMessage[]): number | undefined => {
    const s = settingsRef.current;
  const realArr = arr.filter(isRealChatMessage);
  if (s.historyBookmarkMessageId) {
      const idx = realArr.findIndex(m => m.id === s.historyBookmarkMessageId);
      if (idx !== -1) {
        return Math.max(0, realArr.length - (idx + 1));
      }
    }
    return undefined;
  }, []);

  const computeHistorySubsetForMedia = useCallback((arr: ChatMessage[]): ChatMessage[] => {
  let base = getHistoryRespectingBookmark(arr).filter(isRealChatMessage);
    const max = computeMaxMessagesForArray(base);
    if (typeof max === 'number') {
      base = base.slice(-Math.max(0, max));
    }
    return base;
  }, [getHistoryRespectingBookmark, computeMaxMessagesForArray]);

  const ensureUrisForHistoryForSend = useCallback(async (arr: ChatMessage[], onProgress?: (done: number, total: number, etaMs?: number) => void): Promise<Record<string, { oldUri?: string; newUri: string }>> => {
    const now = Date.now();
    const keepK = Number.POSITIVE_INFINITY;

    let candidates = computeHistorySubsetForMedia(arr);
    if (Number.isFinite(keepK)) {
      const start = Math.max(0, candidates.length - (keepK as number));
      candidates = candidates.slice(start);
    }

    const mediaIndices: number[] = [];
    for (let i = 0; i < candidates.length; i++) {
      const m = candidates[i];
      const hasMedia = !!(m.llmImageUrl && m.llmImageMimeType) || !!(m.imageUrl && m.imageMimeType) || !!(m.llmFileUri && m.llmFileMimeType);
      if (hasMedia) mediaIndices.push(i);
    }
  const maxMedia = MAX_MEDIA_TO_KEEP;
    const keepMediaIdx = new Set<number>(mediaIndices.slice(-maxMedia));

    const cachedUrisToCheck: string[] = [];
    for (let i = 0; i < candidates.length; i++) {
      if (!keepMediaIdx.has(i)) continue;
      const m0 = candidates[i];
      const cachedUri0 = (m0 as any).llmFileUri as string | undefined;
      const cachedMime0 = (m0 as any).llmFileMimeType as string | undefined;
      if (cachedUri0 && cachedMime0) cachedUrisToCheck.push(cachedUri0);
    }
    let cachedStatuses: Record<string, { deleted: boolean }> = {};
    try {
      const uniqUris = Array.from(new Set(cachedUrisToCheck));
      if (uniqUris.length) cachedStatuses = await checkFileStatuses(uniqUris);
    } catch { cachedStatuses = {}; }

    let totalToEnsure = 0;
    for (let i = 0; i < candidates.length; i++) {
      if (!keepMediaIdx.has(i)) continue;
      const m0 = candidates[i];
      const llmUrl0 = (m0 as any).llmImageUrl as string | undefined;
      const llmMime0 = (m0 as any).llmImageMimeType as string | undefined;
      const uiUrl0 = m0.imageUrl as string | undefined;
      const uiMime0 = m0.imageMimeType as string | undefined;
      const cachedUri0 = (m0 as any).llmFileUri as string | undefined;
      const cachedMime0 = (m0 as any).llmFileMimeType as string | undefined;
      const hasAnyMedia0 = !!((llmUrl0 && llmMime0) || (uiUrl0 && uiMime0));
      const missing = !(cachedUri0 && cachedMime0);
      const deleted = !!(cachedUri0 && cachedStatuses[cachedUri0]?.deleted);
      if ((missing || deleted) && hasAnyMedia0) totalToEnsure++;
    }

    let doneCount = 0;
    const startTs = Date.now();
    const tick = () => {
      if (!onProgress) return;
      const elapsed = Date.now() - startTs;
      const avg = doneCount > 0 ? elapsed / doneCount : undefined;
      const remaining = Math.max(0, totalToEnsure - doneCount);
      const eta = avg !== undefined ? Math.round(avg * remaining) : undefined;
      onProgress(doneCount, totalToEnsure, eta);
    };

    const updatedUriMap: Record<string, { oldUri?: string; newUri: string }> = {};
    for (let idx = 0; idx < candidates.length; idx++) {
      if (!keepMediaIdx.has(idx)) continue; 
  const m = candidates[idx];
      const llmUrl = (m as any).llmImageUrl as string | undefined;
      const llmMime = (m as any).llmImageMimeType as string | undefined;
      const uiUrl = m.imageUrl as string | undefined;
      const uiMime = m.imageMimeType as string | undefined;
      if (!(llmUrl && llmMime) && !(uiUrl && uiMime)) continue;

    const cachedUri = (m as any).llmFileUri as string | undefined;
    const cachedMime = (m as any).llmFileMimeType as string | undefined;
    let cachedDeleted = false;
    if (cachedUri && cachedMime) {
      try {
        const st = cachedStatuses && cachedStatuses[cachedUri];
        cachedDeleted = !!(st && st.deleted);
      } catch { cachedDeleted = false; }
      if (!cachedDeleted) continue; 
    }

      let dataForUpload: { dataUrl: string; mimeType: string } | null = null;
      try {
        if (llmUrl && llmMime) {
          dataForUpload = { dataUrl: llmUrl, mimeType: llmMime };
        } else if (uiUrl && uiMime) {
          // Always optimize if needed for upload preparation
          const optimized = await processMediaForUpload(uiUrl, uiMime, { t });
          dataForUpload = { dataUrl: optimized.dataUrl, mimeType: optimized.mimeType };
          updateMessage(m.id, { llmImageUrl: optimized.dataUrl, llmImageMimeType: optimized.mimeType });
        }
        if (dataForUpload) {
          const up = await uploadMediaToFiles(dataForUpload.dataUrl, dataForUpload.mimeType, 'send-history');
          updateMessage(m.id, { llmFileUri: up.uri, llmFileMimeType: up.mimeType });
          (m as any).llmFileUri = up.uri;
          (m as any).llmFileMimeType = up.mimeType;
          updatedUriMap[m.id] = { oldUri: cachedUri, newUri: up.uri };
        }
      } catch (e) {
        console.warn('Pre-send URI ensure failed for message', m.id, e);
      }
      try { doneCount++; tick(); } catch {}
      await new Promise(r => setTimeout(r, 0));
    }
    return updatedUriMap;
  }, [computeHistorySubsetForMedia, updateMessage, getHistoryRespectingBookmark]);


  const [sendPrep, setSendPrep] = useState<{ active: boolean; label: string; done?: number; total?: number; etaMs?: number } | null>(null);
  useEffect(() => { sendPrepRef.current = sendPrep; }, [sendPrep]);

  const speakMessage = useCallback((message: ChatMessage) => {
    if (!selectedLanguagePair) return;

    if (message.role === 'assistant') {
      const partsForTTS: SpeechPart[] = [];
      const targetLang = getPrimaryCode(selectedLanguagePair.targetLanguageCode);
      const nativeLang = getPrimaryCode(selectedLanguagePair.nativeLanguageCode);
      let defaultLangForSpeakText = targetLang || 'es';

      if (message.translations && message.translations.length > 0) {
        if (settings.tts.speakNative) {
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
        if (mightBeNative && settings.tts.speakNative) {
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
        if (mightBeNative && settings.tts.speakNative) {
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
  }, [prepareSpeechPartsWithCache, speak, settings.tts.speakNative, selectedLanguagePair]);

  const { start: startLiveConversation, stop: stopLiveConversation } = useGeminiLiveConversation({
    onStateChange: (state) => {
      setLiveSessionState(state);
      if (state === 'connecting') {
        setLiveSessionError(null);
      }
      if (state === 'active') {
        if (!liveUiTokenRef.current) {
          const token = addUiBusyToken(`live-session:${Date.now()}:${Math.random().toString(36).slice(2, 8)}`);
          liveUiTokenRef.current = token;
        }
      } else {
        if (liveUiTokenRef.current) {
          removeUiBusyToken(liveUiTokenRef.current);
          liveUiTokenRef.current = null;
        }
      }
      if (state === 'idle') {
        restoreSttAfterLiveSession();
        releaseLiveSessionCapture();
      } else if (state === 'error') {
        restoreSttAfterLiveSession();
        releaseLiveSessionCapture();
      }
    },
    onError: (message) => {
      setLiveSessionError(message);
      restoreSttAfterLiveSession();
    },
  });

  const handleStartLiveSession = useCallback(async () => {
    if (liveSessionState === 'connecting' || liveSessionState === 'active') {
      return;
    }

    setLiveSessionError(null);

    let stream: MediaStream | null = liveVideoStream && liveVideoStream.active ? liveVideoStream : null;
    let createdStream = false;

    try {
      if (!stream || !stream.active) {
        const fallback = visualContextStreamRef.current;
        if (fallback && fallback.active) {
          stream = fallback;
        }
      }

      if (!stream || !stream.active) {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
          throw new Error(t('error.cameraAccessNotSupported'));
        }
        const videoConstraints: MediaStreamConstraints['video'] = settingsRef.current.selectedCameraId
          ? { deviceId: { exact: settingsRef.current.selectedCameraId } }
          : true;
        stream = await navigator.mediaDevices.getUserMedia({ video: videoConstraints });
        createdStream = true;
        setLiveVideoStream(stream);
      }

      if (!stream || !stream.active) {
        throw new Error(t('error.cameraStreamNotAvailable'));
      }

      liveSessionCaptureRef.current = { stream, created: createdStream };

      if (settingsRef.current.stt.enabled) {
        liveSessionShouldRestoreSttRef.current = true;
        setSettings(prev => {
          const next = {
            ...prev,
            stt: {
              ...prev.stt,
              enabled: false,
            },
          } as typeof prev;
          settingsRef.current = next;
          return next;
        });
        if (isListening) {
          stopListening();
        }
        clearTranscript();
      } else {
        liveSessionShouldRestoreSttRef.current = false;
      }

      handleUserInputActivity();
      cancelReengagement();

      await startLiveConversation({
        stream,
        videoElement: visualContextVideoRef.current,
        systemInstruction: currentSystemPromptText || undefined,
      });
    } catch (error) {
      releaseLiveSessionCapture();
      restoreSttAfterLiveSession();
      const message = error instanceof Error ? error.message : t('general.error');
      setLiveSessionError(message);
      throw error;
    }
  }, [cancelReengagement, clearTranscript, currentSystemPromptText, handleUserInputActivity, isListening, liveSessionState, liveVideoStream, releaseLiveSessionCapture, restoreSttAfterLiveSession, setLiveSessionError, setLiveVideoStream, setSettings, startLiveConversation, stopListening, t]);

  const handleStopLiveSession = useCallback(async () => {
    try {
      await stopLiveConversation();
    } catch (error) {
      console.warn('Failed to stop live session', error);
    } finally {
      releaseLiveSessionCapture();
      restoreSttAfterLiveSession();
      setLiveSessionError(null);
      scheduleReengagement('live-session-stopped');
    }
  }, [releaseLiveSessionCapture, restoreSttAfterLiveSession, scheduleReengagement, stopLiveConversation]);

  const _handleSendMessageInternal = useCallback(async (
    text: string,
    passedImageBase64?: string,
    passedImageMimeType?: string,
    messageType: 'user' | 'conversational-reengagement' | 'image-reengagement' = 'user'
  ): Promise<boolean> => {
  if (isLoadingHistoryRef.current) return false;
    const sanitizedText = stripBracketedContent(text);
  if (!sanitizedText && !passedImageBase64 && messageType === 'user') return false;
    if (!selectedLanguagePairRef.current) {
      console.error("No language pair selected, cannot send message.");
      addMessage({ role: 'error', text: t('error.noLanguagePair') });
      return false;
    }

    if (isSendingRef.current || speechIsSpeakingRef.current) {
        console.log("SendMessage blocked: AI is currently sending or speaking.");
        return false;
    }

  setIsSending(true);
  if (settingsRef.current.stt.enabled && isListening) {
    try { stopListening(); } catch { /* ignore */ }
    sttInterruptedBySendRef.current = true;
    clearTranscript();
  } else {
    sttInterruptedBySendRef.current = false;
  }
  sendWithFileUploadInProgressRef.current = true;
    setReplySuggestions([]);
    setIsLoadingSuggestions(false);
    lastFetchedSuggestionsForRef.current = null;

    if (messageType === 'user') setSnapshotUserError(null);
    pendingRecordedAudioMessageRef.current = null;

  let userMessageId: string | null = null;
  let userMessageText = sanitizedText;
  let recordedSpeechForMessage: RecordedUtterance | null = null;
  let userImageToProcessBase64: string | undefined = (typeof passedImageBase64 === 'string' && passedImageBase64) ? passedImageBase64 : undefined;
  let userImageToProcessMimeType: string | undefined = (typeof passedImageMimeType === 'string' && passedImageMimeType) ? passedImageMimeType : undefined;
  let userImageToProcessLlmBase64: string | undefined = undefined;
  let userImageToProcessLlmMimeType: string | undefined = undefined;
  const currentSettingsVal = settingsRef.current;
    
  const shouldGenerateUserImage = currentSettingsVal.selectedCameraId === IMAGE_GEN_CAMERA_ID;

  if (messageType === 'user') {
      for (let attempt = 0; attempt < 2; attempt++) {
        const claimed = typeof claimRecordedUtterance === 'function' ? claimRecordedUtterance() : null;
        if (claimed && typeof claimed.dataUrl === 'string' && claimed.dataUrl.length > 0) {
          recordedSpeechForMessage = claimed;
          recordedUtterancePendingRef.current = null;
          break;
        }
        if (recordedUtterancePendingRef.current && typeof recordedUtterancePendingRef.current.dataUrl === 'string' && recordedUtterancePendingRef.current.dataUrl.length > 0) {
          recordedSpeechForMessage = recordedUtterancePendingRef.current;
          recordedUtterancePendingRef.current = null;
          break;
        }
        if (attempt === 0) {
          await new Promise((resolve) => setTimeout(resolve, 60));
        }
      }
      if (recordedSpeechForMessage && recordedSpeechForMessage.dataUrl.length > INLINE_CAP_AUDIO) {
        recordedSpeechForMessage = null;
      }
  }

  if (currentSettingsVal.sendWithSnapshotEnabled && !userImageToProcessBase64 && messageType === 'user' && !shouldGenerateUserImage) {
      const snapshotResult = await captureSnapshot(false);
      if (snapshotResult) {
        userImageToProcessBase64 = snapshotResult.base64;
        userImageToProcessMimeType = snapshotResult.mimeType;
        // Also set LLM variant immediately from snapshot (which is already resized/compressed usually)
        userImageToProcessLlmBase64 = snapshotResult.llmBase64;
        userImageToProcessLlmMimeType = snapshotResult.llmMimeType;
      }
    }

  if (messageType === 'user') {
      const keyframeSrcBase64 = (typeof passedImageBase64 === 'string' && passedImageBase64) ? passedImageBase64 : attachedImageBase64;
      const keyframeSrcMime = (typeof passedImageMimeType === 'string' && passedImageMimeType) ? passedImageMimeType : attachedImageMimeType;
      if (keyframeSrcBase64 && keyframeSrcMime && keyframeSrcMime.startsWith('video/')) {
        try {
          const kf = await createKeyframeFromVideoDataUrl(keyframeSrcBase64, { at: 'start', maxDim: 768, quality: 0.75, outputMime: 'image/jpeg' });
          const kfId = addMessage({ role: 'user', text: userMessageText, imageUrl: kf.dataUrl, imageMimeType: kf.mimeType });
          try {
            if (!sendWithFileUploadInProgressRef.current) {
              sendWithFileUploadInProgressRef.current = true;
            }
            updateMessage(kfId, { llmImageUrl: kf.dataUrl, llmImageMimeType: kf.mimeType });
            const up = await uploadMediaToFiles(kf.dataUrl, kf.mimeType, 'keyframe-image');
            updateMessage(kfId, { llmFileUri: up.uri, llmFileMimeType: up.mimeType });
          } catch (e) {
          }
          userMessageText = '';
          userImageToProcessBase64 = keyframeSrcBase64;
          userImageToProcessMimeType = keyframeSrcMime;
        } catch (e) {
          console.warn('Failed to create keyframe from video; proceeding without keyframe image message', e);
        }
      } 
      if (!userImageToProcessLlmBase64 && attachedImageBase64 && attachedImageMimeType) {
        try {
          if (!sendWithFileUploadInProgressRef.current) {
            sendWithFileUploadInProgressRef.current = true;
          }
          // Always generate low res for persistence
          const optimized = await processMediaForUpload(attachedImageBase64, attachedImageMimeType, { t });
          userImageToProcessLlmBase64 = optimized.dataUrl;
          userImageToProcessLlmMimeType = optimized.mimeType;
          
          if (userMessageId) updateMessage(userMessageId, { llmImageUrl: optimized.dataUrl, llmImageMimeType: optimized.mimeType });
        } catch {}
      }

      // Logic for selecting display vs persistence image
      let displayUrl = userImageToProcessBase64;
      let displayMime = userImageToProcessMimeType;
      let persistenceUrl = userImageToProcessLlmBase64;
      let persistenceMime = userImageToProcessLlmMimeType;

      userMessageId = addMessage({
        role: 'user',
        text: userMessageText,
        recordedUtterance: recordedSpeechForMessage || undefined,
        imageUrl: displayUrl,
        imageMimeType: displayMime,
        llmImageUrl: persistenceUrl,
        llmImageMimeType: persistenceMime,
      });
    }

    const thinkingMessageId = addMessage({ role: 'assistant', thinking: true });

  cancelReengagement();

  let historyForGemini = messagesRef.current.filter(m => m.id !== thinkingMessageId);
    if (messageType === 'user' && userMessageId) {
      historyForGemini = historyForGemini.filter(m => m.id !== userMessageId);
    }
  const historyForGeminiWithUris = historyForGemini;

    let geminiPromptText: string;
  let systemInstructionForGemini: string = currentSystemPromptText;
    try {
      await getGlobalProfileDB();
    } finally {
      systemInstructionForGemini = composeMaestroSystemInstruction(systemInstructionForGemini);
    }
    if (messageType === 'user' && userImageToProcessBase64 && !userImageToProcessLlmBase64 && userImageToProcessMimeType) {
      if (!sendWithFileUploadInProgressRef.current) {
        sendWithFileUploadInProgressRef.current = true;
      }
      try {
        setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media…' });
        // Always optimize for persistence version
        const optimized = await processMediaForUpload(userImageToProcessBase64, userImageToProcessMimeType, { 
            t, 
            onProgress: (label, done, total, etaMs) => setSendPrep({ active: true, label, done, total, etaMs }) 
        });
        userImageToProcessLlmBase64 = optimized.dataUrl;
        userImageToProcessLlmMimeType = optimized.mimeType;

        if (messageType === 'user' && userMessageId) {
            updateMessage(userMessageId, { llmImageUrl: optimized.dataUrl, llmImageMimeType: optimized.mimeType });
        }
      } catch (e) { console.warn('Failed to derive low-res for current user media, will omit persistence media', e); }
  finally { setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.preparingMedia') || 'Preparing media…' } : prev)); }
    }
  
  // Decide which image to upload to Gemini
  let imageForGeminiContextBase64: string | undefined;
  let imageForGeminiContextMimeType: string | undefined;

  if (messageType === 'user') {
      if (userImageToProcessBase64) {
          // Use High Res if available
          imageForGeminiContextBase64 = userImageToProcessBase64;
          imageForGeminiContextMimeType = userImageToProcessMimeType;
      } else {
          // Fallback to Low Res
          imageForGeminiContextBase64 = userImageToProcessLlmBase64;
          imageForGeminiContextMimeType = userImageToProcessLlmMimeType;
      }
  } else {
      imageForGeminiContextBase64 = (typeof passedImageBase64 === 'string' && passedImageBase64) ? passedImageBase64 : undefined;
      imageForGeminiContextMimeType = (typeof passedImageMimeType === 'string' && passedImageMimeType) ? passedImageMimeType : undefined;
  }

  let imageForGeminiContextFileUri: string | undefined = undefined;

  switch (messageType) {
      case 'image-reengagement':
        geminiPromptText = "...";
        break;
      case 'conversational-reengagement':
        geminiPromptText = "...";
        imageForGeminiContextBase64 = undefined;
        imageForGeminiContextMimeType = undefined;
        break;
      case 'user':
      default:
        geminiPromptText = userMessageText;
        break;
    }

    if (messageType === 'image-reengagement') {
      if (typeof passedImageBase64 === 'string' && passedImageBase64 && typeof passedImageMimeType === 'string' && passedImageMimeType) {
        try {
          if (!sendWithFileUploadInProgressRef.current) {
            sendWithFileUploadInProgressRef.current = true;
          }
          const optimized = await processMediaForUpload(passedImageBase64, passedImageMimeType, { 
            t, 
            onProgress: (label, done, total, etaMs) => setSendPrep({ active: true, label, done, total, etaMs }) 
          });
          // For re-engagement, optimize strictly
          imageForGeminiContextBase64 = optimized.dataUrl; 
          imageForGeminiContextMimeType = optimized.mimeType;
        } catch (e) { console.warn('Failed to derive low-res for re-engagement media, omitting media', e); }
      }
    }

  if (imageForGeminiContextBase64 && imageForGeminiContextMimeType) {
      try {
        sendWithFileUploadInProgressRef.current = true;
  setSendPrep({ active: true, label: t('chat.sendPrep.uploadingMedia') || 'Uploading media…' });
  const up = await uploadMediaToFiles(imageForGeminiContextBase64, imageForGeminiContextMimeType, 'current-user-media');
        if (messageType === 'user' && userMessageId) {
          const existing = (messagesRef.current || []).find(m => m.id === userMessageId);
          const hasExisting = !!(existing && typeof existing.llmFileUri === 'string' && existing.llmFileUri);
          if (!hasExisting) {
            updateMessage(userMessageId, {
              llmFileUri: up.uri,
              llmFileMimeType: up.mimeType,
            });
            imageForGeminiContextFileUri = up.uri;
          } else {
            imageForGeminiContextFileUri = existing!.llmFileUri as string;
          }
        } else {
          imageForGeminiContextFileUri = up.uri;
        }
      } catch (e) {
        console.warn('Failed to upload current media to Files API; will send without media');
        imageForGeminiContextFileUri = undefined;
      } finally {
  setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.preparingMedia') || 'Preparing media…' } : prev));
      }
    }

    setLatestGroundingChunks(undefined);

    try {
      const historySubsetForSend: ChatMessage[] = (() => {
  const base = getHistoryRespectingBookmark(historyForGeminiWithUris as any);
        return base as ChatMessage[];
      })();
  setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media…', done: 0, total: 0 });
      let ensuredUpdates: Record<string, { oldUri?: string; newUri: string }> = {};
      try {
        ensuredUpdates = await ensureUrisForHistoryForSend(historySubsetForSend, (done, total, etaMs) => {
          setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media…', done, total, etaMs });
        });
      } finally {
  setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.finalizing') || 'Finalizing…' } : prev));
      }

      let historyForGeminiPostEnsure = messagesRef.current.filter(m => m.id !== thinkingMessageId);
      if (messageType === 'user' && userMessageId) {
        historyForGeminiPostEnsure = historyForGeminiPostEnsure.filter(m => m.id !== userMessageId);
      }
      const historySubsetForSendFinal: ChatMessage[] = (() => {
  const base = getHistoryRespectingBookmark(historyForGeminiPostEnsure as any);
        const updated = base.map((m: ChatMessage) => {
          if (ensuredUpdates[m.id]?.newUri) {
            const nu = ensuredUpdates[m.id].newUri;
            if ((m as any).llmFileUri !== nu) {
              return { ...m, llmFileUri: nu } as ChatMessage;
            }
          }
          return m;
        });
        return updated as ChatMessage[];
      })();

      try {
        for (const m of historySubsetForSendFinal) {
          const upd = ensuredUpdates[m.id];
          if (upd && upd.newUri && (m as any).llmFileUri !== upd.newUri) {
            console.warn('Correcting stale llmFileUri just before send for message', m.id);
            (m as any).llmFileUri = upd.newUri;
          }
        }
      } catch {}

      let globalProfileText: string | undefined = undefined;
      try {
        const gp2 = await getGlobalProfileDB();
        globalProfileText = gp2?.text || undefined;
      } catch {}

      const derivedHistory = deriveHistoryForApi(historySubsetForSendFinal, {
        maxMessages: computeMaxMessagesForArray(historySubsetForSendFinal.filter(m => m.role === 'user' || m.role === 'assistant')),
        maxMediaToKeep: MAX_MEDIA_TO_KEEP,
        contextSummary: resolveBookmarkContextSummary() || undefined,
        globalProfileText,
      });
      const sanitizedDerivedHistory = await sanitizeHistoryWithVerifiedUris(derivedHistory as any);
      if (shouldGenerateUserImage && currentSettingsVal.sendWithSnapshotEnabled && messageType === 'user' && userMessageText.trim() && userMessageId && !userImageToProcessBase64) {
        const userImageGenStartTime = Date.now();
        updateMessage(userMessageId, {
          isGeneratingImage: true,
          imageGenerationStartTime: userImageGenStartTime,
          imageUrl: undefined,
          imageMimeType: undefined,
        });

        // Use constant prompt template for system instruction
        let systemInstructionForUserImage = IMAGE_GEN_SYSTEM_INSTRUCTION;

        const sanitizedUserHistoryForImage = sanitizedDerivedHistory as any;
        let finalResult: any = null;
        for (let attempt = 0; attempt < 3; attempt++) {
          const prompt = IMAGE_GEN_USER_PROMPT_TEMPLATE.replace("{TEXT}", userMessageText);
          const userImgGenResult = await generateImage({
            history: sanitizedUserHistoryForImage,
            latestMessageText: prompt,
            latestMessageRole: 'user',
            systemInstruction: systemInstructionForUserImage,
            maestroAvatarUri: maestroAvatarUriRef.current || undefined,
            maestroAvatarMimeType: maestroAvatarMimeTypeRef.current || undefined,
          });
          finalResult = userImgGenResult;
          if ('base64Image' in userImgGenResult) break;
          if (attempt < 2) await new Promise(r => setTimeout(r, 1500));
        }

        if (finalResult && 'base64Image' in finalResult) {
          const duration = Date.now() - userImageGenStartTime;
          setImageLoadDurations(prev => [...prev, duration]);
          try {
            const optimized = await processMediaForUpload((finalResult as any).base64Image as string, (finalResult as any).mimeType as string, { t });
            const lowResDataUrl = optimized.dataUrl;
            const lowResMime = optimized.mimeType;

            sendWithFileUploadInProgressRef.current = true;
            setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.uploadingMedia') || 'Uploading media…' } : { active: true, label: t('chat.sendPrep.uploadingMedia') || 'Uploading media…' }));
            const up = await uploadMediaToFiles((finalResult as any).base64Image as string, (finalResult as any).mimeType as string, 'user-generated');

            updateMessage(userMessageId!, {
              imageUrl: (finalResult as any).base64Image,
              imageMimeType: (finalResult as any).mimeType,
              llmImageUrl: lowResDataUrl,
              llmImageMimeType: lowResMime,
              llmFileUri: up.uri,
              llmFileMimeType: up.mimeType,
              isGeneratingImage: false,
              imageGenError: null,
              imageGenerationStartTime: undefined
            });
            imageForGeminiContextFileUri = up.uri;
            imageForGeminiContextMimeType = up.mimeType;
          } catch (e) {
            updateMessage(userMessageId!, {
              imageUrl: (finalResult as any).base64Image,
              imageMimeType: (finalResult as any).mimeType,
              isGeneratingImage: false,
              imageGenError: null,
              imageGenerationStartTime: undefined
            });
          } finally {
            setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.preparingMedia') || 'Preparing media…' } : prev));
          }
        } else if (finalResult) {
          updateMessage(userMessageId!, {
            imageGenError: (finalResult as any).error,
            isGeneratingImage: false,
            imageGenerationStartTime: undefined
          });
        }
      }

      const response = await generateGeminiResponse(
        DEFAULT_TEXT_MODEL_ID,
        geminiPromptText,
        sanitizedDerivedHistory,
        systemInstructionForGemini,
        undefined,
        imageForGeminiContextMimeType,
        imageForGeminiContextFileUri,
        currentSettingsVal.enableGoogleSearch,
        undefined
      );
      
      const accumulatedFullText = response.text || "";
      const parsedTranslationsOnComplete = parseGeminiResponse(accumulatedFullText);
      const groundingChunks = response.candidates?.[0]?.groundingMetadata?.groundingChunks as GroundingChunk[] | undefined;
      if (groundingChunks?.length) {
          setLatestGroundingChunks(groundingChunks);
      }
      
      const finalMessageUpdates = {
          thinking: false,
          translations: parsedTranslationsOnComplete.length > 0 ? parsedTranslationsOnComplete : undefined,
          rawAssistantResponse: accumulatedFullText,
          text: parsedTranslationsOnComplete.length === 0 ? accumulatedFullText : undefined,
      };
      updateMessage(thinkingMessageId, finalMessageUpdates);
      
      try {
        const textForSuggestionsEarly = finalMessageUpdates.rawAssistantResponse || (finalMessageUpdates.translations?.find(t => t.spanish)?.spanish) || "";
        if (!isLoadingSuggestionsRef.current && textForSuggestionsEarly.trim()) {
          const historyWithFinalAssistant = messagesRef.current.map(m =>
            m.id === thinkingMessageId ? ({ ...m, ...finalMessageUpdates }) : m
          );
          fetchAndSetReplySuggestions(thinkingMessageId, textForSuggestionsEarly, getHistoryRespectingBookmark(historyWithFinalAssistant));
          lastFetchedSuggestionsForRef.current = thinkingMessageId;
        }
      } catch (e) {
        console.warn('Failed to prefetch suggestions before TTS:', e);
      }

      const originalMessage = messagesRef.current.find(m => m.id === thinkingMessageId);
      if (originalMessage) {
        const finalMessageForSpeech = { ...originalMessage, ...finalMessageUpdates };
        speakMessage(finalMessageForSpeech);
      }

  if (messageType === 'user') {
        setAttachedImageBase64(null);
        setAttachedImageMimeType(null);
        if (sanitizedText === stripBracketedContent(transcript || '') && (transcript || '').length > 0) {
          clearTranscript();
        }
      }

      await new Promise(resolve => setTimeout(resolve, 0));

  if (currentSettingsVal.imageGenerationModeEnabled && accumulatedFullText.trim()) {
            const assistantStartTime = Date.now();
            updateMessage(thinkingMessageId, {
                isGeneratingImage: true,
                imageGenerationStartTime: assistantStartTime
            });
            
            let historyForAssistantImageGen: ChatMessage[] | undefined = undefined;
            try {
              sendWithFileUploadInProgressRef.current = true;
              const baseForEnsure: ChatMessage[] = (() => {
                const base = getHistoryRespectingBookmark(messagesRef.current as any);
                return base as any as ChatMessage[];
              })();
              setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media…', done: 0, total: 0 });
              const ensuredUpdates = await ensureUrisForHistoryForSend(baseForEnsure, (done, total, etaMs) => {
                setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media…', done, total, etaMs });
              });
              historyForAssistantImageGen = baseForEnsure.map(m => {
                const upd = ensuredUpdates[m.id];
                if (upd && upd.newUri && (m as any).llmFileUri !== upd.newUri) {
                  return { ...m, llmFileUri: upd.newUri } as ChatMessage;
                }
                return m;
              });
              await new Promise(r => setTimeout(r, 0));
            } catch { }

            for (let attempt = 0; attempt < 3; attempt++) {
                const histForAssistantImgBase = (historyForAssistantImageGen || (() => {
                  const base = getHistoryRespectingBookmark(messagesRef.current as any);
                  return base as any as ChatMessage[];
                })());
                let systemInstructionForAssistantImage = IMAGE_GEN_SYSTEM_INSTRUCTION;

                let gpTextForAssistant: string | undefined = undefined;
                try {
                  const gp3 = await getGlobalProfileDB();
                  gpTextForAssistant = gp3?.text || undefined;
                } catch {}

                const assistantHistory = deriveHistoryForApi(histForAssistantImgBase, {
                  maxMessages: computeMaxMessagesForArray(getHistoryRespectingBookmark(messagesRef.current as any).filter(m => m.role === 'user' || m.role === 'assistant')),
                  maxMediaToKeep: MAX_MEDIA_TO_KEEP,
                  contextSummary: resolveBookmarkContextSummary() || undefined,
                  globalProfileText: gpTextForAssistant,
                  placeholderLatestUserMessage: DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE,
                });
                const sanitizedAssistantHistoryForImage = await sanitizeHistoryWithVerifiedUris(assistantHistory as any);
                
                const prompt = IMAGE_GEN_USER_PROMPT_TEMPLATE.replace("{TEXT}", accumulatedFullText);
                const assistantImgGenResult = await generateImage({
                  history: sanitizedAssistantHistoryForImage,
                  latestMessageText: prompt,
                  latestMessageRole: 'user', 
                  systemInstruction: systemInstructionForAssistantImage,
                  maestroAvatarUri: maestroAvatarUriRef.current || undefined,
                  maestroAvatarMimeType: maestroAvatarMimeTypeRef.current || undefined,
                });
                if ('base64Image' in assistantImgGenResult) {
                const duration = Date.now() - assistantStartTime;
                setImageLoadDurations(prev => [...prev, duration]);
                try {
                  const optimized = await processMediaForUpload(assistantImgGenResult.base64Image as string, assistantImgGenResult.mimeType as string, { t });
                  const lowResDataUrl = optimized.dataUrl;
                  const lowResMime = optimized.mimeType;

                  sendWithFileUploadInProgressRef.current = true;
                  const up = await uploadMediaToFiles(assistantImgGenResult.base64Image as string, assistantImgGenResult.mimeType as string, 'assistant-generated');

                  updateMessage(thinkingMessageId, {
                    imageUrl: assistantImgGenResult.base64Image,
                    imageMimeType: assistantImgGenResult.mimeType,
                    llmImageUrl: lowResDataUrl,
                    llmImageMimeType: lowResMime,
                    llmFileUri: up.uri,
                    llmFileMimeType: up.mimeType,
                    isGeneratingImage: false,
                    imageGenError: null,
                    imageGenerationStartTime: undefined
                  });
                } catch (e) {
                  updateMessage(thinkingMessageId, {
                    imageUrl: assistantImgGenResult.base64Image,
                    imageMimeType: assistantImgGenResult.mimeType,
                    isGeneratingImage: false,
                    imageGenError: null,
                    imageGenerationStartTime: undefined
                  });
                }
                break;
        } else if (attempt < 2) {
                await new Promise(r => setTimeout(r, 1500));
                } else {
                updateMessage(thinkingMessageId, {
                    imageGenError: (assistantImgGenResult as any).error,
                    isGeneratingImage: false,
                    imageGenerationStartTime: undefined
                });
                }
            }
      }
      
      try {
        sendWithFileUploadInProgressRef.current = false;
      } catch {}

  setIsSending(false);
  setSendPrep(null);
  scheduleReengagement('send-complete');

      // Manual STT resume check after send + TTS queue completion check
      const isSpeechActive = speechIsSpeakingRef.current || (typeof (hasPendingQueueItems as any) === 'function' && (hasPendingQueueItems as any)());
      
      if (sttInterruptedBySendRef.current && settingsRef.current.stt.enabled && !isSpeechActive) {
        try {
          startListening(settingsRef.current.stt.language);
        } finally {
          sttInterruptedBySendRef.current = false;
        }
      }

      if (!isSpeechSynthesisSupported) {
          const finalAssistantMessage = messagesRef.current.find(m => m.id === thinkingMessageId);
          if (finalAssistantMessage && finalAssistantMessage.role === 'assistant' &&
              (finalAssistantMessage.rawAssistantResponse || (finalAssistantMessage.translations && finalAssistantMessage.translations.length > 0)) &&
              !isLoadingSuggestionsRef.current &&
              finalAssistantMessage.id !== lastFetchedSuggestionsForRef.current ) {
                  const textForSuggestions = finalAssistantMessage.rawAssistantResponse ||
                                            (finalAssistantMessage.translations?.find(t => t.spanish)?.spanish) || "";
          if(textForSuggestions.trim()){
            fetchAndSetReplySuggestions(finalAssistantMessage.id, textForSuggestions, getHistoryRespectingBookmark(messagesRef.current));
                      lastFetchedSuggestionsForRef.current = finalAssistantMessage.id;
                  }
          }
      }
      
      return true;

    } catch (error) {
      console.error("Error sending message (stream consumer):", error);
      let errorMessage = t('general.error');
      if (error instanceof ApiError) {
        errorMessage = error.message || error.code || `HTTP ${error.status}`;
      } else if (error instanceof Error) {
        errorMessage = error.message;
      }
      updateMessage(thinkingMessageId, {
        thinking: false, role: 'error', text: errorMessage, rawAssistantResponse: undefined, translations: undefined,
      });
  setIsSending(false);
  setSendPrep(null);
  sendWithFileUploadInProgressRef.current = false;

      // On error, also try to resume STT if it was paused for send
      if (sttInterruptedBySendRef.current && settingsRef.current.stt.enabled && !speechIsSpeakingRef.current) {
        try {
          startListening(settingsRef.current.stt.language);
        } finally {
          sttInterruptedBySendRef.current = false;
        }
      }

  scheduleReengagement('send-error');

      if (messageType === 'user') {
        setAttachedImageBase64(null);
        setAttachedImageMimeType(null);
      }
      setReplySuggestions([]);
      setIsLoadingSuggestions(false);
      speechIsSpeakingRef.current = false;
      return false;
    }
  }, [
    addMessage, t, clearTranscript, transcript, captureSnapshot,
    currentSystemPromptText, fetchAndSetReplySuggestions, parseGeminiResponse, 
    isSpeechSynthesisSupported, speakMessage, scheduleReengagement,
    hasPendingQueueItems
  ]); 
  
  useEffect(() => {
    _handleSendMessageInternalRef.current = _handleSendMessageInternal;
  }, [_handleSendMessageInternal]);

  useEffect(() => {
    (async () => {
      try {
        const a = await getMaestroProfileImageDB();
        if (a && (a.dataUrl || a.uri)) {
          maestroAvatarUriRef.current = a.uri || null;
          maestroAvatarMimeTypeRef.current = (a?.mimeType && typeof a.mimeType === 'string') ? a.mimeType : (a?.dataUrl?.startsWith('data:image/') ? 'image/svg+xml' : null);
        } else {
          try {
            const blob = await fetchDefaultAvatarBlob();
            if (blob) {
              const defaultMime = blob.type || 'image/png';
              const defaultDataUrl: string = await new Promise((resolve, reject) => {
                const fr = new FileReader();
                fr.onloadend = () => resolve(fr.result as string);
                fr.onerror = () => reject(fr.error || new Error('DataURL conversion failed'));
                fr.readAsDataURL(blob);
              });
              await setMaestroProfileImageDB({ dataUrl: defaultDataUrl, mimeType: defaultMime, uri: undefined, updatedAt: Date.now() });
              maestroAvatarUriRef.current = null;
              maestroAvatarMimeTypeRef.current = defaultMime;
              try { window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: { dataUrl: defaultDataUrl, mimeType: defaultMime, uri: undefined } })); } catch {}
            } else {
              maestroAvatarUriRef.current = null;
              maestroAvatarMimeTypeRef.current = null;
            }
          } catch {
            maestroAvatarUriRef.current = null;
            maestroAvatarMimeTypeRef.current = null;
          }
        }
      } catch { maestroAvatarUriRef.current = null; }
    })();
  }, []);

  useEffect(() => {
    const handler = (e: any) => {
      try {
        const uri = e?.detail?.uri as string | undefined;
        const mt = e?.detail?.mimeType as string | undefined;
        maestroAvatarUriRef.current = uri || null;
        if (mt && typeof mt === 'string') maestroAvatarMimeTypeRef.current = mt;
      } catch { /* ignore */ }
    };
    window.addEventListener('maestro-avatar-updated', handler as any);
    return () => window.removeEventListener('maestro-avatar-updated', handler as any);
  }, []);

  useEffect(() => {
    prevIsListeningRef.current = isListening;
  });
  const wasListening = prevIsListeningRef.current ?? false;
  
  const handleToggleSuggestionMode = useCallback((forceState?: boolean) => {
    const newIsSuggestionMode = typeof forceState === 'boolean' ? forceState : !settingsRef.current.isSuggestionMode;

    if (newIsSuggestionMode === settingsRef.current.isSuggestionMode) {
        return;
    }

    const currentSttSettings = settingsRef.current.stt;
    const sttShouldBeActive = currentSttSettings.enabled;
    let newSttLang = currentSttSettings.language;

    if (selectedLanguagePairRef.current) {
        newSttLang = newIsSuggestionMode
            ? getPrimaryCode(selectedLanguagePairRef.current.nativeLanguageCode)
            : getPrimaryCode(selectedLanguagePairRef.current.targetLanguageCode);
    }
    
    const langDidChange = newSttLang !== currentSttSettings.language;

    setSettings(prev => ({
        ...prev,
        isSuggestionMode: newIsSuggestionMode,
        stt: {
            ...prev.stt,
            language: newSttLang
        }
    }));

    if (langDidChange) {
        if (sttShouldBeActive && isListening) {
            stopListening();
            setTimeout(() => {
                if (settingsRef.current.stt.enabled) {
                    clearTranscript();
                    startListening(newSttLang);
                }
            }, 250);
        } else {
            clearTranscript();
        }
    }
  }, [isListening, stopListening, startListening, clearTranscript]);

  const handleCreateSuggestion = useCallback(async (textToTranslate: string) => {
    const sanitized = stripBracketedContent(textToTranslate);
    if (!sanitized || !selectedLanguagePairRef.current) return;
    
    setIsCreatingSuggestion(true);

    const sttLang = settingsRef.current.stt.language;
    const sttLangCode = sttLang.substring(0, 2);
    const targetLangCode = selectedLanguagePairRef.current.targetLanguageCode.substring(0, 2);
    
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
  const { translatedText } = await translateText(sanitized, fromLangName, toLangName);
        const newSuggestion: ReplySuggestion = {
            target: originalTextIsTarget ? sanitized : translatedText,
            native: originalTextIsTarget ? translatedText : sanitized,
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
        setIsCreatingSuggestion(false);
        handleToggleSuggestionMode(false); 
    }
  }, [addMessage, t, handleToggleSuggestionMode]);

  useEffect(() => {
    if (!settingsRef.current.stt.enabled) {
      if (autoSendTimerRef.current) {
        clearTimeout(autoSendTimerRef.current);
        autoSendTimerRef.current = null;
      }
      autoSendSnapshotRef.current = '';
      return;
    }

  const text = stripBracketedContent(transcript || '');
    if (autoSendTimerRef.current) {
      clearTimeout(autoSendTimerRef.current);
      autoSendTimerRef.current = null;
    }

    if (text.length < 2) {
      autoSendSnapshotRef.current = '';
      return;
    }

    autoSendSnapshotRef.current = stripBracketedContent(transcript || '');
    autoSendTimerRef.current = window.setTimeout(() => {
      const snap = autoSendSnapshotRef.current;
      const current = stripBracketedContent(transcript || '');
      if (
        settingsRef.current.stt.enabled &&
        !isSendingRef.current &&
        !speechIsSpeakingRef.current &&
        current.length >= 2 &&
        stripBracketedContent(transcript || '') === snap 
      ) {
        clearTranscript();
        if (settingsRef.current.isSuggestionMode) {
          handleCreateSuggestion(current);
        } else {
          _handleSendMessageInternalRef.current(current, attachedImageBase64, attachedImageMimeType, 'user');
        }
      }
      autoSendTimerRef.current = null;
      autoSendSnapshotRef.current = '';
    }, STT_STABLE_NO_TEXT_MS);

    return () => {
      if (autoSendTimerRef.current) {
        clearTimeout(autoSendTimerRef.current);
        autoSendTimerRef.current = null;
      }
    };
  }, [transcript, attachedImageBase64, attachedImageMimeType, handleCreateSuggestion, clearTranscript]);

  useEffect(() => {
    if (wasListening && !isListening) {
      
      if (settingsRef.current.isSuggestionMode) {
        if (settingsRef.current.stt.enabled) {
            setTimeout(() => startListening(settingsRef.current.stt.language), 100);
        }
        return;
      }
    }
  }, [wasListening, isListening, transcript, handleCreateSuggestion, clearTranscript, attachedImageBase64, attachedImageMimeType, startListening]);

  useEffect(() => {
    if (!selectedLanguagePair) {
      cancelReengagement();
      return;
    }
    if (isSpeaking || isSending || isListening || isUserActive) {
      cancelReengagement();
      return;
    }
    if (reengagementPhase === 'idle') {
      scheduleReengagement('became-idle');
    }
  }, [selectedLanguagePair, isSpeaking, isSending, isListening, isUserActive, reengagementPhase, settings.smartReengagement.thresholdSeconds, cancelReengagement, scheduleReengagement]);

  useEffect(() => {
    // If there are blocking UI tasks (popups, annotations, recording), Maestro yields visual status to them.
    if (externalUiTaskCount > 0) {
      setMaestroActivityStage('idle');
      return;
    }

    if (isSpeaking) {
      setMaestroActivityStage('speaking');
    } else if (isSending) {
      setMaestroActivityStage('typing');
    } else if (isListening || isUserActive) {
      setMaestroActivityStage('listening');
    } else if (reengagementPhase === 'countdown' || reengagementPhase === 'engaging') {
      setMaestroActivityStage('observing_high');
    } else if (reengagementPhase === 'watching') {
      setMaestroActivityStage('observing_medium');
    } else if (reengagementPhase === 'waiting') {
      setMaestroActivityStage('observing_low');
    } else {
      setMaestroActivityStage('idle');
    }
  }, [isSpeaking, isSending, isListening, isUserActive, reengagementPhase, externalUiTaskCount]);

  useEffect(() => {
    const startVisualContextStream = async () => {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            setVisualContextCameraError(t('error.cameraAccessNotSupported'));
            return;
        }
        try {
            if (visualContextStreamRef.current) {
                visualContextStreamRef.current.getTracks().forEach(track => track.stop());
            }

            const videoConstraints: MediaStreamConstraints['video'] = settingsRef.current.selectedCameraId
                ? { deviceId: { exact: settingsRef.current.selectedCameraId } }
                : true;
            const stream = await navigator.mediaDevices.getUserMedia({ video: videoConstraints });
            visualContextStreamRef.current = stream;
            setLiveVideoStream(stream);
            if (visualContextVideoRef.current) {
                visualContextVideoRef.current.srcObject = stream;
                visualContextVideoRef.current.muted = true;
                visualContextVideoRef.current.playsInline = true;
                visualContextVideoRef.current.play().catch(playError => {
                    console.error("Error playing visual context video:", playError);
                    setVisualContextCameraError(t('error.visualContextStreamPlayback', {details: playError.message}));
                });
            }
            setVisualContextCameraError(null);
        } catch (err) {
            console.error("Error accessing camera for visual context:", err);
            let message = t("error.cameraUnknown");
            if (err instanceof Error) {
                if (err.name === "NotAllowedError" || err.name === "PermissionDeniedError") message = t('error.cameraPermissionDenied');
                else if (err.name === "NotFoundError" || err.name === "DevicesNotFoundError") message = t('error.cameraNotFound');
                else if (err.name === "OverconstrainedError" ) message = t('error.cameraOverconstrained', {errorMessage: err.message});
                else message = t('error.visualContextCameraGeneric', {details: err.message});
            }
            setVisualContextCameraError(message);
            setLiveVideoStream(null);
        }
    };

    const stopVisualContextStream = () => {
        if (visualContextStreamRef.current) {
            visualContextStreamRef.current.getTracks().forEach(track => track.stop());
            visualContextStreamRef.current = null;
        }
        setLiveVideoStream(null);
        if (visualContextVideoRef.current && visualContextVideoRef.current.srcObject) {
            visualContextVideoRef.current.srcObject = null;
            visualContextVideoRef.current.load();
        }
    };
    
    const shouldStream = (settings.smartReengagement.useVisualContext || settings.sendWithSnapshotEnabled) && settings.selectedCameraId !== IMAGE_GEN_CAMERA_ID;

    if (shouldStream) {
        startVisualContextStream();
    } else {
        stopVisualContextStream();
        setVisualContextCameraError(null);
    }

    return () => {
        stopVisualContextStream();
    };
  }, [settings.smartReengagement.useVisualContext, settings.sendWithSnapshotEnabled, settings.selectedCameraId, t]);

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

  const activeSttProvider: any = settings.stt.provider || 'browser';
  const browserSttAvailable = isSpeechRecognitionSupported;
  const effectiveSttSupported = activeSttProvider === 'browser'
    ? browserSttAvailable
    : microphoneApiAvailable;

  const sttMasterToggle = useCallback(() => {
    const currentSttSettings = settingsRef.current.stt;
    const newSttEnabledState = !currentSttSettings.enabled;
    const nextSettings = { ...settingsRef.current, stt: { ...currentSttSettings, enabled: newSttEnabledState } };
    settingsRef.current = nextSettings;
    setSettings(nextSettings);
    setAppSettingsDB(nextSettings).catch(() => {});

    if (newSttEnabledState) {
      clearTranscript();
      startListening(currentSttSettings.language);
    } else {
      if (autoSendTimerRef.current) {
        clearTimeout(autoSendTimerRef.current);
        autoSendTimerRef.current = null;
        autoSendSnapshotRef.current = '';
      }
      stopListening();
    }
  }, [clearTranscript, startListening, stopListening]);

  const handleSttLanguageChange = (langCode: string) => {
    const currentSttSettings = settingsRef.current.stt;
    const sttShouldBeActive = currentSttSettings.enabled;

    if (sttShouldBeActive && isListening) {
      stopListening();
    }
    setSettings(prev => ({...prev, stt: {...prev.stt, language: langCode}}));

    if(sttShouldBeActive) {
      setTimeout(() => {
        if (settingsRef.current.stt.enabled) {
          clearTranscript();
          startListening(langCode);
        }
      }, 250);
    } else {
      clearTranscript();
    }
  };

  const toggleSttProvider = () => {
    const next = settings.stt.provider === 'browser' ? 'gemini' : 'browser';
    handleSettingsChange('stt', { ...settings.stt, provider: next });
  };

  const toggleTtsProvider = () => {
    const next = settings.tts.provider === 'browser' ? 'gemini' : 'browser';
    handleSettingsChange('tts', { ...settings.tts, provider: next });
  };

  const handleToggleSendWithSnapshot = useCallback(() => {
    handleSettingsChange('sendWithSnapshotEnabled', !settingsRef.current.sendWithSnapshotEnabled);
  }, [handleSettingsChange]);

  const handleToggleUseVisualContextForReengagement = useCallback(() => {
    const newUseVisualContext = !settingsRef.current.smartReengagement.useVisualContext;
    handleSettingsChange('smartReengagement', {
      ...settingsRef.current.smartReengagement,
      useVisualContext: newUseVisualContext,
    });
  }, [handleSettingsChange]);

  const handleToggleSpeakNativeLang = useCallback(() => {
    const newSpeakNativeState = !settingsRef.current.tts.speakNative;
     handleSettingsChange('tts', {
      ...settingsRef.current.tts,
      speakNative: newSpeakNativeState,
    });
  }, [handleSettingsChange]);

  const handleSuggestionInteraction = useCallback((suggestion: ReplySuggestion, langType: 'target' | 'native') => {
    if (!selectedLanguagePairRef.current) return;

    if (!speechIsSpeakingRef.current) {
      const textToSpeak = langType === 'target' ? suggestion.target : suggestion.native;
      const langCodeToUse = langType === 'target'
        ? getPrimaryCode(selectedLanguagePairRef.current.targetLanguageCode)
        : getPrimaryCode(selectedLanguagePairRef.current.nativeLanguageCode);

      if (textToSpeak && langCodeToUse) {
        const messageId = lastFetchedSuggestionsForRef.current;
        const suggestionIndex = replySuggestions.findIndex((s) => s.target === suggestion.target && s.native === suggestion.native);
        const context = (messageId && suggestionIndex >= 0)
          ? { source: 'suggestion' as const, messageId, suggestionIndex, suggestionLang: langType }
          : { source: 'adHoc' as const };
        speakWrapper([
          { text: textToSpeak, langCode: langCodeToUse, context },
        ], langCodeToUse);
      }
    }
    handleUserInputActivity();

  }, [speakWrapper, handleUserInputActivity, replySuggestions]);

  const handleToggleImageGenerationMode = useCallback(() => {
    const willBeEnabled = !settingsRef.current.imageGenerationModeEnabled;
    handleSettingsChange('imageGenerationModeEnabled', willBeEnabled);
    if (!willBeEnabled && settingsRef.current.selectedCameraId === IMAGE_GEN_CAMERA_ID) {
      const firstPhysicalCamera = availableCamerasRef.current[0];
      handleSettingsChange('selectedCameraId', firstPhysicalCamera ? firstPhysicalCamera.deviceId : null);
    }
  }, [handleSettingsChange]);

  const _toggleFocusedModeState = useCallback(() => {
    handleSettingsChange('imageFocusedModeEnabled', !settingsRef.current.imageFocusedModeEnabled);
  }, [handleSettingsChange]);
  
  const handleToggleImageFocusedMode = (messageId: string) => {
    // @ts-ignore
    if (!document.startViewTransition) {
      _toggleFocusedModeState();
      return;
    }
  
    setTransitioningImageId(messageId);
  
    // @ts-ignore
    const transition = document.startViewTransition(() => {
      _toggleFocusedModeState();
    });
  
    transition.finished.finally(() => {
      setTransitioningImageId(null);
    });
  };
  
  const calculateEstimatedImageLoadTime = useCallback((): number => {
    if (imageLoadDurations.length > 0) {
        const sum = imageLoadDurations.reduce((a, b) => a + b, 0);
        return sum / imageLoadDurations.length / 1000;
    }
    return 15;
  }, [imageLoadDurations]);
  
  const handleSaveAllChats = useCallback(async (options?: { filename?: string; auto?: boolean }) => {
    const isAuto = options?.auto === true;
    try {
      const selectedPairId = settingsRef.current.selectedLanguagePairId;
      if (selectedPairId) {
        try {
          await safeSaveChatHistoryDB(selectedPairId, messagesRef.current);
        } catch (flushErr) {
          console.warn('Failed to persist current chat before export', flushErr);
        }
      }
  const allChats = await getAllChatHistoriesDB();
  const allMetas = await getAllChatMetasDB();
  const gp = await getGlobalProfileDB();
  let assetsLoadingGifs: string[] = [];
  try { assetsLoadingGifs = (await getLoadingGifsDB()) || []; } catch {}

        if (Object.keys(allChats).length === 0) {
            if (!isAuto) {
                alert(t('startPage.noChatsToSave'));
            }
            return;
        }
  let maestroProfile: any = null;
  try { maestroProfile = await getMaestroProfileImageDB(); } catch {}
  const backup = { version: 7, chats: allChats, metas: allMetas, globalProfile: gp?.text || null, assets: { loadingGifs: assetsLoadingGifs, maestroProfile } };
  const blob = new Blob([JSON.stringify(backup, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        
        const timestamp = new Date().toISOString().slice(0, 19).replace('T', '_').replace(/:/g, '-');
        const prefix = isAuto ? 'maestro-backup-' : 'maestro-all-chats-';
        a.download = options?.filename && options.filename.trim().length > 0
          ? options.filename.trim()
          : `${prefix}${timestamp}.json`;

        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    } catch (error) {
        console.error("Failed to save all chats:", error);
        if (!isAuto) {
            alert(t('startPage.saveError'));
        }
    }
  }, [t]);

  const handleLoadAllChats = useCallback(async (file: File) => {
  await handleSaveAllChats({ auto: true });

    const reader = new FileReader();
    reader.onload = async (event) => {
        try {
            const content = event.target?.result as string;
            const parsed = JSON.parse(content);
            if (typeof parsed !== 'object' || parsed === null) throw new Error("Invalid format");
            let chats: Record<string, ChatMessage[]> = {};
            let metas: Record<string, ChatMeta> | null = null;
            let globalProfileText: string | null = null;
            let importedLoadingGifs: string[] | null = null;
            let importedMaestroProfile: any | null = null;
            if ('chats' in parsed) {
              chats = parsed.chats || {};
              metas = parsed.metas || null;
              globalProfileText = typeof parsed.globalProfile === 'string' ? parsed.globalProfile : null;
              if (parsed.assets && Array.isArray(parsed.assets.loadingGifs)) {
                importedLoadingGifs = parsed.assets.loadingGifs as string[];
              }
              if (parsed.assets && parsed.assets.maestroProfile && typeof parsed.assets.maestroProfile === 'object') {
                const mp = parsed.assets.maestroProfile as any;
                if (mp && (typeof mp.dataUrl === 'string' || typeof mp.uri === 'string')) {
                  importedMaestroProfile = {
                    dataUrl: typeof mp.dataUrl === 'string' ? mp.dataUrl : undefined,
                    mimeType: typeof mp.mimeType === 'string' ? mp.mimeType : undefined,
                    uri: typeof mp.uri === 'string' ? mp.uri : undefined,
                    updatedAt: typeof mp.updatedAt === 'number' ? mp.updatedAt : Date.now(),
                  };
                }
              }
            } else {
              chats = parsed as Record<string, ChatMessage[]>;
            }
            await clearAndSaveAllHistoriesDB(chats, metas, null, globalProfileText);
            try {
              const current = (await getLoadingGifsDB()) || [];
              let manifest: string[] = [];
              try { const resp = await fetch('/gifs/manifest.json', { cache: 'force-cache' }); if (resp.ok) manifest = await resp.json(); } catch {}
              const merged = uniq([ ...current, ...(importedLoadingGifs || []), ...manifest ]);
              await setLoadingGifsDB(merged);
              setLoadingGifs(merged);
            } catch {}
            if (importedMaestroProfile) {
              try {
                let profileToPersist: any = { ...importedMaestroProfile };
                profileToPersist.uri = undefined; 

                await setMaestroProfileImageDB(profileToPersist);
                try { window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: profileToPersist })); } catch {}
              } catch { /* ignore */ }
            }
            const loadedCount = Object.keys(chats).length;
            alert(t('startPage.loadSuccess', {count: loadedCount}));

            const currentPairId = settingsRef.current.selectedLanguagePairId;
            if (currentPairId) {
                const newHistoryForCurrentPair = await getChatHistoryDB(currentPairId);
                setMessages(newHistoryForCurrentPair);
            } else {
                 const browserLangCode = (typeof navigator !== 'undefined' && navigator.language || 'en').substring(0, 2);
                 const defaultNative = ALL_LANGUAGES.find(l => l.langCode === browserLangCode) || ALL_LANGUAGES.find(l => l.langCode === DEFAULT_NATIVE_LANG_CODE)!;
                 setTempNativeLangCode(defaultNative.langCode);
                 setTempTargetLangCode(null);
                 setIsLanguageSelectionOpen(true);
            }
        } catch (e) {
            console.error("Failed to load chats:", e);
            alert(t('startPage.loadError'));
        }
    };
    reader.readAsText(file);
  }, [handleSaveAllChats, t]);

  const handleShowLanguageSelector = useCallback(() => {
    if (isSendingRef.current) return;
    setIsLanguageSelectionOpen(true);
    // Initialize temporary selection with current settings if available
    const currentPairId = settingsRef.current.selectedLanguagePairId;
    if (currentPairId) {
        const [target, native] = currentPairId.split('-');
        setTempNativeLangCode(native);
        setTempTargetLangCode(target);
    }
  }, []);

  const lastInteractionRef = useRef<number>(Date.now());
  const handleTempNativeSelect = useCallback((code: string | null) => {
      lastInteractionRef.current = Date.now();
      setTempNativeLangCode(code);
      if (code && code === tempTargetLangCode) {
          setTempTargetLangCode(null);
      }
  }, [tempTargetLangCode]);

  const handleTempTargetSelect = useCallback((code: string | null) => {
      lastInteractionRef.current = Date.now();
      setTempTargetLangCode(code);
  }, []);

  const handleConfirmLanguageSelection = useCallback(() => {
      if (!tempNativeLangCode || !tempTargetLangCode) return;
      const newPairId = `${tempTargetLangCode}-${tempNativeLangCode}`;
      const oldPairId = settingsRef.current.selectedLanguagePairId;
      const isSamePair = newPairId === oldPairId;

      if (!isSamePair && oldPairId) {
          // Save current history before switching
          safeSaveChatHistoryDB(oldPairId, messagesRef.current);
      }
      
      if (languagePairs.some(p => p.id === newPairId)) {
          handleSettingsChange('selectedLanguagePairId', newPairId);
      }
      setIsLanguageSelectionOpen(false);
  }, [tempNativeLangCode, tempTargetLangCode, languagePairs, handleSettingsChange]);

  // Restore auto-confirm functionality for language selection
  useEffect(() => {
    let timeout: number;
    if (isLanguageSelectionOpen && tempNativeLangCode && tempTargetLangCode) {
        timeout = window.setTimeout(() => {
            const idleTime = Date.now() - lastInteractionRef.current;
            if (idleTime >= 4500) { 
                handleConfirmLanguageSelection();
            }
        }, 5000);
    }
    return () => clearTimeout(timeout);
  }, [isLanguageSelectionOpen, tempNativeLangCode, tempTargetLangCode, handleConfirmLanguageSelection]);

  const [targetCode, nativeCode] = useMemo(() => (selectedLanguagePair ? selectedLanguagePair.id.split('-') : [DEFAULT_TARGET_LANG_CODE, DEFAULT_NATIVE_LANG_CODE]), [selectedLanguagePair]);
  const targetLanguageDef = useMemo(() => ALL_LANGUAGES.find(lang => lang.langCode === targetCode)!, [targetCode]);
  const nativeLanguageDef = useMemo(() => ALL_LANGUAGES.find(lang => lang.langCode === nativeCode)!, [nativeCode]);
  
  if (isLoadingHistory && settings.selectedLanguagePairId) {
    return (
        <div className="flex h-screen w-full items-center justify-center bg-slate-50">
            <div className="text-center">
                <div className="image-placeholder-spinner mx-auto"></div>
                <p className="mt-2 text-gray-600">{t('chat.loadingHistory')}</p>
            </div>
        </div>
    );
  }

  return (
    <div className="flex flex-col min-h-screen antialiased text-gray-800 bg-gray-100">
      <Header
        isTopbarOpen={isTopbarOpen}
        setIsTopbarOpen={setIsTopbarOpen}
        maestroActivityStage={maestroActivityStage}
        t={t}
        uiBusyTaskTags={uiBusyTaskTags}
        targetLanguageDef={targetLanguageDef}
        selectedLanguagePair={selectedLanguagePair}
        messages={messages}
        onLanguageSelectorClick={(e) => { e.stopPropagation(); handleShowLanguageSelector(); }}
        onToggleDebugLogs={() => setShowDebugLogs(prev => !prev)}
      />
      {showDebugLogs && <DebugLogPanel onClose={() => setShowDebugLogs(false)} />}
  <video ref={visualContextVideoRef} playsInline muted className="hidden w-px h-px" aria-hidden="true" />
      <div className="flex flex-1 overflow-hidden">
        <main className="flex-1 flex flex-col bg-slate-50">
            <ChatInterface
              messages={messages}
              onSendMessage={_handleSendMessageInternalRef.current}
              onDeleteMessage={handleDeleteMessage}
              updateMessage={updateMessage}
              onBookmarkAt={(id) => {
                setSettings(prev => {
                  const next = { ...prev, historyBookmarkMessageId: id } as typeof prev;
                  settingsRef.current = next;
                  setAppSettingsDB(next).catch(() => {});
                  return next;
                });
                const pairId = settingsRef.current.selectedLanguagePairId;
                if (pairId) {
                  (async () => { try { await setChatMetaDB(pairId, { bookmarkMessageId: id }); } catch {} })();
                }
              }}
              bookmarkedMessageId={settingsRef.current.historyBookmarkMessageId || null}
              maxVisibleMessages={settingsRef.current.maxVisibleMessages ?? MAX_VISIBLE_MESSAGES_DEFAULT}
              onChangeMaxVisibleMessages={(n) => {
                const clamped = Math.max(1, Math.min(100, Math.floor(n || MAX_VISIBLE_MESSAGES_DEFAULT)));
                setSettings(prev => { const next = { ...prev, maxVisibleMessages: clamped } as typeof prev; settingsRef.current = next; setAppSettingsDB(next).catch(() => {}); return next; });
              }}
              isSending={isSending}
              bubbleWrapperRefs={bubbleWrapperRefs}
              
              isLanguageSelectionOpen={isLanguageSelectionOpen}
              tempNativeLangCode={tempNativeLangCode}
              tempTargetLangCode={tempTargetLangCode}
              onTempNativeSelect={handleTempNativeSelect}
              onTempTargetSelect={handleTempTargetSelect}
              onConfirmLanguageSelection={handleConfirmLanguageSelection}

              onSaveAllChats={handleSaveAllChats}
              onLoadAllChats={handleLoadAllChats}
              loadingGifs={loadingGifs}

              attachedImageBase64={attachedImageBase64}
              attachedImageMimeType={attachedImageMimeType}
              onSetAttachedImage={handleSetAttachedImage}

              isSttSupported={effectiveSttSupported}
              isSttGloballyEnabled={settings.stt.enabled}
              isListening={isListening}
              sttError={sttError}
              transcript={transcript}
              onSttToggle={sttMasterToggle}
              clearTranscript={clearTranscript}
              sttLanguageCode={settings.stt.language}
              onSttLanguageChange={handleSttLanguageChange}
              targetLanguageDef={targetLanguageDef}
              nativeLanguageDef={nativeLanguageDef}

              isTtsSupported={isSpeechSynthesisSupported}
              isSpeaking={isSpeaking}
              speakText={speakWrapper}
              stopSpeaking={stopSpeaking}
              speakingUtteranceText={speakingUtteranceText}

              speakNativeLang={settings.tts.speakNative}
              onToggleSpeakNativeLang={handleToggleSpeakNativeLang}

              currentTargetLangCode={getPrimaryCode(selectedLanguagePair?.targetLanguageCode || targetLanguageDef.code)}
              currentNativeLangCode={getPrimaryCode(selectedLanguagePair?.nativeLanguageCode || nativeLanguageDef.code)}
              currentNativeLangForTranslations={getPrimaryCode(selectedLanguagePair?.nativeLanguageCode || nativeLanguageDef.code)}
              latestGroundingChunks={latestGroundingChunks}
              onUserInputActivity={handleUserInputActivity}
              autoCaptureError={visualContextCameraError}
              selectedCameraId={settings.selectedCameraId}
              currentCameraFacingMode={currentCameraFacingMode}
              snapshotUserError={snapshotUserError}
              onToggleSendWithSnapshot={handleToggleSendWithSnapshot}
              sendWithSnapshotEnabled={settings.sendWithSnapshotEnabled}
              onToggleUseVisualContextForReengagement={handleToggleUseVisualContextForReengagement}
              useVisualContextForReengagementEnabled={settings.smartReengagement.useVisualContext}
              availableCameras={availableCameras}
              onSelectCamera={(id) => handleSettingsChange('selectedCameraId', id)}

              replySuggestions={replySuggestions}
              isLoadingSuggestions={isLoadingSuggestions}
              onSuggestionClick={handleSuggestionInteraction}

              maestroActivityStage={maestroActivityStage}
              t={t}

              imageGenerationModeEnabled={settings.imageGenerationModeEnabled}
              imageFocusedModeEnabled={settings.imageFocusedModeEnabled}
              onToggleImageGenerationMode={handleToggleImageGenerationMode}
              onToggleImageFocusedMode={handleToggleImageFocusedMode}
              transitioningImageId={transitioningImageId}
              estimatedImageLoadTime={calculateEstimatedImageLoadTime()}
              isImageGenCameraSelected={settings.selectedCameraId === IMAGE_GEN_CAMERA_ID}
              liveVideoStream={liveVideoStream}
              liveSessionState={liveSessionState}
              liveSessionError={liveSessionError}
              onStartLiveSession={handleStartLiveSession}
              onStopLiveSession={handleStopLiveSession}
              
              isSuggestionMode={settings.isSuggestionMode}
              onToggleSuggestionMode={handleToggleSuggestionMode}
              onCreateSuggestion={handleCreateSuggestion}
              isCreatingSuggestion={isCreatingSuggestion}
              sendPrep={sendPrep}
              onUiTaskStart={onUiTaskStart}
              onUiTaskEnd={onUiTaskEnd}

              sttProvider={settings.stt.provider || 'browser'}
              ttsProvider={settings.tts.provider || 'browser'}
              onToggleSttProvider={toggleSttProvider}
              onToggleTtsProvider={toggleTtsProvider}
              isSpeechRecognitionSupported={!!window.SpeechRecognition || !!window.webkitSpeechRecognition}
            />
        </main>
      </div>
    </div>
  );
};

export default App;
