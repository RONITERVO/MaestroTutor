// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

const MARKDOWN_EXTENSIONS = new Set(['md', 'markdown', 'mdx']);
const JSON_EXTENSIONS = new Set(['json', 'json5', 'jsonc']);
const CSV_EXTENSIONS = new Set(['csv']);
const MICROSOFT_WORD_EXTENSIONS = new Set(['doc', 'docx', 'docm', 'dot', 'dotx', 'dotm', 'rtf']);
const MICROSOFT_EXCEL_EXTENSIONS = new Set(['xls', 'xlsx', 'xlsm', 'xlsb', 'xltx', 'xltm']);
const MICROSOFT_POWERPOINT_EXTENSIONS = new Set(['ppt', 'pptx', 'pptm', 'pps', 'ppsx', 'pot', 'potx']);
const OPEN_DOCUMENT_EXTENSIONS = new Set(['odt', 'ods', 'odp']);
const GOOGLE_WORKSPACE_SHORTCUT_EXTENSIONS = new Set(['gdoc', 'gsheet', 'gslides']);

const MICROSOFT_WORD_MIME_TYPES = new Set([
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.template',
  'application/vnd.ms-word.document.macroenabled.12',
  'application/vnd.ms-word.template.macroenabled.12',
  'application/rtf',
  'text/rtf',
]);

const MICROSOFT_EXCEL_MIME_TYPES = new Set([
  'application/vnd.ms-excel',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.template',
  'application/vnd.ms-excel.sheet.macroenabled.12',
  'application/vnd.ms-excel.template.macroenabled.12',
  'application/vnd.ms-excel.sheet.binary.macroenabled.12',
]);

const MICROSOFT_POWERPOINT_MIME_TYPES = new Set([
  'application/vnd.ms-powerpoint',
  'application/vnd.openxmlformats-officedocument.presentationml.presentation',
  'application/vnd.openxmlformats-officedocument.presentationml.slideshow',
  'application/vnd.openxmlformats-officedocument.presentationml.template',
  'application/vnd.ms-powerpoint.presentation.macroenabled.12',
  'application/vnd.ms-powerpoint.slideshow.macroenabled.12',
  'application/vnd.ms-powerpoint.template.macroenabled.12',
]);

const OPEN_DOCUMENT_MIME_TYPES = new Set([
  'application/vnd.oasis.opendocument.text',
  'application/vnd.oasis.opendocument.spreadsheet',
  'application/vnd.oasis.opendocument.presentation',
]);

const GOOGLE_WORKSPACE_SHORTCUT_MIME_TYPES = new Set([
  'application/vnd.google-apps.document',
  'application/vnd.google-apps.spreadsheet',
  'application/vnd.google-apps.presentation',
]);

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

const inferOfficeMimeTypeFromFileName = (fileName?: string | null): string | null => {
  const ext = getExtension(fileName);
  if (!ext) return null;

  if (MICROSOFT_WORD_EXTENSIONS.has(ext)) {
    switch (ext) {
      case 'doc':
      case 'dot':
        return 'application/msword';
      case 'docx':
        return 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
      case 'dotx':
        return 'application/vnd.openxmlformats-officedocument.wordprocessingml.template';
      case 'docm':
        return 'application/vnd.ms-word.document.macroenabled.12';
      case 'dotm':
        return 'application/vnd.ms-word.template.macroenabled.12';
      case 'rtf':
        return 'application/rtf';
      default:
        return 'application/msword';
    }
  }

  if (MICROSOFT_EXCEL_EXTENSIONS.has(ext)) {
    switch (ext) {
      case 'xls':
        return 'application/vnd.ms-excel';
      case 'xlsx':
        return 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
      case 'xltx':
        return 'application/vnd.openxmlformats-officedocument.spreadsheetml.template';
      case 'xlsm':
        return 'application/vnd.ms-excel.sheet.macroenabled.12';
      case 'xltm':
        return 'application/vnd.ms-excel.template.macroenabled.12';
      case 'xlsb':
        return 'application/vnd.ms-excel.sheet.binary.macroenabled.12';
      default:
        return 'application/vnd.ms-excel';
    }
  }

  if (MICROSOFT_POWERPOINT_EXTENSIONS.has(ext)) {
    switch (ext) {
      case 'ppt':
      case 'pps':
      case 'pot':
        return 'application/vnd.ms-powerpoint';
      case 'pptx':
        return 'application/vnd.openxmlformats-officedocument.presentationml.presentation';
      case 'ppsx':
        return 'application/vnd.openxmlformats-officedocument.presentationml.slideshow';
      case 'potx':
        return 'application/vnd.openxmlformats-officedocument.presentationml.template';
      case 'pptm':
        return 'application/vnd.ms-powerpoint.presentation.macroenabled.12';
      default:
        return 'application/vnd.ms-powerpoint';
    }
  }

  if (OPEN_DOCUMENT_EXTENSIONS.has(ext)) {
    if (ext === 'odt') return 'application/vnd.oasis.opendocument.text';
    if (ext === 'ods') return 'application/vnd.oasis.opendocument.spreadsheet';
    if (ext === 'odp') return 'application/vnd.oasis.opendocument.presentation';
  }

  if (GOOGLE_WORKSPACE_SHORTCUT_EXTENSIONS.has(ext)) {
    // Desktop Google Workspace shortcuts are JSON manifests.
    return 'application/json';
  }

  return null;
};

export const isMicrosoftOfficeMimeType = (mimeType?: string | null): boolean => {
  const mime = (mimeType || '').trim().toLowerCase();
  if (!mime) return false;
  return (
    MICROSOFT_WORD_MIME_TYPES.has(mime) ||
    MICROSOFT_EXCEL_MIME_TYPES.has(mime) ||
    MICROSOFT_POWERPOINT_MIME_TYPES.has(mime) ||
    OPEN_DOCUMENT_MIME_TYPES.has(mime)
  );
};

