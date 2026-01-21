// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * AppShell.tsx - The Composition Root
 * 
 * This component is now a "Hollow Shell" that:
 * 1. Composes all the orchestration hooks
 * 2. Passes data/handlers to the UI layer
 * 3. Contains minimal business logic
 * 
 * The actual logic has been extracted into specialized hooks in src/app/hooks/
 */
import React, { useEffect, useCallback, useRef, useMemo } from 'react';
import { useShallow } from 'zustand/shallow';

// --- Features Components ---
import { ChatInterface } from '../features/chat';
import { Header, useSmartReengagement } from '../features/session';
import { DebugLogPanel } from '../features/diagnostics';

// --- Hooks ---
import {
  useTranslations,
  useAppLifecycle,
  useAppAssets,
  useMaestroActivityStage,
  useAutoSendOnSilence,
  useAutoFetchSuggestions,
  useSuggestionModeAutoRestart,
  useIdleReengagement,
  useLanguageSelectionController,
  useAppSettings,
  useChatStore,
  useHardware,
  useSpeechController,
  useMaestroController,
  useUiBusyState,
  useLiveSession,
  useDataBackup,
  MAX_VISIBLE_MESSAGES_DEFAULT,
  useMaestroStore,
} from './hooks';

// --- Feature Hooks ---
// --- Services ---
import { setChatMetaDB } from '../features/chat';

// --- Config ---
import { IMAGE_GEN_CAMERA_ID } from '../core/config/app';
import { ALL_LANGUAGES, DEFAULT_NATIVE_LANG_CODE, DEFAULT_TARGET_LANG_CODE } from '../core/config/languages';

// --- Utils ---
import { getPrimaryCode } from '../shared/utils/languageUtils';

