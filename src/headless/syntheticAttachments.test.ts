// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it, vi } from 'vitest';
import JSZip from 'jszip';
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

  it('keeps office fixture bytes stable across different creation times', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    try {
      vi.setSystemTime(new Date('2024-01-01T00:00:00Z'));
      const first = await createSyntheticAttachmentFixture('office');
      vi.setSystemTime(new Date('2026-10-18T16:00:00Z'));
      const later = await createSyntheticAttachmentFixture('office');
      expect(later).toEqual(first);
      const archive = await JSZip.loadAsync(decode(later.dataUrl));
      expect(await archive.file('word/document.xml')?.async('string'))
        .toContain('Maestro synthetic office lesson: red apple vocabulary.');
    } finally {
      vi.useRealTimers();
    }
  });

  it.each<SyntheticAttachmentKind>(['svg', 'video', 'office'])('builds deterministic advanced %s payloads', async kind => {
    const first = await createSyntheticAttachmentFixture(kind);
    const second = await createSyntheticAttachmentFixture(kind);
    expect(first).toEqual(second);
    expect(decode(first.dataUrl).length).toBeGreaterThan(0);
  });
});
