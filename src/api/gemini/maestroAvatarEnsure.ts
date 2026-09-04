// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { getMaestroProfileImageDB, setMaestroProfileImageDB } from '../../core/db/assets';
import { checkFileStatuses, uploadMediaToFiles } from './files';
import { createAvatarWithOverlay } from '../../features/vision';
import { getAvatarAccessScope } from './avatarAccessScope';

const MAESTRO_URI_REFRESH_MS = (48 * 60 * 60 * 1000) - (5 * 60 * 1000); // 47h 55m

let cachedRawUri: string | null = null;
let cachedRawMimeType: string | null = null;
let cachedRawUpdatedAt = 0;

let cachedOverlayUri: string | null = null;
let cachedOverlayMimeType: string | null = null;
let cachedOverlayUpdatedAt = 0;
let cachedScope: string | null = null;
let cacheRevision = 0;
let inFlight: { scope: string; revision: number; promise: Promise<EnsuredAvatarResult> } | null = null;

export interface EnsuredAvatarResult {
  rawUri: string | null;
  rawMimeType: string | null;
  overlayUri: string | null;
  overlayMimeType: string | null;
}

const NULL_RESULT: EnsuredAvatarResult = { rawUri: null, rawMimeType: null, overlayUri: null, overlayMimeType: null };

const isActiveGeminiFileUri = async (uri: string | null | undefined): Promise<boolean> => {
  const candidate = typeof uri === 'string' ? uri.trim() : '';
  if (!candidate) return false;

  try {
    const statuses = await checkFileStatuses([candidate]);
    const status = statuses[candidate];
    return !!status && !status.deleted && status.active;
  } catch {
    return false;
  }
};

const findFirstActiveGeminiFileUri = async (candidates: Array<string | null | undefined>): Promise<string | null> => {
  const seen = new Set<string>();

  for (const candidate of candidates) {
    const trimmed = typeof candidate === 'string' ? candidate.trim() : '';
    if (!trimmed || seen.has(trimmed)) continue;
    seen.add(trimmed);

    if (await isActiveGeminiFileUri(trimmed)) {
      return trimmed;
    }
  }

  return null;
};

export const invalidateMaestroAvatarCache = (): void => {
  cacheRevision += 1;
  inFlight = null;
  cachedScope = null;
  cachedRawUri = null;
  cachedRawMimeType = null;
  cachedRawUpdatedAt = 0;
  cachedOverlayUri = null;
  cachedOverlayMimeType = null;
  cachedOverlayUpdatedAt = 0;
};

export const ensureMaestroAvatarUris = async (): Promise<EnsuredAvatarResult> => {
  const scope = await getAvatarAccessScope();
  if (cachedScope !== scope) invalidateMaestroAvatarCache();
  cachedScope = scope;
  const revision = cacheRevision;
  if (inFlight?.scope === scope && inFlight.revision === revision) return inFlight.promise;
  const promise = ensureForAccess(scope, revision);
  inFlight = { scope, revision, promise };
  try {
    return await promise;
  } finally {
    if (inFlight?.promise === promise) inFlight = null;
  }
};

const ensureForAccess = async (scope: string, revision: number): Promise<EnsuredAvatarResult> => {
  const assertCurrentAccess = async () => {
    if (scope !== await getAvatarAccessScope() || revision !== cacheRevision) {
      throw new Error('Avatar access changed while refreshing; retry with current access.');
    }
  };
  const asset = await getMaestroProfileImageDB();
  if (!asset?.dataUrl) return NULL_RESULT;

  const mimeType = asset.mimeType || 'image/png';

  // --- Ensure raw URI (for image generation) ---
  let rawUri: string | null = cachedRawUri;
  let rawMimeType: string | null = cachedRawMimeType || mimeType;

  const rawAge = cachedRawUpdatedAt > 0 ? Date.now() - cachedRawUpdatedAt : Number.POSITIVE_INFINITY;
  if (rawUri && !(await isActiveGeminiFileUri(rawUri))) {
    rawUri = null;
    rawMimeType = null;
  }

  if (!rawUri || rawAge > MAESTRO_URI_REFRESH_MS) {
    const existingRawUri = await findFirstActiveGeminiFileUri([rawUri, asset.accessScope === scope ? asset.uri : null]);

    if (existingRawUri) {
      rawUri = existingRawUri;
      rawMimeType = mimeType;
    } else {
      const uploaded = await uploadMediaToFiles(asset.dataUrl, mimeType, 'maestro-avatar');
      rawUri = uploaded.uri;
      rawMimeType = uploaded.mimeType || mimeType;
      await assertCurrentAccess();
      await setMaestroProfileImageDB({
        accessScope: scope,
        dataUrl: asset.dataUrl,
        mimeType: rawMimeType,
        uri: rawUri,
        updatedAt: Date.now(),
      });
    }

    await assertCurrentAccess();
    cachedRawUri = rawUri;
    cachedRawMimeType = rawMimeType;
    cachedRawUpdatedAt = Date.now();
  }

  // --- Ensure overlay URI (for chat LLM context) ---
  let overlayUri: string | null = cachedOverlayUri;
  let overlayMimeType: string | null = cachedOverlayMimeType;

  const overlayAge = cachedOverlayUpdatedAt > 0 ? Date.now() - cachedOverlayUpdatedAt : Number.POSITIVE_INFINITY;
  if (overlayUri && !(await isActiveGeminiFileUri(overlayUri))) {
    overlayUri = null;
    overlayMimeType = null;
  }

  if (!overlayUri || overlayAge > MAESTRO_URI_REFRESH_MS) {
    const overlay = await createAvatarWithOverlay(asset.dataUrl);
    const uploaded = await uploadMediaToFiles(overlay.dataUrl, overlay.mimeType, 'maestro-avatar-overlay');
    overlayUri = uploaded.uri;
    overlayMimeType = uploaded.mimeType || overlay.mimeType;
    await assertCurrentAccess();
    cachedOverlayUri = overlayUri;
    cachedOverlayMimeType = overlayMimeType;
    cachedOverlayUpdatedAt = Date.now();
  }

  await assertCurrentAccess();
  return { rawUri, rawMimeType, overlayUri, overlayMimeType };
};
