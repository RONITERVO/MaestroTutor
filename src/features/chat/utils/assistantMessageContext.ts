// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import type { ChatMessage } from '../../../core/types';
import { parseAssistantResponseForAttachment } from './assistantResponseAttachments';
import { decodeTextFromDataUrl, isTextLikeAttachment } from './fileAttachments';

type AssistantMessageLike = Pick<
  ChatMessage,
  | 'translations'
  | 'rawAssistantResponse'
  | 'text'
  | 'llmRawResponse'
  | 'imageUrl'
  | 'imageMimeType'
  | 'storageOptimizedImageUrl'
  | 'storageOptimizedImageMimeType'
  | 'attachmentName'
  | 'maestroToolKind'
>;

export type CompactAssistantArtifact = {
  mimeType: string;
  fileName?: string;
  dataUrl?: string;
  source?: string;
};

export type CompactAssistantToolRequest = {
  tool: 'image' | 'audio-note' | 'music';
  prompt?: string;
  text?: string;
  durationSeconds?: number;
  source?: string;
};

const TOOL_BLOCK_REGEX = /```maestro-tool\b([\s\S]*?)```/ig;
const COMPACT_HISTORY_MARKER_REGEX = /compactHistory"\s*:\s*true|compact history/i;
const MAX_TOOL_TEXT_CHARS = 280;
const MAX_ARTIFACT_PREVIEW_CHARS = 900;
const MAX_HISTORY_TEXT_CHARS = 2200;

const FILE_EXTENSION_TO_FENCE: Record<string, string> = {
  css: 'css',
  csv: 'csv',
  htm: 'html',
  html: 'html',
  js: 'javascript',
  json: 'json',
  jsx: 'jsx',
  md: 'markdown',
  mjs: 'javascript',
  svg: 'svg',
  ts: 'typescript',
  tsx: 'tsx',
  tsv: 'tsv',
  txt: 'text',
  xml: 'xml',
};

const MIME_TYPE_TO_FENCE: Record<string, string> = {
  'application/javascript': 'javascript',
  'application/json': 'json',
  'application/xml': 'xml',
  'image/svg+xml': 'svg',
  'text/css': 'css',
  'text/csv': 'csv',
  'text/html': 'html',
  'text/javascript': 'javascript',
  'text/markdown': 'markdown',
  'text/plain': 'text',
  'text/tab-separated-values': 'tsv',
  'text/typescript': 'typescript',
  'text/xml': 'xml',
};

const normalizeMultiline = (value: string): string => (
  (value || '')
    .replace(/\r\n/g, '\n')
    .replace(/\r/g, '\n')
    .trim()
);

const truncateInline = (value: string, maxChars: number): string => {
  const normalized = (value || '').replace(/\s+/g, ' ').trim();
  if (!normalized) return '';
  return normalized.length > maxChars ? `${normalized.slice(0, maxChars - 3).trimEnd()}...` : normalized;
};

const truncateMultiline = (value: string, maxChars: number): string => {
  const normalized = normalizeMultiline(value);
  if (!normalized) return '';
  if (normalized.length <= maxChars) return normalized;
  return `${normalized.slice(0, maxChars).trimEnd()}\n... [truncated for compact history]`;
};

const getExtension = (fileName?: string | null): string => {
  const normalized = (fileName || '').trim().toLowerCase();
  const lastDot = normalized.lastIndexOf('.');
  if (lastDot === -1 || lastDot === normalized.length - 1) return '';
  return normalized.slice(lastDot + 1);
};

const getFenceLabelForArtifact = (mimeType?: string | null, fileName?: string | null): string => {
  const normalizedMime = (mimeType || '').trim().toLowerCase();
  if (normalizedMime && MIME_TYPE_TO_FENCE[normalizedMime]) {
    return MIME_TYPE_TO_FENCE[normalizedMime];
  }
  const ext = getExtension(fileName);
  if (ext && FILE_EXTENSION_TO_FENCE[ext]) {
    return FILE_EXTENSION_TO_FENCE[ext];
  }
  return 'text';
};

const getFenceToken = (body: string): string => {
  let tickCount = 3;
  while (body.includes('`'.repeat(tickCount))) {
    tickCount += 1;
  }
  return '`'.repeat(tickCount);
};

