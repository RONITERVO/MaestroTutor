// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

interface AttachmentCandidate {
  start: number;
  end: number;
  priority: number;
  attachment: ParsedAssistantAttachment;
}

export interface ParsedAssistantAttachment {
  kind: 'svg' | 'image' | 'chart' | 'code';
  dataUrl: string;
  mimeType: string;
  fileName: string;
}

export interface ParsedAssistantResponse {
  cleanedText: string;
  attachment?: ParsedAssistantAttachment;
}

const MAX_ATTACHMENT_TEXT_CHARS = 180_000;

const FENCE_REGEX = /```([^\n`]*)\n?([\s\S]*?)```/g;
const INLINE_SVG_REGEX = /<svg[\s\S]*?<\/svg>/i;
const STANDALONE_SVG_REGEX = /^\s*(?:<\?xml[\s\S]*?\?>\s*)?<svg[\s\S]*<\/svg>\s*$/i;
const RAW_HTML_DOCUMENT_REGEX = /<!doctype\s+html|<html[\s>]|<body[\s>]|<head[\s>]|<\/html>/i;
const INLINE_IMAGE_DATA_URL_REGEX = /data:image\/[a-z0-9.+-]+(?:;[a-z0-9=._-]+)*,[a-z0-9+/%=._\-\s]+/i;
const INLINE_MINI_GAME_MARKER_REGEX = /data-maestro-mini-game|@maestro-mini-game/i;
const HTML_TAG_REGEX = /<\/?([a-z][\w:-]*)\b[^>]*>/ig;

const CODE_LANGUAGE_TO_EXTENSION: Record<string, string> = {
  js: 'js',
  javascript: 'js',
  mjs: 'mjs',
  cjs: 'cjs',
  text: 'txt',
  plaintext: 'txt',
  txt: 'txt',
  ts: 'ts',
  typescript: 'ts',
  jsx: 'jsx',
  tsx: 'tsx',
  py: 'py',
  python: 'py',
  java: 'java',
  kotlin: 'kt',
  kt: 'kt',
  swift: 'swift',
  go: 'go',
  rust: 'rs',
  rs: 'rs',
  ruby: 'rb',
  rb: 'rb',
  php: 'php',
  c: 'c',
  cpp: 'cpp',
  'c++': 'cpp',
  cc: 'cc',
  cxx: 'cxx',
  cs: 'cs',
  csharp: 'cs',
  'c#': 'cs',
  html: 'html',
  css: 'css',
  scss: 'scss',
  sql: 'sql',
  sh: 'sh',
  bash: 'sh',
  zsh: 'sh',
  fish: 'fish',
  ps1: 'ps1',
  json: 'json',
  yaml: 'yaml',
  yml: 'yml',
  toml: 'toml',
  ini: 'ini',
  conf: 'conf',
  env: 'env',
  xml: 'xml',
  md: 'md',
  markdown: 'md',
  dockerfile: 'dockerfile',
  makefile: 'makefile',
};

const MIME_TYPE_TO_EXTENSION: Record<string, string> = {
  'text/plain': 'txt',
  'text/markdown': 'md',
  'text/html': 'html',
  'text/css': 'css',
  'text/javascript': 'js',
  'text/typescript': 'ts',
  'text/csv': 'csv',
  'text/tab-separated-values': 'tsv',
  'application/json': 'json',
  'application/ld+json': 'json',
  'application/xml': 'xml',
  'application/yaml': 'yaml',
  'text/yaml': 'yaml',
  'application/toml': 'toml',
  'image/svg+xml': 'svg',
};

const IMAGE_EXTENSION_TO_MIME: Record<string, string> = {
  svg: 'image/svg+xml',
  png: 'image/png',
  jpg: 'image/jpeg',
  jpeg: 'image/jpeg',
  gif: 'image/gif',
  webp: 'image/webp',
  avif: 'image/avif',
  bmp: 'image/bmp',
};

const IMAGE_MIME_TO_EXTENSION: Record<string, string> = {
  'image/svg+xml': 'svg',
  'image/png': 'png',
  'image/jpeg': 'jpg',
  'image/gif': 'gif',
  'image/webp': 'webp',
  'image/avif': 'avif',
  'image/bmp': 'bmp',
};

const CHART_LANG_TAGS = new Set(['chart', 'chart-json', 'chartjson', 'chart_data', 'chartdata', 'plot', 'graph']);
const CODE_EXCLUDED_LANG_TAGS = new Set(['svg', 'csv', 'tsv']);

const sanitizeFileName = (name: string): string => {
  const cleaned = name.trim().replace(/[\\/:*?"<>|]/g, '-').replace(/\s+/g, '_');
  return cleaned || 'attachment.txt';
};

const toUtf8DataUrl = (mimeType: string, text: string): string => {
  try {
    const bytes = new TextEncoder().encode(text);
    let binary = '';
    for (let i = 0; i < bytes.length; i++) {
      binary += String.fromCharCode(bytes[i]);
    }
    return `data:${mimeType};charset=utf-8;base64,${btoa(binary)}`;
  } catch {
    return `data:${mimeType};charset=utf-8,${encodeURIComponent(text)}`;
  }
};

const csvEscape = (value: unknown): string => {
  const text = String(value ?? '');
  if (!/[",\n\r]/.test(text)) return text;
  return `"${text.replace(/"/g, '""')}"`;
};