const AppShell: React.FC = () => {
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

  useAppLifecycle(t);

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

  const {
    isLanguageSelectionOpen,
    tempNativeLangCode,
    tempTargetLangCode,
    languageSelectorLastInteraction,
    isTopbarOpen,
    loadingGifs,
    transitioningImageId,
  } = useMaestroStore(useShallow(state => ({
    isLanguageSelectionOpen: state.isLanguageSelectionOpen,
    tempNativeLangCode: state.tempNativeLangCode,
    tempTargetLangCode: state.tempTargetLangCode,
    languageSelectorLastInteraction: state.languageSelectorLastInteraction,
    isTopbarOpen: state.isTopbarOpen,
    loadingGifs: state.loadingGifs,
    transitioningImageId: state.transitioningImageId,
  })));

  const showDebugLogs = useMaestroStore(state => state.showDebugLogs);
  const attachedImageBase64 = useMaestroStore(state => state.attachedImageBase64);
  const attachedImageMimeType = useMaestroStore(state => state.attachedImageMimeType);
  const isUserActive = useMaestroStore(state => state.isUserActive);
  
  // Compute derived values in the selector to avoid Zustand getter issues
  const currentSystemPromptText = useMaestroStore(state => {
    const pair = state.languagePairs.find(p => p.id === state.settings.selectedLanguagePairId);
    return pair?.baseSystemPrompt || '';
  });
  const currentReplySuggestionsPromptText = useMaestroStore(state => {
    const pair = state.languagePairs.find(p => p.id === state.settings.selectedLanguagePairId);
    return pair?.baseReplySuggestionsPrompt || '';
  });

  const setIsLanguageSelectionOpen = useMaestroStore(state => state.setIsLanguageSelectionOpen);
  const setTempNativeLangCode = useMaestroStore(state => state.setTempNativeLangCode);
  const setTempTargetLangCode = useMaestroStore(state => state.setTempTargetLangCode);
  const setIsTopbarOpen = useMaestroStore(state => state.setIsTopbarOpen);
  const setLoadingGifs = useMaestroStore(state => state.setLoadingGifs);
  const setTransitioningImageId = useMaestroStore(state => state.setTransitioningImageId);
  const setShowDebugLogs = useMaestroStore(state => state.setShowDebugLogs);
  const toggleDebugLogs = useMaestroStore(state => state.toggleDebugLogs);
  const setAttachedImage = useMaestroStore(state => state.setAttachedImage);
  const setMaestroAvatar = useMaestroStore(state => state.setMaestroAvatar);

  // --- Refs ---
  const bubbleWrapperRefs = useRef<Map<string, HTMLDivElement>>(new Map());
  const maestroAvatarUriRef = useRef<string | null>(null);
  const maestroAvatarMimeTypeRef = useRef<string | null>(null);
  const STT_STABLE_NO_TEXT_MS = 4000;

  useAppAssets({
    setLoadingGifs,
    setMaestroAvatar,
    maestroAvatarUriRef,
    maestroAvatarMimeTypeRef,
  });

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
    transcript,
    currentSystemPromptText,
    currentReplySuggestionsPromptText,
    setReplySuggestions,
    setIsLoadingSuggestions,
    isLoadingSuggestionsRef,
    handleToggleSuggestionModeRef, // Pass the ref - will be populated after handleToggleSuggestionMode is defined
    maestroAvatarUriRef, // Pass avatar refs loaded in App.tsx
    maestroAvatarMimeTypeRef,
    setSnapshotUserError, // Pass hardware error setter
  });

  const {
    handleShowLanguageSelector,
    handleTempNativeSelect,
    handleTempTargetSelect,
    handleConfirmLanguageSelection,
  } = useLanguageSelectionController({
    isSettingsLoaded,
    settings,
    settingsRef,
    isSendingRef,
    languagePairs,
    handleSettingsChange,
    messagesRef,
    isLanguageSelectionOpen,
    tempNativeLangCode,
    tempTargetLangCode,
    languageSelectorLastInteraction,
    setIsLanguageSelectionOpen,
    setTempNativeLangCode,
    setTempTargetLangCode,
  });

  const { clearAutoSend } = useAutoSendOnSilence({
    settingsRef,
    transcript,
    attachedImageBase64,
    attachedImageMimeType,
    isSendingRef,
    speechIsSpeakingRef,
    clearTranscript,
    handleCreateSuggestion,
    handleSendMessageInternal,
    stableMs: STT_STABLE_NO_TEXT_MS,
  });

  useAutoFetchSuggestions({
    isSpeaking,
    messagesRef,
    lastFetchedSuggestionsForRef,
    isLoadingSuggestionsRef,
    fetchAndSetReplySuggestions,
    getHistoryRespectingBookmark,
  });

  useSuggestionModeAutoRestart({
    isListening,
    settingsRef,
    startListening,
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
    // Intentionally unused - destructured to prevent "unused export" warnings in hook
    isReengagementToken: _unusedIsReengagementToken,
    setReengagementPhase: _unusedSetReengagementPhase,
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

  useIdleReengagement({
    selectedLanguagePair,
    isSpeaking,
    isSending,
    isListening,
    isUserActive,
    reengagementPhase,
    scheduleReengagement,
    cancelReengagement,
  });

  useMaestroActivityStage({
    externalUiTaskCount,
    isSpeaking,
    isSending,
    isListening,
    isUserActive,
    reengagementPhase,
    setMaestroActivityStage,
  });

  // CRITICAL: Sync re-engagement callbacks to refs for useMaestroController
  useEffect(() => {
    scheduleReengagementRef.current = scheduleReengagement;
    cancelReengagementRef.current = cancelReengagement;
  }, [scheduleReengagement, cancelReengagement]);

  // ============================================================
  // EFFECTS - Side Effects and Synchronization
  // ============================================================

  // Sync replySuggestions to ref for use by useSpeechController
  useEffect(() => {
    replySuggestionsRef.current = replySuggestions;
  }, [replySuggestions]);

  // ============================================================
  // HANDLERS - Event Handlers and Callbacks
  // ============================================================

  const handleUserInputActivity = useCallback(() => {
    clearAutoSend();
    setMaestroActivityStage('listening');
    handleUserActivity();
  }, [clearAutoSend, handleUserActivity, setMaestroActivityStage]);

  const handleSetAttachedImage = useCallback((base64: string | null, mimeType: string | null) => {
    setAttachedImage(base64, mimeType);
  }, [setAttachedImage]);

  const handleDeleteMessage = useCallback((messageId: string) => {
    deleteMessage(messageId);
  }, [deleteMessage]);

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

    if (newSttEnabledState) {
      clearTranscript();
      startListening(currentSttSettings.language);
    } else {
      clearAutoSend();
      stopListening();
    }
  }, [clearAutoSend, clearTranscript, startListening, stopListening, settingsRef, setSettings]);

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
        onToggleDebugLogs={toggleDebugLogs}
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
                return next;
              });
              const pairId = settingsRef.current.selectedLanguagePairId;
              if (pairId) {
                (async () => { try { await setChatMetaDB(pairId, { bookmarkMessageId: id }); } catch (e) { console.error(`[AppShell] Failed to persist bookmark for pairId=${pairId}, messageId=${id}:`, e); } })();
              }
            }}
            bookmarkedMessageId={settingsRef.current.historyBookmarkMessageId || null}
            maxVisibleMessages={settingsRef.current.maxVisibleMessages ?? MAX_VISIBLE_MESSAGES_DEFAULT}
            onChangeMaxVisibleMessages={(n) => {
              const clamped = Math.max(1, Math.min(100, Math.floor(n || MAX_VISIBLE_MESSAGES_DEFAULT)));
              setSettings(prev => { 
                const next = { ...prev, maxVisibleMessages: clamped }; 
                settingsRef.current = next; 
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

export default AppShell;
