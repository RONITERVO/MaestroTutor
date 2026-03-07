// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import JSZip from 'jszip';
import {
  decodeTextFromDataUrl,
  extractGoogleWorkspaceUrlFromDataUrl,
  isGoogleWorkspaceShortcutFileName,
  isGoogleWorkspaceShortcutMimeType,
  isMicrosoftOfficeMimeType,
} from './fileAttachments';
import { deriveChartSeriesListFromRows, type TabularChartSeries, type TabularSheetPreview } from './tabularPreview';

const WORD_OPENXML_EXTENSIONS = new Set(['docx', 'docm', 'dotx', 'dotm']);
const EXCEL_OPENXML_EXTENSIONS = new Set(['xlsx', 'xlsm', 'xlsb', 'xltx', 'xltm']);
const POWERPOINT_OPENXML_EXTENSIONS = new Set(['pptx', 'pptm', 'ppsx', 'potx']);
const LEGACY_BINARY_EXTENSIONS = new Set(['doc', 'xls', 'ppt', 'pps', 'pot']);
const OPEN_DOCUMENT_EXTENSIONS = new Set(['odt', 'ods', 'odp']);

const MAX_PREVIEW_CHARS = 120_000;
const MAX_SHEET_ROWS = 220;
const MAX_SHEET_COLS = 40;
const MAX_SHEETS = 12;
const MAX_SLIDES = 20;
const PREVIEW_CACHE_MAX = 24;

type OfficePreviewStatus = 'ready' | 'unsupported' | 'error';

export interface OfficePreviewResult {
  status: OfficePreviewStatus;
  text: string | null;
  note?: string;
  tableRows?: string[][];
  chartSeries?: TabularChartSeries;
  sheets?: TabularSheetPreview[];
}

const previewCache = new Map<string, Promise<OfficePreviewResult>>();

const getExtension = (fileName?: string | null): string => {
  const name = (fileName || '').trim().toLowerCase();
  const dotIndex = name.lastIndexOf('.');
  if (dotIndex < 0 || dotIndex >= name.length - 1) return '';
  return name.slice(dotIndex + 1);
};

const normalizeNewLines = (value: string): string => value.replace(/\r\n/g, '\n').replace(/\r/g, '\n');

const clipPreviewText = (value: string): string => {
  const normalized = normalizeNewLines(value).trim();
  if (!normalized) return '';
  if (normalized.length <= MAX_PREVIEW_CHARS) return normalized;
  return `${normalized.slice(0, MAX_PREVIEW_CHARS)}\n...`;
};

const dataUrlToUint8Array = (dataUrl: string): Uint8Array | null => {
  if (!dataUrl || typeof dataUrl !== 'string' || !dataUrl.startsWith('data:')) return null;
  const commaIndex = dataUrl.indexOf(',');
  if (commaIndex < 0) return null;
  const header = dataUrl.slice(0, commaIndex).toLowerCase();
  const payload = dataUrl.slice(commaIndex + 1);
  if (!payload) return null;

  if (!header.includes(';base64')) {
    try {
      const decoded = decodeURIComponent(payload);
      return new TextEncoder().encode(decoded);
    } catch {
      return null;
    }
  }

  try {
    const binaryString = atob(payload);
    const bytes = new Uint8Array(binaryString.length);
    for (let i = 0; i < binaryString.length; i++) {
      bytes[i] = binaryString.charCodeAt(i);
    }
    return bytes;
  } catch {
    return null;
  }
};

const parseXml = (xml: string): Document | null => {
  if (!xml) return null;
  try {
    const doc = new DOMParser().parseFromString(xml, 'application/xml');
    if (doc.getElementsByTagName('parsererror').length > 0) return null;
    return doc;
  } catch {
    return null;
  }
};

const findByLocalName = (root: Document | Element, localName: string): Element[] => {
  const out: Element[] = [];
  const nodes = root.getElementsByTagName('*');
  for (let i = 0; i < nodes.length; i++) {
    const node = nodes[i];
    if (node.localName === localName) out.push(node);
  }
  return out;
};

const elementText = (el?: Element | null): string => (el?.textContent || '').replace(/\s+/g, ' ').trim();

const getParagraphTextFromDoc = (doc: Document): string => {
  const paragraphs = findByLocalName(doc, 'p');
  const lines: string[] = [];

  if (paragraphs.length > 0) {
    for (const paragraph of paragraphs) {
      const runs = findByLocalName(paragraph, 't');
      const line = runs.map((run) => run.textContent || '').join('').replace(/\s+/g, ' ').trim();
      if (line) lines.push(line);
    }
  } else {
    const genericRuns = findByLocalName(doc, 't');
    for (const run of genericRuns) {
      const value = (run.textContent || '').replace(/\s+/g, ' ').trim();
      if (value) lines.push(value);
    }
  }

  return lines.join('\n');
};

