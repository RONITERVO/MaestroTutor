// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * App composition and UI wiring. Feature hooks own their workflows; speech
 * routing and idle handoffs live in app/coordinators with explicit state/action
 * ports. React lifecycle and store projection remain in the app adapters.
 */
 
import React, { useEffect, useCallback, useRef, useMemo, useState } from 'react';

// --- Features Components ---
import { ChatInterface } from '../features/chat';
import { ApiKeyGate, Header, useSmartReengagement } from '../features/session';
import { DebugLogPanel } from '../features/diagnostics';
import { VisualContextVideo } from '../features/vision';

// --- Hooks ---
import { useAppInitialization, useMaestroActivityStage, useIdleReengagement } from './hooks';

import { useTutorConversation, useSuggestions, useChatPersistence } from '../features/chat';
import { useSpeechOrchestrator, type GeminiLiveSttTurnComplete } from '../features/speech';
import { useCameraManager } from '../features/vision';
import { useLiveSessionController, useSilentObserverController } from '../features/live';
import { useApplyCustomColors } from '../features/theme';
import { MAX_VISIBLE_MESSAGES_DEFAULT, useMaestroStore } from '../store';

// --- Feature Hooks ---
// --- Services ---
import { setChatMetaDB } from '../features/chat';

// --- Config ---
import { IMAGE_GEN_CAMERA_ID } from '../core/config/app';
import { selectBlocksSilentObserver, selectNonReengagementBusy } from '../store/slices/uiSlice';
import { selectSelectedLanguagePair } from '../store/slices/settingsSlice';

// --- Types ---
import { SpeechPart } from '../core/types';

// --- Utils ---
import { getPrimaryCode } from '../shared/utils/languageUtils';
import { createSmartRef } from '../shared/utils/smartRef';
import { useApiKey } from '../shared/hooks/useApiKey';
import { useManagedAccess } from '../shared/hooks/useManagedAccess';
import { logSttFlow, warnSttFlow } from '../shared/utils/sttFlowDebug';
import { SmallSpinner } from '../shared/ui/SmallSpinner';
import { createSttTurnHandler } from './coordinators/sttTurn';
import { createReengagementSequence } from './coordinators/reengagement';
import { createSpeechModeActions } from './coordinators/speechMode';
import { readSpeechRoutingState } from './speechRoutingState';
import { useLanguageSessionReset } from './hooks/useLanguageSessionReset';

