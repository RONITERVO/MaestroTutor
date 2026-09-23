// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { AppSettings, ChatMessage } from '../../../core/types';
import type { UseTutorConversationConfig, UseTutorConversationReturn, MutableValue } from './conversationContracts';
import type { createMediaPersistence, OptimizedMedia, SetSendPrep } from './mediaPersistence';
import type { runMaestroImageGeneration as generateImage } from '../../../api/gemini/journeys';
import type { sanitizeHistoryWithVerifiedUris as sanitizeHistory } from '../../../api/gemini/files';
import type { deriveHistoryForApi as deriveHistory } from '..';
import type { getGlobalProfileDB as getProfile } from '../../session';
import { buildUploadedAttachmentState, PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID, inferUploadedAttachmentTargetsForMimeType } from '../../../core-sdk/chat/uploadedAttachmentVariants';
import { MAX_MEDIA_TO_KEEP } from '../../../core/config/app';
import { DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE } from '../../../core/config/prompts';
export interface GeneratedImagesPorts extends Pick<UseTutorConversationConfig, 't' | 'updateMessage' | 'addMessage' | 'getHistoryRespectingBookmark' | 'computeMaxMessagesForArray' | 'maestroAvatarUriRef' | 'maestroAvatarMimeTypeRef'> {
  messagesRef: MutableValue<ChatMessage[]>;
  sendWithFileUploadInProgressRef: MutableValue<boolean>;
  setSendPrep: SetSendPrep;
  optimizeAndUploadMedia: ReturnType<typeof createMediaPersistence>['optimizeAndUploadMedia'];
  ensureUrisForHistoryForSend: UseTutorConversationReturn['ensureUrisForHistoryForSend'];
  resolveBookmarkContextSummary: UseTutorConversationReturn['resolveBookmarkContextSummary'];
  addImageLoadDuration(duration: number): void;
  runMaestroImageGeneration: typeof generateImage;
  sanitizeHistoryWithVerifiedUris: typeof sanitizeHistory;
  deriveHistoryForApi: typeof deriveHistory;
  getGlobalProfileDB: typeof getProfile;
  hasShownCostWarning(): boolean;
  setCostWarningShown(): void;
}
/** AI-camera and assistant image workflows retain their distinct history,
 * placeholders and upload failure behavior. Provider prompting stays in Core. */
