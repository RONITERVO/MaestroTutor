// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * App.tsx - The Composition Root
 * 
 * This component is a "Hollow Shell" that:
 * 1. Composes all the orchestration hooks
 * 2. Passes data/handlers to the UI layer
 * 3. Contains minimal business logic
 * 
 * The actual logic has been extracted into specialized hooks in src/app/hooks/
 * and src/features/ /hooks.
 */
 
import React, { useEffect, useCallback, useRef, useMemo } from 'react';

// --- Features Components ---
import { ChatInterface } from '../features/chat';
import { Header, useSmartReengagement } from '../features/session';
import { DebugLogPanel } from '../features/diagnostics';
import { VisualContextVideo } from '../features/vision';

// --- Hooks ---
import { useAppInitialization, useMaestroActivityStage, useIdleReengagement } from './hooks';

import { useTutorConversation, useSuggestions, useChatPersistence } from '../features/chat';
import { useSpeechOrchestrator, useAutoSendOnSilence, useSuggestionModeAutoRestart } from '../features/speech';
import { useCameraManager } from '../features/vision';
import { useLiveSessionController } from '../features/live';
import { MAX_VISIBLE_MESSAGES_DEFAULT, useMaestroStore } from '../store';

// --- Feature Hooks ---
// --- Services ---
import { setChatMetaDB } from '../features/chat';

// --- Config ---
import { IMAGE_GEN_CAMERA_ID } from '../core/config/app';
import { selectNonReengagementBusy } from '../store/slices/uiSlice';
import { selectSelectedLanguagePair } from '../store/slices/settingsSlice';

// --- Utils ---
import { getPrimaryCode } from '../shared/utils/languageUtils';
import { createSmartRef } from '../shared/utils/smartRef';

/** Delay in ms before restarting STT after language change */
const STT_RESTART_DELAY_MS = 250;