const toColumnIndex = (letters: string): number => {
  let out = 0;
  const upper = letters.toUpperCase();
  for (let i = 0; i < upper.length; i++) {
    const code = upper.charCodeAt(i);
    if (code < 65 || code > 90) return -1;
    out = out * 26 + (code - 64);
  }
  return out - 1;
};

const toSheetPath = (target?: string | null): string | null => {
  if (!target) return null;
  const normalized = target.replace(/^\/+/, '');
  if (!normalized) return null;
  if (normalized.startsWith('xl/')) return normalized;
  return `xl/${normalized.replace(/^\.?\//, '')}`;
};

const getFirstDescendant = (root: Element, localName: string): Element | null => {
  const nodes = findByLocalName(root, localName);
  return nodes.length > 0 ? nodes[0] : null;
};

const parseSharedStrings = (sharedXml?: string | null): string[] => {
  if (!sharedXml) return [];
  const doc = parseXml(sharedXml);
  if (!doc) return [];

  const items = findByLocalName(doc, 'si');
  return items.map((item) => {
    const textNodes = findByLocalName(item, 't');
    const text = textNodes.map((node) => node.textContent || '').join('');
    return text.replace(/\s+/g, ' ').trim();
  });
};

const parseSpreadsheetCell = (cell: Element, sharedStrings: string[]): string => {
  const type = (cell.getAttribute('t') || '').trim().toLowerCase();
  const valueNode = getFirstDescendant(cell, 'v');
  const formulaNode = getFirstDescendant(cell, 'f');
  const inlineText = findByLocalName(cell, 't').map((node) => node.textContent || '').join('').trim();
  const rawValue = elementText(valueNode);

  if (type === 'inlineStr' && inlineText) return inlineText;
  if (type === 's') {
    const idx = Number(rawValue);
    if (Number.isFinite(idx) && idx >= 0 && idx < sharedStrings.length) return sharedStrings[idx];
    return rawValue;
  }
  if (type === 'b') return rawValue === '1' ? 'TRUE' : 'FALSE';
  if (type === 'str') return rawValue;
  if (type === 'e') return rawValue ? `#${rawValue}` : '#ERROR';
  if (rawValue) return rawValue;
  if (inlineText) return inlineText;
  if (formulaNode?.textContent?.trim()) return `=${formulaNode.textContent.trim()}`;
  return '';
};

const parseWorkbookSheetEntries = (
  zip: JSZip,
  workbookXml?: string | null,
  workbookRelsXml?: string | null,
): Array<{ name: string; path: string }> => {
  const entries: Array<{ name: string; path: string }> = [];

  const relTargets = new Map<string, string>();
  if (workbookRelsXml) {
    const relDoc = parseXml(workbookRelsXml);
    if (relDoc) {
      const relNodes = findByLocalName(relDoc, 'Relationship');
      for (const relNode of relNodes) {
        const id = relNode.getAttribute('Id') || relNode.getAttribute('id');
        const target = relNode.getAttribute('Target') || relNode.getAttribute('target');
        const path = toSheetPath(target);
        if (id && path) relTargets.set(id, path);
      }
    }
  }

  if (workbookXml) {
    const wbDoc = parseXml(workbookXml);
    if (wbDoc) {
      const sheetNodes = findByLocalName(wbDoc, 'sheet');
      for (let i = 0; i < sheetNodes.length; i++) {
        const sheetNode = sheetNodes[i];
        const name = sheetNode.getAttribute('name') || `Sheet ${i + 1}`;
        const relationshipId =
          sheetNode.getAttribute('r:id') ||
          sheetNode.getAttribute('id');

        let path = relationshipId ? relTargets.get(relationshipId) || null : null;
        if (!path) path = `xl/worksheets/sheet${i + 1}.xml`;
        if (!zip.file(path)) continue;
        entries.push({ name, path });
      }
    }
  }

  if (entries.length > 0) return entries.slice(0, MAX_SHEETS);

  const fallbackPaths = Object.keys(zip.files)
    .filter((path) => /^xl\/worksheets\/[^/]+\.xml$/i.test(path))
    .sort()
    .slice(0, MAX_SHEETS);

  return fallbackPaths.map((path, index) => ({ name: `Sheet ${index + 1}`, path }));
};

