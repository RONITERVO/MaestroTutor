// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import type { ChatMessage } from '../../core/types';
import { decodeTextFromDataUrl, normalizeAttachmentMimeType } from './fileAttachments';
import { sanitizeSvgAnimationStructure } from './sanitizeSvgAnimationStructure';

export type SuggestionToolKind = NonNullable<ChatMessage['maestroToolKind']>;

export interface SuggestionCreatorArtifact {
  mimeType?: string;
  fileName?: string;
  encoding?: string;
  content?: string;
}

export interface NormalizedSuggestionArtifact {
  dataUrl: string;
  mimeType: string;
  fileName: string;
}

export interface SuggestionCreatorToolRequest {
  tool?: string;
  prompt?: string;
  text?: string;
  durationSeconds?: number;
  musicDurationSeconds?: number;
}

export type NormalizedSuggestionToolRequest =
  | { tool: 'image'; prompt: string }
  | { tool: 'audio-note'; text: string }
  | { tool: 'music'; prompt: string; durationSeconds?: number };

const DEFAULT_ARTIFACT_FILE_NAMES: Record<string, string> = {
  'image/svg+xml': 'artifact.svg',
  'text/html': 'artifact.html',
  'text/markdown': 'artifact.md',
  'text/csv': 'artifact.csv',
  'text/tab-separated-values': 'artifact.tsv',
  'application/json': 'artifact.json',
  'application/xml': 'artifact.xml',
  'text/xml': 'artifact.xml',
  'text/css': 'artifact.css',
  'text/javascript': 'artifact.js',
  'application/javascript': 'artifact.js',
  'text/typescript': 'artifact.ts',
  'text/plain': 'artifact.txt',
};

const inferMimeTypeFromDataUrl = (value?: string | null): string | null => {
  const match = typeof value === 'string' ? value.match(/^data:([^;,]+)(?:;[^,]*)?,/i) : null;
  return match?.[1]?.trim().toLowerCase() || null;
};

const sanitizeArtifactFileName = (value?: string | null, mimeType?: string | null): string => {
  const fallback = DEFAULT_ARTIFACT_FILE_NAMES[(mimeType || '').trim().toLowerCase()] || 'artifact.txt';
  return ((value || '').trim() || fallback).replace(/[\\/:*?"<>|]/g, '-').replace(/\s+/g, '_') || fallback;
};

const toUtf8Base64DataUrl = (mimeType: string, text: string): string => {
  const bytes = new TextEncoder().encode(text);
  let binary = '';
  for (let offset = 0; offset < bytes.length; offset += 0x8000) {
    binary += String.fromCharCode(...bytes.subarray(offset, offset + 0x8000));
  }
  return `data:${mimeType};charset=utf-8;base64,${globalThis.btoa(binary)}`;
};

const truncate = (value: string, max: number): string => value.trim().slice(0, max);

export const normalizeSuggestionCreatorArtifact = (artifact: unknown): NormalizedSuggestionArtifact | null => {
  if (!artifact || typeof artifact !== 'object' || Array.isArray(artifact)) return null;
  const candidate = artifact as SuggestionCreatorArtifact;
  const rawContent = typeof candidate.content === 'string' ? candidate.content : '';
  if (!rawContent.trim()) return null;
  const encoding = typeof candidate.encoding === 'string' ? candidate.encoding.trim().toLowerCase() : 'text';
  const dataUrlEncoded = encoding === 'data-url' || encoding === 'dataurl' || encoding === 'data_url';
  let mimeType = typeof candidate.mimeType === 'string' ? candidate.mimeType.trim().toLowerCase() : '';
  let dataUrl: string;

  if (dataUrlEncoded) {
    dataUrl = rawContent.trim();
    if (!/^data:[^,]+,/i.test(dataUrl)) return null;
    mimeType = mimeType || inferMimeTypeFromDataUrl(dataUrl) || '';
    if (mimeType === 'image/svg+xml') {
      const decoded = decodeTextFromDataUrl(dataUrl);
      if (decoded) dataUrl = toUtf8Base64DataUrl(mimeType, sanitizeSvgAnimationStructure(decoded));
    }
  } else {
    mimeType = mimeType || normalizeAttachmentMimeType({
      name: candidate.fileName || 'artifact.txt',
      type: 'text/plain',
    });
    let content = rawContent.replace(/\r\n/g, '\n').replace(/\r/g, '\n').trim();
    if (!content || (mimeType.startsWith('image/') && mimeType !== 'image/svg+xml')) return null;
    if (mimeType === 'image/svg+xml') content = sanitizeSvgAnimationStructure(content);
    dataUrl = toUtf8Base64DataUrl(mimeType || 'text/plain', content);
  }

  return {
    dataUrl,
    mimeType: mimeType || 'text/plain',
    fileName: sanitizeArtifactFileName(candidate.fileName, mimeType),
  };
};

export const normalizeSuggestionCreatorToolRequest = (
  toolRequest: unknown,
  fallbackText: string,
): NormalizedSuggestionToolRequest | null => {
  if (!toolRequest || typeof toolRequest !== 'object' || Array.isArray(toolRequest)) return null;
  const candidate = toolRequest as SuggestionCreatorToolRequest;
  const tool = typeof candidate.tool === 'string' ? candidate.tool.trim().toLowerCase() : '';
  if (tool !== 'image' && tool !== 'audio-note' && tool !== 'music') return null;
  const prompt = typeof candidate.prompt === 'string' ? candidate.prompt.trim() : '';
  const text = typeof candidate.text === 'string' ? candidate.text.trim() : '';
  if (tool === 'audio-note') return { tool, text: truncate(text || prompt || fallbackText, 500) };
  const normalizedPrompt = truncate(prompt || text || fallbackText, 280);
  if (tool === 'image') return { tool, prompt: normalizedPrompt };
  const rawDuration = Number(candidate.durationSeconds ?? candidate.musicDurationSeconds);
  return {
    tool,
    prompt: normalizedPrompt,
    ...(Number.isFinite(rawDuration)
      ? { durationSeconds: Math.max(8, Math.min(20, Math.round(rawDuration))) }
      : {}),
  };
};

export const executeSuggestionToolRequest = async <T>(
  request: NormalizedSuggestionToolRequest,
  handlers: {
    image: (request: Extract<NormalizedSuggestionToolRequest, { tool: 'image' }>) => Promise<T>;
    audioNote: (request: Extract<NormalizedSuggestionToolRequest, { tool: 'audio-note' }>) => Promise<T>;
    music: (request: Extract<NormalizedSuggestionToolRequest, { tool: 'music' }>) => Promise<T>;
  },
): Promise<T> => {
  if (request.tool === 'image') return handlers.image(request);
  if (request.tool === 'audio-note') return handlers.audioNote(request);
  return handlers.music(request);
};
