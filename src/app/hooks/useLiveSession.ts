// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * useLiveSession - Hook for managing Gemini Live conversation sessions.
 * 
 * This hook extracts all Gemini Live conversation logic from App.tsx, including:
 * - Session lifecycle (start, stop, cleanup)
 * - Turn completion handling (user/model text + audio)
 * - Camera stream management for live sessions
 * - STT state preservation across session
 * - Image generation during live turns
 * - Reply suggestion generation
 */

import { useCallback, useRef } from 'react';
import { 
  ChatMessage, 
  AppSettings,
  LanguagePair,
  RecordedUtterance,
  TtsAudioCacheEntry 
} from '../../core/types';
import { useGeminiLiveConversation, LiveSessionState, pcmToWav, splitPcmBySilence } from '../../features/speech';
import { generateImage, sanitizeHistoryWithVerifiedUris, uploadMediaToFiles } from '../../api/gemini';
import { getGlobalProfileDB } from '../../features/session';
import { deriveHistoryForApi, computeTtsCacheKey } from '../../features/chat';
import { processMediaForUpload } from '../../features/vision';
import { MAX_MEDIA_TO_KEEP } from '../../core/config/app';
import { 
  DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE, 
  IMAGE_GEN_SYSTEM_INSTRUCTION, 
  IMAGE_GEN_USER_PROMPT_TEMPLATE 
} from '../../core/config/prompts';
import { getPrimaryCode } from '../../shared/utils/languageUtils';
import type { TranslationFunction } from './useTranslations';
import { useMaestroStore } from '../../store';

export interface UseLiveSessionConfig {
  // Translation function
  t: TranslationFunction;
  
  // Settings
  settingsRef: React.MutableRefObject<AppSettings>;
  setSettings: React.Dispatch<React.SetStateAction<AppSettings>>;
  selectedLanguagePairRef: React.MutableRefObject<LanguagePair | undefined>;
  
  // Chat store
  messagesRef: React.MutableRefObject<ChatMessage[]>;
  addMessage: (message: Omit<ChatMessage, 'id' | 'timestamp'>) => string;
  updateMessage: (messageId: string, updates: Partial<ChatMessage>) => void;
  getHistoryRespectingBookmark: (arr: ChatMessage[]) => ChatMessage[];
  computeMaxMessagesForArray: (arr: ChatMessage[]) => number | undefined;
  lastFetchedSuggestionsForRef: React.MutableRefObject<string | null>;
  fetchAndSetReplySuggestions: (assistantMessageId: string, lastTutorMessage: string, history: ChatMessage[]) => Promise<void>;
  upsertMessageTtsCache: (messageId: string, entry: TtsAudioCacheEntry) => void;
  
  // Hardware
  liveVideoStream: MediaStream | null;
  setLiveVideoStream: React.Dispatch<React.SetStateAction<MediaStream | null>>;
  visualContextVideoRef: React.RefObject<HTMLVideoElement | null>;
  visualContextStreamRef: React.MutableRefObject<MediaStream | null>;
  captureSnapshot: (isForReengagement?: boolean) => Promise<{ base64: string; mimeType: string; storageOptimizedBase64: string; storageOptimizedMimeType: string } | null>;
  
  // Speech
  isListening: boolean;
  stopListening: () => void;
  startListening: (lang: string) => void;
  clearTranscript: () => void;
  
  // UI Busy state
  addUiBusyToken: (token: string) => string;
  removeUiBusyToken: (token?: string | null) => void;
  
  // Re-engagement
  scheduleReengagement: (reason: string, delayOverrideMs?: number) => void;
  cancelReengagement: () => void;
  handleUserInputActivity: () => void;
  
  // Prompts
  currentSystemPromptText: string;
  
  // Parsing/utilities (from useMaestroController)
  parseGeminiResponse: (responseText: string | undefined) => Array<{ spanish: string; english: string }>;
  resolveBookmarkContextSummary: () => string | null;
  computeHistorySubsetForMedia: (arr: ChatMessage[]) => ChatMessage[];
  
  // Avatar refs
  maestroAvatarUriRef: React.MutableRefObject<string | null>;
  maestroAvatarMimeTypeRef: React.MutableRefObject<string | null>;
}

export interface UseLiveSessionReturn {
  // State
  liveSessionState: LiveSessionState;
  liveSessionError: string | null;
  
  // Handlers
  handleStartLiveSession: () => Promise<void>;
  handleStopLiveSession: () => Promise<void>;
}