interface SpreadsheetExtractionResult {
  text: string;
  sheets: TabularSheetPreview[];
}

const extractSpreadsheetText = async (zip: JSZip): Promise<SpreadsheetExtractionResult> => {
  const sharedStringsXml = await zip.file('xl/sharedStrings.xml')?.async('string');
  const workbookXml = await zip.file('xl/workbook.xml')?.async('string');
  const workbookRelsXml = await zip.file('xl/_rels/workbook.xml.rels')?.async('string');

  const sharedStrings = parseSharedStrings(sharedStringsXml);
  const entries = parseWorkbookSheetEntries(zip, workbookXml, workbookRelsXml);
  if (entries.length === 0) {
    return { text: '', sheets: [] };
  }

  const chunks: string[] = [];
  const sheets: TabularSheetPreview[] = [];

  for (const entry of entries) {
    const sheetXml = await zip.file(entry.path)?.async('string');
    if (!sheetXml) continue;
    const sheetDoc = parseXml(sheetXml);
    if (!sheetDoc) continue;

    const rowNodes = findByLocalName(sheetDoc, 'row');
    const rows: string[][] = [];

    for (let rowIndex = 0; rowIndex < rowNodes.length && rows.length < MAX_SHEET_ROWS; rowIndex++) {
      const rowNode = rowNodes[rowIndex];
      const cellNodes = findByLocalName(rowNode, 'c');
      if (cellNodes.length === 0) continue;

      const cols = new Array<string>(MAX_SHEET_COLS).fill('');
      for (let c = 0; c < cellNodes.length; c++) {
        const cell = cellNodes[c];
        const ref = (cell.getAttribute('r') || '').trim();
        const match = ref.match(/^[A-Za-z]+/);
        const colIndex = match ? toColumnIndex(match[0]) : c;
        if (colIndex < 0 || colIndex >= MAX_SHEET_COLS) continue;
        cols[colIndex] = parseSpreadsheetCell(cell, sharedStrings);
      }

      let lastNonEmpty = cols.length - 1;
      while (lastNonEmpty >= 0 && !cols[lastNonEmpty]) lastNonEmpty--;
      if (lastNonEmpty < 0) continue;
      rows.push(cols.slice(0, lastNonEmpty + 1));
    }

    if (rows.length > 0) {
      chunks.push(`[Sheet: ${entry.name}]`);
      chunks.push(rows.map((row) => row.join('\t')).join('\n'));
      chunks.push('');

      sheets.push({
        name: entry.name,
        rows: rows.map((row) => [...row]),
        chartSeriesList: deriveChartSeriesListFromRows(rows),
      });
    }
  }

  return {
    text: chunks.join('\n').trim(),
    sheets,
  };
};

const extractSlidesText = async (zip: JSZip): Promise<string> => {
  const slidePaths = Object.keys(zip.files)
    .filter((path) => /^ppt\/slides\/slide\d+\.xml$/i.test(path))
    .sort((a, b) => {
      const aMatch = a.match(/slide(\d+)\.xml$/i);
      const bMatch = b.match(/slide(\d+)\.xml$/i);
      const aNum = aMatch ? Number(aMatch[1]) : 0;
      const bNum = bMatch ? Number(bMatch[1]) : 0;
      return aNum - bNum;
    })
    .slice(0, MAX_SLIDES);

  if (slidePaths.length === 0) return '';

  const chunks: string[] = [];

  for (let i = 0; i < slidePaths.length; i++) {
    const path = slidePaths[i];
    const xml = await zip.file(path)?.async('string');
    if (!xml) continue;
    const doc = parseXml(xml);
    if (!doc) continue;

    const textRuns = findByLocalName(doc, 't')
      .map((node) => (node.textContent || '').replace(/\s+/g, ' ').trim())
      .filter(Boolean);

    if (textRuns.length === 0) continue;

    chunks.push(`[Slide ${i + 1}]`);
    chunks.push(textRuns.join('\n'));
    chunks.push('');
  }

  return chunks.join('\n').trim();
};

const extractWordProcessingText = async (zip: JSZip): Promise<string> => {
  const documentXml = await zip.file('word/document.xml')?.async('string');
  if (!documentXml) return '';
  const doc = parseXml(documentXml);
  if (!doc) return '';
  return getParagraphTextFromDoc(doc);
};

const extractOpenDocumentText = async (zip: JSZip): Promise<string> => {
  const contentXml = await zip.file('content.xml')?.async('string');
  if (!contentXml) return '';
  const doc = parseXml(contentXml);
  if (!doc) return '';
  return getParagraphTextFromDoc(doc);
};

