// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import sharp from 'sharp';

export interface SyntheticVisualFrame {
  dataBase64: string;
  mimeType: 'image/jpeg';
  width: number;
  height: number;
  semanticLabel: string;
}

const escapeXml = (value: string): string => value
  .replace(/&/g, '&amp;')
  .replace(/</g, '&lt;')
  .replace(/>/g, '&gt;')
  .replace(/"/g, '&quot;')
  .replace(/'/g, '&apos;');

/** A deterministic, genuinely decodable visual frame for Live video injection. */
export const createSyntheticVisualFrame = async (
  semanticLabel = 'RED APPLE',
): Promise<SyntheticVisualFrame> => {
  const width = 320;
  const height = 240;
  const label = escapeXml(semanticLabel.trim() || 'RED APPLE');
  const svg = Buffer.from(`
    <svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
      <rect width="100%" height="100%" fill="#dbeafe"/>
      <circle cx="160" cy="103" r="62" fill="#dc2626"/>
      <rect x="154" y="28" width="12" height="30" rx="4" fill="#166534"/>
      <text x="160" y="205" font-family="Arial, sans-serif" font-size="30" font-weight="700"
        text-anchor="middle" fill="#111827">${label}</text>
    </svg>
  `);
  const jpeg = await sharp(svg).jpeg({ quality: 82, chromaSubsampling: '4:4:4' }).toBuffer();
  return {
    dataBase64: jpeg.toString('base64'),
    mimeType: 'image/jpeg',
    width,
    height,
    semanticLabel: semanticLabel.trim() || 'RED APPLE',
  };
};
