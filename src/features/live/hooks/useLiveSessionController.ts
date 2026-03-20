// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * useLiveSessionController - Hook for managing Gemini Live conversation sessions.
 * 
 * This hook extracts all Gemini Live conversation logic from App.tsx, including:
 * - Session lifecycle (start, stop, cleanup)
 * - Turn completion handling (user/model text + audio)
 * - Camera stream management for live sessions
 * - STT state preservation across session
 * - Attachment generation via the shared suggestion-creator path
 * - Reply suggestion generation
 */

import { useCallback, useRef, useMemo } from 'react';
import { 
  ChatMessage, 
  AppSettings,
  RecordedUtterance,
  TtsAudioCacheEntry 
} from '../../../core/types';
import { useGeminiLiveConversation, LiveSessionState, pcmToWav, mapAudioSegmentsToTextLines } from '../../speech';
import { uploadMediaToFiles } from '../../../api/gemini/files';
import { computeTtsCacheKey } from '../../chat';
import { processMediaForUpload } from '../../vision';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE, type TokenCategory } from '../../../core/config/activityTokens';
import { getPrimaryCode } from '../../../shared/utils/languageUtils';
import type { TranslationFunction } from '../../../app/hooks/useTranslations';
import { useMaestroStore } from '../../../store';
import { selectSelectedLanguagePair } from '../../../store/slices/settingsSlice';
import { createSmartRef } from '../../../shared/utils/smartRef';
import { buildLiveSystemInstruction } from '../utils/liveSystemInstruction';
import {
  buildUploadedAttachmentState,
  inferUploadedAttachmentTargetsForMimeType,
  PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID,
} from '../../chat/utils/uploadedAttachmentVariants';

export interface UseLiveSessionControllerConfig {
  // Translation function
  t: TranslationFunction;
  
  // Settings
  setSettings: React.Dispatch<React.SetStateAction<AppSettings>>;
  
  // Chat store
  addMessage: (message: Omit<ChatMessage, 'id' | 'timestamp'>) => string;
  updateMessage: (messageId: string, updates: Partial<ChatMessage>) => void;
  getHistoryRespectingBookmark: (arr: ChatMessage[]) => ChatMessage[];
  computeMaxMessagesForArray: (arr: ChatMessage[]) => number | undefined;
  fetchAndSetReplySuggestions: (
    assistantMessageId: string,
    lastTutorMessage: string,
    history: ChatMessage[],
    options?: { responseSource?: 'chat' | 'live' }
  ) => Promise<void>;
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
  
  // Activity token state
  addActivityToken: (category: TokenCategory, subtype?: string) => string;
  removeActivityToken: (token: string) => void;
  
  // Re-engagement
  scheduleReengagement: (reason: string, delayOverrideMs?: number) => void;
  cancelReengagement: () => void;
  handleUserInputActivity: () => void;
  
  // Prompts
  currentSystemPromptText: string;
  
  // Parsing/utilities (from useMaestroController)
  parseGeminiResponse: (responseText: string | undefined) => Array<{ target: string; native: string }>;
  resolveBookmarkContextSummary: () => string | null;
  computeHistorySubsetForMedia: (arr: ChatMessage[]) => ChatMessage[];
  
  // Avatar refs
  maestroAvatarUriRef: React.MutableRefObject<string | null>;
  maestroAvatarMimeTypeRef: React.MutableRefObject<string | null>;
}

export interface UseLiveSessionControllerReturn {
  // State
  liveSessionState: LiveSessionState;
  liveSessionError: string | null;
  
  // Handlers
  handleStartLiveSession: () => Promise<void>;
  handleStopLiveSession: (options?: { scheduleReengagement?: boolean }) => Promise<void>;
  handleLiveTurnComplete: (
    userText: string,
    modelText: string,
    userAudioPcm?: Int16Array,
    modelAudioLines?: Int16Array[]
  ) => Promise<void>;
}

/**
 * Hook for managing Gemini Live conversation sessions.
 * Encapsulates all live session lifecycle and turn handling logic.
 */
