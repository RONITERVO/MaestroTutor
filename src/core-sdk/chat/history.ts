// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { MAX_MEDIA_TO_KEEP } from '../../core/config/app';
import type { ChatMessage } from '../../core/types';
import { selectUploadedAttachmentParts } from './uploadedAttachmentVariants';

export interface DerivedHistoryItem {
  messageId?: string;
  role: 'user' | 'assistant';
  text?: string;
  rawAssistantResponse?: string;
  fileParts?: Array<{ fileUri: string; mimeType: string }>;
  chatSummary?: string;
  avatarFileUri?: string;
  avatarMimeType?: string;
}

export interface DeriveHistoryOptions {
  roles?: Array<'user' | 'assistant' | 'system'>;
  maxMessages?: number;
  maxMediaToKeep?: number;
  contextSummary?: string;
  globalProfileText?: string;
  placeholderLatestUserMessage?: string;
  avatarOverlayFileUri?: string;
  avatarOverlayMimeType?: string;
}

export interface ManagedFileStatus {
  deleted: boolean;
  active: boolean;
}

export type ManagedFileStatusResolver = (
  uris: string[],
) => Promise<Record<string, ManagedFileStatus>>;

export const sanitizeHistoryWithVerifiedMedia = async (
  history: DerivedHistoryItem[],
  resolveStatuses: ManagedFileStatusResolver,
  onStrip?: (input: { uri: string; historyIndex: number; kind: 'file' | 'avatar' }) => void,
): Promise<DerivedHistoryItem[]> => {
  const uris = Array.from(new Set(history.flatMap(item => {
    const fileUris = (item.fileParts || []).map(part => part.fileUri).filter(Boolean);
    return item.avatarFileUri ? [...fileUris, item.avatarFileUri] : fileUris;
  })));
  if (!uris.length) return history;

  const statuses = await resolveStatuses(uris);
  return history.map((item, historyIndex) => {
    const hadFileParts = Array.isArray(item.fileParts);
    const fileParts = hadFileParts
      ? item.fileParts?.filter(part => {
        const status = statuses[part.fileUri];
        const keep = !status?.deleted && status?.active === true;
        if (!keep) onStrip?.({ uri: part.fileUri, historyIndex, kind: 'file' });
        return keep;
      })
      : undefined;
    const hadAvatar = Boolean(item.avatarFileUri);
    const avatarStatus = item.avatarFileUri ? statuses[item.avatarFileUri] : undefined;
    const keepAvatar = !hadAvatar || (!avatarStatus?.deleted && avatarStatus?.active === true);
    if (hadAvatar && !keepAvatar) {
      onStrip?.({ uri: item.avatarFileUri as string, historyIndex, kind: 'avatar' });
    }
    if (!hadFileParts && keepAvatar) return item;

    const next = { ...item };
    if (fileParts?.length) next.fileParts = fileParts;
    else delete next.fileParts;
    if (!keepAvatar) {
      delete next.avatarFileUri;
      delete next.avatarMimeType;
    }
    return next;
  });
};

export const deriveHistoryForApi = (
  fullHistory: ChatMessage[],
  opts: DeriveHistoryOptions = {},
): DerivedHistoryItem[] => {
  const {
    roles = ['user', 'assistant'],
    maxMessages,
    maxMediaToKeep = MAX_MEDIA_TO_KEEP,
    contextSummary,
    globalProfileText,
    placeholderLatestUserMessage,
    avatarOverlayFileUri,
    avatarOverlayMimeType,
  } = opts;
  const roleSet = new Set(roles);
  let filtered = fullHistory.filter(message => (
    roleSet.has(message.role as 'user' | 'assistant' | 'system')
    && ((message.role !== 'user' && message.role !== 'assistant') || !message.thinking)
  ));
  if (typeof maxMessages === 'number' && maxMessages >= 0 && filtered.length > maxMessages) {
    filtered = maxMessages > 0 ? filtered.slice(-maxMessages) : [];
  }

  const history: DerivedHistoryItem[] = filtered.map(message => {
    const fileParts = selectUploadedAttachmentParts(message, 'chat');
    return {
      messageId: message.id,
      role: message.role === 'assistant' ? 'assistant' : 'user',
      text: message.text,
      rawAssistantResponse: message.rawAssistantResponse,
      fileParts: fileParts.length ? fileParts : undefined,
      chatSummary: message.chatSummary,
    };
  });

  if (history.length > 0 && Number.isFinite(maxMediaToKeep) && maxMediaToKeep >= 0) {
    const mediaIndexes = history
      .map((item, index) => item.fileParts?.length ? index : -1)
      .filter(index => index >= 0);
    const keep = new Set(mediaIndexes.slice(-maxMediaToKeep));
    history.forEach((item, index) => {
      if (item.fileParts?.length && !keep.has(index)) item.fileParts = undefined;
    });
  }

  const contextParts: string[] = [];
  if (globalProfileText?.trim()) {
    contextParts.push(`Learner Profile (global):\n${globalProfileText.trim().slice(0, 10_000)}\nEND OF GLOBAL PROFILE MEMORY.`);
  }
  if (contextSummary?.trim()) {
    contextParts.push(`Conversation Summary:\n${contextSummary.trim().slice(0, 10_000)}`);
  }
  if (contextParts.length > 0) {
    const prefaceText = contextParts.join('\n\n');
    if (history[0]?.role === 'user') history[0].text = `${prefaceText}\n\n${history[0].text || ''}`;
    else history.unshift({ role: 'user', text: prefaceText });
  }
  if (avatarOverlayFileUri && avatarOverlayMimeType && history[0]?.role === 'user') {
    history[0].avatarFileUri = avatarOverlayFileUri;
    history[0].avatarMimeType = avatarOverlayMimeType;
  }
  if (placeholderLatestUserMessage?.trim()) {
    history.push({ role: 'user', text: placeholderLatestUserMessage.trim().slice(0, 10_000) });
  }
  return history;
};