export const isGoogleWorkspaceShortcutMimeType = (mimeType?: string | null): boolean => {
  const mime = (mimeType || '').trim().toLowerCase();
  return GOOGLE_WORKSPACE_SHORTCUT_MIME_TYPES.has(mime);
};

export const isGoogleWorkspaceShortcutFileName = (fileName?: string | null): boolean => {
  const ext = getExtension(fileName);
  return GOOGLE_WORKSPACE_SHORTCUT_EXTENSIONS.has(ext);
};

export const isOfficeAttachment = (mimeType?: string | null, fileName?: string | null): boolean => {
  if (isMicrosoftOfficeMimeType(mimeType)) return true;
  if (isGoogleWorkspaceShortcutMimeType(mimeType)) return true;

  const ext = getExtension(fileName);
  return (
    MICROSOFT_WORD_EXTENSIONS.has(ext) ||
    MICROSOFT_EXCEL_EXTENSIONS.has(ext) ||
    MICROSOFT_POWERPOINT_EXTENSIONS.has(ext) ||
    OPEN_DOCUMENT_EXTENSIONS.has(ext) ||
    GOOGLE_WORKSPACE_SHORTCUT_EXTENSIONS.has(ext)
  );
};

export const inferTextMimeTypeFromFileName = (fileName?: string | null): string | null => {
  const name = getLowerName(fileName);
  if (!name) return null;

  const ext = getExtension(name);
  if (ext === 'svg') return 'image/svg+xml';
  if (MARKDOWN_EXTENSIONS.has(ext)) return 'text/markdown';
  if (JSON_EXTENSIONS.has(ext)) return 'application/json';
  if (CSV_EXTENSIONS.has(ext)) return 'text/csv';
  if (CODE_TEXT_EXTENSIONS.has(ext)) return 'text/plain';
  if (SPECIAL_TEXT_BASENAMES.has(name)) return 'text/plain';

  return null;
};

export const normalizeAttachmentMimeType = (fileLike: { name?: string | null; type?: string | null }): string => {
  const inferredOffice = inferOfficeMimeTypeFromFileName(fileLike.name);
  if (inferredOffice) return inferredOffice;

  const inferred = inferTextMimeTypeFromFileName(fileLike.name);
  if (inferred) return inferred;

  const normalized = (fileLike.type || '').trim().toLowerCase();
  if (normalized) return normalized;

  return 'application/octet-stream';
};

export const isTextLikeMimeType = (mimeType?: string | null): boolean => {
  const mime = (mimeType || '').trim().toLowerCase();
  if (!mime) return false;
  if (isMicrosoftOfficeMimeType(mime)) return false;
  if (isGoogleWorkspaceShortcutMimeType(mime)) return false;
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
  if (isOfficeAttachment(mimeType, fileName)) return false;
  if (isTextLikeMimeType(mimeType)) return true;
  return !!inferTextMimeTypeFromFileName(fileName);
};

interface DecodePreviewOptions {
  maxChars?: number;
  maxBytes?: number;
}

export const decodeTextFromDataUrl = (dataUrl?: string | null): string | null => {
  if (!dataUrl || typeof dataUrl !== 'string' || !dataUrl.startsWith('data:')) return null;

  const commaIndex = dataUrl.indexOf(',');
  if (commaIndex <= 5) return null;

  const header = dataUrl.slice(5, commaIndex);
  const payload = dataUrl.slice(commaIndex + 1);
  if (!payload) return null;

  const charsetMatch = /charset=([^;]+)/i.exec(header);
  const requestedCharset = charsetMatch?.[1]?.trim() || 'utf-8';

  let decoder: TextDecoder;
  try {
    decoder = new TextDecoder(requestedCharset);
  } catch {
    decoder = new TextDecoder('utf-8');
  }

  const isBase64 = header.toLowerCase().includes(';base64');
  let bytes: Uint8Array;

  if (isBase64) {
    let binary = '';
    try {
      binary = atob(payload);
    } catch {
      return null;
    }
    bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  } else {
    try {
      const decodedPayload = decodeURIComponent(payload);
      bytes = new TextEncoder().encode(decodedPayload);
    } catch {
      return null;
    }
  }

  const text = decoder.decode(bytes).replace(/\u0000/g, '');
  if (!text.trim()) return null;
  return text.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
};

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

const GOOGLE_WORKSPACE_URL_REGEX = /https:\/\/docs\.google\.com\/(?:document|spreadsheets|presentation)\/[^\s"'\\]+/i;

const isGoogleWorkspaceUrl = (value?: string | null): boolean => {
  if (!value || typeof value !== 'string') return false;
  return GOOGLE_WORKSPACE_URL_REGEX.test(value.trim());
};

export const extractGoogleWorkspaceUrlFromDataUrl = (dataUrl?: string | null): string | null => {
  const decodedText = decodeTextFromDataUrl(dataUrl);
  if (!decodedText) return null;

  try {
    const parsed = JSON.parse(decodedText) as Record<string, unknown>;
    const candidates = [parsed.url, parsed.doc_url, parsed.alternateLink, parsed.alternate_link];
    for (const candidate of candidates) {
      if (typeof candidate === 'string' && isGoogleWorkspaceUrl(candidate)) {
        return candidate.trim();
      }
    }
  } catch {
    // Fallback to regex extraction below.
  }

  const match = decodedText.match(GOOGLE_WORKSPACE_URL_REGEX);
  return match?.[0] || null;
};
