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
 * - Image generation during live turns
 * - Reply suggestion generation
 */

import { useCallback, useRef, useMemo } from 'react';
import { 
  ChatMessage, 
  AppSettings,
  RecordedUtterance,
  TtsAudioCacheEntry 
} from '../../../core/types';
import { useGeminiLiveConversation, LiveSessionState, pcmToWav } from '../../speech';
import { sanitizeHistoryWithVerifiedUris, uploadMediaToFiles } from '../../../api/gemini/files';
import { generateImage } from '../../../api/gemini/vision';
import { getGlobalProfileDB } from '../../session';
import { deriveHistoryForApi, computeTtsCacheKey } from '../../chat';
import { processMediaForUpload } from '../../vision';
import { MAX_MEDIA_TO_KEEP } from '../../../core/config/app';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE, type TokenCategory } from '../../../core/config/activityTokens';
import { 
  DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE, 
  IMAGE_GEN_SYSTEM_INSTRUCTION, 
  IMAGE_GEN_USER_PROMPT_TEMPLATE 
} from '../../../core/config/prompts';
import { getPrimaryCode } from '../../../shared/utils/languageUtils';
import type { TranslationFunction } from '../../../app/hooks/useTranslations';
import { useMaestroStore } from '../../../store';
import { selectSelectedLanguagePair } from '../../../store/slices/settingsSlice';
import { createSmartRef } from '../../../shared/utils/smartRef';

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
  handleStopLiveSession: () => Promise<void>;
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
    handleUserInputActivity,
    currentSystemPromptText,
    parseGeminiResponse,
    resolveBookmarkContextSummary,
    computeHistorySubsetForMedia,
    maestroAvatarUriRef,
    maestroAvatarMimeTypeRef,
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
    let userMessageId = '';
    let snapshotData: any = null;
    let snapshotUploadPromise: Promise<{ uri: string; mimeType: string } | null> | null = null;
    
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
        snapshotUploadPromise = (async () => {
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
            return { uri: up.uri, mimeType: up.mimeType };
          } catch (e) {
            console.warn('Upload failed', e);
            // Still update persistence image
            updateMessage(userMessageId, {
              storageOptimizedImageUrl: optimizedDataUrl,
              storageOptimizedImageMimeType: optimizedMime
            });
            return null;
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

          // ============================================================================
          // AUDIO-TO-TEXT ALIGNMENT FOR GOOGLE LIVE API
          // ============================================================================
          // 
          // KNOWN BEHAVIOR (as of Jan 2026):
          // Google's Live API transcript includes short intro lines (e.g., "¡Perfecto!" / 
          // "Täydellistä!") that are transcribed but NOT included in the audio stream.
          // The audio segments start AFTER these intro lines, causing an offset.
          //
          // Example:
          //   Transcript: "¡Perfecto!", "Täydellistä!", "Describiste...", "Kuvasit..."  (8 lines)
          //   Audio:      [segment for "Describiste..."], [segment for "Kuvasit..."]   (6 segments)
          //   Offset:     8 - 6 = 2 → skip first 2 text lines when mapping audio
          //
          // TO REVERT (if Google fixes this behavior):
          //   Set ENABLE_LIVE_AUDIO_TEXT_OFFSET_COMPENSATION = false below.
          //   This will use 1:1 mapping (audio[0] → text[0], audio[1] → text[1], etc.)
          // ============================================================================
          
          // Toggle this to false if Google Live API starts including all lines in audio
          const ENABLE_LIVE_AUDIO_TEXT_OFFSET_COMPENSATION = true;
          
          if (modelAudioLines && modelAudioLines.length > 0) {
            const targetLang = getPrimaryCode(selectedLanguagePairRef.current.targetLanguageCode);
            const nativeLang = getPrimaryCode(selectedLanguagePairRef.current.nativeLanguageCode);
            
            // Flatten translations to a linear list of text lines with their languages
            const textLines: Array<{text: string; lang: string}> = [];
            translations.forEach(pair => {
              if (pair.target) textLines.push({ text: pair.target, lang: targetLang });
              if (pair.native) textLines.push({ text: pair.native, lang: nativeLang });
            });
            
            // Calculate offset: intro lines in transcript that have no audio
            // When disabled, offset = 0 for direct 1:1 mapping
            const offset = ENABLE_LIVE_AUDIO_TEXT_OFFSET_COMPENSATION
              ? Math.max(0, textLines.length - modelAudioLines.length)
              : 0;
            
            if (offset > 0) {
              const skippedLines = textLines.slice(0, offset).map(l => l.text).join(' / ');
              console.debug(
                `[Live] Audio-text offset: ${offset} intro text lines without audio. ` +
                `Skipped: "${skippedLines.substring(0, 80)}${skippedLines.length > 80 ? '...' : ''}"`
              );
            } else if (textLines.length !== modelAudioLines.length) {
              console.debug(
                `[Live] Audio-text count: ${modelAudioLines.length} audio segments for ${textLines.length} text lines.`
              );
            }
            
            // Map audio segments to text lines, applying offset if enabled
            // Use voiceName from settings to match the key used during playback lookup
            const voiceName = settingsRef.current.tts.voiceName || 'Kore';
            for (let i = 0; i < modelAudioLines.length && (i + offset) < textLines.length; i++) {
              const audioPcm = modelAudioLines[i];
              const textEntry = textLines[i + offset];
              
              if (audioPcm && audioPcm.length > 0 && textEntry) {
                const cleanedText = textEntry.text.trim();
                const key = computeTtsCacheKey(cleanedText, textEntry.lang, 'gemini-live', voiceName);
                console.debug(`[TTS Cache] STORE key=${key.substring(0, 20)}... text="${cleanedText.substring(0, 30)}..." voice=${voiceName}`);
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
        
        let latestSnapshotUri: string | undefined;
        let latestSnapshotMime: string | undefined;
        if (snapshotUploadPromise) {
          try {
            const up = await snapshotUploadPromise;
            if (up?.uri) {
              latestSnapshotUri = up.uri;
              latestSnapshotMime = up.mimeType;
            }
          } catch { /* ignore */ }
        }

        // Add current turn if not in history subset yet
        if (userText && !apiHistory.some(h => h.role === 'user' && h.text === userText)) {
          apiHistory.push({ role: 'user', text: userText });
        }

        if (userText && latestSnapshotUri) {
          const reverseIndex = [...apiHistory].reverse().findIndex(h => h.role === 'user' && h.text === userText);
          if (reverseIndex >= 0) {
            const idx = apiHistory.length - 1 - reverseIndex;
            apiHistory[idx].imageFileUri = latestSnapshotUri;
            apiHistory[idx].imageMimeType = latestSnapshotMime;
          } else {
            apiHistory.push({ role: 'user', text: userText, imageFileUri: latestSnapshotUri, imageMimeType: latestSnapshotMime });
          }
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

export default useLiveSessionController;