/**
 * Hook for managing Gemini Live conversation sessions.
 * Encapsulates all live session lifecycle and turn handling logic.
 */
export const useLiveSession = (config: UseLiveSessionConfig): UseLiveSessionReturn => {
  const {
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
    handleUserInputActivity,
    currentSystemPromptText,
    parseGeminiResponse,
    resolveBookmarkContextSummary,
    computeHistorySubsetForMedia,
    maestroAvatarUriRef,
    maestroAvatarMimeTypeRef,
  } = config;

  // --- State (Zustand) ---
  const liveSessionState = useMaestroStore(state => state.liveSessionState);
  const liveSessionError = useMaestroStore(state => state.liveSessionError);
  const setLiveSessionState = useMaestroStore(state => state.setLiveSessionState);
  const setLiveSessionError = useMaestroStore(state => state.setLiveSessionError);

  // --- Refs ---
  const liveSessionShouldRestoreSttRef = useRef(false);
  const liveSessionCaptureRef = useRef<{ stream: MediaStream; created: boolean } | null>(null);
  const liveUiTokenRef = useRef<string | null>(null);

  // --- Helper Functions ---

  /**
   * Release the camera stream captured for live session
   */
  const releaseLiveSessionCapture = useCallback(() => {
    if (liveSessionCaptureRef.current) {
      const { stream, created } = liveSessionCaptureRef.current;
      if (created && stream) {
        stream.getTracks().forEach(t => t.stop());
      }
      liveSessionCaptureRef.current = null;
    }
    setLiveVideoStream(null);
  }, [setLiveVideoStream]);

  /**
   * Restore STT state after live session ends
   */
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
  }, [startListening, settingsRef, setSettings]);

  /**
   * Generate a context-rich system instruction for the live session
   */
  const generateLiveSystemInstruction = useCallback(async (): Promise<string> => {
    let basePrompt = currentSystemPromptText;

    const historySubset = computeHistorySubsetForMedia(messagesRef.current);
    const apiHistory = deriveHistoryForApi(historySubset, {
      maxMessages: 10,
      contextSummary: resolveBookmarkContextSummary() || undefined,
      globalProfileText: (await getGlobalProfileDB())?.text || undefined
    });

    let historyContext = "";
    apiHistory.forEach((h: any) => {
      const text = h.rawAssistantResponse || h.text || "(image)";
      const role = h.role === 'user' ? 'User' : 'Maestro';
      historyContext += `${role}: ${text}\n`;
    });

    if (historyContext) {
      basePrompt += `\n\n--- CURRENT CONVERSATION CONTEXT (History) ---\n${historyContext}\n--- END CONTEXT ---`;
    }

    return basePrompt;
  }, [currentSystemPromptText, resolveBookmarkContextSummary, computeHistorySubsetForMedia, messagesRef]);

  /**
   * Handle a completed live turn (user spoke, model responded)
   */
  const handleLiveTurnComplete = useCallback(async (
    userText: string, 
    modelText: string, 
    userAudioPcm?: Int16Array, 
    modelAudioPcm?: Int16Array
  ) => {
    let userMessageId = '';
    
    // 1. Add User Message with Snapshot & Audio
    if (userText) {
      let snapshotData: any = null;
      try {
        // Capture snapshot of the user when they finished speaking
        snapshotData = await captureSnapshot(false);
      } catch { /* ignore */ }

      // Save User Audio if available
      let recordedUtterance: RecordedUtterance | undefined = undefined;
      if (userAudioPcm && userAudioPcm.length > 0) {
        const wavBase64 = pcmToWav(userAudioPcm, 16000);
        recordedUtterance = {
          dataUrl: wavBase64,
          provider: 'gemini', // Using Gemini Live worklet capture
          langCode: settingsRef.current.stt.language,
          transcript: userText
        };
      }

      userMessageId = addMessage({
        role: 'user',
        text: userText,
        imageUrl: snapshotData?.base64,
        imageMimeType: snapshotData?.mimeType,
        storageOptimizedImageUrl: snapshotData?.storageOptimizedBase64,
        storageOptimizedImageMimeType: snapshotData?.storageOptimizedMimeType,
        recordedUtterance
      });

      // Background optimization and upload for live snapshots
      if (snapshotData && userMessageId) {
        (async () => {
          let optimizedDataUrl = snapshotData.storageOptimizedBase64;
          let optimizedMime = snapshotData.storageOptimizedMimeType;
          
          try {
            // 1. Optimize for local persistence (low-res)
            const optimized = await processMediaForUpload(snapshotData.base64, snapshotData.mimeType, { t });
            optimizedDataUrl = optimized.dataUrl;
            optimizedMime = optimized.mimeType;
          } catch (e) {
            console.warn('Optimization failed, using original for persistence', e);
          }

          try {
            // 2. Upload FULL resolution to Files API for model context
            const up = await uploadMediaToFiles(snapshotData.base64, snapshotData.mimeType, 'live-user-snapshot');
            
            // 3. Update message with both low-res (local) and URI (remote)
            updateMessage(userMessageId, {
              storageOptimizedImageUrl: optimizedDataUrl,
              storageOptimizedImageMimeType: optimizedMime,
              uploadedFileUri: up.uri,
              uploadedFileMimeType: up.mimeType
            });
          } catch (e) {
            console.warn('Upload failed', e);
            // Still update persistence image
            updateMessage(userMessageId, {
              storageOptimizedImageUrl: optimizedDataUrl,
              storageOptimizedImageMimeType: optimizedMime
            });
          }
        })();
      }
    }

    // 2. Add Model Message
    if (modelText) {
      const assistantId = addMessage({
        role: 'assistant',
        text: modelText,
        rawAssistantResponse: modelText
      });

        // 3. Post-processing: Formatting Transcript & Translations
        if (selectedLanguagePairRef.current) {
          // The live transcript is already formatted correctly by the system instruction in Live API
          const structuredText = modelText;
          const translations = parseGeminiResponse(structuredText);
          let completeHistory: ChatMessage[] = [...messagesRef.current];
          if (userMessageId) {
            completeHistory.push({
              id: userMessageId,
              role: 'user',
              text: userText,
              timestamp: Date.now()
            } as ChatMessage);
          }
          completeHistory.push({
            id: assistantId,
            role: 'assistant',
            rawAssistantResponse: structuredText,
            translations: translations,
            timestamp: Date.now()
          } as ChatMessage);

          // Now apply audio caching if we have chunks
          if (modelAudioPcm && modelAudioPcm.length > 0) {
            const targetLang = getPrimaryCode(selectedLanguagePairRef.current.targetLanguageCode);
            const nativeLang = getPrimaryCode(selectedLanguagePairRef.current.nativeLanguageCode);
            const chunks = splitPcmBySilence(modelAudioPcm, 24000, 400);
            
            let flatIndex = 0;
            translations.forEach((pair) => {
              // Target Line
              if (pair.spanish && flatIndex < chunks.length) {
                const key = computeTtsCacheKey(pair.spanish, targetLang, 'gemini');
                upsertMessageTtsCache(assistantId, {
                  key,
                  langCode: targetLang,
                  provider: 'gemini',
                  audioDataUrl: pcmToWav(chunks[flatIndex], 24000),
                  updatedAt: Date.now()
                });
                flatIndex++;
              }
              // Native Line
              if (pair.english && flatIndex < chunks.length) {
                const key = computeTtsCacheKey(pair.english, nativeLang, 'gemini');
                upsertMessageTtsCache(assistantId, {
                  key,
                  langCode: nativeLang,
                  provider: 'gemini',
                  audioDataUrl: pcmToWav(chunks[flatIndex], 24000),
                  updatedAt: Date.now()
                });
                flatIndex++;
              }
            });
          }

          updateMessage(assistantId, {
            rawAssistantResponse: structuredText,
            translations: translations
          });

          // 4. Generate Suggestions Immediately
          fetchAndSetReplySuggestions(assistantId, structuredText, getHistoryRespectingBookmark(completeHistory));
          lastFetchedSuggestionsForRef.current = assistantId;
        }


      // 5. Background Image Generation (Full Context)
      if (settingsRef.current.imageGenerationModeEnabled) {
        const assistantStartTime = Date.now();
        updateMessage(assistantId, {
          isGeneratingImage: true,
          imageGenerationStartTime: assistantStartTime
        });

        // Use FULL history context logic
        const historySubsetForImg = computeHistorySubsetForMedia(messagesRef.current);
        let gpText: string | undefined = undefined;
        try { gpText = (await getGlobalProfileDB())?.text || undefined; } catch {}

        const apiHistory = deriveHistoryForApi(historySubsetForImg, {
          maxMessages: computeMaxMessagesForArray(getHistoryRespectingBookmark(messagesRef.current)),
          maxMediaToKeep: MAX_MEDIA_TO_KEEP,
          contextSummary: resolveBookmarkContextSummary() || undefined,
          globalProfileText: gpText,
        });
        
        // Add current turn if not in history subset yet
        if (userText && !apiHistory.some(h => h.role === 'user' && h.text === userText)) {
          apiHistory.push({ role: 'user', text: userText });
        }
        // Append camera instructions as the final User message, matching standard flow context
        apiHistory.push({ role: 'user', text: DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE });

        // Construct prompt asking for "Next Image" based on this context
        const prompt = IMAGE_GEN_USER_PROMPT_TEMPLATE.replace("{TEXT}", modelText);
        
        const sanitizedHistory = await sanitizeHistoryWithVerifiedUris(apiHistory as any);

        generateImage({
          history: sanitizedHistory,
          latestMessageText: prompt,
          latestMessageRole: 'user',
          systemInstruction: IMAGE_GEN_SYSTEM_INSTRUCTION,
          maestroAvatarUri: maestroAvatarUriRef.current || undefined,
          maestroAvatarMimeType: maestroAvatarMimeTypeRef.current || undefined,
        }).then(async (res: any) => {
          if (res.base64Image) {
            const optimized = await processMediaForUpload(res.base64Image, res.mimeType, { t });
            const up = await uploadMediaToFiles(res.base64Image, res.mimeType, 'live-gen');
            
            updateMessage(assistantId, {
              imageUrl: res.base64Image,
              imageMimeType: res.mimeType,
              storageOptimizedImageUrl: optimized.dataUrl,
              storageOptimizedImageMimeType: optimized.mimeType,
              uploadedFileUri: up.uri,
              uploadedFileMimeType: up.mimeType,
              isGeneratingImage: false,
              imageGenerationStartTime: undefined
            });
          } else {
            updateMessage(assistantId, { isGeneratingImage: false });
          }
        }).catch(() => {
          updateMessage(assistantId, { isGeneratingImage: false });
        });
      }
    }
  }, [
    addMessage, 
    captureSnapshot, 
    t, 
    parseGeminiResponse, 
    fetchAndSetReplySuggestions, 
    getHistoryRespectingBookmark, 
    computeHistorySubsetForMedia, 
    resolveBookmarkContextSummary, 
    upsertMessageTtsCache, 
    computeMaxMessagesForArray, 
    updateMessage, 
    settingsRef, 
    selectedLanguagePairRef, 
    messagesRef, 
    lastFetchedSuggestionsForRef,
    maestroAvatarUriRef,
    maestroAvatarMimeTypeRef
  ]);

  // --- Initialize useGeminiLiveConversation ---
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
      if (state === 'idle' || state === 'error') {
        restoreSttAfterLiveSession();
        releaseLiveSessionCapture();
      }
    },
    onError: (message) => {
      setLiveSessionError(message);
      restoreSttAfterLiveSession();
    },
    onTurnComplete: handleLiveTurnComplete
  });

  // --- Public Handlers ---

  /**
   * Start a new Gemini Live conversation session
   */
  const handleStartLiveSession = useCallback(async () => {
    if (liveSessionState === 'connecting' || liveSessionState === 'active') return;

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
          const next = { ...prev, stt: { ...prev.stt, enabled: false } };
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

      const liveSystemInstruction = await generateLiveSystemInstruction();

      await startLiveConversation({
        stream,
        videoElement: visualContextVideoRef.current,
        systemInstruction: liveSystemInstruction,
      });
    } catch (error) {
      releaseLiveSessionCapture();
      restoreSttAfterLiveSession();
      const message = error instanceof Error ? error.message : t('general.error');
      setLiveSessionError(message);
      throw error;
    }
  }, [
    cancelReengagement, 
    clearTranscript, 
    generateLiveSystemInstruction, 
    handleUserInputActivity, 
    isListening, 
    liveSessionState, 
    liveVideoStream, 
    releaseLiveSessionCapture, 
    restoreSttAfterLiveSession, 
    setLiveVideoStream, 
    setSettings, 
    startLiveConversation, 
    stopListening, 
    t,
    settingsRef,
    visualContextStreamRef,
    visualContextVideoRef,
  ]);

  /**
   * Stop the current Gemini Live conversation session
   */
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

  return {
    liveSessionState,
    liveSessionError,
    handleStartLiveSession,
    handleStopLiveSession,
  };
};