const parseGoogleWorkspaceShortcut = (src?: string | null): OfficePreviewResult => {
  const link = extractGoogleWorkspaceUrlFromDataUrl(src);
  if (link) {
    return {
      status: 'ready',
      text: `Google Workspace link:\n${link}`,
    };
  }

  const raw = decodeTextFromDataUrl(src);
  if (raw && raw.trim()) {
    return {
      status: 'ready',
      text: clipPreviewText(raw),
    };
  }

  return {
    status: 'unsupported',
    text: null,
    note: 'Could not decode Google Workspace shortcut content.',
  };
};

const parseOfficePreviewInternal = async (src: string, mimeType?: string | null, fileName?: string | null): Promise<OfficePreviewResult> => {
  const ext = getExtension(fileName);
  const mime = (mimeType || '').trim().toLowerCase();

  if (isGoogleWorkspaceShortcutFileName(fileName) || isGoogleWorkspaceShortcutMimeType(mime)) {
    return parseGoogleWorkspaceShortcut(src);
  }

  const bytes = dataUrlToUint8Array(src);
  if (!bytes) {
    return {
      status: 'error',
      text: null,
      note: 'Failed to read Office attachment data.',
    };
  }

  if (LEGACY_BINARY_EXTENSIONS.has(ext)) {
    return {
      status: 'unsupported',
      text: null,
      note: 'Legacy Office binaries (.doc/.xls/.ppt) are not previewable inline yet.',
    };
  }

  if (!WORD_OPENXML_EXTENSIONS.has(ext) &&
      !EXCEL_OPENXML_EXTENSIONS.has(ext) &&
      !POWERPOINT_OPENXML_EXTENSIONS.has(ext) &&
      !OPEN_DOCUMENT_EXTENSIONS.has(ext) &&
      !isMicrosoftOfficeMimeType(mime)) {
    return {
      status: 'unsupported',
      text: null,
      note: 'Unsupported Office format for inline preview.',
    };
  }

  try {
    const zip = await JSZip.loadAsync(bytes);
    let extracted = '';
    let tableRows: string[][] | undefined;
    let chartSeries: TabularChartSeries | undefined;
    let sheets: TabularSheetPreview[] | undefined;

    if (WORD_OPENXML_EXTENSIONS.has(ext) || zip.file('word/document.xml')) {
      extracted = await extractWordProcessingText(zip);
    } else if (EXCEL_OPENXML_EXTENSIONS.has(ext) || zip.file('xl/workbook.xml')) {
      const spreadsheet = await extractSpreadsheetText(zip);
      extracted = spreadsheet.text;
      if (spreadsheet.sheets.length > 0) {
        sheets = spreadsheet.sheets;
        tableRows = spreadsheet.sheets[0].rows;
        chartSeries = spreadsheet.sheets[0].chartSeriesList[0];
      }
    } else if (POWERPOINT_OPENXML_EXTENSIONS.has(ext) || zip.file('ppt/presentation.xml')) {
      extracted = await extractSlidesText(zip);
    } else if (OPEN_DOCUMENT_EXTENSIONS.has(ext) || zip.file('content.xml')) {
      extracted = await extractOpenDocumentText(zip);
    }

    const clipped = clipPreviewText(extracted);
    if (!clipped) {
      return {
        status: 'unsupported',
        text: null,
        note: 'No readable text content found in this Office file.',
      };
    }

    return {
      status: 'ready',
      text: clipped,
      tableRows,
      chartSeries,
      sheets,
    };
  } catch (error) {
    return {
      status: 'error',
      text: null,
      note: error instanceof Error ? error.message : 'Failed to parse Office file preview.',
    };
  }
};

export const getOfficePreview = async (
  src?: string | null,
  mimeType?: string | null,
  fileName?: string | null,
): Promise<OfficePreviewResult> => {
  if (!src) {
    return {
      status: 'unsupported',
      text: null,
      note: 'No local file payload available for inline preview.',
    };
  }

  const cacheKey = `${mimeType || ''}|${fileName || ''}|${src}`;
  const existing = previewCache.get(cacheKey);
  if (existing) return existing;

  if (previewCache.size >= PREVIEW_CACHE_MAX) {
    const oldestKey = previewCache.keys().next().value;
    if (oldestKey) previewCache.delete(oldestKey);
  }

  const work = parseOfficePreviewInternal(src, mimeType, fileName).catch((error) => ({
    status: 'error' as const,
    text: null,
    note: error instanceof Error ? error.message : 'Failed to parse Office preview.',
  }));

  previewCache.set(cacheKey, work);
  return work;
};
