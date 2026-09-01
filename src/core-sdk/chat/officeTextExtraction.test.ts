// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import JSZip from 'jszip';
import { describe, expect, it } from 'vitest';
import { extractOfficeTextForUpload } from './officeTextExtraction';

describe('Office upload text extraction', () => {
  it('extracts decoded paragraph text from a real OpenXML ZIP', async () => {
    const zip = new JSZip();
    zip.file('word/document.xml', [
      '<?xml version="1.0"?>',
      '<w:document xmlns:w="urn:test"><w:body>',
      '<w:p><w:r><w:t>Red &amp; green</w:t></w:r></w:p>',
      '<w:p><w:r><w:t>apple vocabulary</w:t></w:r></w:p>',
      '</w:body></w:document>',
    ].join(''));
    const bytes = await zip.generateAsync({ type: 'uint8array' });
    const dataUrl = `data:application/vnd.openxmlformats-officedocument.wordprocessingml.document;base64,${Buffer.from(bytes).toString('base64')}`;
    await expect(extractOfficeTextForUpload({
      dataUrl,
      mimeType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      fileName: 'lesson.docx',
    })).resolves.toBe('Red & green\napple vocabulary');
  });
});
