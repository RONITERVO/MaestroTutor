// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { AppSettings, ChatMessage } from '../../../core/types';
import type { SendCoordinatorPorts } from './send';
import type { HistoryMediaOverride } from './attachmentUploads';
import { getMessageAttachmentSource } from './attachmentUploads';
import { deriveBrowserTutorHistory } from '../../../core-sdk/chat/history';
import { REENGAGEMENT_PROMPT, composeMaestroSystemInstruction } from '../../../core/config/prompts';
import { MAX_MEDIA_TO_KEEP } from '../../../core/config/app';

export interface SendRequestPorts {
  state: Pick<SendCoordinatorPorts['state'], 'messagesRef' | 'sendWithFileUploadInProgressRef' | 'maestroAvatarUriRef' | 'maestroAvatarMimeTypeRef'>;
  view: Pick<SendCoordinatorPorts['view'], 't' | 'currentSystemPromptText' | 'attachedFileName' | 'setSendPrep' | 'setLatestGroundingChunks'>;
  context: SendCoordinatorPorts['context'];
  attachments: SendCoordinatorPorts['attachments'];
  updateMessage: SendCoordinatorPorts['messages']['updateMessage'];
  runUserImageGeneration: SendCoordinatorPorts['journeys']['runUserImageGeneration'];
}

/** Prepare media, history, profile and avatar before the text journey. Reads
 * history again after asynchronous uploads, preserving the existing send policy. */