const normalizeAttachmentText = (value: string): string => {
  if (!value) return '';
  const normalized = value.replace(/\r\n/g, '\n').replace(/\r/g, '\n').trim();
  if (!normalized) return '';
  if (normalized.length <= MAX_ATTACHMENT_TEXT_CHARS) return normalized;
  return `${normalized.slice(0, MAX_ATTACHMENT_TEXT_CHARS)}\n...`;
};

const tryParseJson = (value: string): any | null => {
  const trimmed = value.trim();
  if (!trimmed) return null;
  try {
    return JSON.parse(trimmed);
  } catch {}

  const firstBrace = trimmed.indexOf('{');
  const lastBrace = trimmed.lastIndexOf('}');
  if (firstBrace >= 0 && lastBrace > firstBrace) {
    try {
      return JSON.parse(trimmed.slice(firstBrace, lastBrace + 1));
    } catch {}
  }
  return null;
};

const unwrapLoggedResponseText = (value: string): string => {
  const trimmed = (value || '').trim();
  if (!trimmed.startsWith('{') || !trimmed.endsWith('}')) return value;
  if (!/"text"\s*:/i.test(trimmed)) return value;
  if (!/"usage"\s*:|\"retry\"\s*:/i.test(trimmed)) return value;

  try {
    const parsed = JSON.parse(trimmed);
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return value;
    const text = (parsed as { text?: unknown }).text;
    if (typeof text !== 'string') return value;
    return text;
  } catch {
    return value;
  }
};

const getExtensionFromFileName = (fileName?: string): string => {
  const normalized = (fileName || '').trim().toLowerCase();
  const dot = normalized.lastIndexOf('.');
  if (dot < 0 || dot >= normalized.length - 1) return '';
  return normalized.slice(dot + 1).replace(/[^a-z0-9_+-]/g, '');
};

const normalizeImageMimeType = (mimeType?: string | null): string | null => {
  const normalized = (mimeType || '').trim().toLowerCase();
  if (!normalized.startsWith('image/')) return null;
  const canonical = normalized === 'image/jpg' ? 'image/jpeg' : normalized;
  return IMAGE_MIME_TO_EXTENSION[canonical] ? canonical : null;
};

const inferImageMimeType = (langToken: string, mimeToken: string, fileNameHint?: string): string | null => {
  const fromMime = normalizeImageMimeType(mimeToken);
  if (fromMime) return fromMime;

  const normalizedLangToken = langToken.replace(/^\.+/, '');
  const fromLang = IMAGE_EXTENSION_TO_MIME[normalizedLangToken];
  if (fromLang) return fromLang;

  const fromFileName = IMAGE_EXTENSION_TO_MIME[getExtensionFromFileName(fileNameHint)];
  if (fromFileName) return fromFileName;

  return null;
};

const extractInlineImageDataUrl = (value: string): { dataUrl: string; mimeType: string } | null => {
  const match = INLINE_IMAGE_DATA_URL_REGEX.exec(value || '');
  if (!match) return null;

  const raw = (match[0] || '').trim();
  if (!raw) return null;

  const cleaned = raw.replace(/[)\]}>,.;]+$/g, '').replace(/\s+/g, '');
  const mimeMatch = /^data:(image\/[a-z0-9.+-]+)(;[^,]*)?,/i.exec(cleaned);
  if (!mimeMatch) return null;

  const mimeType = normalizeImageMimeType(mimeMatch[1]);
  if (!mimeType) return null;

  return { dataUrl: cleaned, mimeType };
};