const App: React.FC = () => {
  // ============================================================
  // REFS - Declared before hooks
  // ============================================================
  
  // These refs are used for visual context capture state
  const isCurrentlyPerformingVisualContextCaptureRef = useRef(false);
  const bubbleWrapperRefs = useRef<Map<string, HTMLDivElement>>(new Map());
  const maestroAvatarUriRef = useRef<string | null>(null);
  const maestroAvatarMimeTypeRef = useRef<string | null>(null);
  const scheduleReengagementRef = useRef<(reason: string, delayOverrideMs?: number) => void>(() => {});
  const cancelReengagementRef = useRef<() => void>(() => {});
  const stopSilentObserverRef = useRef<() => Promise<void>>(async () => {});
  const resetSilentObserverRef = useRef<() => Promise<void>>(async () => {});
  const stopLiveSessionForHistoryLoadRef = useRef<() => Promise<void>>(async () => {});
  const handleToggleSuggestionModeRef = useRef<((forceState?: boolean) => void) | undefined>(undefined);
  const handleSttTurnCompleteRef = useRef<(turn: GeminiLiveSttTurnComplete) => void | Promise<void>>(() => {});

  const waitForConversationSystemsIdle = useCallback(async () => {
    await Promise.allSettled([
      Promise.resolve(resetSilentObserverRef.current()),
      Promise.resolve(stopLiveSessionForHistoryLoadRef.current()),
    ]);
  }, []);

  // ============================================================
  // HOOK COMPOSITION - The Controller Layer
  // ============================================================

  // --- Activity Tokens ---
  const activityTokens = useMaestroStore(state => state.activityTokens);
  const addActivityToken = useMaestroStore(state => state.addActivityToken);
  const removeActivityToken = useMaestroStore(state => state.removeActivityToken);
  const isBlockingActivity = useMaestroStore(selectNonReengagementBusy);
  const blocksSilentObserver = useMaestroStore(selectBlocksSilentObserver);
  const liveSessionState = useMaestroStore(state => state.liveSessionState);
  const setLastFetchedSuggestionsFor = useMaestroStore(state => state.setLastFetchedSuggestionsFor);
  const setSttError = useMaestroStore(state => state.setSttError);

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
    upsertLiveTranscriptMessage,
    removeLiveTranscriptMessage,
    clearLiveTranscriptMessages,
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
    waitForConversationSystemsIdle,
  });

  const {
    hasKey: hasApiKey,
    maskedKey: maskedApiKey,
    isLoading: isApiKeyLoading,
    isSaving: isApiKeySaving,
    error: apiKeyError,
    setError: setApiKeyError,
    saveApiKey,
    clearApiKey,
  } = useApiKey();

  const {
    session: managedSession,
    hasManagedAccess,
    isLoading: isManagedAccessLoading,
  } = useManagedAccess();
  const hasAiAccess = hasApiKey || hasManagedAccess;
  const isAccessLoading = isApiKeyLoading || (!hasApiKey && isManagedAccessLoading);

  const [isApiKeyGateOpen, setIsApiKeyGateOpen] = useState(false);
  const [apiKeyGateInstructionIndex, setApiKeyGateInstructionIndex] = useState<number | null>(null);
  const [apiKeyInvalid, setApiKeyInvalid] = useState(false);
  const showApiKeyGate = !hasAiAccess || isApiKeyGateOpen;

  const handleApiKeyGateOpen = useCallback((options?: { reason?: 'missing' | 'invalid' | 'quota'; instructionIndex?: number }) => {
    setApiKeyError(null);
    setIsApiKeyGateOpen(true);
    setApiKeyInvalid(options?.reason === 'invalid');
    if (typeof options?.instructionIndex === 'number') {
      setApiKeyGateInstructionIndex(options.instructionIndex);
    } else {
      setApiKeyGateInstructionIndex(null);
    }
  }, [setApiKeyError]);

  const settingsRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.settings), []);
  const selectedLanguagePairRef = useMemo(() => createSmartRef(useMaestroStore.getState, selectSelectedLanguagePair), []);
  const isLoadingHistoryRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.isLoadingHistory), []);
  const lastFetchedSuggestionsForRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.lastFetchedSuggestionsFor), []);

  useChatPersistence();

  // Apply user-customized theme colors to CSS variables
  useApplyCustomColors();

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
    onSttTurnComplete: (turn) => handleSttTurnCompleteRef.current(turn),
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
    onApiKeyGateOpen: handleApiKeyGateOpen,
  });

  useSuggestions({
    isSpeaking,
    fetchAndSetReplySuggestions,
    getHistoryRespectingBookmark,
  });
  
  const handleSttTurnComplete = useMemo(() => createSttTurnHandler({
    readState: readSpeechRoutingState, stopListening, startListening, clearTranscript,
    handleCreateSuggestion, handleSendMessageInternal, logSttFlow, warnSttFlow,
    warn: console.warn,
  }), [clearTranscript, handleCreateSuggestion, handleSendMessageInternal, startListening, stopListening]);

  useEffect(() => {
    handleSttTurnCompleteRef.current = handleSttTurnComplete;
  }, [handleSttTurnComplete]);
  
  // --- Smart Reengagement ---
  // NOTE: Moved AFTER useMaestroController to have access to isSending, isSpeaking
  const triggerReengagementSequence = useMemo(() => createReengagementSequence({
    isLoadingHistoryRef, isSendingRef, speechIsSpeakingRef,
    isCurrentlyPerformingVisualContextCaptureRef, stopSilentObserverRef,
    settingsRef, visualContextStreamRef, captureSnapshot, handleSendMessageInternal,
    setReplySuggestions, setLastFetchedSuggestionsFor,
  }), [captureSnapshot, settingsRef, visualContextStreamRef, handleSendMessageInternal, isLoadingHistoryRef, isSendingRef, speechIsSpeakingRef, setReplySuggestions, setLastFetchedSuggestionsFor]);

  const {
    reengagementPhase,
    scheduleReengagement,
    cancelReengagement,
    handleUserActivity,
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
    handleUserActivity();
  }, [handleUserActivity]);


  const handleSetAttachedImage = useCallback((base64: string | null, mimeType: string | null) => {
    setAttachedImage(base64, mimeType);
  }, [setAttachedImage]);

  const handleDeleteMessage = useCallback((messageId: string) => {
    deleteMessage(messageId);
  }, [deleteMessage]);

  const { handleToggleSuggestionMode, sttMasterToggle } = useMemo(() => createSpeechModeActions({
    isListening, stopListening, startListening, clearTranscript, settingsRef,
    selectedLanguagePairRef, setSettings, stopSilentObserverRef,
    setSttError: error => useMaestroStore.getState().setSttError(error),
    delay: (callback, milliseconds) => setTimeout(callback, milliseconds), warn: console.warn,
  }), [isListening, stopListening, startListening, clearTranscript, settingsRef, selectedLanguagePairRef, setSettings]);

  // CRITICAL: Sync handleToggleSuggestionMode to ref for useMaestroController
  useEffect(() => {
    handleToggleSuggestionModeRef.current = handleToggleSuggestionMode;
  }, [handleToggleSuggestionMode]);



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

  const toggleFocusedModeState = useCallback(() => {
    handleSettingsChange('imageFocusedModeEnabled', !settingsRef.current.imageFocusedModeEnabled);
  }, [handleSettingsChange, settingsRef]);

  const handleToggleImageFocusedMode = useCallback((messageId: string) => {
    // @ts-ignore
    if (!document.startViewTransition) {
      toggleFocusedModeState();
      return;
    }

    setTransitioningImageId(messageId);

    // @ts-ignore
    const transition = document.startViewTransition(() => {
      toggleFocusedModeState();
    });

    transition.finished.finally(() => {
      setTransitioningImageId(null);
    });
  }, [toggleFocusedModeState]);

  // ============================================================
  // GEMINI LIVE SESSION HANDLING
  // ============================================================

  const {
    handleStartLiveSession,
    handleStopLiveSession,
    handleLiveTurnComplete,
    handleLiveTurnTranscriptUpdate,
  } = useLiveSessionController({
    t,
    setSettings,
    addMessage,
    updateMessage,
    upsertLiveTranscriptMessage,
    removeLiveTranscriptMessage,
    clearLiveTranscriptMessages,
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
  stopLiveSessionForHistoryLoadRef.current = () => handleStopLiveSession({ scheduleReengagement: false });

  const handleSilentObserverTurnComplete = useCallback(async (
    userText: string,
    modelText: string,
    userAudioPcm?: Int16Array,
    modelAudioLines?: Int16Array[]
  ) => {
    await handleLiveTurnComplete(userText, modelText, userAudioPcm, modelAudioLines);
    scheduleReengagement('silent-observer-response');
  }, [handleLiveTurnComplete, scheduleReengagement]);

  const { stopSilentObserver, resetSilentObserver } = useSilentObserverController({
    enabled: hasAiAccess && !showApiKeyGate,
    isBlockingActivity: blocksSilentObserver,
    liveSessionState,
    liveVideoStream,
    visualContextVideoRef,
    currentSystemPromptText,
    resolveBookmarkContextSummary,
    computeHistorySubsetForMedia,
    onTurnTranscriptUpdate: handleLiveTurnTranscriptUpdate,
    onTurnComplete: handleSilentObserverTurnComplete,
  });
  stopSilentObserverRef.current = stopSilentObserver;
  resetSilentObserverRef.current = resetSilentObserver;

  const handleStartLiveSessionWithObserverStop = useCallback(async () => {
    await stopSilentObserver();
    await handleStartLiveSession();
  }, [handleStartLiveSession, stopSilentObserver]);

  useLanguageSessionReset({
    selectedLanguagePairId: settings.selectedLanguagePairId, settingsRef,
    cancelReengagement, clearTranscript, handleStopLiveSession, resetSilentObserver,
    setSttError, startListening, stopListening, stopSpeaking,
  });

  // ============================================================
  // QUOTA ERROR ACTIONS
  // ============================================================

  const handleQuotaSetupBilling = useCallback(() => {
    handleApiKeyGateOpen({ reason: 'quota', instructionIndex: 9 });
  }, [handleApiKeyGateOpen]);

  const handleQuotaStartLive = useCallback(async () => {
    // Select the first available physical camera if none is selected
    const currentCameraId = settingsRef.current.selectedCameraId;
    if (!currentCameraId || currentCameraId === IMAGE_GEN_CAMERA_ID) {
      const firstPhysicalCamera = availableCamerasRef.current[0];
      if (firstPhysicalCamera) {
        handleSettingsChange('selectedCameraId', firstPhysicalCamera.deviceId);
      }
    }
    // Enable snapshot sending so the camera feed + preview are activated
    // by useCameraManager's effect (creates the stream via getUserMedia).
    if (!settingsRef.current.sendWithSnapshotEnabled) {
      handleSettingsChange('sendWithSnapshotEnabled', true);
    }
    // Wait for useCameraManager's effect to create the camera stream before
    // starting the live session. Without this, handleStartLiveSession would
    // also call getUserMedia concurrently, causing race conditions that can
    // invalidate the stream ("No active stream provided") on some platforms.
    const maxWaitMs = 5000;
    const pollMs = 50;
    const startTime = Date.now();
    while (Date.now() - startTime < maxWaitMs) {
      if (visualContextStreamRef.current && visualContextStreamRef.current.active) break;
      await new Promise(resolve => setTimeout(resolve, pollMs));
    }
    // Start the live session. handleStartLiveSession will reuse the stream
    // from visualContextStreamRef instead of creating a competing one.
    try {
      await handleStartLiveSessionWithObserverStop();
    } catch {
      // handleStartLiveSession already handles its own errors
    }
  }, [settingsRef, availableCamerasRef, handleSettingsChange, handleStartLiveSessionWithObserverStop, visualContextStreamRef]);

  const handleImageGenViewCost = useCallback(() => {
    handleApiKeyGateOpen();
  }, [handleApiKeyGateOpen]);


  // ============================================================
  // RENDER
  // ============================================================

  if (isAccessLoading) {
    return (
      <div className="flex h-screen w-full items-center justify-center bg-page-bg paper-texture">
        <div className="text-center relative z-10">
          <SmallSpinner className="w-8 h-8 text-loading-spinner block mx-auto" />
          <p className="mt-2 text-page-text/70 font-hand">{t('app.loading') || 'Loading app...'}</p>
        </div>
      </div>
    );
  }

  if (isLoadingHistory && settings.selectedLanguagePairId) {
    return (
      <div className="flex h-screen w-full items-center justify-center bg-page-bg paper-texture">
        <div className="text-center relative z-10">
          <SmallSpinner className="w-8 h-8 text-loading-spinner block mx-auto" />
          <p className="mt-2 text-page-text/70 font-hand">{t('chat.loadingHistory')}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col min-h-screen antialiased text-page-text bg-page-bg paper-texture">
      <Header
        onOpenApiKey={() => {
          setApiKeyError(null);
          setApiKeyGateInstructionIndex(null);
          setIsApiKeyGateOpen(true);
        }}
        hasApiKey={hasApiKey}
        hasManagedAccess={hasManagedAccess}
      />
      {showDebugLogs && <DebugLogPanel onClose={() => setShowDebugLogs(false)} />}
      <VisualContextVideo videoRef={visualContextVideoRef} />
      <ApiKeyGate
        isOpen={showApiKeyGate}
        isBlocking={!hasAiAccess}
        hasKey={hasApiKey}
        maskedKey={maskedApiKey}
        isSaving={isApiKeySaving}
        error={apiKeyError}
        keyInvalid={apiKeyInvalid}
        instructionFocusIndex={apiKeyGateInstructionIndex}
        onSave={saveApiKey}
        onClear={clearApiKey}
        onValueChange={() => setApiKeyError(null)}
        managedSession={managedSession}
        onClose={() => {
          setApiKeyError(null);
          setApiKeyGateInstructionIndex(null);
          setApiKeyInvalid(false);
          setIsApiKeyGateOpen(false);
        }}
      />
      <div className="flex flex-1 overflow-hidden">
        <main className="flex-1 flex flex-col bg-paper-surface relative z-10">
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
                const targetLang = getPrimaryCode(selectedLanguagePairRef.current.targetLanguageCode);
                const nativeLang = getPrimaryCode(selectedLanguagePairRef.current.nativeLanguageCode);
                const messageId = lastFetchedSuggestionsForRef.current;
                const suggestionIndex = replySuggestions.findIndex((s) => s.target === suggestion.target && s.native === suggestion.native);
                const isSuggestionCtx = messageId && suggestionIndex >= 0;
                const targetContext = isSuggestionCtx
                  ? { source: 'suggestion' as const, messageId, suggestionIndex, suggestionLang: 'target' as const }
                  : { source: 'adHoc' as const };
                const nativeContext = isSuggestionCtx
                  ? { source: 'suggestion' as const, messageId, suggestionIndex, suggestionLang: 'native' as const }
                  : { source: 'adHoc' as const };

                const partsToSpeak: SpeechPart[] = [];

                if (suggestion.target && suggestion.target.trim() && targetLang) {
                  partsToSpeak.push({ text: suggestion.target, langCode: targetLang, context: targetContext });
                }
                if (settingsRef.current.tts.speakNative && suggestion.native && suggestion.native.trim() && nativeLang) {
                  partsToSpeak.push({ text: suggestion.native, langCode: nativeLang, context: nativeContext });
                }

                if (partsToSpeak.length > 0) {
                  speakWrapper(partsToSpeak, targetLang);
                }
              }
              handleUserInputActivity();
            }}
            onToggleImageFocusedMode={handleToggleImageFocusedMode}
            onStartLiveSession={handleStartLiveSessionWithObserverStop}
            onStopLiveSession={handleStopLiveSession}
            onStopSilentObserver={stopSilentObserver}
            onToggleSuggestionMode={handleToggleSuggestionMode}
            onCreateSuggestion={handleCreateSuggestion}
            onQuotaSetupBilling={handleQuotaSetupBilling}
            onQuotaStartLive={handleQuotaStartLive}
            onImageGenViewCost={handleImageGenViewCost}
          />
        </main>
      </div>
    </div>
  );
};

export default App;
