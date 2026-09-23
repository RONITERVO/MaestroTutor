// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { ChatMessage, UploadedAttachmentVariant } from '../../../core/types';
import type { TranslationFunction } from '../../../app/hooks/useTranslations';
import { MAX_MEDIA_TO_KEEP } from '../../../core/config/app';
import { normalizeUploadedAttachmentVariants, upsertUploadedAttachmentVariant, buildUploadedAttachmentState, selectUploadedAttachmentParts } from '../../../core-sdk/chat/uploadedAttachmentVariants';
import type { checkFileStatuses as checkStatuses, uploadMediaToFiles as uploadMedia } from '../../../api/gemini/files';
import type { buildAttachmentUploadPlans as buildPlans } from '../../../core-sdk/chat/attachmentUploadPlans';

export type HistoryMediaOverride = { newVariants?: UploadedAttachmentVariant[]; transient?: boolean; omitFromHistory?: boolean };
export type AttachmentSource = { dataUrl: string; mimeType: string; attachmentName?: string };
export interface AttachmentUploadPorts {
  t: TranslationFunction;
  updateMessage(id: string, patch: Partial<ChatMessage>): void;
  computeHistorySubsetForMedia(history: ChatMessage[]): ChatMessage[];
  checkFileStatuses: typeof checkStatuses;
  uploadMediaToFiles: typeof uploadMedia;
  buildAttachmentUploadPlans(source: AttachmentSource, t: TranslationFunction): ReturnType<typeof buildPlans>;
}

/** Upload/reuse/expiry policy for current and historical attachments. The original
 * bytes feed provider files; storage optimization remains a separate operation. */
