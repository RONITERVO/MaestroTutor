// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { ChatMessage } from '../../../core/types';
import type { UseTutorConversationConfig, UseTutorConversationReturn, MutableValue } from './conversationContracts';
import type { processMediaForUpload as processMedia } from '../../vision';
import type { uploadMediaToFiles as uploadMedia } from '../../../api/gemini/files';
import { buildUploadedAttachmentState, PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID, inferUploadedAttachmentTargetsForMimeType } from '../../../core-sdk/chat/uploadedAttachmentVariants';
type MaestroToolKind = NonNullable<ChatMessage['maestroToolKind']>;
type SendPrep = UseTutorConversationReturn['sendPrep'];
export type OptimizedMedia = Awaited<ReturnType<typeof processMedia>>;
export type SetSendPrep = (value: SendPrep | ((previous: SendPrep) => SendPrep)) => void;
export interface MediaPersistencePorts extends Pick<UseTutorConversationConfig, 't' | 'updateMessage'> {
  sendWithFileUploadInProgressRef: MutableValue<boolean>;
  setSendPrep: SetSendPrep;
  processMediaForUpload: typeof processMedia;
  uploadMediaToFiles: typeof uploadMedia;
}
/** Local optimization and provider upload have different byte sources and failure
 * guarantees. Generated attachments remain visible if uploading fails. */
export function createMediaPersistence(ports: MediaPersistencePorts) {
  const { t, updateMessage, sendWithFileUploadInProgressRef, setSendPrep, processMediaForUpload, uploadMediaToFiles } = ports;
  const optimizeAndUploadMedia = async (params: {
    dataUrl: string;
    mimeType: string;
    displayName: string;
    onProgress?: (label: string, done?: number, total?: number, etaMs?: number) => void;
    onOptimized?: (media: OptimizedMedia) => void;
    setUploadPrepLabel?: boolean;
  }) => {
    // Create optimized version for local storage (reduces DB size)
    const optimized = await processMediaForUpload(params.dataUrl, params.mimeType, { t, onProgress: params.onProgress });
    params.onOptimized?.(optimized);
    sendWithFileUploadInProgressRef.current = true;
    if (params.setUploadPrepLabel !== false) {
      setSendPrep(prev => (prev && prev.active
        ? { ...prev, label: t('chat.sendPrep.uploadingMedia') || 'Uploading media...' }
        : { active: true, label: t('chat.sendPrep.uploadingMedia') || 'Uploading media...' }));
    }
    // CRITICAL: Upload the ORIGINAL full-resolution data to the LLM for best quality
    // The optimized version is only used for local storage to reduce DB backup/reload size
    const upload = await uploadMediaToFiles(params.dataUrl, params.mimeType, params.displayName);
    return { optimized, upload };
  };

  const attachGeneratedToolMedia = async (params: {
    messageId: string;
    toolKind: MaestroToolKind;
    dataUrl: string;
    mimeType: string;
    attachmentName: string;
  }) => {
    let optimizedMedia: OptimizedMedia | undefined;
    try {
      const { optimized, upload } = await optimizeAndUploadMedia({
        dataUrl: params.dataUrl,
        mimeType: params.mimeType,
        displayName: params.attachmentName,
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

      updateMessage(params.messageId, {
        imageUrl: params.dataUrl,
        imageMimeType: params.mimeType,
        attachmentName: params.attachmentName,
        storageOptimizedImageUrl: optimized.dataUrl,
        storageOptimizedImageMimeType: optimized.mimeType,
        ...uploadedAttachmentState,
        isGeneratingToolAttachment: false,
        toolAttachmentStartTime: undefined,
        toolAttachmentPhase: undefined,
        maestroToolKind: params.toolKind,
      });
    } catch (error) {
      console.warn(`[MaestroTool] Failed to upload ${params.toolKind} attachment.`, error);
      updateMessage(params.messageId, {
        imageUrl: params.dataUrl,
        imageMimeType: params.mimeType,
        attachmentName: params.attachmentName,
        ...(optimizedMedia ? { storageOptimizedImageUrl: optimizedMedia.dataUrl, storageOptimizedImageMimeType: optimizedMedia.mimeType } : {}),
        isGeneratingToolAttachment: false,
        toolAttachmentStartTime: undefined,
        toolAttachmentPhase: undefined,
        maestroToolKind: params.toolKind,
      });
    }
  };
  return { optimizeAndUploadMedia, attachGeneratedToolMedia };
}
