// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { ConversationDiagnostics } from './conversationContracts';
import type { AppSettings, RecordedUtterance } from '../../../core/types';
import type { UseTutorConversationConfig, UseTutorConversationReturn, MutableValue } from './conversationContracts';
import type { processMediaForUpload as processMedia } from '../../vision';

export interface UserMessagePorts extends Pick<UseTutorConversationConfig, 't' | 'addMessage' | 'captureSnapshot' | 'claimRecordedUtterance'> {
  diagnostics: Pick<ConversationDiagnostics, 'logSttFlow'>;
  attachedImageBase64: string | null;
  attachedImageMimeType: string | null;
  attachedFileName: string | null;
  recordedUtterancePendingRef: MutableValue<RecordedUtterance | null>;
  sendWithFileUploadInProgressRef: MutableValue<boolean>;
  setSendPrep(value: UseTutorConversationReturn['sendPrep']): void;
  processMediaForUpload: typeof processMedia;
  INLINE_CAP_AUDIO: number;
}

/** Capture and assemble a user message before request preparation. Captured
 * render values and mutable speech ownership are intentionally distinct ports. */
export function createUserMessageCoordinator(ports: UserMessagePorts) {
  const { logSttFlow } = ports.diagnostics;
  const { t, addMessage, captureSnapshot, claimRecordedUtterance, attachedImageBase64,
    attachedImageMimeType, attachedFileName, recordedUtterancePendingRef,
    sendWithFileUploadInProgressRef, setSendPrep, processMediaForUpload, INLINE_CAP_AUDIO } = ports;
  const createUserMessage = async (params: {
    text: string;
    passedImageBase64?: string;
    passedImageMimeType?: string;
    messageType: 'user' | 'conversational-reengagement' | 'image-reengagement';
    shouldGenerateUserImage: boolean;
    currentSettingsVal: AppSettings;
    triggeredByStt?: boolean;
  }) => {
    let userMessageId: string | null = null;
    let userMessageText = params.text;
    let recordedSpeechForMessage: RecordedUtterance | null = null;
    let userImageToProcessBase64: string | undefined = (typeof params.passedImageBase64 === 'string' && params.passedImageBase64)
      ? params.passedImageBase64
      : undefined;
    let userImageToProcessMimeType: string | undefined = (typeof params.passedImageMimeType === 'string' && params.passedImageMimeType)
      ? params.passedImageMimeType
      : undefined;
    let userImageToProcessStorageOptimizedBase64: string | undefined = undefined;
    let userImageToProcessStorageOptimizedMimeType: string | undefined = undefined;
    logSttFlow('send.createUserMessage.start', {
      messageType: params.messageType,
      textLength: params.text.length,
      triggeredByStt: params.triggeredByStt === true,
      hasPassedImage: Boolean(userImageToProcessBase64),
      sendWithSnapshotEnabled: params.currentSettingsVal.sendWithSnapshotEnabled,
      shouldGenerateUserImage: params.shouldGenerateUserImage,
    });

    if (params.messageType !== 'user') {
      logSttFlow('send.createUserMessage.skip.nonUser', {
        messageType: params.messageType,
      });
      return {
        userMessageId,
        userMessageText,
        recordedSpeechForMessage,
        userImageToProcessBase64,
        userImageToProcessMimeType,
        userImageToProcessStorageOptimizedBase64,
        userImageToProcessStorageOptimizedMimeType,
      };
    }

    for (let attempt = 0; attempt < 2; attempt++) {
      const claimed = typeof claimRecordedUtterance === 'function' ? claimRecordedUtterance() : null;
      if (claimed && typeof claimed.dataUrl === 'string' && claimed.dataUrl.length > 0) {
        recordedSpeechForMessage = claimed;
        recordedUtterancePendingRef.current = null;
        break;
      }
      if (recordedUtterancePendingRef.current && typeof recordedUtterancePendingRef.current.dataUrl === 'string' && recordedUtterancePendingRef.current.dataUrl.length > 0) {
        recordedSpeechForMessage = recordedUtterancePendingRef.current;
        recordedUtterancePendingRef.current = null;
        break;
      }
      if (attempt === 0) {
        await new Promise((resolve) => setTimeout(resolve, 60));
      }
    }
    if (recordedSpeechForMessage && recordedSpeechForMessage.dataUrl.length > INLINE_CAP_AUDIO) {
      recordedSpeechForMessage = null;
    }

    if (params.currentSettingsVal.sendWithSnapshotEnabled && !userImageToProcessBase64 && !params.shouldGenerateUserImage) {
      logSttFlow('send.createUserMessage.snapshot.start', {
        triggeredByStt: params.triggeredByStt === true,
      });
      const snapshotResult = await captureSnapshot({
        isForReengagement: false,
        requireReadyFrame: params.triggeredByStt === true,
      });
      logSttFlow('send.createUserMessage.snapshot.done', {
        triggeredByStt: params.triggeredByStt === true,
        capturedImage: Boolean(snapshotResult),
      });
      if (snapshotResult) {
        userImageToProcessBase64 = snapshotResult.base64;
        userImageToProcessMimeType = snapshotResult.mimeType;
        userImageToProcessStorageOptimizedBase64 = snapshotResult.storageOptimizedBase64;
        userImageToProcessStorageOptimizedMimeType = snapshotResult.storageOptimizedMimeType;
      }
    }

    if (!userImageToProcessStorageOptimizedBase64 && attachedImageBase64 && attachedImageMimeType) {
      try {
        if (!sendWithFileUploadInProgressRef.current) {
          sendWithFileUploadInProgressRef.current = true;
        }
        setSendPrep({ active: true, label: t('chat.sendPrep.optimizingImage') || 'Optimizing...' });
        const optimized = await processMediaForUpload(attachedImageBase64, attachedImageMimeType, {
          t,
          onProgress: (label, done, total, etaMs) => {
            setSendPrep({ active: true, label, done, total, etaMs });
          }
        });
        userImageToProcessStorageOptimizedBase64 = optimized.dataUrl;
        userImageToProcessStorageOptimizedMimeType = optimized.mimeType;
      } catch { }
    }

    userMessageId = addMessage({
      role: 'user',
      text: userMessageText,
      recordedUtterance: recordedSpeechForMessage || undefined,
      imageUrl: userImageToProcessBase64,
      imageMimeType: userImageToProcessMimeType,
      attachmentName: attachedFileName || undefined,
      storageOptimizedImageUrl: userImageToProcessStorageOptimizedBase64,
      storageOptimizedImageMimeType: userImageToProcessStorageOptimizedMimeType,
    });
    logSttFlow('send.createUserMessage.done', {
      userMessageId,
      hasRecordedAudio: Boolean(recordedSpeechForMessage),
      hasImage: Boolean(userImageToProcessBase64),
      hasStorageOptimizedImage: Boolean(userImageToProcessStorageOptimizedBase64),
    });

    return {
      userMessageId,
      userMessageText,
      recordedSpeechForMessage,
      userImageToProcessBase64,
      userImageToProcessMimeType,
      userImageToProcessStorageOptimizedBase64,
      userImageToProcessStorageOptimizedMimeType,
    };
  };
  return createUserMessage;
}
