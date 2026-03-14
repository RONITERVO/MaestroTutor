// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import type {
  ChatMessage,
  UploadedAttachmentTarget,
  UploadedAttachmentVariant,
} from '../../../core/types';

export const PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID = 'primary';
export const VIDEO_KEYFRAME_UPLOADED_ATTACHMENT_VARIANT_ID = 'video-keyframe';
export const OFFICE_TEXT_UPLOADED_ATTACHMENT_VARIANT_ID = 'office-text';
export const SVG_RASTER_UPLOADED_ATTACHMENT_VARIANT_ID = 'svg-rasterized';

const VALID_UPLOAD_TARGETS: UploadedAttachmentTarget[] = ['chat', 'image-generation'];

const DEFAULT_ORDER_BY_VARIANT_ID: Record<string, number> = {
  [VIDEO_KEYFRAME_UPLOADED_ATTACHMENT_VARIANT_ID]: 0,
  [OFFICE_TEXT_UPLOADED_ATTACHMENT_VARIANT_ID]: 0,
  [SVG_RASTER_UPLOADED_ATTACHMENT_VARIANT_ID]: 0,
  [PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID]: 10,
};

const normalizeMime = (value?: string | null): string => (value || '').trim().toLowerCase();

export const inferUploadedAttachmentTargetsForMimeType = (mimeType?: string | null): UploadedAttachmentTarget[] => {
  const mime = normalizeMime(mimeType);
  if (mime.startsWith('image/')) {
    return ['chat', 'image-generation'];
  }
  return ['chat'];
};

const normalizeTargets = (value: unknown, mimeType?: string): UploadedAttachmentTarget[] => {
  const rawTargets = Array.isArray(value)
    ? value.filter((target): target is UploadedAttachmentTarget => VALID_UPLOAD_TARGETS.includes(target as UploadedAttachmentTarget))
    : [];
  if (rawTargets.length > 0) {
    return Array.from(new Set(rawTargets));
  }

  return inferUploadedAttachmentTargetsForMimeType(mimeType);
};

const normalizeVariant = (variant: Partial<UploadedAttachmentVariant> | null | undefined): UploadedAttachmentVariant | null => {
  if (!variant) return null;
  const uri = (variant.uri || '').trim();
  const mimeType = normalizeMime(variant.mimeType);
  if (!uri || !mimeType) return null;

  const id = (variant.id || '').trim() || PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID;
  const order = Number.isFinite(variant.order)
    ? Number(variant.order)
    : (DEFAULT_ORDER_BY_VARIANT_ID[id] ?? 10);

  return {
    id,
    uri,
    mimeType,
    targets: normalizeTargets(variant.targets, mimeType),
    source: variant.source || 'derived',
    order,
  };
};

const dedupeVariants = (variants: UploadedAttachmentVariant[]): UploadedAttachmentVariant[] => {
  const deduped = new Map<string, UploadedAttachmentVariant>();
  variants.forEach((variant) => {
    const key = `${variant.id}::${variant.uri}::${variant.mimeType}`;
    if (!deduped.has(key)) {
      deduped.set(key, variant);
    }
  });

  return Array.from(deduped.values()).sort((left, right) => {
    const leftOrder = Number.isFinite(left.order) ? Number(left.order) : 10;
    const rightOrder = Number.isFinite(right.order) ? Number(right.order) : 10;
    if (leftOrder !== rightOrder) return leftOrder - rightOrder;
    if (left.id !== right.id) return left.id.localeCompare(right.id);
    return left.uri.localeCompare(right.uri);
  });
};

export const normalizeUploadedAttachmentVariants = (
  inputVariants: UploadedAttachmentVariant[] | null | undefined
): UploadedAttachmentVariant[] => {
  const normalizedVariants: UploadedAttachmentVariant[] = [];

  if (Array.isArray(inputVariants)) {
    inputVariants.forEach((variant) => {
      const normalized = normalizeVariant(variant);
      if (normalized) normalizedVariants.push(normalized);
    });
  }
  return dedupeVariants(normalizedVariants);
};

export const upsertUploadedAttachmentVariant = (
  variants: UploadedAttachmentVariant[] | undefined,
  nextVariant: UploadedAttachmentVariant
): UploadedAttachmentVariant[] => {
  const normalizedNext = normalizeVariant(nextVariant);
  const base = Array.isArray(variants)
    ? variants
        .map(variant => normalizeVariant(variant))
        .filter((variant): variant is UploadedAttachmentVariant => Boolean(variant))
        .filter(variant => variant.id !== nextVariant.id)
    : [];
  return dedupeVariants([...(base || []), ...(normalizedNext ? [normalizedNext] : [])]);
};

export const selectPrimaryUploadedAttachmentVariant = (
  message: Pick<ChatMessage, 'uploadedFileVariants'> | UploadedAttachmentVariant[] | undefined
): UploadedAttachmentVariant | undefined => {
  const variants = Array.isArray(message)
    ? dedupeVariants(message.map(variant => normalizeVariant(variant)).filter((variant): variant is UploadedAttachmentVariant => Boolean(variant)))
    : normalizeUploadedAttachmentVariants(message?.uploadedFileVariants);

  return (
    variants.find(variant => variant.id === PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID) ||
    variants.find(variant => variant.source === 'original') ||
    variants[0]
  );
};

export const selectUploadedAttachmentParts = (
  message: Pick<ChatMessage, 'uploadedFileVariants'>,
  target: UploadedAttachmentTarget
): Array<{ fileUri: string; mimeType: string }> => {
  const variants = normalizeUploadedAttachmentVariants(message.uploadedFileVariants);
  const parts = variants
    .filter(variant => variant.targets.includes(target))
    .map(variant => ({ fileUri: variant.uri, mimeType: variant.mimeType }));

  const deduped = new Map<string, { fileUri: string; mimeType: string }>();
  parts.forEach((part) => {
    const key = `${part.fileUri}::${part.mimeType}`;
    if (!deduped.has(key)) {
      deduped.set(key, part);
    }
  });

  return Array.from(deduped.values());
};

export const buildUploadedAttachmentState = (variants: UploadedAttachmentVariant[] | undefined): Pick<ChatMessage, 'uploadedFileVariants'> => {
  const normalized = Array.isArray(variants)
    ? dedupeVariants(variants.map(variant => normalizeVariant(variant)).filter((variant): variant is UploadedAttachmentVariant => Boolean(variant)))
    : [];
  return {
    uploadedFileVariants: normalized.length ? normalized : undefined,
  };
};