export function createGeneratedImages(ports: GeneratedImagesPorts) {
  const { t, updateMessage, addMessage, getHistoryRespectingBookmark, computeMaxMessagesForArray,
    maestroAvatarUriRef, maestroAvatarMimeTypeRef, messagesRef, sendWithFileUploadInProgressRef,
    setSendPrep, optimizeAndUploadMedia, ensureUrisForHistoryForSend, resolveBookmarkContextSummary,
    addImageLoadDuration, runMaestroImageGeneration, sanitizeHistoryWithVerifiedUris,
    deriveHistoryForApi, getGlobalProfileDB, hasShownCostWarning, setCostWarningShown } = ports;
  const runUserImageGeneration = async (params: {
    shouldGenerateUserImage: boolean;
    currentSettingsVal: AppSettings;
    messageType: 'user' | 'conversational-reengagement' | 'image-reengagement';
    userMessageText: string;
    userMessageId: string | null;
    userImageToProcessBase64?: string;
    sanitizedDerivedHistory: any[];
  }) => {
    if (!params.shouldGenerateUserImage || !params.currentSettingsVal.sendWithSnapshotEnabled || params.messageType !== 'user' ||
      !params.userMessageText.trim() || !params.userMessageId || params.userImageToProcessBase64) {
      return {};
    }

    const userImageGenStartTime = Date.now();
    updateMessage(params.userMessageId, {
      isGeneratingImage: true,
      imageGenerationStartTime: userImageGenStartTime,
      imageUrl: undefined,
      imageMimeType: undefined,
    });

    const sanitizedUserHistoryForImage = params.sanitizedDerivedHistory as any;
    let finalResult: Awaited<ReturnType<typeof runMaestroImageGeneration>>;
    try {
      finalResult = await runMaestroImageGeneration({
        contextText: params.userMessageText,
        history: sanitizedUserHistoryForImage,
        maestroAvatarUri: maestroAvatarUriRef.current || undefined,
        maestroAvatarMimeType: maestroAvatarMimeTypeRef.current || undefined,
      });
    } catch (error) {
      console.warn('Optional user image generation failed.', error);
      updateMessage(params.userMessageId, { isGeneratingImage: false, imageGenerationStartTime: undefined });
      return {};
    }

    if (finalResult && 'base64Image' in finalResult) {
      const duration = Date.now() - userImageGenStartTime;
      addImageLoadDuration(duration);
      if (!hasShownCostWarning()) {
        setCostWarningShown();
        addMessage({ role: 'error', text: t('error.imageGenCostWarning'), errorAction: 'imageGenCost' });
      }
      let optimizedMedia: OptimizedMedia | undefined;
      try {
        const { optimized, upload } = await optimizeAndUploadMedia({
          dataUrl: finalResult.base64Image as string,
          mimeType: finalResult.mimeType as string,
          displayName: 'user-generated',
          onOptimized: media => { optimizedMedia = media; },
        });
        const uploadedAttachmentState = buildUploadedAttachmentState([
          {
            id: PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID,
            uri: upload.uri,
            mimeType: upload.mimeType,
            targets: inferUploadedAttachmentTargetsForMimeType(upload.mimeType),
            source: 'original',
            order: 10,
          },
        ]);

        updateMessage(params.userMessageId, {
          imageUrl: finalResult.base64Image,
          imageMimeType: finalResult.mimeType,
          storageOptimizedImageUrl: optimized.dataUrl,
          storageOptimizedImageMimeType: optimized.mimeType,
          ...uploadedAttachmentState,
          isGeneratingImage: false,
          imageGenError: null,
          imageGenerationStartTime: undefined
        });
        return {
          imageForGeminiContextFileUri: [{ fileUri: upload.uri, mimeType: upload.mimeType }],
        };
      } catch (e) {
        updateMessage(params.userMessageId, {
          imageUrl: finalResult.base64Image,
          imageMimeType: finalResult.mimeType,
          ...(optimizedMedia ? { storageOptimizedImageUrl: optimizedMedia.dataUrl, storageOptimizedImageMimeType: optimizedMedia.mimeType } : {}),
          isGeneratingImage: false,
          imageGenError: null,
          imageGenerationStartTime: undefined
        });
      } finally {
        setSendPrep(prev => (prev && prev.active ? { ...prev, label: t('chat.sendPrep.preparingMedia') || 'Preparing media...' } : prev));
      }
    } else {
      updateMessage(params.userMessageId, {
        isGeneratingImage: false,
        imageGenerationStartTime: undefined
      });
    }

    return {};
  };

  const runAssistantImageGeneration = async (params: {
    thinkingMessageId: string;
    accumulatedFullText: string;
  }) => {
    if (!params.accumulatedFullText.trim()) return;
    const existing = messagesRef.current.find((m) => m.id === params.thinkingMessageId);
    if (existing && ((existing.imageUrl && existing.imageMimeType) || (existing.uploadedFileVariants && existing.uploadedFileVariants.length > 0))) {
      return;
    }

    const assistantStartTime = Date.now();
    updateMessage(params.thinkingMessageId, {
      isGeneratingImage: true,
      imageGenerationStartTime: assistantStartTime
    });

    // Exclude the latest assistant message since it's injected into the image-gen prompt as a user turn.
    const baseForEnsure: ChatMessage[] = getHistoryRespectingBookmark(messagesRef.current)
      .filter(m => m.id !== params.thinkingMessageId);

    let historyForAssistantImageGen: ChatMessage[] | undefined = undefined;
    try {
      sendWithFileUploadInProgressRef.current = true;
      const ensuredUpdates = await ensureUrisForHistoryForSend(baseForEnsure);
      historyForAssistantImageGen = baseForEnsure.map(m => {
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
      await new Promise(r => setTimeout(r, 0));
    } catch { /* ignore */ }

    const histForAssistantImgBase = historyForAssistantImageGen || baseForEnsure;
    let gpTextForAssistant: string | undefined;
    try {
      gpTextForAssistant = (await getGlobalProfileDB())?.text || undefined;
    } catch {
      gpTextForAssistant = undefined;
    }
    const assistantHistory = deriveHistoryForApi(histForAssistantImgBase, {
      maxMessages: computeMaxMessagesForArray(baseForEnsure.filter((message: ChatMessage) => message.role === 'user' || message.role === 'assistant')),
      maxMediaToKeep: MAX_MEDIA_TO_KEEP,
      contextSummary: resolveBookmarkContextSummary() || undefined,
      globalProfileText: gpTextForAssistant,
      placeholderLatestUserMessage: DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE,
    });
    let assistantImgGenResult: Awaited<ReturnType<typeof runMaestroImageGeneration>>;
    try {
      const sanitizedAssistantHistoryForImage = await sanitizeHistoryWithVerifiedUris(assistantHistory as any);
      assistantImgGenResult = await runMaestroImageGeneration({
        contextText: params.accumulatedFullText,
        history: sanitizedAssistantHistoryForImage,
        maestroAvatarUri: maestroAvatarUriRef.current || undefined,
        maestroAvatarMimeType: maestroAvatarMimeTypeRef.current || undefined,
      });
    } catch (error) {
      updateMessage(params.thinkingMessageId, { isGeneratingImage: false, imageGenerationStartTime: undefined });
      throw error;
    }

    if ('base64Image' in assistantImgGenResult) {
      const duration = Date.now() - assistantStartTime;
      addImageLoadDuration(duration);
      if (!hasShownCostWarning()) {
        setCostWarningShown();
        addMessage({ role: 'error', text: t('error.imageGenCostWarning'), errorAction: 'imageGenCost' });
      }
      let optimizedMedia: OptimizedMedia | undefined;
      try {
        const { optimized, upload } = await optimizeAndUploadMedia({
          dataUrl: assistantImgGenResult.base64Image,
          mimeType: assistantImgGenResult.mimeType,
          displayName: 'assistant-generated',
          setUploadPrepLabel: false,
          onOptimized: media => { optimizedMedia = media; },
        });
        const uploadedAttachmentState = buildUploadedAttachmentState([
          {
            id: PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID,
            uri: upload.uri,
            mimeType: upload.mimeType,
            targets: inferUploadedAttachmentTargetsForMimeType(upload.mimeType),
            source: 'original',
            order: 10,
          },
        ]);
        updateMessage(params.thinkingMessageId, {
          imageUrl: assistantImgGenResult.base64Image,
          imageMimeType: assistantImgGenResult.mimeType,
          storageOptimizedImageUrl: optimized.dataUrl,
          storageOptimizedImageMimeType: optimized.mimeType,
          ...uploadedAttachmentState,
          attachmentName: 'assistant-generated.jpg',
          isGeneratingImage: false,
          imageGenError: null,
          imageGenerationStartTime: undefined,
          maestroToolKind: 'image',
        });
      } catch {
        updateMessage(params.thinkingMessageId, {
          imageUrl: assistantImgGenResult.base64Image,
          imageMimeType: assistantImgGenResult.mimeType,
          attachmentName: 'assistant-generated.jpg',
          ...(optimizedMedia ? { storageOptimizedImageUrl: optimizedMedia.dataUrl, storageOptimizedImageMimeType: optimizedMedia.mimeType } : {}),
          isGeneratingImage: false,
          imageGenError: null,
          imageGenerationStartTime: undefined,
          maestroToolKind: 'image',
        });
      }
    } else {
      updateMessage(params.thinkingMessageId, {
        isGeneratingImage: false,
        imageGenerationStartTime: undefined,
      });
    }
  };
  return { runUserImageGeneration, runAssistantImageGeneration };
}
