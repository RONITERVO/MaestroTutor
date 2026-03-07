// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

const MARKDOWN_EXTENSIONS = new Set(['md', 'markdown', 'mdx']);
const JSON_EXTENSIONS = new Set(['json', 'json5', 'jsonc']);
const CSV_EXTENSIONS = new Set(['csv']);

const CODE_TEXT_EXTENSIONS = new Set([
  'js', 'jsx', 'ts', 'tsx', 'mjs', 'cjs',
  'py', 'java', 'kt', 'kts', 'swift',
  'go', 'rs', 'rb', 'php',
  'c', 'cc', 'cpp', 'cxx', 'h', 'hh', 'hpp', 'hxx',
  'cs',
  'html', 'htm', 'css', 'scss', 'sass', 'less',
  'vue', 'svelte',
  'xml', 'xsd', 'xsl', 'svg',
  'yml', 'yaml', 'toml', 'ini', 'cfg', 'conf', 'properties',
  'env', 'sql', 'graphql', 'gql',
  'sh', 'bash', 'zsh', 'fish', 'ps1', 'bat', 'cmd',
  'dockerfile', 'makefile',
  'txt', 'log',
]);

const SPECIAL_TEXT_BASENAMES = new Set([
  'readme',
  'license',
  'copying',
  'changelog',
  'dockerfile',
  'makefile',
  'cmakelists.txt',
  '.gitignore',
  '.gitattributes',
  '.npmrc',
  '.editorconfig',
  '.env',
  '.env.example',
]);

const getLowerName = (fileName?: string | null): string => (fileName || '').trim().toLowerCase();

const getExtension = (fileName?: string | null): string => {
  const name = getLowerName(fileName);
  const lastDot = name.lastIndexOf('.');
  if (lastDot < 0 || lastDot === name.length - 1) return '';
  return name.slice(lastDot + 1);
};

export const inferTextMimeTypeFromFileName = (fileName?: string | null): string | null => {
  const name = getLowerName(fileName);
  if (!name) return null;

  const ext = getExtension(name);
  if (MARKDOWN_EXTENSIONS.has(ext)) return 'text/markdown';
  if (JSON_EXTENSIONS.has(ext)) return 'application/json';
  if (CSV_EXTENSIONS.has(ext)) return 'text/csv';
  if (CODE_TEXT_EXTENSIONS.has(ext)) return 'text/plain';
  if (SPECIAL_TEXT_BASENAMES.has(name)) return 'text/plain';

  return null;
};

export const normalizeAttachmentMimeType = (fileLike: { name?: string | null; type?: string | null }): string => {
  const inferred = inferTextMimeTypeFromFileName(fileLike.name);
  if (inferred) return inferred;

  const normalized = (fileLike.type || '').trim().toLowerCase();
  if (normalized) return normalized;

  return 'application/octet-stream';
};

export const isTextLikeMimeType = (mimeType?: string | null): boolean => {
  const mime = (mimeType || '').trim().toLowerCase();
  if (!mime) return false;
  if (mime.startsWith('text/')) return true;

  return (
    mime.includes('json') ||
    mime.includes('xml') ||
    mime.includes('yaml') ||
    mime.includes('javascript') ||
    mime.includes('typescript') ||
    mime.includes('x-python') ||
    mime.includes('x-shellscript') ||
    mime.includes('x-sh') ||
    mime.includes('toml') ||
    mime.includes('ini') ||
    mime.includes('csv') ||
    mime.includes('markdown')
  );
};

export const isTextLikeAttachment = (mimeType?: string | null, fileName?: string | null): boolean => {
  if (isTextLikeMimeType(mimeType)) return true;
  return !!inferTextMimeTypeFromFileName(fileName);
};

interface DecodePreviewOptions {
  maxChars?: number;
  maxBytes?: number;
}

export const decodeTextPreviewFromDataUrl = (
  dataUrl?: string | null,
  options?: DecodePreviewOptions
): string | null => {
  if (!dataUrl || typeof dataUrl !== 'string' || !dataUrl.startsWith('data:')) return null;

  const commaIndex = dataUrl.indexOf(',');
  if (commaIndex <= 5) return null;

  const header = dataUrl.slice(5, commaIndex);
  const lowerHeader = header.toLowerCase();
  if (!lowerHeader.includes(';base64')) return null;

  const payload = dataUrl.slice(commaIndex + 1);
  if (!payload) return null;

  const maxBytes = Math.max(1024, options?.maxBytes ?? 96 * 1024);
  const maxChars = Math.max(256, options?.maxChars ?? 3000);
  const maxBase64Chars = Math.ceil(maxBytes / 3) * 4;

  let previewPayload = payload.slice(0, maxBase64Chars);
  const remainder = previewPayload.length % 4;
  if (remainder !== 0) {
    previewPayload = previewPayload.padEnd(previewPayload.length + (4 - remainder), '=');
  }

  let binary = '';
  try {
    binary = atob(previewPayload);
  } catch {
    return null;
  }

  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);

  const charsetMatch = /charset=([^;]+)/i.exec(header);
  const requestedCharset = charsetMatch?.[1]?.trim() || 'utf-8';

  let decoder: TextDecoder;
  try {
    decoder = new TextDecoder(requestedCharset);
  } catch {
    decoder = new TextDecoder('utf-8');
  }

  let text = decoder.decode(bytes).replace(/\u0000/g, '');
  if (!text.trim()) return null;

  text = text.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
  const clipped = text.slice(0, maxChars);
  const truncated = text.length > maxChars || payload.length > maxBase64Chars;

  return truncated ? `${clipped}\n...` : clipped;
};

