// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { UploadedAttachmentVariant } from '../../core/types';
import { decodeTextFromDataUrl } from './fileAttachments';
import { resolveAttachmentStrategy } from './attachmentStrategy';

export interface AttachmentUploadSource {
  dataUrl: string;
  mimeType: string;
  attachmentName?: string;
}

export interface AttachmentUploadPayload {
  dataUrl: string;
  mimeType: string;
  displayName?: string;
}

export interface AttachmentUploadAdapters {
  createVideoKeyframe(source: AttachmentUploadSource): Promise<{ dataUrl: string; mimeType: string }>;
  extractOfficeText(source: AttachmentUploadSource): Promise<string>;
  rasterizeSvg(source: AttachmentUploadSource): Promise<{ dataUrl: string; mimeType: string }>;
}

export interface AttachmentUploadPlan {
  id: string;
  source: UploadedAttachmentVariant['source'];
  targets: UploadedAttachmentVariant['targets'];
  order: number;
  build(): Promise<AttachmentUploadPayload>;
}

const toUtf8Base64DataUrl = (mimeType: string, text: string): string => {
  const bytes = new TextEncoder().encode(text);
  let binary = '';
  const chunkSize = 0x8000;
  for (let offset = 0; offset < bytes.length; offset += chunkSize) {
    binary += String.fromCharCode(...bytes.subarray(offset, offset + chunkSize));
  }
  return `data:${mimeType};charset=utf-8;base64,${globalThis.btoa(binary)}`;
};

/**
 * Creates the upload plan used by both the browser UI and headless client.
 * Runtime-specific media decoding remains behind injected adapters, while
 * ordering, surrogate selection, MIME validation and upload metadata stay in
 * the shared Core path.
 */
export const buildAttachmentUploadPlans = (
  source: AttachmentUploadSource,
  adapters: AttachmentUploadAdapters,
): AttachmentUploadPlan[] => {
  const displayName = source.attachmentName || 'attachment';
  return resolveAttachmentStrategy(source.mimeType).map(step => ({
    ...step,
    build: async (): Promise<AttachmentUploadPayload> => {
      switch (step.source) {
        case 'original':
          return {
            dataUrl: source.dataUrl,
            mimeType: source.mimeType,
            displayName,
          };
        case 'video-keyframe': {
          const keyframe = await adapters.createVideoKeyframe(source);
          if (!keyframe.dataUrl || !keyframe.mimeType.startsWith('image/')) {
            throw new Error(`Video keyframe did not produce a supported image MIME: ${keyframe.mimeType}`);
          }
          return {
            ...keyframe,
            displayName: `${displayName}-keyframe.jpg`,
          };
        }
        case 'office-text': {
          const extracted = (await adapters.extractOfficeText(source)).trim();
          if (!extracted) throw new Error('No extracted Office text is available for Gemini upload conversion.');
          return {
            dataUrl: toUtf8Base64DataUrl('text/plain', extracted),
            mimeType: 'text/plain',
            displayName: `${displayName}.txt`,
          };
        }
        case 'svg-source': {
          const extracted = decodeTextFromDataUrl(source.dataUrl)?.trim();
          if (!extracted) throw new Error('SVG source could not be decoded for Gemini upload conversion.');
          return {
            dataUrl: toUtf8Base64DataUrl('text/plain', extracted),
            mimeType: 'text/plain',
            displayName: `${displayName}.txt`,
          };
        }
        case 'svg-rasterized': {
          const rasterized = await adapters.rasterizeSvg(source);
          const normalizedMime = (rasterized.mimeType || '').trim().toLowerCase();
          if (!rasterized.dataUrl || !normalizedMime.startsWith('image/') || normalizedMime === 'image/svg+xml') {
            throw new Error(`SVG rasterization did not produce a supported image MIME: ${rasterized.mimeType}`);
          }
          return {
            ...rasterized,
            displayName: `${displayName}.jpg`,
          };
        }
        default: {
          const exhaustive: never = step.source;
          throw new Error(`Unsupported attachment upload source: ${String(exhaustive)}`);
        }
      }
    },
  }));
};
