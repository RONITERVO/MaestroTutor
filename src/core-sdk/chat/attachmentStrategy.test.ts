// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { resolveAttachmentStrategy } from './attachmentStrategy';

describe('attachment strategy', () => {
  it.each([
    ['video/mp4', ['video-keyframe', 'original']],
    ['image/svg+xml', ['svg-source', 'svg-rasterized']],
    ['application/vnd.openxmlformats-officedocument.wordprocessingml.document', ['office-text']],
    ['application/pdf', ['original']],
    ['audio/wav', ['original']],
  ])('maps %s through the same UI/headless variant plan', (mimeType, sources) => {
    expect(resolveAttachmentStrategy(mimeType).map(step => step.source)).toEqual(sources);
  });
});