export function createAttachmentUploads(ports: AttachmentUploadPorts) {
  const { t, updateMessage, computeHistorySubsetForMedia, checkFileStatuses, uploadMediaToFiles, buildAttachmentUploadPlans } = ports;
  const ensureUploadedAttachmentVariantsForMessage = async (
    message: ChatMessage,
    knownStatuses?: Record<string, { deleted: boolean; active: boolean }>
  ): Promise<{ variants: UploadedAttachmentVariant[]; chatFileParts: Array<{ fileUri: string; mimeType: string }> }> => {
    let nextVariants = normalizeUploadedAttachmentVariants(message.uploadedFileVariants);
    if (knownStatuses) {
      nextVariants = nextVariants.filter(variant => !knownStatuses[variant.uri]?.deleted);
    }
    const source = getMessageAttachmentSource(message);
    const plans = source ? buildAttachmentUploadPlans(source, t) : [];

    if (plans.length > 0) {
      let cachedStatuses: Record<string, { deleted: boolean; active: boolean }> = knownStatuses || {};
      const plannedVariantIds = new Set(plans.map(plan => plan.id));
      const existingUris = nextVariants
        .filter(variant => plannedVariantIds.has(variant.id))
        .map(variant => variant.uri);

      const urisNeedingStatusCheck = !knownStatuses
        ? Array.from(new Set(existingUris))
        : Array.from(new Set(existingUris.filter(uri => !cachedStatuses[uri])));

      if (urisNeedingStatusCheck.length > 0) {
        try {
          const refreshedStatuses = await checkFileStatuses(urisNeedingStatusCheck);
          cachedStatuses = {
            ...cachedStatuses,
            ...refreshedStatuses,
          };
        } catch {
          if (!knownStatuses) {
            cachedStatuses = {};
          }
        }
      }

      for (const plan of plans) {
        const existingVariant = nextVariants.find(variant => variant.id === plan.id);
        const existingDeleted = !!(existingVariant && cachedStatuses[existingVariant.uri]?.deleted);
        if (existingVariant && !existingDeleted) {
          nextVariants = upsertUploadedAttachmentVariant(nextVariants, {
            ...existingVariant,
            id: plan.id,
            source: plan.source,
            targets: plan.targets,
            order: plan.order,
          });
          continue;
        }

        if (!source) continue;

        try {
          const uploadSource = await plan.build();
          const upload = await uploadMediaToFiles(
            uploadSource.dataUrl,
            uploadSource.mimeType,
            uploadSource.displayName || message.attachmentName || 'send-history'
          );
          nextVariants = upsertUploadedAttachmentVariant(nextVariants, {
            id: plan.id,
            uri: upload.uri,
            mimeType: upload.mimeType,
            targets: plan.targets,
            source: plan.source,
            order: plan.order,
          });
        } catch (error) {
          console.warn(`[ensureUploads] Failed to upload ${plan.id} variant for message ${message.id}.`, error);
          nextVariants = nextVariants.filter(variant => variant.id !== plan.id);
        }
      }
    }

    const normalizedState = buildUploadedAttachmentState(nextVariants);
    const normalizedCurrentVariants = normalizeUploadedAttachmentVariants(message.uploadedFileVariants);
    const variantsChanged = JSON.stringify(normalizedCurrentVariants) !== JSON.stringify(normalizedState.uploadedFileVariants || []);

    if (variantsChanged) {
      updateMessage(message.id, normalizedState);
      message.uploadedFileVariants = normalizedState.uploadedFileVariants;
    }

    return {
      variants: normalizedState.uploadedFileVariants || [],
      chatFileParts: selectUploadedAttachmentParts(normalizedState, 'chat'),
    };
  };

  const uploadAttachmentVariantsForSource = async (
    source: { dataUrl: string; mimeType: string; attachmentName?: string },
    fallbackDisplayName: string
  ): Promise<Array<{ fileUri: string; mimeType: string }>> => {
    const plans = buildAttachmentUploadPlans(source, t);
    const uploadedVariants: UploadedAttachmentVariant[] = [];

    for (const plan of plans) {
      try {
        const uploadSource = await plan.build();
        const upload = await uploadMediaToFiles(
          uploadSource.dataUrl,
          uploadSource.mimeType,
          uploadSource.displayName || source.attachmentName || fallbackDisplayName
        );
        uploadedVariants.push({
          id: plan.id,
          uri: upload.uri,
          mimeType: upload.mimeType,
          targets: plan.targets,
          source: plan.source,
          order: plan.order,
        });
      } catch (error) {
        console.warn(`[uploadAttachmentVariantsForSource] Failed to upload ${plan.id} for ${fallbackDisplayName}.`, error);
      }
    }

    return selectUploadedAttachmentParts(buildUploadedAttachmentState(uploadedVariants), 'chat');
  };

  const ensureUrisForHistoryForSend = async (
    arr: ChatMessage[], 
    onProgress?: (done: number, total: number, etaMs?: number) => void
  ): Promise<Record<string, HistoryMediaOverride>> => {
    const candidates = computeHistorySubsetForMedia(arr);

    const mediaIndices: number[] = [];
    for (let i = 0; i < candidates.length; i++) {
      const m = candidates[i];
      const hasMedia = !!getMessageAttachmentSource(m) || normalizeUploadedAttachmentVariants(m.uploadedFileVariants).length > 0;
      if (hasMedia) mediaIndices.push(i);
    }
    const maxMedia = MAX_MEDIA_TO_KEEP;
    const keepMediaIdx = new Set<number>(mediaIndices.slice(-maxMedia));

    const cachedUrisToCheck: string[] = [];
    for (let i = 0; i < candidates.length; i++) {
      if (!keepMediaIdx.has(i)) continue;
      const m0 = candidates[i];
      normalizeUploadedAttachmentVariants(m0.uploadedFileVariants).forEach((variant) => {
        if (variant.uri) cachedUrisToCheck.push(variant.uri);
      });
    }
    let cachedStatuses: Record<string, { deleted: boolean; active: boolean }> = {};
    try {
      const uniqUris = Array.from(new Set(cachedUrisToCheck));
      if (uniqUris.length) cachedStatuses = await checkFileStatuses(uniqUris);
    } catch { cachedStatuses = {}; }

    let totalToEnsure = 0;
    const indicesNeedingUpload = new Set<number>();
    for (let i = 0; i < candidates.length; i++) {
      if (!keepMediaIdx.has(i)) continue;
      const message = candidates[i];
      const source = getMessageAttachmentSource(message);
      if (!source) continue;

      const plans = buildAttachmentUploadPlans(source, t);
      const existingVariants = normalizeUploadedAttachmentVariants(message.uploadedFileVariants);
      const needsUpload = plans.some((plan) => {
        const existingVariant = existingVariants.find(variant => variant.id === plan.id);
        if (!existingVariant) return true;
        return !!cachedStatuses[existingVariant.uri]?.deleted;
      });

      if (needsUpload) {
        totalToEnsure++;
        indicesNeedingUpload.add(i);
      }
    }

    let doneCount = 0;
    const startTs = Date.now();
    const tick = () => {
      if (!onProgress) return;
      const elapsed = Date.now() - startTs;
      const avg = doneCount > 0 ? elapsed / doneCount : undefined;
      const remaining = Math.max(0, totalToEnsure - doneCount);
      const eta = avg !== undefined ? Math.round(avg * remaining) : undefined;
      onProgress(doneCount, totalToEnsure, eta);
    };
    if (totalToEnsure > 0) tick();

    const updatedUriMap: Record<string, HistoryMediaOverride> = {};
    for (let idx = 0; idx < candidates.length; idx++) {
      if (!keepMediaIdx.has(idx)) continue;
      const m = candidates[idx];
      const previousVariants = normalizeUploadedAttachmentVariants(m.uploadedFileVariants);
      const localSource = getMessageAttachmentSource(m);
      const ensured = await ensureUploadedAttachmentVariantsForMessage(m, cachedStatuses);
      const nextState = buildUploadedAttachmentState(ensured.variants);
      const chatFileParts = ensured.chatFileParts;

      if (chatFileParts.length === 0 && localSource) {
        throw new Error(t('streaming.failedToPrepareAttachment', { name: m.attachmentName || 'attachment' }) || `Failed to prepare recent attachment "${m.attachmentName || 'attachment'}" for send. Try again or reattach the file.`);
      }

      if (chatFileParts.length === 0 && previousVariants.length > 0) {
        console.warn(`[ensureUris] Message ${m.id} has no valid uploaded file variants and will be omitted from request history.`);
        updatedUriMap[m.id] = {
          transient: true,
          omitFromHistory: true,
        };
      } else if (JSON.stringify(previousVariants) !== JSON.stringify(nextState.uploadedFileVariants || [])) {
        updatedUriMap[m.id] = {
          newVariants: nextState.uploadedFileVariants,
        };
      }
      if (indicesNeedingUpload.has(idx)) {
        try { doneCount++; tick(); } catch {}
      }
      await new Promise(r => setTimeout(r, 0));
    }
    return updatedUriMap;
  };
  return { ensureUploadedAttachmentVariantsForMessage, uploadAttachmentVariantsForSource, ensureUrisForHistoryForSend };
}

export const getMessageAttachmentSource = (
  message: Pick<ChatMessage, 'imageUrl' | 'imageMimeType' | 'storageOptimizedImageUrl' | 'storageOptimizedImageMimeType' | 'attachmentName'>
): { dataUrl: string; mimeType: string; attachmentName?: string } | null => {
  if (typeof message.imageUrl === 'string' && message.imageUrl && typeof message.imageMimeType === 'string' && message.imageMimeType) {
    return {
      dataUrl: message.imageUrl,
      mimeType: message.imageMimeType,
      attachmentName: message.attachmentName,
    };
  }

  if (
    typeof message.storageOptimizedImageUrl === 'string' &&
    message.storageOptimizedImageUrl &&
    typeof message.storageOptimizedImageMimeType === 'string' &&
    message.storageOptimizedImageMimeType
  ) {
    return {
      dataUrl: message.storageOptimizedImageUrl,
      mimeType: message.storageOptimizedImageMimeType,
      attachmentName: message.attachmentName,
    };
  }

  return null;
};