const App: React.FC = () => {
  // ============================================================
  // REFS - Declared before hooks
  // ============================================================
  
  // These refs are used for visual context capture state
  const isCurrentlyPerformingVisualContextCaptureRef = useRef(false);
  const bubbleWrapperRefs = useRef<Map<string, HTMLDivElement>>(new Map());
  const maestroAvatarUriRef = useRef<string | null>(null);
  const maestroAvatarMimeTypeRef = useRef<string | null>(null);

  // ============================================================
  // HOOK COMPOSITION - The Controller Layer
  // ============================================================

  // --- Activity Tokens ---
  const activityTokens = useMaestroStore(state => state.activityTokens);
  const addActivityToken = useMaestroStore(state => state.addActivityToken);
  const removeActivityToken = useMaestroStore(state => state.removeActivityToken);
  const isBlockingActivity = useMaestroStore(selectNonReengagementBusy);
  const setLastFetchedSuggestionsFor = useMaestroStore(state => state.setLastFetchedSuggestionsFor);

  const {
    t,
    settings,
    handleSettingsChange,
    setSettings,
    selectedLanguagePair,
    isLoadingHistory,
    addMessage,
    updateMessage,
    deleteMessage,
    setMessages,
    getHistoryRespectingBookmark,
    computeMaxMessagesForArray,
    upsertMessageTtsCache,
    upsertSuggestionTtsCache,
    replySuggestions,
    setReplySuggestions,
  } = useAppInitialization({
    maestroAvatarUriRef,
    maestroAvatarMimeTypeRef,
  });

  const settingsRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.settings), []);
  const selectedLanguagePairRef = useMemo(() => createSmartRef(useMaestroStore.getState, selectSelectedLanguagePair), []);
  const isLoadingHistoryRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.isLoadingHistory), []);
  const lastFetchedSuggestionsForRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.lastFetchedSuggestionsFor), []);

  useChatPersistence();

  // --- Hardware ---
  const {
    availableCamerasRef,
    liveVideoStream,
    setLiveVideoStream,
    visualContextVideoRef,
    visualContextStreamRef,
    setSnapshotUserError,
    captureSnapshot,
  } = useCameraManager({
    t,
    sendWithSnapshotEnabled: settings.sendWithSnapshotEnabled,
    useVisualContext: settings.smartReengagement.useVisualContext,
    selectedCameraId: settings.selectedCameraId,
  });

  const showDebugLogs = useMaestroStore(state => state.showDebugLogs);
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

  const setTransitioningImageId = useMaestroStore(state => state.setTransitioningImageId);
  const setShowDebugLogs = useMaestroStore(state => state.setShowDebugLogs);
  const setAttachedImage = useMaestroStore(state => state.setAttachedImage);

  // --- Refs ---
  const STT_STABLE_NO_TEXT_MS = 4000;

  const attachedImageBase64 = useMaestroStore(state => state.attachedImageBase64);
  const attachedImageMimeType = useMaestroStore(state => state.attachedImageMimeType);
  
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
    hasPendingQueueItems,
    isListening,
    transcript,
    startListening,
    stopListening,
    clearTranscript,
    claimRecordedUtterance,
    speechIsSpeakingRef,
    speakMessage,
    speakWrapper,
  } = useSpeechOrchestrator({
    upsertMessageTtsCache,
    upsertSuggestionTtsCache,
    setMessages,
  });
  
  // --- Maestro Controller ---
  const {
    isSending,
    isSendingRef,
    handleSendMessageInternal,
    handleSendMessageInternalRef,
    handleCreateSuggestion,
    handleSuggestionInteraction,
    setMaestroActivityStage,
    parseGeminiResponse,
    resolveBookmarkContextSummary,
    computeHistorySubsetForMedia,
    fetchAndSetReplySuggestions,
  } = useTutorConversation({
    t,
    setSettings,
    addMessage,
    updateMessage,
    setMessages,
    getHistoryRespectingBookmark,
    computeMaxMessagesForArray,
    captureSnapshot,
    speakMessage,
    isSpeechSynthesisSupported,
    isListening,
    stopListening,
    startListening,
    clearTranscript,
    hasPendingQueueItems,
    claimRecordedUtterance,
    scheduleReengagementRef,
    cancelReengagementRef,
    transcript,
    currentSystemPromptText,
    currentReplySuggestionsPromptText,
    setReplySuggestions,
    handleToggleSuggestionModeRef,
    maestroAvatarUriRef,
    maestroAvatarMimeTypeRef,
    setSnapshotUserError,
  });


  const { clearAutoSend } = useAutoSendOnSilence({
    transcript,
    attachedImageBase64,
    attachedImageMimeType,
    clearTranscript,
    handleCreateSuggestion,
    handleSendMessageInternal,
    stableMs: STT_STABLE_NO_TEXT_MS,
  });

  useSuggestions({
    isSpeaking,
    fetchAndSetReplySuggestions,
    getHistoryRespectingBookmark,
  });

  useSuggestionModeAutoRestart({
    isListening,
    startListening,
  });
  
  // --- Smart Reengagement ---
  // NOTE: Moved AFTER useMaestroController to have access to isSending, isSpeaking
  const triggerReengagementSequence = useCallback(async () => {
    // Guard conditions - don't re-engage if busy
    if (isLoadingHistoryRef.current || isSendingRef.current || speechIsSpeakingRef.current || isCurrentlyPerformingVisualContextCaptureRef.current) {
      return;
    }

    setReplySuggestions([]);
    // Note: isLoadingSuggestions is now managed via tokens in useMaestroController
    // Clearing suggestions above is sufficient; token will be removed when generation completes
    setLastFetchedSuggestionsFor(null);

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
  }, [captureSnapshot, settingsRef, visualContextStreamRef, handleSendMessageInternal, isLoadingHistoryRef, isSendingRef, speechIsSpeakingRef, setReplySuggestions, setLastFetchedSuggestionsFor]);

  const {
    reengagementPhase,
    scheduleReengagement,
    cancelReengagement,
    handleUserActivity,
    // Intentionally unused - destructured to prevent "unused export" warnings in hook
    isReengagementToken: _unusedIsReengagementToken,
    setReengagementPhase: _unusedSetReengagementPhase,
  } = useSmartReengagement({
    isLoadingHistory,
    selectedLanguagePairId: settings.selectedLanguagePairId,
    activityTokens, // Unified token set replaces isSending, isSpeaking, refs, etc.
    isVisualContextActive: isCurrentlyPerformingVisualContextCaptureRef.current,
    triggerReengagementSequence,
    addActivityToken,
    removeActivityToken,
  });

  useIdleReengagement({
    selectedLanguagePair,
    isBlockingActivity,
    isUserActive,
    reengagementPhase,
    scheduleReengagement,
    cancelReengagement,
  });

  useMaestroActivityStage({
    isSpeaking,
    isSending,
    isListening,
    reengagementPhase,
    setMaestroActivityStage,
  });

  // CRITICAL: Sync re-engagement callbacks to refs for useMaestroController
  useEffect(() => {
    scheduleReengagementRef.current = scheduleReengagement;
    cancelReengagementRef.current = cancelReengagement;
  }, [scheduleReengagement, cancelReengagement]);

  // ============================================================
  // HANDLERS - Event Handlers and Callbacks
  // ============================================================

  const handleUserInputActivity = useCallback(() => {
    clearAutoSend();
    handleUserActivity();
  }, [clearAutoSend, handleUserActivity]);


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
      }, STT_RESTART_DELAY_MS);
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
    setSettings(nextSettings);

    if (newSttEnabledState) {
      clearTranscript();
      startListening(currentSttSettings.language);
    } else {
      clearAutoSend();
      stopListening();
    }
  }, [clearAutoSend, clearTranscript, startListening, stopListening, settingsRef, setSettings]);

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

  // ============================================================
  // GEMINI LIVE SESSION HANDLING
  // ============================================================

  const {
    handleStartLiveSession,
    handleStopLiveSession,
  } = useLiveSessionController({
    t,
    setSettings,
    addMessage,
    updateMessage,
    getHistoryRespectingBookmark,
    computeMaxMessagesForArray,
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
    addActivityToken,
    removeActivityToken,
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
      <Header />
      {showDebugLogs && <DebugLogPanel onClose={() => setShowDebugLogs(false)} />}
      <VisualContextVideo videoRef={visualContextVideoRef} />
      <div className="flex flex-1 overflow-hidden">
        <main className="flex-1 flex flex-col bg-slate-50">
          <ChatInterface
            onSendMessage={handleSendMessageInternalRef.current || handleSendMessageInternal}
            onDeleteMessage={handleDeleteMessage}
            updateMessage={updateMessage}
            onBookmarkAt={(id) => {
              setSettings(prev => {
                const next = { ...prev, historyBookmarkMessageId: id };
                return next;
              });
              const pairId = settingsRef.current.selectedLanguagePairId;
              if (pairId) {
                (async () => { try { await setChatMetaDB(pairId, { bookmarkMessageId: id }); } catch (e) { console.error(`[App] Failed to persist bookmark for pairId=${pairId}, messageId=${id}:`, e); } })();
              }
            }}
            onChangeMaxVisibleMessages={(n) => {
              const clamped = Math.max(1, Math.min(100, Math.floor(n || MAX_VISIBLE_MESSAGES_DEFAULT)));
              setSettings(prev => { 
                const next = { ...prev, maxVisibleMessages: clamped }; 
                return next; 
              });
            }}
            bubbleWrapperRefs={bubbleWrapperRefs}
            onSetAttachedImage={handleSetAttachedImage}
            onSttToggle={sttMasterToggle}
            speakText={speakWrapper}
            stopSpeaking={stopSpeaking}
            onToggleSpeakNativeLang={handleToggleSpeakNativeLang}
            onUserInputActivity={handleUserInputActivity}
            onToggleSendWithSnapshot={handleToggleSendWithSnapshot}
            onToggleUseVisualContextForReengagement={handleToggleUseVisualContextForReengagement}
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
            onToggleImageGenerationMode={handleToggleImageGenerationMode}
            onToggleImageFocusedMode={handleToggleImageFocusedMode}
            onStartLiveSession={handleStartLiveSession}
            onStopLiveSession={handleStopLiveSession}
            onToggleSuggestionMode={handleToggleSuggestionMode}
            onCreateSuggestion={handleCreateSuggestion}
          />
        </main>
      </div>
    </div>
  );
};

export default App;
