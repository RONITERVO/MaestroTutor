// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { pcmToWav } from '../core-sdk/media/audioProcessing';
import { createAdvancedSyntheticAttachment } from './syntheticAdvancedAttachments';

export type BasicSyntheticAttachmentKind = 'text' | 'image' | 'audio' | 'pdf';
export type AdvancedSyntheticAttachmentKind = 'svg' | 'video' | 'office';
export type SyntheticAttachmentKind = BasicSyntheticAttachmentKind | AdvancedSyntheticAttachmentKind;

export interface SyntheticAttachment {
  kind: SyntheticAttachmentKind;
  dataUrl: string;
  mimeType: string;
  displayName: string;
}

const bytesToBase64 = (bytes: Uint8Array): string => {
  let binary = '';
  const chunkSize = 0x8000;
  for (let index = 0; index < bytes.length; index += chunkSize) {
    binary += String.fromCharCode(...bytes.subarray(index, index + chunkSize));
  }
  return globalThis.btoa(binary);
};

const textDataUrl = (text: string, mimeType: string) => (
  `data:${mimeType};base64,${bytesToBase64(new TextEncoder().encode(text))}`
);

const createPdf = (text: string): string => {
  if (/[^\x20-\x7e]/.test(text)) {
    throw new Error('Synthetic PDF labels must contain printable ASCII characters only.');
  }
  const safeText = text.replace(/[()\\]/g, match => `\\${match}`);
  const stream = `BT /F1 12 Tf 72 720 Td (${safeText}) Tj ET`;
  const objects = [
    '1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n',
    '2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n',
    '3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>\nendobj\n',
    `4 0 obj\n<< /Length ${stream.length} >>\nstream\n${stream}\nendstream\nendobj\n`,
    '5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n',
  ];
  let pdf = '%PDF-1.4\n%Maestro\n';
  const offsets = [0];
  for (const object of objects) {
    offsets.push(pdf.length);
    pdf += object;
  }
  const xrefOffset = pdf.length;
  pdf += `xref\n0 ${objects.length + 1}\n`;
  pdf += '0000000000 65535 f \n';
  for (let index = 1; index <= objects.length; index += 1) {
    pdf += `${String(offsets[index]).padStart(10, '0')} 00000 n \n`;
  }
  pdf += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xrefOffset}\n%%EOF\n`;
  return textDataUrl(pdf, 'application/pdf');
};

const createAudio = (): string => {
  const sampleRate = 16_000;
  const samples = new Int16Array(sampleRate / 2);
  for (let index = 0; index < samples.length; index += 1) {
    const envelope = Math.sin(Math.PI * index / samples.length);
    samples[index] = Math.round(Math.sin(2 * Math.PI * 440 * index / sampleRate) * envelope * 7000);
  }
  return pcmToWav(samples, sampleRate, 1);
};

export const createSyntheticAttachment = (
  kind: BasicSyntheticAttachmentKind,
  label = 'Maestro deterministic attachment fixture',
): SyntheticAttachment => {
  switch (kind) {
    case 'text':
      return {
        kind,
        dataUrl: textDataUrl(`${label}\nThis file is synthetic test data.\n`, 'text/plain'),
        mimeType: 'text/plain',
        displayName: 'maestro-fixture.txt',
      };
    case 'image':
      return {
        kind,
        dataUrl: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
        mimeType: 'image/png',
        displayName: 'maestro-fixture.png',
      };
    case 'audio':
      return {
        kind,
        dataUrl: createAudio(),
        mimeType: 'audio/wav',
        displayName: 'maestro-fixture.wav',
      };
    case 'pdf':
      return {
        kind,
        dataUrl: createPdf(label),
        mimeType: 'application/pdf',
        displayName: 'maestro-fixture.pdf',
      };
    default:
      throw new Error(`Unsupported synthetic attachment kind: ${String(kind)}`);
  }
};

export const createSyntheticAttachmentFixture = async (
  kind: SyntheticAttachmentKind,
  label = 'Maestro deterministic attachment fixture',
): Promise<SyntheticAttachment> => (
  kind === 'svg' || kind === 'video' || kind === 'office'
    ? createAdvancedSyntheticAttachment(kind)
    : createSyntheticAttachment(kind, label)
);