const normalizeBase64 = (value: string): string | null => {
  const raw = (value || '').trim();
  if (!raw) return null;

  const withoutPrefix = raw
    .replace(/^data:[^,]*,/i, '')
    .replace(/^base64,/i, '')
    .replace(/\s+/g, '');
  if (!withoutPrefix || withoutPrefix.length < 32) return null;
  if (!/^[a-z0-9+/]+={0,2}$/i.test(withoutPrefix)) return null;

  const padded = withoutPrefix.padEnd(withoutPrefix.length + ((4 - (withoutPrefix.length % 4)) % 4), '=');
  try {
    atob(padded);
  } catch {
    return null;
  }
  return padded;
};

const resolveImageFileName = (mimeType: string, fileNameHint?: string): string => {
  const extFromName = getExtensionFromFileName(fileNameHint);
  if (extFromName && IMAGE_EXTENSION_TO_MIME[extFromName]) {
    return sanitizeFileName(fileNameHint || `generated.${extFromName}`);
  }
  const ext = IMAGE_MIME_TO_EXTENSION[mimeType] || 'img';
  return sanitizeFileName(fileNameHint || `generated.${ext}`);
};

const inferCodeExtension = (lang: string, fileNameHint?: string): string => {
  const fromFileName = getExtensionFromFileName(fileNameHint);
  if (fromFileName) return fromFileName;

  const normalizedLang = (lang || '').trim().toLowerCase();
  if (!normalizedLang) return 'txt';

  const mimeToken = normalizedLang.split(';')[0].trim();
  const fromMime = MIME_TYPE_TO_EXTENSION[mimeToken];
  if (fromMime) return fromMime;

  const fromKnownLanguage = CODE_LANGUAGE_TO_EXTENSION[normalizedLang];
  if (fromKnownLanguage) return fromKnownLanguage;

  const fallback = normalizedLang
    .replace(/^\.+/, '')
    .replace(/[^a-z0-9_+-]/g, '')
    .slice(0, 16);
  return fallback || 'txt';
};

