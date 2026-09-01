// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import {
  isGoogleWorkspaceShortcutMimeType,
  isMicrosoftOfficeMimeType,
} from './fileAttachments';
import {
  inferUploadedAttachmentTargetsForMimeType,
  OFFICE_TEXT_UPLOADED_ATTACHMENT_VARIANT_ID,
  PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID,
  SVG_RASTER_UPLOADED_ATTACHMENT_VARIANT_ID,
  SVG_SOURCE_UPLOADED_ATTACHMENT_VARIANT_ID,
  VIDEO_KEYFRAME_UPLOADED_ATTACHMENT_VARIANT_ID,
} from './uploadedAttachmentVariants';

export interface AttachmentStrategyStep {
  id: string;
  source: 'original' | 'video-keyframe' | 'office-text' | 'svg-source' | 'svg-rasterized';
  targets: Array<'chat' | 'image-generation'>;
  order: number;
}

export const resolveAttachmentStrategy = (mimeType?: string | null): AttachmentStrategyStep[] => {
  const mime = (mimeType || '').trim().toLowerCase().split(';', 1)[0];
  if (mime.startsWith('video/')) {
    return [
      { id: VIDEO_KEYFRAME_UPLOADED_ATTACHMENT_VARIANT_ID, source: 'video-keyframe', targets: ['chat', 'image-generation'], order: 0 },
      { id: PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID, source: 'original', targets: ['chat'], order: 10 },
    ];
  }
  if (isMicrosoftOfficeMimeType(mime) || isGoogleWorkspaceShortcutMimeType(mime)) {
    return [{ id: OFFICE_TEXT_UPLOADED_ATTACHMENT_VARIANT_ID, source: 'office-text', targets: ['chat'], order: 0 }];
  }
  if (mime === 'image/svg+xml') {
    return [
      { id: SVG_SOURCE_UPLOADED_ATTACHMENT_VARIANT_ID, source: 'svg-source', targets: ['chat'], order: 0 },
      { id: SVG_RASTER_UPLOADED_ATTACHMENT_VARIANT_ID, source: 'svg-rasterized', targets: ['chat', 'image-generation'], order: 5 },
    ];
  }
  return [{
    id: PRIMARY_UPLOADED_ATTACHMENT_VARIANT_ID,
    source: 'original',
    targets: inferUploadedAttachmentTargetsForMimeType(mime),
    order: 10,
  }];
};
