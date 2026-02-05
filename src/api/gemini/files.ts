// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { debugLogService } from '../../features/diagnostics';
import { getAi } from './client';

/**
 * Set of URIs known to be expired/deleted (403/404).
 * Once expired, a URI never becomes valid again, so we track them permanently
 * for the session to avoid redundant API calls.
 */
const knownExpiredUris = new Set<string>();

/**
 * Normalizes a MIME type to avoid encoding issues with parameters.
 * For audio/webm;codecs=opus, the = sign can get escaped as \u003d in JSON
 * which causes the Gemini API to reject the file with a MIME type mismatch.
 * This function strips codec parameters for audio types to avoid this issue.
 */
const normalizeMimeTypeForUpload = (mimeType: string): string => {
  if (!mimeType) return mimeType;

  if (mimeType.startsWith('audio/') && mimeType.includes(';')) {
    return mimeType.split(';')[0];
  }

  return mimeType;
};

/**
 * Waits for an uploaded file to become ACTIVE before it can be used.
 * Files go through PROCESSING -> ACTIVE (or FAILED) state transitions.
 */
const MAX_POLL_RETRIES = 10;

const waitForFileActive = async (
  fileNameOrUri: string,
  maxWaitMs: number = 60000,
  pollIntervalMs: number = 1000
): Promise<any> => {
  const ai = await getAi();

  let name = fileNameOrUri;
  const m = /\/files\/([^?\s]+)/.exec(fileNameOrUri || '');
  if (m) name = `files/${m[1]}`;

  const startTime = Date.now();
  let retryCount = 0;

  while (Date.now() - startTime < maxWaitMs) {
    try {
      const file = await ai.files.get({ name });

      if (file.state === 'ACTIVE') {
        debugLogService
          .logRequest('files.waitForActive', 'Files API', { name, state: 'ACTIVE', waitMs: Date.now() - startTime })
          .complete({ state: 'ACTIVE' });
        return file;
      }

      if (file.state === 'FAILED') {
        const error = file.error ? JSON.stringify(file.error) : 'Unknown error';
        throw new Error(`File processing failed: ${error}`);
      }

      await new Promise(resolve => setTimeout(resolve, pollIntervalMs));
    } catch (e: any) {
      if (e.message?.includes('File processing failed')) {
        throw e;
      }
      retryCount++;
      console.warn(`Error polling file state (attempt ${retryCount}/${MAX_POLL_RETRIES}), retrying...`, e.message);
      if (retryCount >= MAX_POLL_RETRIES) {
        throw new Error(`Max retries (${MAX_POLL_RETRIES}) exceeded while polling file ${name}: ${e.message}`);
      }
      await new Promise(resolve => setTimeout(resolve, pollIntervalMs));
    }
  }

  throw new Error(`Timeout waiting for file ${name} to become ACTIVE after ${maxWaitMs}ms`);
};

export const checkFileStatuses = async (uris: string[]): Promise<Record<string, { deleted: boolean; active: boolean }>> => {
  if (!uris || !uris.length) return {};
  const ai = await getAi();
  const out: Record<string, { deleted: boolean; active: boolean }> = {};

  // Filter out URIs already known to be expired
  const uncheckedUris: string[] = [];
  for (const uri of uris) {
    if (knownExpiredUris.has(uri)) {
      out[uri] = { deleted: true, active: false };
    } else {
      uncheckedUris.push(uri);
    }
  }

  // Only make API calls for URIs not known to be expired
  if (uncheckedUris.length > 0) {
    await Promise.all(
      uncheckedUris.map(async (uri) => {
        try {
          let name = uri;
          const m = /\/files\/([^?\s]+)/.exec(uri || '');
          if (m) name = `files/${m[1]}`;
          const f = await ai.files.get({ name });
          out[uri] = {
            deleted: f.state === 'FAILED',
            active: f.state === 'ACTIVE',
          };
          // If FAILED, mark as expired
          if (f.state === 'FAILED') {
            knownExpiredUris.add(uri);
          }
        } catch (e: any) {
          // Check numeric status first, then fall back to safer string checks
          const is404or403 = e.status === 404 || e.status === 403 ||
            Number(e.status) === 404 || Number(e.status) === 403 ||
            (e.status === undefined && (
              e.message?.toLowerCase() === 'not found' ||
              e.message?.includes('404') ||
              e.message?.includes('403') ||
              e.message?.toLowerCase().includes('forbidden')
            ));
          if (is404or403) {
            out[uri] = { deleted: true, active: false };
            knownExpiredUris.add(uri);
          } else {
            out[uri] = { deleted: false, active: false };
          }
        }
      })
    );
  }
  return out;
};

export const sanitizeHistoryWithVerifiedUris = async (history: any[]) => {
  const uris = history.map(h => h.imageFileUri).filter(u => u);
  if (uris.length === 0) return history;

  const statuses = await checkFileStatuses(uris);
  let strippedCount = 0;
  const result = history.map((h, idx) => {
    if (h.imageFileUri) {
      const status = statuses[h.imageFileUri];
      if (status?.deleted || !status?.active) {
        strippedCount++;
        console.warn(`[sanitizeHistory] Stripping expired/invalid media URI from history item ${idx}:`, h.imageFileUri?.slice(0, 80));
        const { imageFileUri, imageMimeType, ...rest } = h;
        return rest;
      }
    }
    return h;
  });
  if (strippedCount > 0) {
    console.warn(`[sanitizeHistory] Total media items stripped due to expired URIs: ${strippedCount}/${uris.length}`);
  }
  return result;
};

export const uploadMediaToFiles = async (
  dataUrl: string,
  mimeType: string,
  displayName?: string
): Promise<{ uri: string; mimeType: string }> => {
  const ai = await getAi();

  const normalizedMimeType = normalizeMimeTypeForUpload(mimeType);

  const base64Data = dataUrl.split(',')[1];
  const byteCharacters = atob(base64Data);
  const byteNumbers = new Array(byteCharacters.length);
  for (let i = 0; i < byteCharacters.length; i++) {
    byteNumbers[i] = byteCharacters.charCodeAt(i);
  }
  const byteArray = new Uint8Array(byteNumbers);
  const blob = new Blob([byteArray], { type: normalizedMimeType });
  const file = new File([blob], displayName || 'upload', { type: normalizedMimeType });

  const log = debugLogService.logRequest('files.upload', 'Files API', {
    mimeType: normalizedMimeType,
    displayName,
    size: blob.size,
  });

  try {
    const uploadResult = await ai.files.upload({
      file,
      config: { displayName, mimeType: normalizedMimeType },
    });
    if (!uploadResult) {
      throw new Error('Upload failed: missing result');
    }
    if (!uploadResult.uri || !uploadResult.uri.trim()) {
      throw new Error('Upload failed: missing uri');
    }
    if (!uploadResult.mimeType || !uploadResult.mimeType.trim()) {
      throw new Error('Upload failed: missing mimeType');
    }

    if (uploadResult.state !== 'ACTIVE') {
      await waitForFileActive(uploadResult.name || uploadResult.uri);
    }

    log.complete(uploadResult);
    return { uri: uploadResult.uri, mimeType: uploadResult.mimeType };
  } catch (e) {
    log.error(e);
    throw e;
  }
};

export const deleteFileByNameOrUri = async (nameOrUri: string) => {
  const ai = await getAi();
  let name = nameOrUri;
  const m = /\/files\/([^?\s]+)/.exec(nameOrUri || '');
  if (m) name = `files/${m[1]}`;
  try {
    await ai.files.delete({ name });
    return { ok: true };
  } catch {
    return { ok: false };
  }
};
