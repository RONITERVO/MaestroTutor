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
  kind: 'svg' | 'chart' | 'code';
  dataUrl: string;
  mimeType: string;
  fileName: string;
}

export interface ParsedAssistantResponse {
  cleanedText: string;
  attachment?: ParsedAssistantAttachment;
}

const MAX_ATTACHMENT_TEXT_CHARS = 180_000;

const FENCE_REGEX = /```([^\n`]*)\n([\s\S]*?)```/g;
const INLINE_SVG_REGEX = /<svg[\s\S]*?<\/svg>/i;

const CODE_LANGUAGE_TO_EXTENSION: Record<string, string> = {
  js: 'js',
  javascript: 'js',
  mjs: 'mjs',
  cjs: 'cjs',
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
  xml: 'xml',
  md: 'md',
  markdown: 'md',
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

const parseFenceInfo = (rawInfo: string): { lang: string; fileNameHint?: string } => {
  const info = (rawInfo || '').trim();
  if (!info) return { lang: '' };

  const filenameMatch = info.match(/filename\s*=\s*([^\s]+)/i);
  const explicitFileName = filenameMatch?.[1];

  const tokens = info.split(/\s+/).filter(Boolean);
  const lang = (tokens[0] || '').toLowerCase();
  const tokenFileName = tokens.find((token) => token.includes('.') && !token.includes('='));

  return {
    lang,
    fileNameHint: explicitFileName || tokenFileName,
  };
};

const chartJsonToCsv = (input: any): string | null => {
  if (!input || typeof input !== 'object') return null;
  const labels = Array.isArray(input.labels) ? input.labels.map((x: unknown) => String(x ?? '')) : [];
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

  const flatValues = Array.isArray(input.values) ? input.values : (Array.isArray(input.data) ? input.data : null);
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

  const datasets = Array.isArray(input.datasets) ? input.datasets : (Array.isArray(input.series) ? input.series : null);
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
  if (!normalized || !/<svg[\s>]/i.test(normalized)) return null;
  return {
    kind: 'svg',
    dataUrl: toUtf8DataUrl('image/svg+xml', normalized),
    mimeType: 'image/svg+xml',
    fileName: sanitizeFileName(fileNameHint || 'generated.svg'),
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
  if (normalized.length < 20 && !normalized.includes('\n')) return null;

  const ext = CODE_LANGUAGE_TO_EXTENSION[lang] || 'txt';
  return {
    kind: 'code',
    dataUrl: toUtf8DataUrl('text/plain', normalized),
    mimeType: 'text/plain',
    fileName: sanitizeFileName(fileNameHint || `generated.${ext}`),
  };
};

const parseFenceAttachment = (lang: string, content: string, fileNameHint?: string): ParsedAssistantAttachment | null => {
  const normalizedLang = (lang || '').trim().toLowerCase();
  const normalizedContent = content || '';

  if (normalizedLang === 'svg' || /<svg[\s>]/i.test(normalizedContent)) {
    return createSvgAttachment(normalizedContent, fileNameHint);
  }
  if (normalizedLang === 'csv') {
    return createTabularAttachment(normalizedContent, false, fileNameHint);
  }
  if (normalizedLang === 'tsv') {
    return createTabularAttachment(normalizedContent, true, fileNameHint);
  }
  if (CHART_LANG_TAGS.has(normalizedLang)) {
    return createChartJsonAttachment(normalizedContent, fileNameHint);
  }
  if (normalizedLang.includes('json')) {
    const parsedChart = createChartJsonAttachment(normalizedContent, fileNameHint);
    if (parsedChart) return parsedChart;
    return createCodeAttachment(normalizedLang, normalizedContent, fileNameHint || 'generated.json');
  }
  if (CODE_EXCLUDED_LANG_TAGS.has(normalizedLang)) return null;

  if (!normalizedLang) {
    if (/<svg[\s>]/i.test(normalizedContent)) {
      return createSvgAttachment(normalizedContent, fileNameHint);
    }
    return null;
  }

  return createCodeAttachment(normalizedLang, normalizedContent, fileNameHint);
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
  const source = (responseText || '').toString();
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
