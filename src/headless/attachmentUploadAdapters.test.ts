// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { buildHeadlessAttachmentUploadPlans } from './attachmentUploadAdapters';
import { createAdvancedSyntheticAttachment } from './syntheticAdvancedAttachments';

describe('headless attachment upload adapters', () => {
  it.each([
    ['svg', ['text/plain', 'image/jpeg']],
    ['video', ['image/jpeg', 'video/mp4']],
    ['office', ['text/plain']],
  ] as const)('builds real %s upload payloads through the shared Core plan', async (kind, mimeTypes) => {
    const fixture = await createAdvancedSyntheticAttachment(kind);
    const plans = buildHeadlessAttachmentUploadPlans({
      dataUrl: fixture.dataUrl,
      mimeType: fixture.mimeType,
      attachmentName: fixture.displayName,
    });
    const payloads = await Promise.all(plans.map(plan => plan.build()));
    expect(payloads.map(payload => payload.mimeType)).toEqual(mimeTypes);
    for (const payload of payloads) {
      expect(payload.dataUrl).toMatch(/^data:[^;]+(?:;charset=[^;]+)?;base64,/);
      expect(Buffer.from(payload.dataUrl.split(',')[1], 'base64').length).toBeGreaterThan(0);
    }
    if (kind === 'office') {
      expect(Buffer.from(payloads[0].dataUrl.split(',')[1], 'base64').toString('utf8'))
        .toContain('red apple vocabulary');
    }
  });
});
