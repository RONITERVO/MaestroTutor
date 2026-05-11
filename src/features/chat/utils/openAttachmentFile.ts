// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { Capacitor } from '@capacitor/core';
import { Directory, Filesystem } from '@capacitor/filesystem';
import { Share } from '@capacitor/share';
import { openExternalUrl } from '../../../shared/utils/openExternalUrl';

interface OpenAttachmentFileOptions {
  url: string;
  fileName?: string | null;
  mimeType?: string | null;
}

const MIME_EXTENSION_FALLBACKS: Record<string, string> = {
  'application/msword': 'doc',
  'application/rtf': 'rtf',
  'application/vnd.ms-excel': 'xls',
  'application/vnd.ms-powerpoint': 'ppt',
  'application/vnd.oasis.opendocument.presentation': 'odp',
  'application/vnd.oasis.opendocument.spreadsheet': 'ods',
  'application/vnd.oasis.opendocument.text': 'odt',
  'application/vnd.openxmlformats-officedocument.presentationml.presentation': 'pptx',
  'application/vnd.openxmlformats-officedocument.presentationml.slideshow': 'ppsx',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': 'xlsx',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document': 'docx',
  'application/pdf': 'pdf',
  'application/json': 'json',
  'text/csv': 'csv',
  'text/plain': 'txt',
};

const isHttpUrl = (url: string): boolean => /^https?:/i.test(url);
const isDataUrl = (url: string): boolean => /^data:/i.test(url);
const isBlobUrl = (url: string): boolean => /^blob:/i.test(url);
const isFileUrl = (url: string): boolean => /^file:/i.test(url);

const inferExtension = (mimeType?: string | null): string => {
  const normalized = (mimeType || '').split(';')[0].trim().toLowerCase();
  return MIME_EXTENSION_FALLBACKS[normalized] || '';
};

const sanitizeFileName = (fileName?: string | null, mimeType?: string | null): string => {
  const requestedName = (fileName || '').trim().split(/[\\/]/).pop() || '';
  const cleanedName = requestedName
    .replace(/[\u0000-\u001f<>:"/\\|?*]+/g, '-')
    .replace(/\s+/g, ' ')
    .replace(/^\.+/, '')
    .trim();
  const fallbackExtension = inferExtension(mimeType);
  const fallbackName = fallbackExtension ? `attachment.${fallbackExtension}` : 'attachment';
  const withFallback = cleanedName || fallbackName;
  const hasExtension = /\.[A-Za-z0-9]{1,12}$/.test(withFallback);
  const withExtension = hasExtension || !fallbackExtension ? withFallback : `${withFallback}.${fallbackExtension}`;

  if (withExtension.length <= 96) return withExtension;

  const dotIndex = withExtension.lastIndexOf('.');
  const extension = dotIndex > 0 ? withExtension.slice(dotIndex) : '';
  const stem = dotIndex > 0 ? withExtension.slice(0, dotIndex) : withExtension;
  return `${stem.slice(0, Math.max(1, 96 - extension.length)).trimEnd()}${extension}`;
};

const bytesToBase64 = (bytes: Uint8Array): string => {
  let binary = '';
  const chunkSize = 0x8000;
  for (let index = 0; index < bytes.length; index += chunkSize) {
    const chunk = bytes.subarray(index, index + chunkSize);
    binary += String.fromCharCode(...chunk);
  }
  return btoa(binary);
};

const parseDataUrl = (url: string): { base64: string; mimeType: string | null } | null => {
  const commaIndex = url.indexOf(',');
  if (commaIndex <= 5) return null;

  const header = url.slice(5, commaIndex);
  const payload = url.slice(commaIndex + 1);
  if (!payload) return null;

  const mimeType = header.split(';')[0]?.trim() || null;
  const isBase64 = header.toLowerCase().split(';').includes('base64');

  if (isBase64) {
    let decodedPayload = payload;
    try {
      decodedPayload = decodeURIComponent(payload);
    } catch {
      decodedPayload = payload;
    }
    return { base64: decodedPayload.replace(/\s/g, ''), mimeType };
  }

  try {
    const decodedPayload = decodeURIComponent(payload);
    return {
      base64: bytesToBase64(new TextEncoder().encode(decodedPayload)),
      mimeType,
    };
  } catch {
    return null;
  }
};

const blobUrlToBase64 = async (url: string): Promise<{ base64: string; mimeType: string | null }> => {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`Failed to read blob attachment: ${response.status}`);

  const blob = await response.blob();
  const bytes = new Uint8Array(await blob.arrayBuffer());
  return {
    base64: bytesToBase64(bytes),
    mimeType: blob.type || null,
  };
};

const getAttachmentPayload = async (url: string): Promise<{ base64: string; mimeType: string | null }> => {
  if (isDataUrl(url)) {
    const parsed = parseDataUrl(url);
    if (!parsed) throw new Error('Failed to parse attachment data URL');
    return parsed;
  }

  if (isBlobUrl(url)) {
    return blobUrlToBase64(url);
  }

  throw new Error('Unsupported attachment URL');
};

const triggerBrowserDownload = (url: string, fileName: string): void => {
  if (typeof document === 'undefined') return;

  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.rel = 'noopener noreferrer';
  anchor.style.display = 'none';
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
};

export const isAttachmentOpenCancelError = (error: unknown): boolean => {
  const message = error instanceof Error ? error.message : String(error || '');
  return /cancel/i.test(message);
};

export const openAttachmentFile = async ({
  url,
  fileName,
  mimeType,
}: OpenAttachmentFileOptions): Promise<void> => {
  const trimmedUrl = url.trim();
  if (!trimmedUrl) return;

  if (isHttpUrl(trimmedUrl)) {
    await openExternalUrl(trimmedUrl);
    return;
  }

  if (Capacitor.isNativePlatform()) {
    if (isFileUrl(trimmedUrl)) {
      const safeName = sanitizeFileName(fileName, mimeType);
      await Share.share({
        title: safeName,
        files: [trimmedUrl],
        dialogTitle: safeName,
      });
      return;
    }

    if (isDataUrl(trimmedUrl) || isBlobUrl(trimmedUrl)) {
      const attachment = await getAttachmentPayload(trimmedUrl);
      const safeName = sanitizeFileName(fileName, attachment.mimeType || mimeType);
      const result = await Filesystem.writeFile({
        path: `attachments/${Date.now()}-${safeName}`,
        data: attachment.base64,
        directory: Directory.Cache,
        recursive: true,
      });

      if (!result.uri) throw new Error('Failed to write attachment file');

      await Share.share({
        title: safeName,
        files: [result.uri],
        dialogTitle: safeName,
      });
      return;
    }
  }

  if (isDataUrl(trimmedUrl) || isBlobUrl(trimmedUrl)) {
    triggerBrowserDownload(trimmedUrl, sanitizeFileName(fileName, mimeType));
    return;
  }

  if (typeof window !== 'undefined') {
    window.open(trimmedUrl, '_blank', 'noopener,noreferrer');
  }
};
