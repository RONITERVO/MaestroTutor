// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { GoogleGenAI } from '@google/genai';
import type { CoreGeminiClient } from '../core-sdk/managedGeminiClient';
import { createLiveReasonGatedGeminiClient } from '../core-sdk/managedGeminiClient';
import type { ReturnTypeOfCreateManagedBackendClient } from './types';

export type HeadlessAccessMode = 'managed' | 'byok';

export interface HeadlessFilePort {
  upload(input: { dataUrl: string; mimeType: string; displayName?: string }): Promise<{ uri: string; mimeType: string }>;
  statuses(uris: string[]): Promise<Record<string, { deleted: boolean; active: boolean }>>;
  delete(nameOrUri: string): Promise<{ ok: boolean }>;
  clear(): Promise<{ deletedCount: number; failedCount: number; failedNames: string[] }>;
}

const normalizeMimeType = (value: string): string => value.split(';', 1)[0]?.trim() || value.trim();

const fileNameFromUri = (nameOrUri: string): string => {
  const match = /\/files\/([^?\s]+)/.exec(nameOrUri);
  return match ? `files/${match[1]}` : nameOrUri;
};

const decodeDataUrl = (dataUrl: string): Uint8Array => {
  const comma = dataUrl.indexOf(',');
  if (comma < 0) throw new Error('Media data must be a data URL.');
  const header = dataUrl.slice(0, comma);
  const payload = dataUrl.slice(comma + 1);
  if (/;base64(?:;|$)/i.test(header)) {
    return Uint8Array.from(globalThis.atob(payload), character => character.charCodeAt(0));
  }
  return new TextEncoder().encode(decodeURIComponent(payload));
};

const waitForDirectFileActive = async (ai: GoogleGenAI, nameOrUri: string) => {
  const name = fileNameFromUri(nameOrUri);
  const deadline = Date.now() + 60_000;
  while (Date.now() < deadline) {
    const file = await ai.files.get({ name });
    if (file.state === 'ACTIVE') return;
    if (file.state === 'FAILED') throw new Error(`File processing failed for ${name}.`);
    await new Promise(resolve => globalThis.setTimeout(resolve, 1_000));
  }
  throw new Error(`Timed out waiting for ${name} to become active.`);
};

export const resolveHeadlessAccessMode = (explicit?: string): HeadlessAccessMode => {
  const candidate = (explicit || process.env.MAESTRO_HEADLESS_ACCESS_MODE || 'managed').trim().toLowerCase();
  if (candidate !== 'managed' && candidate !== 'byok') {
    throw new Error('Headless access mode must be "managed" or "byok".');
  }
  return candidate;
};

export const createDirectHeadlessAi = (apiKey?: string): {
  ai: CoreGeminiClient;
  direct: GoogleGenAI;
} => {
  const key = (apiKey || process.env.MAESTRO_GEMINI_API_KEY || '').trim();
  if (!key) {
    throw new Error('BYOK headless mode requires MAESTRO_GEMINI_API_KEY. The key is read from the environment and is never persisted.');
  }
  const direct = new GoogleGenAI({ apiKey: key, apiVersion: 'v1alpha' });
  return {
    ai: createLiveReasonGatedGeminiClient(direct as unknown as CoreGeminiClient),
    direct,
  };
};

export const createManagedHeadlessFilePort = (
  backend: ReturnTypeOfCreateManagedBackendClient,
): HeadlessFilePort => ({
  upload: input => backend.uploadMedia(input),
  statuses: async uris => (await backend.checkFileStatuses({ uris })).statuses,
  delete: nameOrUri => backend.deleteFile({ nameOrUri }),
  clear: () => backend.clearFiles(),
});

export const createDirectHeadlessFilePort = (ai: GoogleGenAI): HeadlessFilePort => ({
  async upload(input) {
    const mimeType = normalizeMimeType(input.mimeType);
    const bytes = decodeDataUrl(input.dataUrl);
    const ownedBytes = Uint8Array.from(bytes);
    const blob = new Blob([ownedBytes.buffer], { type: mimeType });
    const file = new File([blob], input.displayName || 'headless-upload', { type: mimeType });
    const uploaded = await ai.files.upload({
      file,
      config: { displayName: input.displayName, mimeType },
    });
    const uri = typeof uploaded?.uri === 'string' ? uploaded.uri.trim() : '';
    const uploadedMimeType = typeof uploaded?.mimeType === 'string' ? uploaded.mimeType.trim() : '';
    if (!uri || !uploadedMimeType) throw new Error('Direct Files API upload returned no URI or MIME type.');
    if (uploaded.state !== 'ACTIVE') await waitForDirectFileActive(ai, uploaded.name || uri);
    return { uri, mimeType: uploadedMimeType };
  },
  async statuses(uris) {
    const statuses: Record<string, { deleted: boolean; active: boolean }> = {};
    await Promise.all(uris.map(async uri => {
      try {
        const file = await ai.files.get({ name: fileNameFromUri(uri) });
        statuses[uri] = { deleted: file.state === 'FAILED', active: file.state === 'ACTIVE' };
      } catch (error) {
        const status = Number((error as { status?: unknown })?.status);
        statuses[uri] = status === 403 || status === 404
          ? { deleted: true, active: false }
          : { deleted: false, active: false };
      }
    }));
    return statuses;
  },
  async delete(nameOrUri) {
    try {
      await ai.files.delete({ name: fileNameFromUri(nameOrUri) });
      return { ok: true };
    } catch {
      return { ok: false };
    }
  },
  async clear() {
    const pager = await ai.files.list({ config: { pageSize: 100 } });
    let deletedCount = 0;
    let failedCount = 0;
    const failedNames: string[] = [];
    for await (const file of pager) {
      const name = typeof file?.name === 'string' ? file.name.trim() : '';
      if (!name) continue;
      try {
        await ai.files.delete({ name });
        deletedCount += 1;
      } catch {
        failedCount += 1;
        failedNames.push(name);
      }
    }
    return { deletedCount, failedCount, failedNames };
  },
});
