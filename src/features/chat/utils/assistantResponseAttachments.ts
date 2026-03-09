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

interface SourceVariant {
  label: string;
  source: string;
}

const MAX_ATTACHMENT_TEXT_CHARS = 180_000;

const FENCE_REGEX = /```([^\n`]*)\n?([\s\S]*?)```/g;
const INLINE_SVG_REGEX = /<svg[\s\S]*?<\/svg>/i;
const STANDALONE_SVG_REGEX = /^\s*(?:<\?xml[\s\S]*?\?>\s*)?<svg[\s\S]*<\/svg>\s*$/i;
const RAW_HTML_DOCUMENT_REGEX = /<!doctype\s+html|<html[\s>]|<body[\s>]|<head[\s>]|<\/html>/i;
const INLINE_IMAGE_DATA_URL_REGEX = /data:image\/[a-z0-9.+-]+(?:;[a-z0-9=._-]+)*,[a-z0-9+/%=._\-\s]+/i;
const INLINE_MINI_GAME_MARKER_REGEX = /data-maestro-mini-game|@maestro-mini-game/i;
const HTML_TAG_REGEX = /<\/?([a-z][\w:-]*)\b[^>]*>/ig;
// Strong signals that a bare (unfenced) HTML artifact is present
const STRONG_HTML_ARTIFACT_REGEX = /<script\b|<style\b|data-maestro-mini-game|@maestro-mini-game/i;
// Paragraph break (two or more newlines) optionally followed by whitespace, immediately before '<'
const PARA_BREAK_BEFORE_TAG_REGEX = /\n{2,}\s*(?=<)/;

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

const extractTopLevelTextField = (value: string): string | null => {
  const trimmed = (value || '').trim();
  if (!trimmed.startsWith('{') || !trimmed.endsWith('}')) return null;
  if (!/"text"\s*:/i.test(trimmed)) return null;

  try {
    const parsed = JSON.parse(trimmed);
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return null;
    const text = (parsed as { text?: unknown }).text;
    return typeof text === 'string' ? text : null;
  } catch {
    return null;
  }
};

const toJsonStringLiteral = (value: string): string => {
  let out = '"';
  for (let i = 0; i < value.length; i++) {
    const ch = value.charAt(i);
    if (ch === '"') {
      out += '\\"';
    } else if (ch === '\n') {
      out += '\\n';
    } else if (ch === '\r') {
      out += '\\r';
    } else if (ch === '\t') {
      out += '\\t';
    } else if (ch === '\u2028') {
      out += '\\u2028';
    } else if (ch === '\u2029') {
      out += '\\u2029';
    } else {
      out += ch;
    }
  }
  out += '"';
  return out;
};

const tryDecodeEscapedTextLayer = (value: string): string | null => {
  if (!value) return null;
  const escapedTokenCount = (value.match(/\\(?:n|r|t|u000a|u000d|u0009|")/gi) || []).length;
  if (escapedTokenCount < 2) return null;

  const wrapped = toJsonStringLiteral(value);
  try {
    const decoded = JSON.parse(wrapped);
    if (typeof decoded !== 'string') return null;
    if (decoded === value) return null;
    return decoded;
  } catch {
    return null;
  }
};

/**
 * When an AI response embeds an HTML artifact inline (no fenced code block),
 * wrap the HTML portion in an explicit ```html fence so the existing fence
 * parser handles it correctly.
 * @param source - raw response text to inspect
 * @returns normalised string with the HTML block fenced, or null if no change is needed
 */
const normalizeMissingFences = (source: string): string | null => {
  // Already has fenced code blocks — the standard parser will handle them.
  if (source.includes('```')) return null;

  // Only act when there is a strong signal of a scripted HTML artifact or a
  // full HTML document.
  if (!STRONG_HTML_ARTIFACT_REGEX.test(source) && !RAW_HTML_DOCUMENT_REGEX.test(source)) return null;

  // Find the first paragraph break immediately before a '<' tag opener.
  const match = PARA_BREAK_BEFORE_TAG_REGEX.exec(source);
  if (!match) return null;

  const htmlStart = match.index + match[0].length;
  const textPart = source.slice(0, match.index).trimEnd();
  const htmlPart = source.slice(htmlStart).trim();
  if (!htmlPart) return null;

  // Wrap the HTML portion in an explicit ```html fence.
  return textPart
    ? `${textPart}\n\n\`\`\`html\n${htmlPart}\n\`\`\``
    : `\`\`\`html\n${htmlPart}\n\`\`\``;
};

