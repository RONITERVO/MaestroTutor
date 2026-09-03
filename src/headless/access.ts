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

export interface HeadlessFileOwnership {
  list(): readonly string[];
  add(nameOrUri: string): Promise<void>;
  remove(nameOrUri: string): Promise<void>;
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

const createMemoryFileOwnership = (): HeadlessFileOwnership => {
  const owned = new Set<string>();
  return {
    list: () => [...owned],
    add: async nameOrUri => { owned.add(fileNameFromUri(nameOrUri)); },
    remove: async nameOrUri => { owned.delete(fileNameFromUri(nameOrUri)); },
  };
};

export const createDirectHeadlessFilePort = (
  ai: GoogleGenAI,
  ownership: HeadlessFileOwnership = createMemoryFileOwnership(),
): HeadlessFilePort => {
  const deleteOwned = async (nameOrUri: string): Promise<{ ok: boolean }> => {
    const name = fileNameFromUri(nameOrUri);
    const ownedName = ownership.list().find(candidate => fileNameFromUri(candidate) === name);
    if (!ownedName) return { ok: false };
    try {
      await ai.files.delete({ name });
      await ownership.remove(ownedName);
      return { ok: true };
    } catch (error) {
      const status = Number((error as { status?: unknown })?.status);
      if (status === 403 || status === 404) {
        await ownership.remove(ownedName);
        return { ok: true };
      }
      return { ok: false };
    }
  };

  return ({
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
    const ownedName = typeof uploaded?.name === 'string' && uploaded.name.trim()
      ? uploaded.name.trim()
      : fileNameFromUri(uri);
    if (!uri || !uploadedMimeType) {
      if (ownedName) await ai.files.delete({ name: fileNameFromUri(ownedName) }).catch(() => undefined);
      throw new Error('Direct Files API upload returned no URI or MIME type.');
    }
    try {
      if (uploaded.state !== 'ACTIVE') await waitForDirectFileActive(ai, ownedName);
      await ownership.add(ownedName);
    } catch (error) {
      // If local ownership cannot be made durable, do not leave a provider file
      // that a later safe cleanup cannot identify.
      await ai.files.delete({ name: fileNameFromUri(ownedName) }).catch(() => undefined);
      throw error;
    }
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
  delete: deleteOwned,
  async clear() {
    let deletedCount = 0;
    let failedCount = 0;
    const failedNames: string[] = [];
    for (const name of [...new Set(ownership.list().map(fileNameFromUri))]) {
      const result = await deleteOwned(name);
      if (result.ok) {
        deletedCount += 1;
      } else {
        failedCount += 1;
        failedNames.push(name);
      }
    }
    return { deletedCount, failedCount, failedNames };
  },
  });
};
