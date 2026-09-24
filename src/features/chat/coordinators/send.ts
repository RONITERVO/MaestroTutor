// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { ConversationDiagnostics } from './conversationContracts';
import { createSendRequestPreparer } from './sendRequest';
import type { sanitizeHistoryWithVerifiedUris as sanitizeHistory } from '../../../api/gemini/files';
import type { ensureMaestroAvatarUris as ensureAvatar } from '../../../api/gemini/maestroAvatarEnsure';
import { ApiError } from '../../../core-sdk/errors';
import { TOKEN_CATEGORY, TOKEN_SUBTYPE, type TokenCategory } from '../../../core/config/activityTokens';
import { IMAGE_GEN_CAMERA_ID } from '../../../core/config/app';
import type { AppSettings, ChatMessage, GroundingChunk, LanguagePair, RecordedUtterance } from '../../../core/types';
import type { getGlobalProfileDB as getProfile } from '../../session';
import type { processMediaForUpload as processMedia } from '../../vision';
import type { createAttachmentUploads } from './attachmentUploads';
import type { UseTutorConversationConfig as Config, UseTutorConversationReturn as Conversation, MutableValue } from './conversationContracts';
import type { createGeneratedImages } from './generatedImages';
import type { SetSendPrep } from './mediaPersistence';
import type { createTextResponseCoordinator } from './textResponse';
import type { createUserMessageCoordinator } from './userMessage';

export interface SendCoordinatorPorts {
  diagnostics: Pick<ConversationDiagnostics, 'logSttFlow' | 'warnSttFlow' | 'errorSttFlow'>;
  state: Pick<Config, 'maestroAvatarUriRef' | 'maestroAvatarMimeTypeRef'> & {
    settingsRef: MutableValue<AppSettings>;
    selectedLanguagePairRef: MutableValue<LanguagePair | undefined>;
    messagesRef: MutableValue<ChatMessage[]>;
    isLoadingHistoryRef: MutableValue<boolean>;
    isResponsePendingRef: MutableValue<boolean>;
    speechIsSpeakingRef: MutableValue<boolean>;
    sendingTokenRef: MutableValue<string | null>;
    suggestionsTokenRef: MutableValue<string | null>;
    recordedUtterancePendingRef: MutableValue<RecordedUtterance | null>;
    sttInterruptedBySendRef: MutableValue<boolean>;
    sendWithFileUploadInProgressRef: MutableValue<boolean>;
    lastFetchedSuggestionsForRef: MutableValue<string | null>;
    pendingRecordedAudioMessageRef: MutableValue<string | null>;
  };
  view: Pick<Config, 't' | 'setReplySuggestions' | 'setSnapshotUserError' | 'onApiKeyGateOpen' | 'currentSystemPromptText' | 'transcript'> & {
    attachedFileName: string | null;
    setSuggestionsLoadingStreamText(text: string): void;
    setSendPrep: SetSendPrep;
    setAttachedImage(data: string | null, mime: string | null): void;
    setLatestGroundingChunks(chunks: GroundingChunk[] | undefined): void;
  };
  speech: Pick<Config, 'claimRecordedUtterance' | 'stopListening' | 'clearTranscript' | 'startListening' | 'speakMessage' | 'hasPendingQueueItems' | 'isSpeechSynthesisSupported'> & { isListening(): boolean };
  activity: { addActivityToken(category: TokenCategory, subtype: string): string; removeActivityToken(token: string): void };
  reengagement: Pick<Config, 'scheduleReengagementRef' | 'cancelReengagementRef'>;
  messages: Pick<Config, 'addMessage' | 'updateMessage'>;
  context: Pick<Config, 'getHistoryRespectingBookmark' | 'computeMaxMessagesForArray'> & {
    resolveBookmarkContextSummary: Conversation['resolveBookmarkContextSummary'];
    getGlobalProfileDB: typeof getProfile;
    ensureMaestroAvatarUris: typeof ensureAvatar;
    sanitizeHistoryWithVerifiedUris: typeof sanitizeHistory;
  };
  attachments: ReturnType<typeof createAttachmentUploads> & { processMediaForUpload: typeof processMedia };
  journeys: {
    createUserMessage: ReturnType<typeof createUserMessageCoordinator>;
    handleGeminiResponse: ReturnType<typeof createTextResponseCoordinator>;
    runUserImageGeneration: ReturnType<typeof createGeneratedImages>['runUserImageGeneration'];
    requestReplySuggestions(id: string, text: string, history: ChatMessage[]): void;
  };
}

