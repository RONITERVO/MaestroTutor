// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type { ChatMessage } from '../../core/types';
import { decodeTextFromDataUrl, isTextLikeAttachment } from '../../core-sdk/chat/fileAttachments';
import { isRunnableMiniGameAttachment } from '../../features/chat/utils/miniGameAttachment';

export interface BookArtifact {
  id: string;
  title: string;
  src: string;
  mimeType: string;
  kind: 'html' | 'image' | 'video' | 'audio' | 'pdf' | 'text';
  sourceCode?: string;
}

export type BookLayout = 'conversation' | 'practice';
export const BOOK_LAYOUT_STORAGE_KEY = 'maestro.quest.bookLayout.v1';
export function readBookLayout(storage: Pick<Storage, 'getItem'>): BookLayout {
  try { return storage.getItem(BOOK_LAYOUT_STORAGE_KEY) === 'practice' ? 'practice' : 'conversation'; }
  catch { return 'conversation'; }
}

/** Derive from conversation data, never from which bubbles happen to be mounted. */
export function collectBookArtifacts(messages: readonly ChatMessage[]): BookArtifact[] {
  return messages.flatMap<BookArtifact>(message => {
    if (message.thinking || message.isGeneratingImage || message.isGeneratingToolAttachment || message.isLoadingArtifact || message.imageGenError) return [];
    const src = message.imageUrl || message.storageOptimizedImageUrl;
    const mimeType = (message.imageMimeType || message.storageOptimizedImageMimeType || '').toLowerCase();
    if (typeof src !== 'string' || !src) return [];
    const title = message.attachmentName || 'Maestro artifact';
    const base = { id: message.id, title, src, mimeType };
    if (mimeType.startsWith('image/')) return [{ ...base, kind: 'image' as const }];
    if (mimeType.startsWith('video/')) return [{ ...base, kind: 'video' as const }];
    if (mimeType.startsWith('audio/')) return [{ ...base, kind: 'audio' as const }];
    if (mimeType === 'application/pdf') return [{ ...base, kind: 'pdf' as const }];
    if (!isTextLikeAttachment(mimeType, message.attachmentName)) return [];
    const sourceCode = decodeTextFromDataUrl(src);
    if (!sourceCode) return [];
    const runnable = isRunnableMiniGameAttachment({ sourceCode, fileName: message.attachmentName, mimeType });
    return [{ ...base, kind: runnable ? 'html' as const : 'text' as const, sourceCode }];
  });
}

export function resolveBookArtifact(artifacts: readonly BookArtifact[], selectedId: string | null): BookArtifact | null {
  return artifacts.find(artifact => artifact.id === selectedId) ?? artifacts[artifacts.length - 1] ?? null;
}

export function resolveHistoryPage(ids: readonly string[], anchorId: string | null, size = 8) {
  if (!Number.isSafeInteger(size) || size < 1) throw new RangeError('Invalid history page size');
  const anchor = anchorId === null ? -1 : ids.indexOf(anchorId);
  const start = anchor < 0 ? Math.max(0, ids.length - size) : anchor;
  const end = Math.min(ids.length, start + size);
  return {
    ids: ids.slice(start, end), start, end, total: ids.length,
    earlierIds: ids.slice(Math.max(0, start - size), start),
    previousAnchor: start > 0 ? ids[Math.max(0, start - size)] : null,
    nextAnchor: end < ids.length ? ids[Math.min(end, Math.max(0, ids.length - size))] : null,
    isLatest: end === ids.length,
  };
}

export const QUEST_BRIDGE_VERSION = 1;
export type BookCommand =
  | { version: 1; type: 'history.step'; direction: -1 | 1 }
  | { version: 1; type: 'history.latest' | 'bookmark.jump' | 'artifact.latest' }
  | { version: 1; type: 'artifact.select'; messageId: string }
  | { version: 1; type: 'layout.set'; layout: BookLayout };

/** No eval, arbitrary URLs, file paths, account tokens or generic method dispatch. */
export function parseBookCommand(input: unknown): BookCommand | null {
  if (!input || typeof input !== 'object' || Array.isArray(input)) return null;
  const record = input as Record<string, unknown>;
  if (record.version !== QUEST_BRIDGE_VERSION) return null;
  if (record.type === 'layout.set' && (record.layout === 'conversation' || record.layout === 'practice')) return { version: 1, type: record.type, layout: record.layout };
  if (record.type === 'history.step' && (record.direction === -1 || record.direction === 1)) {
    return { version: 1, type: record.type, direction: record.direction };
  }
  if (record.type === 'history.latest' || record.type === 'bookmark.jump' || record.type === 'artifact.latest') {
    return { version: 1, type: record.type };
  }
  if (record.type === 'artifact.select' && typeof record.messageId === 'string' && record.messageId.length > 0 && record.messageId.length <= 256) {
    return { version: 1, type: record.type, messageId: record.messageId };
  }
  return null;
}