const tryParseJsonObject = (value: string): Record<string, unknown> | null => {
  const trimmed = (value || '').trim();
  if (!trimmed) return null;

  try {
    const parsed = JSON.parse(trimmed);
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
  } catch {}

  const firstBrace = trimmed.indexOf('{');
  const lastBrace = trimmed.lastIndexOf('}');
  if (firstBrace === -1 || lastBrace <= firstBrace) return null;

  try {
    const parsed = JSON.parse(trimmed.slice(firstBrace, lastBrace + 1));
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
  } catch {}

  return null;
};

const extractLastToolRequestFromRaw = (rawText?: string | null): CompactAssistantToolRequest | null => {
  const source = (rawText || '').trim();
  if (!source) return null;

  let lastPayload = '';
  let match: RegExpExecArray | null;
  TOOL_BLOCK_REGEX.lastIndex = 0;
  while ((match = TOOL_BLOCK_REGEX.exec(source)) !== null) {
    lastPayload = match[1] || '';
  }

  if (!lastPayload) return null;
  const parsed = tryParseJsonObject(lastPayload);
  const tool = typeof parsed?.tool === 'string' ? parsed.tool.trim().toLowerCase() : '';
  if (tool !== 'image' && tool !== 'audio-note' && tool !== 'music') return null;

  const prompt = typeof parsed?.prompt === 'string' ? parsed.prompt.trim() : '';
  const text = typeof parsed?.text === 'string' ? parsed.text.trim() : '';
  const durationSeconds = Number(parsed?.durationSeconds);

  return {
    tool,
    prompt: prompt || undefined,
    text: text || undefined,
    durationSeconds: Number.isFinite(durationSeconds) ? durationSeconds : undefined,
    source: typeof parsed?.source === 'string' ? parsed.source.trim() : undefined,
  };
};

const stripToolBlocksFromRaw = (rawText?: string | null): string => (
  (rawText || '').replace(TOOL_BLOCK_REGEX, '').trim()
);

const getAttachmentArtifactFromMessage = (message?: AssistantMessageLike | null): CompactAssistantArtifact | null => {
  if (!message) return null;

  const primaryDataUrl = typeof message.imageUrl === 'string' && message.imageUrl ? message.imageUrl : '';
  const primaryMimeType = typeof message.imageMimeType === 'string' && message.imageMimeType ? message.imageMimeType : '';
  const optimizedDataUrl = typeof message.storageOptimizedImageUrl === 'string' && message.storageOptimizedImageUrl
    ? message.storageOptimizedImageUrl
    : '';
  const optimizedMimeType = typeof message.storageOptimizedImageMimeType === 'string' && message.storageOptimizedImageMimeType
    ? message.storageOptimizedImageMimeType
    : '';

  const dataUrl = primaryDataUrl || optimizedDataUrl;
  const mimeType = (primaryMimeType || optimizedMimeType).trim().toLowerCase();
  if (!dataUrl || !mimeType) return null;

  const fileName = message.attachmentName || undefined;
  const isPreviewableTextArtifact =
    mimeType === 'image/svg+xml' ||
    isTextLikeAttachment(mimeType, fileName);

  if (!isPreviewableTextArtifact) return null;

  return {
    mimeType,
    fileName,
    dataUrl,
    source: 'message-attachment',
  };
};

const buildArtifactPreviewBody = (artifact: CompactAssistantArtifact): string => {
  const decoded = artifact.dataUrl ? decodeTextFromDataUrl(artifact.dataUrl) : null;
  const truncatedPreview = truncateMultiline(decoded || '', MAX_ARTIFACT_PREVIEW_CHARS);
  if (truncatedPreview) return truncatedPreview;

  const lines = [
    '[compact history artifact preview unavailable]',
    artifact.fileName ? `file: ${artifact.fileName}` : '',
    artifact.mimeType ? `mimeType: ${artifact.mimeType}` : '',
    artifact.source ? `source: ${artifact.source}` : '',
  ].filter(Boolean);
  return lines.join('\n');
};

const joinAssistantContextParts = (visibleText: string, extraParts: Array<string | null | undefined>): string => {
  const segments = [normalizeMultiline(visibleText), ...extraParts.map(part => normalizeMultiline(part || ''))]
    .filter(Boolean);
  if (!segments.length) return '';

  const combined = segments.join('\n\n').trim();
  if (combined.length <= MAX_HISTORY_TEXT_CHARS) return combined;
  return `${combined.slice(0, MAX_HISTORY_TEXT_CHARS).trimEnd()}\n... [truncated for compact history]`;
};

