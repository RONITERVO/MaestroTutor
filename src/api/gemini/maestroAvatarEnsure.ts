// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { getMaestroProfileImageDB, setMaestroProfileImageDB } from '../../core/db/assets';
import { checkFileStatuses, uploadMediaToFiles } from './files';
import { createAvatarWithOverlay } from '../../features/vision';

const MAESTRO_URI_REFRESH_MS = (48 * 60 * 60 * 1000) - (5 * 60 * 1000); // 47h 55m

let cachedRawUri: string | null = null;
let cachedRawMimeType: string | null = null;
let cachedRawUpdatedAt = 0;

let cachedOverlayUri: string | null = null;
let cachedOverlayMimeType: string | null = null;
let cachedOverlayUpdatedAt = 0;

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
  cachedRawUri = null;
  cachedRawMimeType = null;
  cachedRawUpdatedAt = 0;
  cachedOverlayUri = null;
  cachedOverlayMimeType = null;
  cachedOverlayUpdatedAt = 0;
};

export const ensureMaestroAvatarUris = async (): Promise<EnsuredAvatarResult> => {
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
    const existingRawUri = await findFirstActiveGeminiFileUri([rawUri, asset.uri]);

    if (existingRawUri) {
      rawUri = existingRawUri;
      rawMimeType = mimeType;
    } else {
      const uploaded = await uploadMediaToFiles(asset.dataUrl, mimeType, 'maestro-avatar');
      rawUri = uploaded.uri;
      rawMimeType = uploaded.mimeType || mimeType;
      await setMaestroProfileImageDB({
        dataUrl: asset.dataUrl,
        mimeType: rawMimeType,
        uri: rawUri,
        updatedAt: Date.now(),
      });
      try {
        window.dispatchEvent(new CustomEvent('maestro-avatar-updated', {
          detail: { dataUrl: asset.dataUrl, mimeType: rawMimeType, uri: rawUri },
        }));
      } catch { /* ignore */ }
    }

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

    cachedOverlayUri = overlayUri;
    cachedOverlayMimeType = overlayMimeType;
    cachedOverlayUpdatedAt = Date.now();
  }

  return { rawUri, rawMimeType, overlayUri, overlayMimeType };
};