export function createSendRequestPreparer(ports: SendRequestPorts) {
  const { messagesRef, sendWithFileUploadInProgressRef, maestroAvatarUriRef, maestroAvatarMimeTypeRef } = ports.state;
  const { t, currentSystemPromptText, attachedFileName, setSendPrep, setLatestGroundingChunks } = ports.view;
  const { getHistoryRespectingBookmark, computeMaxMessagesForArray, resolveBookmarkContextSummary, getGlobalProfileDB, ensureMaestroAvatarUris, sanitizeHistoryWithVerifiedUris } = ports.context;
  const { ensureUploadedAttachmentVariantsForMessage, uploadAttachmentVariantsForSource, ensureUrisForHistoryForSend, processMediaForUpload } = ports.attachments;
  const { updateMessage, runUserImageGeneration } = ports;
  return async (input: {
    thinkingMessageId: string;
    userMessageContext: Awaited<ReturnType<SendCoordinatorPorts['journeys']['createUserMessage']>>;
    currentSettingsVal: AppSettings;
    shouldGenerateUserImage: boolean;
    messageType: 'user' | 'conversational-reengagement' | 'image-reengagement';
    passedImageBase64?: string;
    passedImageMimeType?: string;
    markSendStage(stage: string, details?: Record<string, unknown>): void;
  }) => {
    const { thinkingMessageId, userMessageContext, currentSettingsVal, shouldGenerateUserImage, messageType, passedImageBase64, passedImageMimeType, markSendStage } = input;
    let {
      userMessageId,
      userMessageText,
      userImageToProcessBase64,
      userImageToProcessMimeType,
      userImageToProcessStorageOptimizedBase64,
      userImageToProcessStorageOptimizedMimeType,
    } = userMessageContext;
    let historyForGemini = messagesRef.current.filter(m => m.id !== thinkingMessageId);
    if (messageType === 'user' && userMessageId) {
      historyForGemini = historyForGemini.filter(m => m.id !== userMessageId);
    }

    let geminiPromptText: string;
    let systemInstructionForGemini: string = currentSystemPromptText;
    try {
      markSendStage('send.systemInstruction.globalProfile.start');
      await getGlobalProfileDB();
    } finally {
      systemInstructionForGemini = composeMaestroSystemInstruction(systemInstructionForGemini);
      markSendStage('send.systemInstruction.globalProfile.done', {
        systemInstructionLength: systemInstructionForGemini.length,
      });
    }

    // Optimize user image if needed
    if (messageType === 'user' && userImageToProcessBase64 && !userImageToProcessStorageOptimizedBase64 && userImageToProcessMimeType) {
      if (!sendWithFileUploadInProgressRef.current) {
        sendWithFileUploadInProgressRef.current = true;
      }
      try {
        markSendStage('send.currentMedia.optimize.start', {
          userMessageId,
        });
        setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...' });
        const optimized = await processMediaForUpload(userImageToProcessBase64, userImageToProcessMimeType, {
          t,
          onProgress: (label, done, total, etaMs) => setSendPrep({ active: true, label, done, total, etaMs })
        });
        userImageToProcessStorageOptimizedBase64 = optimized.dataUrl;
        userImageToProcessStorageOptimizedMimeType = optimized.mimeType;

        if (messageType === 'user' && userMessageId) {
          updateMessage(userMessageId, { storageOptimizedImageUrl: optimized.dataUrl, storageOptimizedImageMimeType: optimized.mimeType });
        }
        markSendStage('send.currentMedia.optimize.done', {
          userMessageId,
        });
      } catch (e) {
        console.warn('Failed to derive low-res for current user media, will omit persistence media', e);
      } finally {
        setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...' } : prev));
      }
    }

    // Decide which image to upload to Gemini
    let imageForGeminiContextBase64: string | undefined;
    let imageForGeminiContextMimeType: string | undefined;

    if (messageType === 'user') {
      if (userImageToProcessBase64) {
        imageForGeminiContextBase64 = userImageToProcessBase64;
        imageForGeminiContextMimeType = userImageToProcessMimeType;
      } else {
        imageForGeminiContextBase64 = userImageToProcessStorageOptimizedBase64;
        imageForGeminiContextMimeType = userImageToProcessStorageOptimizedMimeType;
      }
    } else {
      imageForGeminiContextBase64 = (typeof passedImageBase64 === 'string' && passedImageBase64) ? passedImageBase64 : undefined;
      imageForGeminiContextMimeType = (typeof passedImageMimeType === 'string' && passedImageMimeType) ? passedImageMimeType : undefined;
    }

    let imageForGeminiContextFileUri: Array<{ fileUri: string; mimeType: string }> | undefined = undefined;

    switch (messageType) {
      case 'image-reengagement':
        geminiPromptText = REENGAGEMENT_PROMPT;
        break;
      case 'conversational-reengagement':
        geminiPromptText = REENGAGEMENT_PROMPT;
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
        imageForGeminiContextBase64 = passedImageBase64;
        imageForGeminiContextMimeType = passedImageMimeType;
      }
    }

    if (messageType === 'user' && userMessageId) {
      const currentUserMessage = messagesRef.current.find(m => m.id === userMessageId);
      if (currentUserMessage && getMessageAttachmentSource(currentUserMessage)) {
        if (!sendWithFileUploadInProgressRef.current) {
          sendWithFileUploadInProgressRef.current = true;
        }
        setSendPrep({ active: true, label: t('chat.sendPrep.uploadingMedia') || 'Uploading media...' });
        try {
          markSendStage('send.currentAttachment.upload.start', {
            userMessageId,
          });
          const ensured = await ensureUploadedAttachmentVariantsForMessage(currentUserMessage);
          if (ensured.chatFileParts.length === 0) {
            throw new Error(t('streaming.failedToPrepareCurrent', { name: currentUserMessage.attachmentName || 'attachment' }) || `Failed to prepare attached media "${currentUserMessage.attachmentName || 'attachment'}" for Gemini. Try again or reattach the file.`);
          }
          imageForGeminiContextFileUri = ensured.chatFileParts;
          markSendStage('send.currentAttachment.upload.done', {
            filePartCount: ensured.chatFileParts.length,
          });
        } finally {
          setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...' } : prev));
        }
      }
    } else if (imageForGeminiContextBase64 && imageForGeminiContextMimeType) {
      if (!sendWithFileUploadInProgressRef.current) {
        sendWithFileUploadInProgressRef.current = true;
      }
      setSendPrep({ active: true, label: t('chat.sendPrep.uploadingMedia') || 'Uploading media...' });
      try {
        markSendStage('send.inlineMedia.upload.start', {
          mimeType: imageForGeminiContextMimeType,
        });
        const chatFileParts = await uploadAttachmentVariantsForSource(
          {
            dataUrl: imageForGeminiContextBase64,
            mimeType: imageForGeminiContextMimeType,
            attachmentName: attachedFileName || undefined,
          },
          attachedFileName || 'current-user-media'
        );
        if (chatFileParts.length === 0) {
          throw new Error(t('streaming.failedToPrepareCurrent', { name: attachedFileName || 'attachment' }) || `Failed to prepare attached media "${attachedFileName || 'attachment'}" for Gemini. Try again or reattach the file.`);
        }
        imageForGeminiContextFileUri = chatFileParts;
        markSendStage('send.inlineMedia.upload.done', {
          filePartCount: chatFileParts.length,
        });
      } finally {
        setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...' } : prev));
      }
    }

    setLatestGroundingChunks(undefined);

    const historySubsetForSend: ChatMessage[] = getHistoryRespectingBookmark(historyForGemini);

    let ensuredUpdates: Record<string, HistoryMediaOverride> = {};
    try {
      markSendStage('send.history.ensureUris.start', {
        historyCount: historySubsetForSend.length,
      });
      ensuredUpdates = await ensureUrisForHistoryForSend(historySubsetForSend, (done, total, etaMs) => {
        setSendPrep({ active: true, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...', done, total, etaMs });
      });
      markSendStage('send.history.ensureUris.done', {
        updatedMessageCount: Object.keys(ensuredUpdates).length,
      });
    } finally {
      setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.finalizing') || 'Finalizing...' } : prev));
    }

    let historyForGeminiPostEnsure = messagesRef.current.filter(m => m.id !== thinkingMessageId);
    if (messageType === 'user' && userMessageId) {
      historyForGeminiPostEnsure = historyForGeminiPostEnsure.filter(m => m.id !== userMessageId);
    }
    const historySubsetForSendFinal: ChatMessage[] = getHistoryRespectingBookmark(historyForGeminiPostEnsure)
      .map((m: ChatMessage) => {
        const upd = ensuredUpdates[m.id];
        if (!upd) return m;
        if (upd.omitFromHistory) {
          return {
            ...m,
            uploadedFileVariants: undefined,
          } as ChatMessage;
        }
        if (upd.newVariants) {
          const nextVariants = upd.newVariants || m.uploadedFileVariants;
          if (
            (upd.newVariants && JSON.stringify(m.uploadedFileVariants || []) !== JSON.stringify(upd.newVariants || []))
          ) {
            return {
              ...m,
              uploadedFileVariants: nextVariants,
            } as ChatMessage;
          }
        }
        return m;
      });

    let globalProfileText: string | undefined = undefined;
    try {
      markSendStage('send.globalProfile.start');
      const gp2 = await getGlobalProfileDB();
      globalProfileText = gp2?.text || undefined;
      markSendStage('send.globalProfile.done', {
        hasGlobalProfile: Boolean(globalProfileText),
      });
    } catch { }

    // Ensure Maestro avatar URIs are valid before sending
    let avatarOverlayFileUri: string | undefined = undefined;
    let avatarOverlayMimeType: string | undefined = undefined;
    try {
      markSendStage('send.avatar.ensure.start');
      const avatarResult = await ensureMaestroAvatarUris();
      if (avatarResult.rawUri) {
        maestroAvatarUriRef.current = avatarResult.rawUri;
        maestroAvatarMimeTypeRef.current = avatarResult.rawMimeType;
      }
      avatarOverlayFileUri = avatarResult.overlayUri || undefined;
      avatarOverlayMimeType = avatarResult.overlayMimeType || undefined;
      markSendStage('send.avatar.ensure.done', {
        hasAvatarOverlay: Boolean(avatarOverlayFileUri),
      });
    } catch (e) {
      console.warn('Failed to ensure Maestro avatar URIs:', e);
    }

    const derivedHistory = deriveBrowserTutorHistory(historySubsetForSendFinal, {
      maxMessages: computeMaxMessagesForArray(historySubsetForSendFinal.filter((m: ChatMessage) => m.role === 'user' || m.role === 'assistant')),
      maxMediaToKeep: MAX_MEDIA_TO_KEEP,
      contextSummary: resolveBookmarkContextSummary() || undefined,
      globalProfileText,
      avatarOverlayFileUri,
      avatarOverlayMimeType,
    });
    markSendStage('send.history.sanitize.start', {
      derivedHistoryCount: derivedHistory.length,
    });
    const sanitizedDerivedHistory = await sanitizeHistoryWithVerifiedUris(derivedHistory as any);
    markSendStage('send.history.sanitize.done', {
      sanitizedHistoryCount: sanitizedDerivedHistory.length,
    });

    // User image generation for AI Camera mode
    markSendStage('send.userImageGeneration.start', {
      shouldGenerateUserImage,
    });
    const userImageContext = await runUserImageGeneration({
      shouldGenerateUserImage,
      currentSettingsVal,
      messageType,
      userMessageText,
      userMessageId,
      userImageToProcessBase64,
      sanitizedDerivedHistory,
    });
    if (userImageContext.imageForGeminiContextFileUri) {
      imageForGeminiContextFileUri = userImageContext.imageForGeminiContextFileUri;
    }
    markSendStage('send.userImageGeneration.done', {
      filePartCount: imageForGeminiContextFileUri?.length || 0,
    });

    setSendPrep(null);

    return { geminiPromptText, systemInstructionForGemini, imageForGeminiContextFileUri, sanitizedDerivedHistory };
  };
}
