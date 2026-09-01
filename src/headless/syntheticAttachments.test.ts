// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import {
  createSyntheticAttachment,
  createSyntheticAttachmentFixture,
  type BasicSyntheticAttachmentKind,
  type SyntheticAttachmentKind,
} from './syntheticAttachments';

const decode = (dataUrl: string): Buffer => Buffer.from(dataUrl.split(',')[1], 'base64');

describe('synthetic attachment fixtures', () => {
  it.each<BasicSyntheticAttachmentKind>(['text', 'image', 'audio', 'pdf'])('builds a deterministic %s payload', kind => {
    const first = createSyntheticAttachment(kind);
    const second = createSyntheticAttachment(kind);
    expect(first).toEqual(second);
    expect(first.dataUrl).toMatch(/^data:[^;]+;base64,/);
    expect(decode(first.dataUrl).length).toBeGreaterThan(0);
  });

  it('produces structurally recognizable WAV and PDF fixtures', () => {
    expect(decode(createSyntheticAttachment('audio').dataUrl).subarray(0, 4).toString()).toBe('RIFF');
    const pdf = decode(createSyntheticAttachment('pdf').dataUrl).toString('ascii');
    expect(pdf.startsWith('%PDF-1.4')).toBe(true);
    expect(pdf.endsWith('%%EOF\n')).toBe(true);
  });

  it('rejects non-ASCII PDF labels before producing incorrect byte offsets', () => {
    expect(() => createSyntheticAttachment('pdf', 'Maestro ä fixture'))
      .toThrow('printable ASCII');
  });

  it.each<SyntheticAttachmentKind>(['svg', 'video', 'office'])('builds deterministic advanced %s payloads', async kind => {
    const first = await createSyntheticAttachmentFixture(kind);
    const second = await createSyntheticAttachmentFixture(kind);
    expect(first).toEqual(second);
    expect(decode(first.dataUrl).length).toBeGreaterThan(0);
  });
});