/** Send transaction coordinator. The React adapter supplies current-state cells
 * separately from render values and typed capability ports. This owner controls
 * request order, failure conversion, activity and speech/re-engagement handoff. */
export function createSendCoordinator(ports: SendCoordinatorPorts) {
  const { logSttFlow, warnSttFlow, errorSttFlow } = ports.diagnostics;
  const prepareRequest = createSendRequestPreparer({ ...ports, updateMessage: ports.messages.updateMessage, runUserImageGeneration: ports.journeys.runUserImageGeneration });
  const { settingsRef, selectedLanguagePairRef, messagesRef, isLoadingHistoryRef, isResponsePendingRef, speechIsSpeakingRef, sendingTokenRef, suggestionsTokenRef, recordedUtterancePendingRef, sttInterruptedBySendRef, sendWithFileUploadInProgressRef, lastFetchedSuggestionsForRef, pendingRecordedAudioMessageRef, } = ports.state;
  const { t, setReplySuggestions, setSuggestionsLoadingStreamText, setSnapshotUserError, setSendPrep, setAttachedImage, onApiKeyGateOpen, transcript } = ports.view;
  const { claimRecordedUtterance, stopListening, clearTranscript, startListening, speakMessage, hasPendingQueueItems, isSpeechSynthesisSupported, isListening } = ports.speech;
  const { addActivityToken, removeActivityToken } = ports.activity;
  const { scheduleReengagementRef, cancelReengagementRef } = ports.reengagement;
  const { addMessage, updateMessage } = ports.messages;
  const { getHistoryRespectingBookmark, } = ports.context;
  const { createUserMessage, handleGeminiResponse, requestReplySuggestions } = ports.journeys;
  return async (
    text: string,
    passedImageBase64?: string,
    passedImageMimeType?: string,
    messageType: 'user' | 'conversational-reengagement' | 'image-reengagement' = 'user',
    options?: { triggeredByStt?: boolean }
  ): Promise<boolean> => {
    let sendStage = 'send.enter';
    const markSendStage = (stage: string, details?: Record<string, unknown>) => {
      sendStage = stage;
      logSttFlow(stage, details);
    };
    markSendStage('send.start', {
      messageType,
      textLength: text.length,
      triggeredByStt: options?.triggeredByStt === true,
      hasPassedImage: Boolean(passedImageBase64),
      isLoadingHistory: isLoadingHistoryRef.current,
      responsePending: isResponsePendingRef.current,
      speaking: speechIsSpeakingRef.current,
    });
    if (isLoadingHistoryRef.current) {
      warnSttFlow('send.skip.loadingHistory', {
        messageType,
      });
      return false;
    }
    if (!text && !passedImageBase64 && messageType === 'user') {
      warnSttFlow('send.skip.emptyUserMessage', {
        triggeredByStt: options?.triggeredByStt === true,
      });
      return false;
    }
    if (!selectedLanguagePairRef.current) {
      errorSttFlow('send.skip.noLanguagePair', {
        messageType,
      });
      console.error("No language pair selected, cannot send message.");
      addMessage({ role: 'error', text: t('error.noLanguagePair') });
      return false;
    }

    if (sendingTokenRef.current || isResponsePendingRef.current || speechIsSpeakingRef.current) {
      warnSttFlow('send.skip.busy', {
        responsePending: isResponsePendingRef.current,
        speaking: speechIsSpeakingRef.current,
      });
      return false;
    }

    // Add sending token for unified busy state tracking (replaces setIsSending(true))
    sendingTokenRef.current = addActivityToken(TOKEN_CATEGORY.GEN, TOKEN_SUBTYPE.RESPONSE);
    const isListeningNow = isListening();
    const shouldResumeSttAfterSend = settingsRef.current.stt.enabled && (isListeningNow || options?.triggeredByStt === true);
    markSendStage('send.token.added', {
      isListeningNow,
      shouldResumeSttAfterSend,
    });
    if (settingsRef.current.stt.enabled && isListeningNow) {
      const claimedUtterance = typeof claimRecordedUtterance === 'function' ? claimRecordedUtterance() : null;
      if (claimedUtterance) {
        recordedUtterancePendingRef.current = claimedUtterance;
      }
      try {
        markSendStage('send.stopListening.start', {
          hadClaimedUtterance: Boolean(claimedUtterance),
        });
        await Promise.resolve(stopListening());
        markSendStage('send.stopListening.done');
      } catch {
        warnSttFlow('send.stopListening.error', {
          stage: sendStage,
        });
        /* ignore */
      }
      clearTranscript();
      markSendStage('send.clearTranscript.afterStop');
    } else if (options?.triggeredByStt) {
      clearTranscript();
      markSendStage('send.clearTranscript.sttTriggered');
    }

    if (shouldResumeSttAfterSend) {
      sttInterruptedBySendRef.current = true;
    } else {
      sttInterruptedBySendRef.current = false;
    }
    sendWithFileUploadInProgressRef.current = true;
    setReplySuggestions([]);
    setSuggestionsLoadingStreamText('');
    // Clear any lingering suggestions token
    if (suggestionsTokenRef.current) {
      removeActivityToken(suggestionsTokenRef.current);
      suggestionsTokenRef.current = null;
    }
    lastFetchedSuggestionsForRef.current = null;

    if (messageType === 'user') {
      // Clear any previous snapshot errors
      if (setSnapshotUserError) setSnapshotUserError(null);
    }
    pendingRecordedAudioMessageRef.current = null;
    let thinkingMessageId: string | null = null;
    const handleSendFailure = (error: unknown): false => {
      errorSttFlow('send.failure', {
        stage: sendStage,
        messageType,
        triggeredByStt: options?.triggeredByStt === true,
        message: error instanceof Error ? error.message : String(error),
      });
      console.error("Error sending message (stream consumer):", error);
      let errorMessage = t('general.error');
      if (error instanceof ApiError) {
        if (error.code === 'MISSING_API_KEY') {
          errorMessage = t('error.apiKeyMissing');
          onApiKeyGateOpen?.({ reason: 'missing', instructionIndex: 0 });
        } else if (isInvalidApiKeyError(error)) {
          errorMessage = t('error.apiKeyInvalid');
          onApiKeyGateOpen?.({ reason: 'invalid', instructionIndex: 0 });
        } else if (isQuotaError(error)) {
          errorMessage = t('error.apiQuotaExceeded');
        } else {
          const parsedMessage = parseApiErrorMessage(error.message);
          errorMessage = parsedMessage || error.code || `HTTP ${error.status}`;
        }
      } else if (error instanceof Error) {
        errorMessage = error.message;
      }
      const isQuota = error instanceof ApiError && isQuotaError(error);

      if (thinkingMessageId) {
        updateMessage(thinkingMessageId, {
          thinking: false,
          thinkingTrace: undefined,
          thinkingDraftText: undefined,
          thinkingPhase: undefined,
          thinkingStatusLine: undefined,
          role: 'error',
          text: errorMessage,
          llmRawResponse: undefined,
          rawAssistantResponse: undefined,
          translations: undefined,
          isLoadingArtifact: false,
          artifactLoadStartTime: undefined,
          ...(isQuota ? { errorAction: 'quota' } : {}),
        });
      } else {
        addMessage({
          role: 'error',
          text: errorMessage,
          ...(isQuota ? { errorAction: 'quota' as const } : {}),
        });
      }

      if (sendingTokenRef.current) {
        removeActivityToken(sendingTokenRef.current);
        sendingTokenRef.current = null;
      }
      setSendPrep(null);
      try {
        sendWithFileUploadInProgressRef.current = false;
      } catch { /* ignore */ }

      if (sttInterruptedBySendRef.current && settingsRef.current.stt.enabled && !speechIsSpeakingRef.current) {
        try {
          startListening(settingsRef.current.stt.language);
        } finally {
          sttInterruptedBySendRef.current = false;
        }
      }

      scheduleReengagementRef.current('send-error');

      if (messageType === 'user') {
        setAttachedImage(null, null);
      }
      setReplySuggestions([]);
      setSuggestionsLoadingStreamText('');
      if (suggestionsTokenRef.current) {
        removeActivityToken(suggestionsTokenRef.current);
        suggestionsTokenRef.current = null;
      }
      return false;
    };

    try {
      const currentSettingsVal = settingsRef.current;
      const shouldGenerateUserImage = currentSettingsVal.selectedCameraId === IMAGE_GEN_CAMERA_ID;
      markSendStage('send.createUserMessage.await', {
        shouldGenerateUserImage,
      });
      const userMessageContext = await createUserMessage({
        text,
        passedImageBase64,
        passedImageMimeType,
        messageType,
        shouldGenerateUserImage,
        currentSettingsVal,
        triggeredByStt: options?.triggeredByStt === true,
      });
      markSendStage('send.createUserMessage.done', {
        userMessageId: userMessageContext.userMessageId,
        hasRecordedAudio: Boolean(userMessageContext.recordedSpeechForMessage),
        hasImage: Boolean(userMessageContext.userImageToProcessBase64),
      });

      thinkingMessageId = addMessage({
        role: 'assistant',
        thinking: true,
        thinkingTrace: [],
        thinkingDraftText: '',
        thinkingPhase: t('streaming.phasePreparingRequest') || 'Preparing request',
        thinkingStatusLine: t('streaming.preparingRequestContext') || 'Preparing request context...',
      });
      markSendStage('send.thinkingMessage.created', {
        thinkingMessageId,
      });

      cancelReengagementRef.current();

      try {
        const { geminiPromptText, systemInstructionForGemini, imageForGeminiContextFileUri, sanitizedDerivedHistory } = await prepareRequest({
          thinkingMessageId, userMessageContext, currentSettingsVal, shouldGenerateUserImage,
          messageType, passedImageBase64, passedImageMimeType, markSendStage,
        });

        markSendStage('send.gemini.await', {
          historyCount: sanitizedDerivedHistory.length,
          filePartCount: imageForGeminiContextFileUri?.length || 0,
        });
        const { finalMessageUpdates } = await handleGeminiResponse({
          thinkingMessageId,
          geminiPromptText,
          sanitizedDerivedHistory,
          systemInstructionForGemini,
          imageForGeminiContextFileUri,
          currentSettingsVal,
        });
        markSendStage('send.gemini.done', {
          thinkingMessageId,
          hasRawResponse: Boolean(finalMessageUpdates.llmRawResponse || finalMessageUpdates.rawAssistantResponse),
        });

        // Early suggestion fetch
        try {
          const textForSuggestionsEarly = finalMessageUpdates.llmRawResponse || finalMessageUpdates.rawAssistantResponse || (finalMessageUpdates.translations?.find(tr => tr.target)?.target) || "";
          // Check if already loading suggestions via token
          if (!suggestionsTokenRef.current && textForSuggestionsEarly.trim()) {
            const historyWithFinalAssistant = messagesRef.current.map(m =>
              m.id === thinkingMessageId ? ({ ...m, ...finalMessageUpdates }) : m
            );
            requestReplySuggestions(thinkingMessageId, textForSuggestionsEarly, getHistoryRespectingBookmark(historyWithFinalAssistant));
          }
        } catch (e) {
          console.warn('Failed to prefetch suggestions before TTS:', e);
        }

        // Speak the response
        const originalMessage = messagesRef.current.find(m => m.id === thinkingMessageId);
        if (originalMessage) {
          const finalMessageForSpeech = { ...originalMessage, ...finalMessageUpdates };
          speakMessage(finalMessageForSpeech);
        }

        if (messageType === 'user') {
          setAttachedImage(null, null);
          if (text === (transcript || '') && (transcript || '').length > 0) {
            clearTranscript();
          }
        }

        await new Promise(resolve => setTimeout(resolve, 0));

        try {
          sendWithFileUploadInProgressRef.current = false;
        } catch { /* ignore */ }
        // Remove sending token (replaces setIsSending(false))
        if (sendingTokenRef.current) {
          removeActivityToken(sendingTokenRef.current);
          sendingTokenRef.current = null;
        }
        setSendPrep(null);
        scheduleReengagementRef.current('send-complete');
        markSendStage('send.complete', {
          thinkingMessageId,
        });

        // Resume STT if needed
        const isSpeechActive = speechIsSpeakingRef.current || (typeof hasPendingQueueItems === 'function' && hasPendingQueueItems());
        if (sttInterruptedBySendRef.current && settingsRef.current.stt.enabled && !isSpeechActive) {
          try {
            markSendStage('send.resumeStt.start');
            startListening(settingsRef.current.stt.language);
            markSendStage('send.resumeStt.done');
          } finally {
            sttInterruptedBySendRef.current = false;
          }
        }

        // Fetch suggestions if TTS not supported
        if (!isSpeechSynthesisSupported) {
          const finalAssistantMessage = messagesRef.current.find(m => m.id === thinkingMessageId);
          if (finalAssistantMessage && finalAssistantMessage.role === 'assistant' &&
            (finalAssistantMessage.llmRawResponse || finalAssistantMessage.rawAssistantResponse || (finalAssistantMessage.translations && finalAssistantMessage.translations.length > 0)) &&
            !suggestionsTokenRef.current &&
            finalAssistantMessage.id !== lastFetchedSuggestionsForRef.current) {
            const textForSuggestions = finalAssistantMessage.llmRawResponse ||
              finalAssistantMessage.rawAssistantResponse ||
              (finalAssistantMessage.translations?.find(tr => tr.target)?.target) || "";
            if (textForSuggestions.trim()) {
              requestReplySuggestions(finalAssistantMessage.id, textForSuggestions, getHistoryRespectingBookmark(messagesRef.current));
            }
          }
        }

        return true;

      } catch (error) {
        return handleSendFailure(error);
      }
    } catch (error) {
      return handleSendFailure(error);
    }
  };
}

const parseApiErrorMessage = (message?: string): string => {
  if (!message) return '';
  const trimmed = message.trim();
  if (!trimmed) return '';
  try {
    const parsed = JSON.parse(trimmed);
    const nestedMessage = parsed?.error?.message;
    if (typeof nestedMessage === 'string' && nestedMessage.trim()) {
      return nestedMessage.trim();
    }
  } catch {
    // Ignore JSON parse errors
  }
  return trimmed;
};

const isQuotaError = (error: ApiError): boolean => {
  if (error.status === 429) return true;
  const code = (error.code ?? '').toString().toLowerCase();
  if (code === '429' || code.includes('resource_exhausted') || code.includes('quota')) return true;
  const msg = parseApiErrorMessage(error.message).toLowerCase();
  return (
    msg.includes('resource_exhausted') ||
    msg.includes('exceeded your current quota') ||
    msg.includes('rate limit') ||
    msg.includes('quota')
  );
};

const isInvalidApiKeyError = (error: ApiError): boolean => {
  const msg = (error.message || '').toLowerCase();
  // Check for both the RPC reason and the message content
  return msg.includes('api_key_invalid') || msg.includes('api key not valid');
};
