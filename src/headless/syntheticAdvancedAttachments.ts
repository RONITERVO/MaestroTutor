// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { readFile } from 'node:fs/promises';
import JSZip from 'jszip';
import type { SyntheticAttachment, SyntheticAttachmentKind } from './syntheticAttachments';

const bytesToDataUrl = (bytes: Uint8Array, mimeType: string): string => (
  `data:${mimeType};base64,${Buffer.from(bytes).toString('base64')}`
);

export const createAdvancedSyntheticAttachment = async (
  kind: Extract<SyntheticAttachmentKind, 'svg' | 'video' | 'office'>,
): Promise<SyntheticAttachment> => {
  if (kind === 'svg') {
    const svg = '<svg xmlns="http://www.w3.org/2000/svg" width="320" height="180"><rect width="320" height="180" fill="#dbeafe"/><circle cx="160" cy="80" r="52" fill="#dc2626"/><text x="160" y="155" text-anchor="middle" font-size="24">RED APPLE</text></svg>';
    return {
      kind,
      dataUrl: bytesToDataUrl(new TextEncoder().encode(svg), 'image/svg+xml'),
      mimeType: 'image/svg+xml',
      displayName: 'maestro-fixture.svg',
    };
  }
  if (kind === 'video') {
    const bytes = await readFile(new URL('../../public/loading-animations/spinner_1.mp4', import.meta.url));
    return {
      kind,
      dataUrl: bytesToDataUrl(bytes, 'video/mp4'),
      mimeType: 'video/mp4',
      displayName: 'maestro-fixture.mp4',
    };
  }

  const zip = new JSZip();
  zip.file('[Content_Types].xml', '<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>');
  zip.file('word/document.xml', '<?xml version="1.0"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>Maestro synthetic office lesson: red apple vocabulary.</w:t></w:r></w:p></w:body></w:document>');
  const bytes = await zip.generateAsync({ type: 'uint8array' });
  const mimeType = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
  return {
    kind,
    dataUrl: bytesToDataUrl(bytes, mimeType),
    mimeType,
    displayName: 'maestro-fixture.docx',
  };
};