export const getVisibleAssistantMessageText = (
  message?: Pick<ChatMessage, 'translations' | 'rawAssistantResponse' | 'text'> | null
): string => {
  if (!message) return '';
  if (message.translations?.length) {
    return message.translations
      .map(pair => [pair?.target?.trim(), pair?.native?.trim()].filter(Boolean).join('\n'))
      .filter(Boolean)
      .join('\n');
  }
  return message.rawAssistantResponse || message.text || '';
};

export const serializeCompactAssistantArtifactBlock = (artifact: CompactAssistantArtifact): string => {
  const body = buildArtifactPreviewBody(artifact);
  const fenceLabel = getFenceLabelForArtifact(artifact.mimeType, artifact.fileName);
  const fence = getFenceToken(body);
  const header = [
    '[Earlier assistant turn artifact preview; compact history only]',
    artifact.fileName ? `file: ${artifact.fileName}` : '',
    artifact.mimeType ? `mimeType: ${artifact.mimeType}` : '',
    artifact.source ? `source: ${artifact.source}` : '',
  ].filter(Boolean).join('\n');

  return `${header}\n${fence}${fenceLabel}\n${body}\n${fence}`;
};

export const serializeCompactAssistantToolBlock = (toolRequest: CompactAssistantToolRequest): string => {
  const compactPayload: Record<string, unknown> = {
    tool: toolRequest.tool,
    compactHistory: true,
    alreadyUsed: true,
  };

  if (toolRequest.tool === 'audio-note') {
    const text = truncateInline(toolRequest.text || toolRequest.prompt || '', MAX_TOOL_TEXT_CHARS);
    if (text) compactPayload.text = text;
  } else {
    const prompt = truncateInline(toolRequest.prompt || toolRequest.text || '', MAX_TOOL_TEXT_CHARS);
    if (prompt) compactPayload.prompt = prompt;
  }

  if (toolRequest.tool === 'music' && typeof toolRequest.durationSeconds === 'number' && Number.isFinite(toolRequest.durationSeconds)) {
    compactPayload.durationSeconds = Math.max(8, Math.min(20, Math.round(toolRequest.durationSeconds)));
  }

  if (toolRequest.source) {
    compactPayload.source = toolRequest.source;
  }

  return [
    '[Earlier assistant turn used this tool; compact history only]',
    '```maestro-tool',
    JSON.stringify(compactPayload),
    '```',
  ].join('\n');
};

export const buildCompactAssistantRawText = (
  visibleText: string,
  options?: {
    artifact?: CompactAssistantArtifact | null;
    toolRequest?: CompactAssistantToolRequest | null;
  }
): string => {
  const artifactBlock = options?.artifact ? serializeCompactAssistantArtifactBlock(options.artifact) : '';
  const toolBlock = options?.toolRequest ? serializeCompactAssistantToolBlock(options.toolRequest) : '';
  return joinAssistantContextParts(visibleText, [artifactBlock, toolBlock]);
};

export const buildCompactAssistantHistoryText = (message?: AssistantMessageLike | null): string => {
  if (!message) return '';

  const rawText = normalizeMultiline(message.llmRawResponse || '');
  if (rawText && COMPACT_HISTORY_MARKER_REGEX.test(rawText)) {
    return joinAssistantContextParts(rawText, []);
  }

  const visibleText = getVisibleAssistantMessageText(message).trim();
  const toolRequestFromRaw = extractLastToolRequestFromRaw(rawText);
  const rawWithoutToolBlocks = stripToolBlocksFromRaw(rawText);
  const parsedAttachment = rawWithoutToolBlocks
    ? parseAssistantResponseForAttachment(rawWithoutToolBlocks)
    : { cleanedText: '', attachment: undefined };

  const effectiveVisibleText = visibleText || parsedAttachment.cleanedText || message.text || '';
  const artifactFromRaw = parsedAttachment.attachment
    ? {
        mimeType: parsedAttachment.attachment.mimeType,
        fileName: parsedAttachment.attachment.fileName,
        dataUrl: parsedAttachment.attachment.dataUrl,
        source: 'assistant-raw',
      }
    : null;
  const artifactFromMessage = artifactFromRaw ? null : getAttachmentArtifactFromMessage(message);
  const fallbackToolRequest = toolRequestFromRaw
    ? {
        ...toolRequestFromRaw,
        source: toolRequestFromRaw.source || 'assistant-raw',
      }
    : (message.maestroToolKind
        ? {
            tool: message.maestroToolKind,
            source: 'message-state',
          } as CompactAssistantToolRequest
        : null);

  return buildCompactAssistantRawText(effectiveVisibleText, {
    artifact: artifactFromRaw || artifactFromMessage,
    toolRequest: fallbackToolRequest,
  });
};