export const useLiveSessionController = (config: UseLiveSessionControllerConfig): UseLiveSessionControllerReturn => {
  const {
    t,
    setSettings,
    addMessage,
    updateMessage,
    getHistoryRespectingBookmark,
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
    handleUserInputActivity,
    currentSystemPromptText,
    parseGeminiResponse,
    resolveBookmarkContextSummary,
    computeHistorySubsetForMedia,
  } = config;

  const setLastFetchedSuggestionsFor = useMaestroStore(state => state.setLastFetchedSuggestionsFor);

  // Smart refs - always return fresh state from store (no stale closures)
  const settingsRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.settings), []);
  const selectedLanguagePairRef = useMemo(() => createSmartRef(useMaestroStore.getState, selectSelectedLanguagePair), []);
  const messagesRef = useMemo(() => createSmartRef(useMaestroStore.getState, state => state.messages), []);

  // Smart ref with setter - needs custom implementation for write support
  const lastFetchedSuggestionsForRef = useMemo<React.MutableRefObject<string | null>>(() => ({
    get current() {
      return useMaestroStore.getState().lastFetchedSuggestionsFor;
    },
    set current(value) {
      setLastFetchedSuggestionsFor(value);
    },
  }), [setLastFetchedSuggestionsFor]);

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
        // We created this stream for the live session, so stop its tracks
        // and clear liveVideoStream since it's no longer valid.
        stream.getTracks().forEach(t => t.stop());
        setLiveVideoStream(null);
      }
      // When the stream was borrowed from useCameraManager (created === false),
      // it's still active and managed by the camera effect - don't clear
      // liveVideoStream so the camera preview remains visible after the session.
      liveSessionCaptureRef.current = null;
    }
  }, [setLiveVideoStream]);

  /**
   * Restore STT state after live session ends
   */
  const restoreSttAfterLiveSession = useCallback(() => {
    if (liveSessionShouldRestoreSttRef.current) {
      const lang = settingsRef.current.stt.language;
      setSettings(prev => ({ ...prev, stt: { ...prev.stt, enabled: true } }));
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
    return buildLiveSystemInstruction({
      basePrompt: currentSystemPromptText,
      messages: messagesRef.current,
      computeHistorySubsetForMedia,
      resolveBookmarkContextSummary,
    });
  }, [currentSystemPromptText, resolveBookmarkContextSummary, computeHistorySubsetForMedia, messagesRef]);

  /**
   * Handle a completed live turn (user spoke, model responded)
   * @param userText - Transcribed user speech
   * @param modelText - Transcribed model response
   * @param userAudioPcm - User's recorded audio (16kHz)
   * @param modelAudioLines - Model audio pre-split by transcript newlines (24kHz).
   *                          Each element corresponds to a line in modelText.
   */
  const handleLiveTurnComplete = useCallback(async (
    userText: string, 
    modelText: string, 
    userAudioPcm?: Int16Array, 
    modelAudioLines?: Int16Array[]
  ) => {
    try {
      let userMessageId = '';
      let snapshotData: any = null;
      
      // 1. Add User Message with Snapshot & Audio
      if (userText) {
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
          void (async () => {
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
              const uploadedAttachmentState = buildUploadedAttachmentState([
                {
                  id: PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID,
                  uri: up.uri,
                  mimeType: up.mimeType,
                  targets: inferUploadedAttachmentTargetsForMimeType(up.mimeType),
                  source: 'original',
                  order: 10,
                },
              ]);
              
              // 3. Update message with both low-res (local) and URI (remote)
              updateMessage(userMessageId, {
                storageOptimizedImageUrl: optimizedDataUrl,
                storageOptimizedImageMimeType: optimizedMime,
                ...uploadedAttachmentState,
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
          if (userMessageId && !completeHistory.some(message => message.id === userMessageId)) {
            completeHistory.push({
              id: userMessageId,
              role: 'user',
              text: userText,
              timestamp: Date.now()
            } as ChatMessage);
          }
          const assistantHistoryIndex = completeHistory.findIndex(message => message.id === assistantId);
          if (assistantHistoryIndex >= 0) {
            completeHistory[assistantHistoryIndex] = {
              ...completeHistory[assistantHistoryIndex],
              rawAssistantResponse: structuredText,
              translations: translations,
            };
          } else {
            completeHistory.push({
              id: assistantId,
              role: 'assistant',
              text: structuredText,
              rawAssistantResponse: structuredText,
              translations: translations,
              timestamp: Date.now()
            } as ChatMessage);
          }

          // ============================================================================
          // AUDIO-TO-TEXT ALIGNMENT FOR CACHE
          // ============================================================================
          // Use text similarity to map audio segments to correct text lines.
          // Handles: skipped lines, empty markers [sv-SE], truncated text.
          // ============================================================================
          
          if (modelAudioLines && modelAudioLines.length > 0) {
            const targetLang = getPrimaryCode(selectedLanguagePairRef.current.targetLanguageCode);
            const nativeLang = getPrimaryCode(selectedLanguagePairRef.current.nativeLanguageCode);
            
            // Flatten translations to text lines
            const textLines: Array<{text: string; lang: string}> = [];
            translations.forEach(pair => {
              if (pair.target) textLines.push({ text: pair.target, lang: targetLang });
              if (pair.native) textLines.push({ text: pair.native, lang: nativeLang });
            });
            
            // Map audio segments to text lines using text similarity
            const originalTexts = textLines.map(t => t.text);
            const mapping = mapAudioSegmentsToTextLines(originalTexts, modelText, modelAudioLines.length);
            
            console.debug(`[Live] Cache: ${modelAudioLines.length} audio, ${textLines.length} text, mapping=[${mapping.join(',')}]`);
            
            // Cache each audio segment with its matched text
            const voiceName = settingsRef.current.tts.voiceName || 'Kore';
            for (let i = 0; i < modelAudioLines.length; i++) {
              const audioPcm = modelAudioLines[i];
              const lineIdx = mapping[i];
              
              if (!audioPcm || audioPcm.length === 0) continue;
              if (lineIdx === -1 || !textLines[lineIdx]) continue;
              
              const textEntry = textLines[lineIdx];
              const key = computeTtsCacheKey(textEntry.text.trim(), textEntry.lang, 'gemini-live', voiceName);
              console.debug(`[TTS Cache] seg${i}→line${lineIdx} "${textEntry.text.substring(0, 30)}..."`);
              upsertMessageTtsCache(assistantId, {
                key,
                langCode: textEntry.lang,
                provider: 'gemini-live',
                audioDataUrl: pcmToWav(audioPcm, 24000),
                updatedAt: Date.now(),
                voiceName,
              });
          }
          }

          updateMessage(assistantId, {
            rawAssistantResponse: structuredText,
            translations: translations
          });

          // 4. Generate Suggestions Immediately. Live turns rely on this shared
          // path to decide whether to attach an artifact or run a tool request.
          void fetchAndSetReplySuggestions(assistantId, structuredText, getHistoryRespectingBookmark(completeHistory), {
            responseSource: 'live',
          })
            .catch((error) => {
              console.warn('Failed to fetch live reply suggestions', error);
            });
          lastFetchedSuggestionsForRef.current = assistantId;
        }
 

 



              // No base64Image returned — retry if attempts remain
              // Generation threw — retry if attempts remain
      }
    } catch (error) {
      console.error('Failed to process live turn completion:', error);
    }
  }, [
    addMessage, 
    captureSnapshot, 
    t, 
    parseGeminiResponse, 
    fetchAndSetReplySuggestions, 
    getHistoryRespectingBookmark, 
    upsertMessageTtsCache, 
    updateMessage, 
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
          const token = addActivityToken(TOKEN_CATEGORY.LIVE, TOKEN_SUBTYPE.SESSION);
          liveUiTokenRef.current = token;
        }
      } else {
        if (liveUiTokenRef.current) {
          removeActivityToken(liveUiTokenRef.current);
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

        // Request BOTH permissions upfront to avoid double prompts or late mic requests
        stream = await navigator.mediaDevices.getUserMedia({
          video: videoConstraints,
          audio: true
        });

        // We only use this stream for Video in the session.
        // The audio handling (Worklet) creates its own dedicated audio stream.
        // To prevent hardware conflicts or echo, we stop the audio tracks on this "permission-priming" stream.
        // The permission grant itself persists for the page context.
        stream.getAudioTracks().forEach(track => track.stop());

        createdStream = true;
        setLiveVideoStream(stream);
      }

      if (!stream || !stream.active) {
        throw new Error(t('error.cameraStreamNotAvailable'));
      }

      liveSessionCaptureRef.current = { stream, created: createdStream };

      if (settingsRef.current.stt.enabled) {
        liveSessionShouldRestoreSttRef.current = true;
        setSettings(prev => ({ ...prev, stt: { ...prev.stt, enabled: false } }));
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
      const voiceName = settingsRef.current.tts.voiceName || 'Kore';

      await startLiveConversation({
        stream,
        videoElement: visualContextVideoRef.current,
        systemInstruction: liveSystemInstruction,
        voiceName,
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
    visualContextStreamRef,
    visualContextVideoRef,
  ]);

  /**
   * Stop the current Gemini Live conversation session
   */
  const handleStopLiveSession = useCallback(async (options?: { scheduleReengagement?: boolean }) => {
    const scheduleStopReengagement = options?.scheduleReengagement ?? true;
    try {
      await stopLiveConversation();
    } catch (error) {
      console.warn('Failed to stop live session', error);
    } finally {
      releaseLiveSessionCapture();
      restoreSttAfterLiveSession();
      setLiveSessionError(null);
      if (scheduleStopReengagement) {
        scheduleReengagement('live-session-stopped');
      }
    }
  }, [releaseLiveSessionCapture, restoreSttAfterLiveSession, scheduleReengagement, stopLiveConversation]);

  return {
    liveSessionState,
    liveSessionError,
    handleStartLiveSession,
    handleStopLiveSession,
    handleLiveTurnComplete,
  };
};

export default useLiveSessionController;
