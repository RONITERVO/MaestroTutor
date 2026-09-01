// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it, vi } from 'vitest';
import { buildAttachmentUploadPlans, type AttachmentUploadSource } from './attachmentUploadPlans';

const source = (mimeType: string, dataUrl = 'data:text/plain;base64,SGVsbG8='): AttachmentUploadSource => ({
  dataUrl,
  mimeType,
  attachmentName: 'lesson-file',
});

const adapters = () => ({
  createVideoKeyframe: vi.fn(async () => ({ dataUrl: 'data:image/jpeg;base64,/9j/', mimeType: 'image/jpeg' })),
  extractOfficeText: vi.fn(async () => 'Extracted lesson text'),
  rasterizeSvg: vi.fn(async () => ({ dataUrl: 'data:image/jpeg;base64,/9j/', mimeType: 'image/jpeg' })),
});

describe('shared attachment upload plans', () => {
  it('builds ordered video surrogate and original payloads', async () => {
    const mediaAdapters = adapters();
    const plans = buildAttachmentUploadPlans(source('video/mp4'), mediaAdapters);
    expect(plans.map(plan => plan.source)).toEqual(['video-keyframe', 'original']);
    expect((await plans[0].build()).mimeType).toBe('image/jpeg');
    expect((await plans[1].build()).mimeType).toBe('video/mp4');
    expect(mediaAdapters.createVideoKeyframe).toHaveBeenCalledOnce();
  });

  it('converts Office and SVG source content to UTF-8 text', async () => {
    const officePlan = buildAttachmentUploadPlans(source(
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    ), adapters())[0];
    expect(globalThis.atob((await officePlan.build()).dataUrl.split(',')[1])).toContain('Extracted lesson text');

    const svgDataUrl = `data:image/svg+xml;base64,${globalThis.btoa('<svg><text>Apple</text></svg>')}`;
    const svgPlans = buildAttachmentUploadPlans(source('image/svg+xml', svgDataUrl), adapters());
    expect(svgPlans.map(plan => plan.source)).toEqual(['svg-source', 'svg-rasterized']);
    expect(globalThis.atob((await svgPlans[0].build()).dataUrl.split(',')[1])).toContain('<svg>');
    expect((await svgPlans[1].build()).mimeType).toBe('image/jpeg');
  });
});
