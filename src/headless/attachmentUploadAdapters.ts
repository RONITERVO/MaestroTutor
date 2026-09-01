// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import sharp from 'sharp';
import {
  buildAttachmentUploadPlans,
  type AttachmentUploadPlan,
  type AttachmentUploadSource,
} from '../core-sdk/chat/attachmentUploadPlans';
import { extractOfficeTextForUpload } from '../core-sdk/chat/officeTextExtraction';
import { createSyntheticVisualFrame } from './syntheticVisual';

const decodeDataUrl = (dataUrl: string): Buffer => {
  const commaIndex = dataUrl.indexOf(',');
  if (commaIndex < 0) throw new Error('Attachment media must be a data URL.');
  const header = dataUrl.slice(0, commaIndex);
  const payload = dataUrl.slice(commaIndex + 1);
  return /;base64(?:;|$)/i.test(header)
    ? Buffer.from(payload, 'base64')
    : Buffer.from(decodeURIComponent(payload), 'utf8');
};

const toDataUrl = (bytes: Uint8Array, mimeType: string): string => (
  `data:${mimeType};base64,${Buffer.from(bytes).toString('base64')}`
);

/**
 * Supplies only the runtime adapters that cannot be shared with the browser.
 * The Core planner still controls every variant, target, order and validation.
 */
export const buildHeadlessAttachmentUploadPlans = (
  source: AttachmentUploadSource,
): AttachmentUploadPlan[] => buildAttachmentUploadPlans(source, {
  async createVideoKeyframe() {
    // Deterministic frame injection is the headless counterpart of the browser's
    // HTMLVideoElement/canvas capture. The original video is uploaded separately.
    const frame = await createSyntheticVisualFrame('VIDEO KEYFRAME FIXTURE');
    return {
      dataUrl: `data:${frame.mimeType};base64,${frame.dataBase64}`,
      mimeType: frame.mimeType,
    };
  },
  async extractOfficeText(mediaSource) {
    return extractOfficeTextForUpload({
      dataUrl: mediaSource.dataUrl,
      mimeType: mediaSource.mimeType,
      fileName: mediaSource.attachmentName,
    });
  },
  async rasterizeSvg(mediaSource) {
    const jpeg = await sharp(decodeDataUrl(mediaSource.dataUrl))
      .flatten({ background: '#ffffff' })
      .jpeg({ quality: 82, chromaSubsampling: '4:4:4' })
      .toBuffer();
    return { dataUrl: toDataUrl(jpeg, 'image/jpeg'), mimeType: 'image/jpeg' };
  },
});