const collectSourceVariants = (rawSource: string): SourceVariant[] => {
  const variants: SourceVariant[] = [];
  const seen = new Set<string>();

  const pushVariant = (label: string, source: string | null | undefined) => {
    const normalized = (source || '').toString();
    if (!normalized.trim()) return;
    if (seen.has(normalized)) return;
    seen.add(normalized);
    variants.push({ label, source: normalized });
  };

  const primary = unwrapLoggedResponseText(rawSource);
  pushVariant('primary', primary);
  if (rawSource !== primary) {
    pushVariant('raw', rawSource);
  }

  const textFromRawJson = extractTopLevelTextField(rawSource);
  if (textFromRawJson && textFromRawJson !== primary) {
    pushVariant('json-text', textFromRawJson);
  }

  const seeds = variants.slice(0, 3);
  for (const seed of seeds) {
    let current = seed.source;
    for (let depth = 1; depth <= 2; depth++) {
      const decoded = tryDecodeEscapedTextLayer(current);
      if (!decoded || decoded === current) break;
      pushVariant(`${seed.label}-decoded-${depth}`, decoded);
      current = decoded;
    }
  }

  // For each variant that may contain an unfenced HTML artifact, add a
  // normalised copy where the HTML block is wrapped in ```html fences.
  // The score-based selection in parseAssistantResponseForAttachment then
  // picks whichever variant produces the best result.
  const variantsSnapshot = variants.slice();
  for (const v of variantsSnapshot) {
    const fenced = normalizeMissingFences(v.source);
    if (fenced) pushVariant(`${v.label}-fenced`, fenced);
  }

  return variants;
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

  const writeMultiSeries = (series: Array<{ name: string; data: unknown[] }>): string | null => {
    if (!series.length) return null;
    const maxLen = Math.min(
      200,
      Math.max(labels.length, ...series.map((s) => s.data.length))
    );
    if (maxLen < 1) return null;

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
    if (flatValues.length >= 1) {
      const maxLen = Math.min(200, Math.max(labels.length, flatValues.length));
      const rows = ['label,value'];
      for (let i = 0; i < maxLen; i++) {
        rows.push(`${csvEscape(labels[i] ?? String(i + 1))},${csvEscape(flatValues[i] ?? '')}`);
      }
      return rows.join('\n');
    }
  }

  const datasets = Array.isArray(dataRoot.datasets) ? dataRoot.datasets : (Array.isArray(dataRoot.series) ? dataRoot.series : null);
  if (Array.isArray(datasets)) {
    const normalized = datasets
      .map((series: any, idx: number) => {
        const raw = Array.isArray(series?.data) ? series.data : [];
        if (raw.length < 1) return null;
        return {
          name: String(series?.label || series?.name || `series_${idx + 1}`),
          data: raw,
        };
      })
      .filter(Boolean) as Array<{ name: string; data: unknown[] }>;
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
  const trimmedContent = normalizedContent.trim();
  const startsWithMarkup = trimmedContent.startsWith('<');
  const isExplicitHtmlLang =
    langToken === 'html' ||
    langToken === 'htm' ||
    langToken === 'xhtml' ||
    mimeToken === 'text/html' ||
    mimeToken === 'application/xhtml+xml';
  const isLikelyHtmlDocument = startsWithMarkup && RAW_HTML_DOCUMENT_REGEX.test(trimmedContent);
  const isLikelyHtmlMiniGameSnippet = startsWithMarkup && INLINE_MINI_GAME_MARKER_REGEX.test(normalizedContent);

  // Important: HTML fences can include inline `data:image/...` snippets (e.g. CSS data URIs).
  // We must keep the full HTML as code instead of misclassifying/truncating it as an image.
  if (isExplicitHtmlLang || isLikelyHtmlDocument || isLikelyHtmlMiniGameSnippet) {
    return createCodeAttachment('html', normalizedContent, fileNameHint || 'generated.html');
  }

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
    const parsedChart = createChartJsonAttachment(normalizedContent, fileNameHint);
    if (parsedChart) return parsedChart;
    return createCodeAttachment('json', normalizedContent, fileNameHint || 'chart-data.json');
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

const consumeEscapedWhitespaceToken = (source: string, fromIndex: number): number => {
  let slashCursor = fromIndex;
  while (source.charCodeAt(slashCursor) === 92) { // '\'
    slashCursor += 1;
  }
  if (slashCursor === fromIndex) return fromIndex;

  const shortEscape = (source.charAt(slashCursor) || '').toLowerCase();
  if (shortEscape === 'n' || shortEscape === 'r' || shortEscape === 't') {
    return slashCursor + 1;
  }

  const unicodeEscape = source.slice(slashCursor, slashCursor + 5).toLowerCase();
  if (unicodeEscape === 'u000a' || unicodeEscape === 'u000d' || unicodeEscape === 'u0009') {
    return slashCursor + 5;
  }

  return fromIndex;
};

const skipIgnorableHtmlBetweenTags = (source: string, fromIndex: number): number => {
  let cursor = fromIndex;
  while (cursor < source.length) {
    const wsMatch = /^[\t\n\r \f]+/.exec(source.slice(cursor));
    if (wsMatch) {
      cursor += wsMatch[0].length;
      continue;
    }

    // Also handle JSON-escaped separators when snippets are pasted from logs.
    const escapedWhitespaceEnd = consumeEscapedWhitespaceToken(source, cursor);
    if (escapedWhitespaceEnd > cursor) {
      cursor = escapedWhitespaceEnd;
      continue;
    }

    if (source.startsWith('<!--', cursor)) {
      const commentEnd = source.indexOf('-->', cursor + 4);
      if (commentEnd < 0) return source.length;
      cursor = commentEnd + 3;
      continue;
    }

    break;
  }
  return cursor;
};

const consumeTrailingScriptBlocks = (source: string, fromIndex: number): number => {
  let cursor = fromIndex;
  const lower = source.toLowerCase();

  while (cursor < source.length) {
    const next = skipIgnorableHtmlBetweenTags(source, cursor);
    const openMatch = /^<script\b[^>]*>/i.exec(source.slice(next));
    if (!openMatch) break;

    const openTag = openMatch[0];
    const contentStart = next + openTag.length;
    if (/\/>$/.test(openTag)) {
      cursor = contentStart;
      continue;
    }

    const closeStart = lower.indexOf('</script', contentStart);
    if (closeStart < 0) {
      cursor = source.length;
      break;
    }
    const closeEnd = source.indexOf('>', closeStart);
    if (closeEnd < 0) {
      cursor = source.length;
      break;
    }

    cursor = closeEnd + 1;
  }

  return cursor;
};

const extractInlineMiniGameHtml = (source: string): { start: number; end: number; html: string } | null => {
  const marker = INLINE_MINI_GAME_MARKER_REGEX.exec(source);
  if (!marker || typeof marker.index !== 'number') return null;

  const openTag = findNearestOpeningTagAt(source, marker.index);
  if (openTag) {
    const end = findMatchingElementEnd(source, openTag.start, openTag.tagName);
    if (typeof end === 'number' && end > openTag.start) {
      const expandedEnd = consumeTrailingScriptBlocks(source, end);
      const html = source.slice(openTag.start, expandedEnd).trim();
      if (html) return { start: openTag.start, end: expandedEnd, html };
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

const parseAttachmentFromSingleSource = (source: string): ParsedAssistantResponse => {
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

const decodeAttachmentTextDataUrl = (dataUrl: string): string => {
  if (!dataUrl || typeof dataUrl !== 'string') return '';
  const comma = dataUrl.indexOf(',');
  if (comma < 0) return '';

  const header = dataUrl.slice(0, comma).toLowerCase();
  const payload = dataUrl.slice(comma + 1);
  if (!payload) return '';

  try {
    if (header.includes(';base64')) {
      const binary = atob(payload);
      const bytes = new Uint8Array(binary.length);
      for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
      }
      return new TextDecoder().decode(bytes);
    }
    return decodeURIComponent(payload);
  } catch {
    return '';
  }
};

const scoreAttachmentParse = (parsed: ParsedAssistantResponse): number => {
  if (!parsed.attachment) return Number.NEGATIVE_INFINITY;

  const attachment = parsed.attachment;
  const cleaned = (parsed.cleanedText || '').trim();

  let score = 10_000;
  if (attachment.kind === 'svg') score += 700;
  else if (attachment.kind === 'image') score += 650;
  else if (attachment.kind === 'chart') score += 550;
  else score += 450;

  score -= Math.min(cleaned.length, 1_200);

  if (attachment.kind === 'code') {
    const attachmentText = decodeAttachmentTextDataUrl(attachment.dataUrl);
    const hasMiniGameMarker = INLINE_MINI_GAME_MARKER_REGEX.test(attachmentText);
    const hasScriptInAttachment = /<script[\s>]/i.test(attachmentText) && /<\/script>/i.test(attachmentText);
    const leakedScriptInCleaned = /<script[\s>]/i.test(cleaned);
    const leakedMiniGameInCleaned = INLINE_MINI_GAME_MARKER_REGEX.test(cleaned);

    if (hasMiniGameMarker) score += 900;
    if (hasScriptInAttachment) score += 250;
    if (hasMiniGameMarker && leakedScriptInCleaned) score -= 1_400;
    if (hasMiniGameMarker && leakedMiniGameInCleaned) score -= 1_100;
  }

  return score;
};

export const parseAssistantResponseForAttachment = (responseText?: string | null): ParsedAssistantResponse => {
  const rawSource = (responseText || '').toString();
  const variants = collectSourceVariants(rawSource);
  if (variants.length === 0) return { cleanedText: '' };

  const parsedByVariant = variants.map((variant, variantIndex) => ({
    variantIndex,
    variant,
    parsed: parseAttachmentFromSingleSource(variant.source),
  }));

  const attachmentParses = parsedByVariant.filter((entry) => Boolean(entry.parsed.attachment));
  if (attachmentParses.length === 0) {
    return parsedByVariant[0].parsed;
  }

  attachmentParses.sort((a, b) => {
    const scoreDiff = scoreAttachmentParse(b.parsed) - scoreAttachmentParse(a.parsed);
    if (scoreDiff !== 0) return scoreDiff;
    return a.variantIndex - b.variantIndex;
  });

  return attachmentParses[0].parsed;
};
