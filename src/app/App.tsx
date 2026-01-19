// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * App.tsx - The Composition Root
 * 
 * This component is now a "Hollow Shell" that:
 * 1. Composes all the orchestration hooks
 * 2. Passes data/handlers to the UI layer
 * 3. Contains minimal business logic
 * 
 * The actual logic has been extracted into specialized hooks in src/app/hooks/
 */
import React, { useState, useEffect, useCallback, useRef, useMemo } from 'react';

// --- Features Components ---
import ChatInterface from '../features/chat/components/ChatInterface';
import Header from '../features/session/components/Header';
import DebugLogPanel from '../features/diagnostics/components/DebugLogPanel';

// --- Hooks ---
import {
  useTranslations,
  useAppSettings,
  useChatStore,
  useHardware,
  useSpeechController,
  useMaestroController,
  useUiBusyState,
  useLiveSession,
  useDataBackup,
  MAX_VISIBLE_MESSAGES_DEFAULT,
} from './hooks';

// --- Feature Hooks ---
import { useSmartReengagement } from '../features/session/hooks/useSmartReengagement';

// --- Services ---
import { setAppSettingsDB } from '../features/session/services/settings';
import { safeSaveChatHistoryDB, setChatMetaDB } from '../features/chat/services/chatHistory';
import { getLoadingGifsDB as getAssetsLoadingGifs, getMaestroProfileImageDB, setMaestroProfileImageDB } from '../core/db/assets';

// --- Config ---
import { APP_TITLE_KEY, IMAGE_GEN_CAMERA_ID } from '../core/config/app';
import { ALL_LANGUAGES, DEFAULT_NATIVE_LANG_CODE, DEFAULT_TARGET_LANG_CODE } from '../core/config/languages';

// --- Utils ---
import { uniq, fetchDefaultAvatarBlob } from '../shared/utils/common';
import { getPrimaryCode } from '../shared/utils/languageUtils';