const parseFenceInfo = (rawInfo: string): { lang: string; fileNameHint?: string } => {
  const info = (rawInfo || '').trim();
  if (!info) return { lang: '' };

  const filenameMatch = info.match(/(?:^|\s)(?:file(?:name)?|name)\s*[:=]\s*(?:"([^"]+)"|'([^']+)'|([^\s]+))/i);
  const explicitFileName = filenameMatch?.[1] || filenameMatch?.[2] || filenameMatch?.[3];

  const tokens = info.split(/\s+/).filter(Boolean);
  const firstToken = (tokens[0] || '').replace(/^["'`]+|["'`,;:]+$/g, '');
  const lang = (firstToken.includes('=') || firstToken.includes(':')) ? '' : firstToken.toLowerCase();
  const tokenFileName = tokens
    .map((token) => token.replace(/^["'`]+|["'`,;:]+$/g, ''))
    .find((token) => token.includes('.') && !token.includes('='));

  return {
    lang,
    fileNameHint: explicitFileName || tokenFileName,
  };
};

const getChartDataRoot = (input: any): any => {
  if (!input || typeof input !== 'object' || Array.isArray(input)) return input;
  const nestedData = (input as Record<string, unknown>).data;
  if (!nestedData || typeof nestedData !== 'object' || Array.isArray(nestedData)) return input;

  const nested = nestedData as Record<string, unknown>;
  const hasChartShape =
    Array.isArray(nested.labels) ||
    Array.isArray(nested.datasets) ||
    Array.isArray(nested.series) ||
    Array.isArray(nested.values) ||
    Array.isArray(nested.data);
  return hasChartShape ? nested : input;
};

const chartJsonToCsv = (input: any): string | null => {
  const dataRoot = getChartDataRoot(input);
  if (!dataRoot || typeof dataRoot !== 'object') return null;
  const labels = Array.isArray(dataRoot.labels) ? dataRoot.labels.map((x: unknown) => String(x ?? '')) : [];
  if (!labels.length) return null;

  const writeMultiSeries = (series: Array<{ name: string; data: number[] }>): string | null => {
    if (!series.length) return null;
    const maxLen = Math.min(
      200,
      Math.max(labels.length, ...series.map((s) => s.data.length))
    );
    if (maxLen < 2) return null;

    const header = ['label', ...series.map((s) => s.name || 'series')];
    const rows: string[] = [header.map(csvEscape).join(',')];
    for (let i = 0; i < maxLen; i++) {
      const line = [labels[i] ?? String(i + 1)];
      for (const s of series) {
        line.push(s.data[i] ?? '');
      }
      rows.push(line.map(csvEscape).join(','));
    }
    return rows.join('\n');
  };

  const flatValues = Array.isArray(dataRoot.values) ? dataRoot.values : (Array.isArray(dataRoot.data) ? dataRoot.data : null);
  if (Array.isArray(flatValues)) {
    const numeric = flatValues.map((v: unknown) => Number(v)).filter((n: number) => Number.isFinite(n));
    if (numeric.length >= 2) {
      const rows = ['label,value'];
      for (let i = 0; i < Math.min(labels.length, numeric.length, 200); i++) {
        rows.push(`${csvEscape(labels[i])},${csvEscape(numeric[i])}`);
      }
      return rows.join('\n');
    }
  }

  const datasets = Array.isArray(dataRoot.datasets) ? dataRoot.datasets : (Array.isArray(dataRoot.series) ? dataRoot.series : null);
  if (Array.isArray(datasets)) {
    const normalized = datasets
      .map((series: any, idx: number) => {
        const raw = Array.isArray(series?.data) ? series.data : [];
        const numeric = raw.map((v: unknown) => Number(v));
        if (numeric.filter((n: number) => Number.isFinite(n)).length < 2) return null;
        return {
          name: String(series?.label || series?.name || `series_${idx + 1}`),
          data: numeric,
        };
      })
      .filter(Boolean) as Array<{ name: string; data: number[] }>;
    return writeMultiSeries(normalized);
  }

  return null;
};

const createSvgAttachment = (svgText: string, fileNameHint?: string): ParsedAssistantAttachment | null => {
  const normalized = normalizeAttachmentText(svgText);
  if (!normalized || !STANDALONE_SVG_REGEX.test(normalized)) return null;
  return {
    kind: 'svg',
    dataUrl: toUtf8DataUrl('image/svg+xml', normalized),
    mimeType: 'image/svg+xml',
    fileName: sanitizeFileName(fileNameHint || 'generated.svg'),
  };
};

const createImageAttachmentFromDataUrl = (
  dataUrl: string,
  mimeType: string,
  fileNameHint?: string
): ParsedAssistantAttachment | null => {
  const normalizedMime = normalizeImageMimeType(mimeType);
  if (!normalizedMime) return null;
  if (!dataUrl || typeof dataUrl !== 'string') return null;
  const headerMatch = /^data:(image\/[a-z0-9.+-]+)(;[^,]*)?,/i.exec(dataUrl);
  if (!headerMatch) return null;
  const mimeFromDataUrl = normalizeImageMimeType(headerMatch[1]);
  if (!mimeFromDataUrl || mimeFromDataUrl !== normalizedMime) return null;
  return {
    kind: 'image',
    dataUrl,
    mimeType: normalizedMime,
    fileName: resolveImageFileName(normalizedMime, fileNameHint),
  };
};

const createImageAttachmentFromBase64 = (
  base64Content: string,
  mimeType: string,
  fileNameHint?: string
): ParsedAssistantAttachment | null => {
  const normalizedMime = normalizeImageMimeType(mimeType);
  if (!normalizedMime) return null;
  const normalizedBase64 = normalizeBase64(base64Content);
  if (!normalizedBase64) return null;
  return {
    kind: 'image',
    dataUrl: `data:${normalizedMime};base64,${normalizedBase64}`,
    mimeType: normalizedMime,
    fileName: resolveImageFileName(normalizedMime, fileNameHint),
  };
};

const createTabularAttachment = (content: string, isTsv: boolean, fileNameHint?: string): ParsedAssistantAttachment | null => {
  const normalized = normalizeAttachmentText(content);
  if (!normalized) return null;
  return {
    kind: 'chart',
    dataUrl: toUtf8DataUrl(isTsv ? 'text/tab-separated-values' : 'text/csv', normalized),
    mimeType: isTsv ? 'text/tab-separated-values' : 'text/csv',
    fileName: sanitizeFileName(fileNameHint || (isTsv ? 'chart-data.tsv' : 'chart-data.csv')),
  };
};

const createChartJsonAttachment = (content: string, fileNameHint?: string): ParsedAssistantAttachment | null => {
  const parsed = tryParseJson(content);
  const csv = chartJsonToCsv(parsed);
  if (!csv) return null;
  return {
    kind: 'chart',
    dataUrl: toUtf8DataUrl('text/csv', csv),
    mimeType: 'text/csv',
    fileName: sanitizeFileName(fileNameHint || 'chart-data.csv'),
  };
};

const createCodeAttachment = (lang: string, content: string, fileNameHint?: string): ParsedAssistantAttachment | null => {
  const normalized = normalizeAttachmentText(content);
  if (!normalized) return null;
  if (!fileNameHint && normalized.length < 20 && !normalized.includes('\n')) return null;

  const ext = inferCodeExtension(lang, fileNameHint);
  return {
    kind: 'code',
    dataUrl: toUtf8DataUrl('text/plain', normalized),
    mimeType: 'text/plain',
    fileName: sanitizeFileName(fileNameHint || `generated.${ext}`),
  };
};

const parseFenceAttachment = (lang: string, content: string, fileNameHint?: string): ParsedAssistantAttachment | null => {
  const normalizedLang = (lang || '').trim().toLowerCase();
  const mimeToken = normalizedLang.split(';')[0].trim();
  const langToken = mimeToken.includes('/') ? mimeToken : normalizedLang;
  const normalizedContent = content || '';
  const inlineImageData = extractInlineImageDataUrl(normalizedContent);
  if (inlineImageData) {
    const inlineImageAttachment = createImageAttachmentFromDataUrl(
      inlineImageData.dataUrl,
      inlineImageData.mimeType,
      fileNameHint
    );
    if (inlineImageAttachment) return inlineImageAttachment;
  }

  if (langToken === 'svg' || mimeToken === 'image/svg+xml' || /<svg[\s>]/i.test(normalizedContent)) {
    const svgAttachment = createSvgAttachment(normalizedContent, fileNameHint);
    if (svgAttachment) return svgAttachment;
  }

  const inferredImageMimeType = inferImageMimeType(langToken, mimeToken, fileNameHint);
  if (inferredImageMimeType && inferredImageMimeType !== 'image/svg+xml') {
    const imageAttachment = createImageAttachmentFromBase64(normalizedContent, inferredImageMimeType, fileNameHint);
    if (imageAttachment) return imageAttachment;
  }

  if (langToken === 'csv' || mimeToken.includes('csv')) {
    return createTabularAttachment(normalizedContent, false, fileNameHint);
  }
  if (langToken === 'tsv' || mimeToken.includes('tab-separated-values')) {
    return createTabularAttachment(normalizedContent, true, fileNameHint);
  }
  if (CHART_LANG_TAGS.has(langToken)) {
    return createChartJsonAttachment(normalizedContent, fileNameHint);
  }
  if (langToken.includes('json') || mimeToken.includes('json')) {
    const parsedChart = createChartJsonAttachment(normalizedContent, fileNameHint);
    if (parsedChart) return parsedChart;
    return createCodeAttachment(langToken, normalizedContent, fileNameHint || 'generated.json');
  }
  if (CODE_EXCLUDED_LANG_TAGS.has(langToken)) return null;

  if (!langToken) {
    if (/<svg[\s>]/i.test(normalizedContent)) {
      const svgAttachment = createSvgAttachment(normalizedContent, fileNameHint);
      if (svgAttachment) return svgAttachment;
    }
    return createCodeAttachment('txt', normalizedContent, fileNameHint || 'generated.txt');
  }

  return createCodeAttachment(langToken, normalizedContent, fileNameHint);
};

const findNearestOpeningTagAt = (source: string, beforeIndex: number): { start: number; tagName: string } | null => {
  for (let idx = beforeIndex; idx >= 0; idx--) {
    if (source.charCodeAt(idx) !== 60) continue; // '<'
    const maybeTag = /^<([a-z][\w:-]*)\b[^>]*>/i.exec(source.slice(idx));
    if (!maybeTag) continue;
    const fullTag = maybeTag[0];
    if (/\/>$/.test(fullTag)) continue;
    return { start: idx, tagName: maybeTag[1].toLowerCase() };
  }
  return null;
};

const findMatchingElementEnd = (source: string, startIndex: number, tagName: string): number | null => {
  HTML_TAG_REGEX.lastIndex = startIndex;
  let depth = 0;
  let match: RegExpExecArray | null;
  while ((match = HTML_TAG_REGEX.exec(source)) !== null) {
    const name = (match[1] || '').toLowerCase();
    if (name !== tagName) continue;
    const token = match[0];
    const isClosing = token.startsWith('</');
    const isSelfClosing = /\/>$/.test(token);

    if (isClosing) {
      depth -= 1;
      if (depth <= 0) {
        return match.index + token.length;
      }
      continue;
    }

    if (!isSelfClosing) {
      depth += 1;
      continue;
    }

    if (depth === 0) {
      return match.index + token.length;
    }
  }
  return null;
};

const extractInlineMiniGameHtml = (source: string): { start: number; end: number; html: string } | null => {
  const marker = INLINE_MINI_GAME_MARKER_REGEX.exec(source);
  if (!marker || typeof marker.index !== 'number') return null;

  const openTag = findNearestOpeningTagAt(source, marker.index);
  if (openTag) {
    const end = findMatchingElementEnd(source, openTag.start, openTag.tagName);
    if (typeof end === 'number' && end > openTag.start) {
      const html = source.slice(openTag.start, end).trim();
      if (html) return { start: openTag.start, end, html };
    }
  }

  const fallbackStart = source.lastIndexOf('<', marker.index);
  if (fallbackStart >= 0) {
    const html = source.slice(fallbackStart).trim();
    if (html) return { start: fallbackStart, end: source.length, html };
  }
  return null;
};

const stripRangeFromText = (source: string, start: number, end: number): string => {
  const before = source.slice(0, start).trimEnd();
  const after = source.slice(end).trimStart();
  if (!before && !after) return '';
  if (!before) return after;
  if (!after) return before;
  return `${before}\n\n${after}`.replace(/\n{3,}/g, '\n\n').trim();
};

export const parseAssistantResponseForAttachment = (responseText?: string | null): ParsedAssistantResponse => {
  const rawSource = (responseText || '').toString();
  const source = unwrapLoggedResponseText(rawSource);
  if (!source.trim()) return { cleanedText: '' };

  const candidates: AttachmentCandidate[] = [];
  FENCE_REGEX.lastIndex = 0;
  let match: RegExpExecArray | null;
  while ((match = FENCE_REGEX.exec(source)) !== null) {
    const rawInfo = match[1] || '';
    const content = match[2] || '';
    const { lang, fileNameHint } = parseFenceInfo(rawInfo);
    const attachment = parseFenceAttachment(lang, content, fileNameHint);
    if (!attachment) continue;

    const priority = attachment.kind === 'svg'
      ? 100
      : attachment.kind === 'image'
        ? 98
      : attachment.kind === 'chart'
        ? 90
        : 70;
    candidates.push({
      start: match.index,
      end: match.index + match[0].length,
      priority,
      attachment,
    });
  }

  if (candidates.length === 0) {
    const normalizedSource = normalizeAttachmentText(source);
    const sourceStartsWithMarkup = normalizedSource.startsWith('<');
    if (sourceStartsWithMarkup && RAW_HTML_DOCUMENT_REGEX.test(normalizedSource)) {
      const htmlAttachment = createCodeAttachment('html', normalizedSource, 'generated.html');
      if (htmlAttachment) {
        return {
          cleanedText: '',
          attachment: htmlAttachment,
        };
      }
    }
  }

  if (candidates.length === 0) {
    const miniGameHtml = extractInlineMiniGameHtml(source);
    if (miniGameHtml) {
      const attachment = createCodeAttachment('html', miniGameHtml.html, 'generated.html');
      if (attachment) {
        candidates.push({
          start: miniGameHtml.start,
          end: miniGameHtml.end,
          priority: 92,
          attachment,
        });
      }
    }
  }

  if (candidates.length === 0) {
    const inlineImageData = extractInlineImageDataUrl(source);
    if (inlineImageData) {
      const attachment = createImageAttachmentFromDataUrl(
        inlineImageData.dataUrl,
        inlineImageData.mimeType
      );
      const rawMatch = INLINE_IMAGE_DATA_URL_REGEX.exec(source);
      if (attachment && rawMatch && typeof rawMatch.index === 'number') {
        candidates.push({
          start: rawMatch.index,
          end: rawMatch.index + rawMatch[0].length,
          priority: 94,
          attachment,
        });
      }
    }
  }

  if (candidates.length === 0) {
    const inlineSvg = source.match(INLINE_SVG_REGEX);
    if (inlineSvg && typeof inlineSvg.index === 'number') {
      const attachment = createSvgAttachment(inlineSvg[0], 'generated.svg');
      if (attachment) {
        candidates.push({
          start: inlineSvg.index,
          end: inlineSvg.index + inlineSvg[0].length,
          priority: 95,
          attachment,
        });
      }
    }
  }

  if (candidates.length === 0) {
    return { cleanedText: source.trim() };
  }

  candidates.sort((a, b) => (b.priority - a.priority) || (a.start - b.start));
  const selected = candidates[0];
  const cleaned = stripRangeFromText(source, selected.start, selected.end);

  return {
    cleanedText: cleaned,
    attachment: selected.attachment,
  };
};
