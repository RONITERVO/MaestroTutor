// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import {
  decodeTextFromDataUrl,
  extractGoogleWorkspaceUrlFromDataUrl,
  isGoogleWorkspaceShortcutFileName,
  isGoogleWorkspaceShortcutMimeType,
} from './fileAttachments';

const MAX_TEXT_CHARS = 120_000;

const decodeDataUrlBytes = (dataUrl: string): Uint8Array => {
  const commaIndex = dataUrl.indexOf(',');
  if (!dataUrl.startsWith('data:') || commaIndex < 0) {
    throw new Error('Office attachment must be a data URL.');
  }
  const header = dataUrl.slice(0, commaIndex);
  const payload = dataUrl.slice(commaIndex + 1);
  if (/;base64(?:;|$)/i.test(header)) {
    return Uint8Array.from(globalThis.atob(payload), character => character.charCodeAt(0));
  }
  return new TextEncoder().encode(decodeURIComponent(payload));
};

const decodeXmlEntities = (value: string): string => value
  .replace(/&#(\d+);/g, (_match, code: string) => String.fromCodePoint(Number(code)))
  .replace(/&#x([0-9a-f]+);/gi, (_match, code: string) => String.fromCodePoint(Number.parseInt(code, 16)))
  .replace(/&lt;/g, '<')
  .replace(/&gt;/g, '>')
  .replace(/&quot;/g, '"')
  .replace(/&apos;/g, "'")
  .replace(/&amp;/g, '&');

const plainXmlText = (value: string): string => decodeXmlEntities(
  value.replace(/<[^>]+>/g, '').replace(/\s+/g, ' '),
).trim();

const collectLocalTagText = (xml: string, localName: string): string[] => {
  const pattern = new RegExp(
    `<(?:[A-Za-z_][\\w.-]*:)?${localName}(?:\\s[^>]*)?>([\\s\\S]*?)<\\/(?:[A-Za-z_][\\w.-]*:)?${localName}\\s*>`,
    'gi',
  );
  return Array.from(xml.matchAll(pattern))
    .map(match => plainXmlText(match[1] || ''))
    .filter(Boolean);
};

const clip = (value: string): string => {
  const normalized = value.replace(/\r\n/g, '\n').replace(/\r/g, '\n').trim();
  if (normalized.length <= MAX_TEXT_CHARS) return normalized;
  return `${normalized.slice(0, MAX_TEXT_CHARS)}\n...`;
};

const sortedNumericPaths = (paths: string[], expression: RegExp): string[] => paths.sort((left, right) => {
  const leftNumber = Number(expression.exec(left)?.[1] || 0);
  expression.lastIndex = 0;
  const rightNumber = Number(expression.exec(right)?.[1] || 0);
  expression.lastIndex = 0;
  return leftNumber - rightNumber;
});

/**
 * Extracts the upload surrogate used by both browser and headless clients.
 * It deliberately has no DOM dependency: OpenXML/ODF are ZIP packages and the
 * provider needs readable text, not a rendered document tree.
 */
export const extractOfficeTextForUpload = async (source: {
  dataUrl: string;
  mimeType?: string | null;
  fileName?: string | null;
}): Promise<string> => {
  const mimeType = (source.mimeType || '').trim().toLowerCase();
  if (isGoogleWorkspaceShortcutFileName(source.fileName)
    || isGoogleWorkspaceShortcutMimeType(mimeType)) {
    const link = extractGoogleWorkspaceUrlFromDataUrl(source.dataUrl);
    const decoded = decodeTextFromDataUrl(source.dataUrl)?.trim();
    const text = link ? `Google Workspace link:\n${link}` : decoded;
    if (!text) throw new Error('Could not decode Google Workspace shortcut content.');
    return clip(text);
  }

  const { default: JSZip } = await import('jszip');
  const zip = await JSZip.loadAsync(decodeDataUrlBytes(source.dataUrl));

  const wordXml = await zip.file('word/document.xml')?.async('string');
  if (wordXml) {
    const paragraphs = collectLocalTagText(wordXml, 'p');
      const text = paragraphs.length ? paragraphs.join('\n') : collectLocalTagText(wordXml, 't').join('\n');
    if (text.trim()) return clip(text);
  }

  const slideExpression = /^ppt\/slides\/slide(\d+)\.xml$/i;
  const slidePaths = sortedNumericPaths(
    Object.keys(zip.files).filter(path => slideExpression.test(path)),
    /slide(\d+)\.xml$/i,
  );
  if (slidePaths.length) {
    const slides: string[] = [];
    for (let index = 0; index < Math.min(slidePaths.length, 20); index += 1) {
      const xml = await zip.file(slidePaths[index])?.async('string');
      const text = xml ? collectLocalTagText(xml, 't').join('\n') : '';
      if (text) slides.push(`[Slide ${index + 1}]\n${text}`);
    }
    if (slides.length) return clip(slides.join('\n\n'));
  }

  const worksheetExpression = /^xl\/worksheets\/sheet(\d+)\.xml$/i;
  const worksheetPaths = sortedNumericPaths(
    Object.keys(zip.files).filter(path => worksheetExpression.test(path)),
    /sheet(\d+)\.xml$/i,
  );
  if (worksheetPaths.length || zip.file('xl/sharedStrings.xml')) {
    const chunks: string[] = [];
    const sharedXml = await zip.file('xl/sharedStrings.xml')?.async('string');
    if (sharedXml) chunks.push(...collectLocalTagText(sharedXml, 't'));
    for (let index = 0; index < Math.min(worksheetPaths.length, 12); index += 1) {
      const xml = await zip.file(worksheetPaths[index])?.async('string');
      if (!xml) continue;
      const inline = collectLocalTagText(xml, 't');
      const values = collectLocalTagText(xml, 'v');
      if (inline.length || values.length) {
        chunks.push(`[Sheet ${index + 1}]`, ...inline, ...values);
      }
    }
    if (chunks.length) return clip(chunks.join('\n'));
  }

  const openDocumentXml = await zip.file('content.xml')?.async('string');
  if (openDocumentXml) {
    const paragraphs = collectLocalTagText(openDocumentXml, 'p');
    const text = paragraphs.join('\n');
    if (text.trim()) return clip(text);
  }

  throw new Error('No readable text content was found in this Office attachment.');
};