const App: React.FC = () => {
  // ============================================================
  // REFS - Declared before hooks
  // ============================================================
  
  // These refs are used for visual context capture state
  const isCurrentlyPerformingVisualContextCaptureRef = useRef(false);

  // ============================================================
  // HOOK COMPOSITION - The Controller Layer
  // ============================================================

  // --- UI Busy State ---
  const {
    uiBusyTaskTags,
    externalUiTaskCount,
    addUiBusyToken,
    removeUiBusyToken,
    clearUiBusyTokens,
    handleToggleHold,
  } = useUiBusyState();

  // --- Settings ---
  const {
    settings,
    settingsRef,
    handleSettingsChange,
    setSettings,
    languagePairs,
    selectedLanguagePair,
    selectedLanguagePairRef,
    isSettingsLoaded,
  } = useAppSettings();

  // --- Translations ---
  const nativeLangForTranslations = useMemo(() => {
    if (selectedLanguagePair) {
      return getPrimaryCode(selectedLanguagePair.nativeLanguageCode);
    }
    const browserLang = (typeof navigator !== 'undefined' ? navigator.language : 'en').substring(0, 2);
    return browserLang || 'en';
  }, [selectedLanguagePair]);

  const { t } = useTranslations(nativeLangForTranslations);

  // --- Chat Store ---
  const {
    messages,
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
    isLoadingSuggestions,
    setIsLoadingSuggestions,
    isLoadingSuggestionsRef,
  } = useChatStore({
    t,
    selectedLanguagePairId: settings.selectedLanguagePairId,
    settingsRef,
    setSettings,
  });

  // --- Hardware ---
  const {
    availableCameras,
    availableCamerasRef,
    currentCameraFacingMode,
    liveVideoStream,
    setLiveVideoStream,
    visualContextVideoRef,
    visualContextStreamRef,
    visualContextCameraError,
    snapshotUserError,
    setSnapshotUserError,
    captureSnapshot,
    microphoneApiAvailable,
  } = useHardware({
    t,
    sendWithSnapshotEnabled: settings.sendWithSnapshotEnabled,
    useVisualContext: settings.smartReengagement.useVisualContext,
    selectedCameraId: settings.selectedCameraId,
    settingsRef,
  });

  // --- Local State (UI-Only) ---
  const [isLanguageSelectionOpen, setIsLanguageSelectionOpen] = useState(false);
  const [tempNativeLangCode, setTempNativeLangCode] = useState<string | null>(null);
  const [tempTargetLangCode, setTempTargetLangCode] = useState<string | null>(null);
  const [isTopbarOpen, setIsTopbarOpen] = useState(false);
  const [showDebugLogs, setShowDebugLogs] = useState(false);
  const [loadingGifs, setLoadingGifs] = useState<string[]>([]);
  const [attachedImageBase64, setAttachedImageBase64] = useState<string | null>(null);
  const [attachedImageMimeType, setAttachedImageMimeType] = useState<string | null>(null);
  const [isUserActive, setIsUserActive] = useState(false);
  const [transitioningImageId, setTransitioningImageId] = useState<string | null>(null);
  const [currentSystemPromptText, setCurrentSystemPromptText] = useState('');
  const [currentReplySuggestionsPromptText, setCurrentReplySuggestionsPromptText] = useState('');

  // --- Refs ---
  const bubbleWrapperRefs = useRef<Map<string, HTMLDivElement>>(new Map());
  const isUserActiveRef = useRef<boolean>(false);
  const userActivityTimerRef = useRef<number | null>(null);
  const autoSendTimerRef = useRef<number | null>(null);
  const autoSendSnapshotRef = useRef<string>('');
  const lastInteractionRef = useRef<number>(Date.now());
  const maestroAvatarUriRef = useRef<string | null>(null);
  const maestroAvatarMimeTypeRef = useRef<string | null>(null);
  const prevIsListeningRef = useRef<boolean>(false);
  const wasSpeakingRef = useRef<boolean>(false);
  const STT_STABLE_NO_TEXT_MS = 4000;

  // Sync refs
  useEffect(() => { isUserActiveRef.current = isUserActive; }, [isUserActive]);

  // Create a stable isSendingRef that will be shared across hooks
  const sharedIsSendingRef = useRef<boolean>(false);
  
  // Re-engagement callbacks refs - will be populated after useSmartReengagement
  const scheduleReengagementRef = useRef<(reason: string, delayOverrideMs?: number) => void>(() => {});
  const cancelReengagementRef = useRef<() => void>(() => {});
  
  // Toggle suggestion mode callback ref - will be populated after handleToggleSuggestionMode is defined
  const handleToggleSuggestionModeRef = useRef<((forceState?: boolean) => void) | undefined>(undefined);

  // --- Speech Controller ---
  // NOTE: Moved before useSmartReengagement to provide speechIsSpeakingRef
  const {
    isSpeaking,
    stopSpeaking,
    isSpeechSynthesisSupported,
    speakingUtteranceText,
    hasPendingQueueItems,
    isListening,
    transcript,
    startListening,
    stopListening,
    sttError,
    isSpeechRecognitionSupported,
    clearTranscript,
    claimRecordedUtterance,
    speechIsSpeakingRef,
    recordedUtterancePendingRef,
    pendingRecordedAudioMessageRef,
    sttInterruptedBySendRef,
    speakMessage,
    speakWrapper,
  } = useSpeechController({
    settingsRef,
    messagesRef,
    selectedLanguagePairRef,
    isSendingRef: sharedIsSendingRef, // Use the shared ref
    lastFetchedSuggestionsForRef,
    replySuggestionsRef,
    upsertMessageTtsCache,
    upsertSuggestionTtsCache,
    setMessages,
  });
  
  // --- Maestro Controller ---
  const {
    isSending,
    isSendingRef,
    sendPrep,
    latestGroundingChunks,
    maestroActivityStage,
    isCreatingSuggestion,
    handleSendMessageInternal,
    handleSendMessageInternalRef,
    handleCreateSuggestion,
    handleSuggestionInteraction,
    setMaestroActivityStage,
    parseGeminiResponse,
    resolveBookmarkContextSummary,
    computeHistorySubsetForMedia,
    calculateEstimatedImageLoadTime,
    fetchAndSetReplySuggestions,
  } = useMaestroController({
    t,
    settingsRef,
    setSettings,
    selectedLanguagePairRef,
    messagesRef,
    addMessage,
    updateMessage,
    setMessages,
    isLoadingHistoryRef,
    getHistoryRespectingBookmark,
    computeMaxMessagesForArray,
    lastFetchedSuggestionsForRef,
    captureSnapshot,
    speechIsSpeakingRef,
    speakMessage,
    isSpeechSynthesisSupported,
    isListening,
    stopListening,
    startListening,
    clearTranscript,
    hasPendingQueueItems,
    claimRecordedUtterance,
    sttInterruptedBySendRef,
    recordedUtterancePendingRef,
    pendingRecordedAudioMessageRef,
    scheduleReengagementRef, // Pass the ref - will be populated after useSmartReengagement
    cancelReengagementRef, // Pass the ref - will be populated after useSmartReengagement
    setAttachedImageBase64,
    setAttachedImageMimeType,
    attachedImageBase64,
    attachedImageMimeType,
    transcript,
    currentSystemPromptText,
    currentReplySuggestionsPromptText,
    replySuggestions,
    setReplySuggestions,
    isLoadingSuggestions,
    setIsLoadingSuggestions,
    isLoadingSuggestionsRef,
    handleToggleSuggestionModeRef, // Pass the ref - will be populated after handleToggleSuggestionMode is defined
    maestroAvatarUriRef, // Pass avatar refs loaded in App.tsx
    maestroAvatarMimeTypeRef,
    setSnapshotUserError, // Pass hardware error setter
  });

  // Sync the shared isSendingRef with isSending state from useMaestroController
  useEffect(() => { sharedIsSendingRef.current = isSending; }, [isSending]);
  
  // --- Smart Reengagement ---
  // NOTE: Moved AFTER useMaestroController to have access to isSending, isSpeaking
  const triggerReengagementSequence = useCallback(async () => {
    // Guard conditions - don't re-engage if busy
    if (isLoadingHistoryRef.current || isSendingRef.current || speechIsSpeakingRef.current || isCurrentlyPerformingVisualContextCaptureRef.current) {
      return;
    }

    setReplySuggestions([]);
    setIsLoadingSuggestions(false);
    lastFetchedSuggestionsForRef.current = null;

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
  }, [captureSnapshot, settingsRef, visualContextStreamRef, handleSendMessageInternal, isLoadingHistoryRef, isSendingRef, speechIsSpeakingRef]);

  const {
    reengagementPhase,
    scheduleReengagement,
    cancelReengagement,
    handleUserActivity,
    isReengagementToken: _isReengagementToken,
    setReengagementPhase: _setReengagementPhase,
  } = useSmartReengagement({
    settings,
    isLoadingHistory,
    selectedLanguagePairId: settings.selectedLanguagePairId,
    isSending, // ACTUAL VALUE - not hardcoded false
    isSpeaking, // ACTUAL VALUE - not hardcoded false
    isSendingRef,
    isSpeakingRef: speechIsSpeakingRef,
    isVisualContextActive: isCurrentlyPerformingVisualContextCaptureRef.current,
    externalUiTaskCount,
    triggerReengagementSequence,
    addUiBusyToken,
    removeUiBusyToken,
  });

  // CRITICAL: Sync re-engagement callbacks to refs for useMaestroController
  useEffect(() => {
    scheduleReengagementRef.current = scheduleReengagement;
    cancelReengagementRef.current = cancelReengagement;
  }, [scheduleReengagement, cancelReengagement]);

  void _isReengagementToken;
  void _setReengagementPhase;

  // ============================================================
  // EFFECTS - Side Effects and Synchronization
  // ============================================================

  // Set document title
  useEffect(() => {
    document.title = t(APP_TITLE_KEY);
  }, [t]);

  // Remove splash screen
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

  // Update system prompts when language pair changes
  useEffect(() => {
    if (!selectedLanguagePair) return;
    setCurrentSystemPromptText(selectedLanguagePair.baseSystemPrompt);
    setCurrentReplySuggestionsPromptText(selectedLanguagePair.baseReplySuggestionsPrompt);
  }, [selectedLanguagePair]);

  // Load initial language selection if needed
  useEffect(() => {
    if (isSettingsLoaded && !settings.selectedLanguagePairId) {
      const browserLangCode = (typeof navigator !== 'undefined' && navigator.language || 'en').substring(0, 2);
      const defaultNative = ALL_LANGUAGES.find(l => l.langCode === browserLangCode) || 
                           ALL_LANGUAGES.find(l => l.langCode === DEFAULT_NATIVE_LANG_CODE)!;
      setTempNativeLangCode(defaultNative.langCode);
      setTempTargetLangCode(null);
      setIsLanguageSelectionOpen(true);
    }
  }, [isSettingsLoaded, settings.selectedLanguagePairId]);

  // Load Maestro avatar
  useEffect(() => {
    (async () => {
      try {
        const a = await getMaestroProfileImageDB();
        if (a && (a.dataUrl || a.uri)) {
          maestroAvatarUriRef.current = a.uri || null;
          maestroAvatarMimeTypeRef.current = (a?.mimeType && typeof a.mimeType === 'string')
            ? a.mimeType
            : (a?.dataUrl?.startsWith('data:image/') ? 'image/svg+xml' : null);
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
              try {
                window.dispatchEvent(new CustomEvent('maestro-avatar-updated', {
                  detail: { dataUrl: defaultDataUrl, mimeType: defaultMime, uri: undefined }
                }));
              } catch { /* ignore */ }
            } else {
              maestroAvatarUriRef.current = null;
              maestroAvatarMimeTypeRef.current = null;
            }
          } catch {
            maestroAvatarUriRef.current = null;
            maestroAvatarMimeTypeRef.current = null;
          }
        }
      } catch {
        maestroAvatarUriRef.current = null;
        maestroAvatarMimeTypeRef.current = null;
      }
    })();
  }, []);

  useEffect(() => {
    const handler = (event: any) => {
      try {
        const uri = event?.detail?.uri as string | undefined;
        const mimeType = event?.detail?.mimeType as string | undefined;
        maestroAvatarUriRef.current = uri || null;
        if (mimeType && typeof mimeType === 'string') {
          maestroAvatarMimeTypeRef.current = mimeType;
        }
      } catch { /* ignore */ }
    };
    window.addEventListener('maestro-avatar-updated', handler as any);
    return () => window.removeEventListener('maestro-avatar-updated', handler as any);
  }, []);

  // Load loading gifs from DB and manifest
  useEffect(() => {
    (async () => {
      try {
        const current = (await getAssetsLoadingGifs()) || [];
        let manifest: string[] = [];
        try { 
          const resp = await fetch('/gifs/manifest.json', { cache: 'force-cache' }); 
          if (resp.ok) manifest = await resp.json(); 
        } catch { /* ignore */ }
        const merged = uniq([...current, ...manifest]);
        setLoadingGifs(merged);
      } catch { /* ignore */ }
    })();
  }, []);

  // Update Maestro activity stage
  useEffect(() => {
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
  }, [isSpeaking, isSending, isListening, isUserActive, reengagementPhase, externalUiTaskCount, setMaestroActivityStage]);

  // Sync replySuggestions to ref for use by useSpeechController
  useEffect(() => {
    replySuggestionsRef.current = replySuggestions;
  }, [replySuggestions]);

  // --- CRITICAL: Auto-Send on Silence (STT) ---
  // When STT is enabled and transcript is stable for STT_STABLE_NO_TEXT_MS, auto-send
  useEffect(() => {
    if (!settingsRef.current.stt.enabled) {
      if (autoSendTimerRef.current) {
        clearTimeout(autoSendTimerRef.current);
        autoSendTimerRef.current = null;
      }
      autoSendSnapshotRef.current = '';
      return;
    }

    const stripBracketedContent = (input: string | undefined | null): string => {
      if (typeof input !== 'string') return '';
      const without = input.replace(/\[[^\]]*\]/g, ' ');
      return without.replace(/\s+/g, ' ').trim();
    };

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
          handleSendMessageInternal(current, attachedImageBase64 || undefined, attachedImageMimeType || undefined, 'user');
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
  }, [transcript, attachedImageBase64, attachedImageMimeType, handleCreateSuggestion, clearTranscript, handleSendMessageInternal, settingsRef, STT_STABLE_NO_TEXT_MS]);

  // --- CRITICAL: Auto-Fetch Suggestions on TTS End ---
  // When TTS stops speaking, fetch reply suggestions for the last assistant message
  useEffect(() => {
    const wasSpeaking = wasSpeakingRef.current;
    wasSpeakingRef.current = isSpeaking;

    if (wasSpeaking && !isSpeaking) {
      // TTS just finished - don't fetch if already loading suggestions
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
  }, [isSpeaking, messagesRef, lastFetchedSuggestionsForRef, fetchAndSetReplySuggestions, getHistoryRespectingBookmark]);

  // --- CRITICAL: STT Auto-Restart in Suggestion Mode ---
  // If STT stops while in suggestion mode, restart it to keep the walkie-talkie flow active
  useEffect(() => {
    const wasListening = prevIsListeningRef.current;
    prevIsListeningRef.current = isListening;

    if (wasListening && !isListening) {
      // STT just stopped
      if (settingsRef.current.isSuggestionMode && settingsRef.current.stt.enabled) {
        // Restart STT to keep suggestion mode active
        setTimeout(() => {
          if (settingsRef.current.stt.enabled && settingsRef.current.isSuggestionMode) {
            startListening(settingsRef.current.stt.language);
          }
        }, 100);
      }
    }
  }, [isListening, startListening, settingsRef]);

  // --- Reengagement scheduling when idle ---
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
  }, [selectedLanguagePair, isSpeaking, isSending, isListening, isUserActive, reengagementPhase, cancelReengagement, scheduleReengagement]);

  // ============================================================
  // HANDLERS - Event Handlers and Callbacks
  // ============================================================

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
  }, [handleUserActivity, setMaestroActivityStage]);

  const handleSetAttachedImage = useCallback((base64: string | null, mimeType: string | null) => {
    setAttachedImageBase64(base64);
    setAttachedImageMimeType(mimeType);
  }, []);

  const handleDeleteMessage = useCallback((messageId: string) => {
    deleteMessage(messageId);
  }, [deleteMessage]);

  const handleShowLanguageSelector = useCallback(() => {
    if (isSendingRef.current) return;
    setIsLanguageSelectionOpen(true);
    const currentPairId = settingsRef.current.selectedLanguagePairId;
    if (currentPairId) {
      const [target, native] = currentPairId.split('-');
      setTempNativeLangCode(native);
      setTempTargetLangCode(target);
    }
  }, [isSendingRef, settingsRef]);

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
      safeSaveChatHistoryDB(oldPairId, messagesRef.current);
    }

    if (languagePairs.some(p => p.id === newPairId)) {
      handleSettingsChange('selectedLanguagePairId', newPairId);
    }
    setIsLanguageSelectionOpen(false);
  }, [tempNativeLangCode, tempTargetLangCode, languagePairs, handleSettingsChange, settingsRef, messagesRef]);

  // Auto-confirm language selection after idle
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

  const handleToggleSuggestionMode = useCallback((forceState?: boolean) => {
    const newIsSuggestionMode = typeof forceState === 'boolean' ? forceState : !settingsRef.current.isSuggestionMode;
    if (newIsSuggestionMode === settingsRef.current.isSuggestionMode) return;

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

    if (langDidChange && sttShouldBeActive && isListening) {
      stopListening();
      setTimeout(() => {
        if (settingsRef.current.stt.enabled) {
          clearTranscript();
          startListening(newSttLang);
        }
      }, 250);
    } else if (langDidChange) {
      clearTranscript();
    }
  }, [isListening, stopListening, startListening, clearTranscript, settingsRef, selectedLanguagePairRef, setSettings]);

  // CRITICAL: Sync handleToggleSuggestionMode to ref for useMaestroController
  useEffect(() => {
    handleToggleSuggestionModeRef.current = handleToggleSuggestionMode;
  }, [handleToggleSuggestionMode]);

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
  }, [clearTranscript, startListening, stopListening, settingsRef, setSettings]);

  const handleSttLanguageChange = useCallback((langCode: string) => {
    const currentSttSettings = settingsRef.current.stt;
    const sttShouldBeActive = currentSttSettings.enabled;

    if (sttShouldBeActive && isListening) {
      stopListening();
    }
    setSettings(prev => ({ ...prev, stt: { ...prev.stt, language: langCode } }));

    if (sttShouldBeActive) {
      setTimeout(() => {
        if (settingsRef.current.stt.enabled) {
          clearTranscript();
          startListening(langCode);
        }
      }, 250);
    } else {
      clearTranscript();
    }
  }, [isListening, stopListening, clearTranscript, startListening, settingsRef, setSettings]);

  const toggleSttProvider = useCallback(() => {
    const next = settings.stt.provider === 'browser' ? 'gemini' : 'browser';
    handleSettingsChange('stt', { ...settings.stt, provider: next });
  }, [settings.stt, handleSettingsChange]);

  const toggleTtsProvider = useCallback(() => {
    const next = settings.tts.provider === 'browser' ? 'gemini' : 'browser';
    handleSettingsChange('tts', { ...settings.tts, provider: next });
  }, [settings.tts, handleSettingsChange]);

  const handleToggleSendWithSnapshot = useCallback(() => {
    handleSettingsChange('sendWithSnapshotEnabled', !settingsRef.current.sendWithSnapshotEnabled);
  }, [handleSettingsChange, settingsRef]);

  const handleToggleUseVisualContextForReengagement = useCallback(() => {
    handleSettingsChange('smartReengagement', {
      ...settingsRef.current.smartReengagement,
      useVisualContext: !settingsRef.current.smartReengagement.useVisualContext,
    });
  }, [handleSettingsChange, settingsRef]);

  const handleToggleSpeakNativeLang = useCallback(() => {
    handleSettingsChange('tts', {
      ...settingsRef.current.tts,
      speakNative: !settingsRef.current.tts.speakNative,
    });
  }, [handleSettingsChange, settingsRef]);

  const handleToggleImageGenerationMode = useCallback(() => {
    const willBeEnabled = !settingsRef.current.imageGenerationModeEnabled;
    handleSettingsChange('imageGenerationModeEnabled', willBeEnabled);
    if (!willBeEnabled && settingsRef.current.selectedCameraId === IMAGE_GEN_CAMERA_ID) {
      const firstPhysicalCamera = availableCamerasRef.current[0];
      handleSettingsChange('selectedCameraId', firstPhysicalCamera ? firstPhysicalCamera.deviceId : null);
    }
  }, [handleSettingsChange, settingsRef, availableCamerasRef]);

  const _toggleFocusedModeState = useCallback(() => {
    handleSettingsChange('imageFocusedModeEnabled', !settingsRef.current.imageFocusedModeEnabled);
  }, [handleSettingsChange, settingsRef]);

  const handleToggleImageFocusedMode = useCallback((messageId: string) => {
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
  }, [_toggleFocusedModeState]);

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

  // ============================================================
  // GEMINI LIVE SESSION HANDLING
  // ============================================================

  const {
    liveSessionState,
    liveSessionError,
    handleStartLiveSession,
    handleStopLiveSession,
  } = useLiveSession({
    t,
    settingsRef,
    setSettings,
    selectedLanguagePairRef,
    messagesRef,
    addMessage,
    updateMessage,
    getHistoryRespectingBookmark,
    computeMaxMessagesForArray,
    lastFetchedSuggestionsForRef,
    fetchAndSetReplySuggestions,
    upsertMessageTtsCache,
    liveVideoStream,
    setLiveVideoStream,
    visualContextVideoRef,
    visualContextStreamRef,
    captureSnapshot,
    isListening,
    stopListening,
    startListening,
    clearTranscript,
    addUiBusyToken,
    removeUiBusyToken,
    scheduleReengagement,
    cancelReengagement,
    handleUserInputActivity: handleUserInputActivity,
    currentSystemPromptText,
    parseGeminiResponse,
    resolveBookmarkContextSummary,
    computeHistorySubsetForMedia,
    maestroAvatarUriRef,
    maestroAvatarMimeTypeRef,
  });

  // --- Data Backup ---
  const {
    handleSaveAllChats,
    handleLoadAllChats,
  } = useDataBackup({
    t,
    settingsRef,
    messagesRef,
    setMessages,
    setLoadingGifs,
    setTempNativeLangCode,
    setTempTargetLangCode,
    setIsLanguageSelectionOpen,
  });

  // ============================================================
  // COMPUTED VALUES
  // ============================================================

  const [targetCode, nativeCode] = useMemo(() => 
    (selectedLanguagePair ? selectedLanguagePair.id.split('-') : [DEFAULT_TARGET_LANG_CODE, DEFAULT_NATIVE_LANG_CODE]), 
    [selectedLanguagePair]
  );
  const targetLanguageDef = useMemo(() => ALL_LANGUAGES.find(lang => lang.langCode === targetCode)!, [targetCode]);
  const nativeLanguageDef = useMemo(() => ALL_LANGUAGES.find(lang => lang.langCode === nativeCode)!, [nativeCode]);

  const activeSttProvider = settings.stt.provider || 'browser';
  const browserSttAvailable = isSpeechRecognitionSupported;
  const effectiveSttSupported = activeSttProvider === 'browser' ? browserSttAvailable : microphoneApiAvailable;

  // ============================================================
  // RENDER
  // ============================================================

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
        onToggleHold={handleToggleHold}
      />
      {showDebugLogs && <DebugLogPanel onClose={() => setShowDebugLogs(false)} />}
      <video ref={visualContextVideoRef} playsInline muted className="hidden w-px h-px" aria-hidden="true" />
      <div className="flex flex-1 overflow-hidden">
        <main className="flex-1 flex flex-col bg-slate-50">
          <ChatInterface
            messages={messages}
            onSendMessage={handleSendMessageInternalRef.current || handleSendMessageInternal}
            onDeleteMessage={handleDeleteMessage}
            updateMessage={updateMessage}
            onBookmarkAt={(id) => {
              setSettings(prev => {
                const next = { ...prev, historyBookmarkMessageId: id };
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
              setSettings(prev => { 
                const next = { ...prev, maxVisibleMessages: clamped }; 
                settingsRef.current = next; 
                setAppSettingsDB(next).catch(() => {}); 
                return next; 
              });
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
            onSuggestionClick={(suggestion, langType) => {
              handleSuggestionInteraction(suggestion, langType);
              if (!speechIsSpeakingRef.current && selectedLanguagePairRef.current) {
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
                  speakWrapper([{ text: textToSpeak, langCode: langCodeToUse, context }], langCodeToUse);
                }
              }
              handleUserInputActivity();
            }}

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
