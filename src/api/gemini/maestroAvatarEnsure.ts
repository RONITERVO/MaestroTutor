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
  let rawUri = cachedRawUri;
  let rawMimeType = cachedRawMimeType;

  const rawAge = cachedRawUpdatedAt > 0 ? Date.now() - cachedRawUpdatedAt : Number.POSITIVE_INFINITY;
  if (!rawUri || rawAge > MAESTRO_URI_REFRESH_MS) {
    // Check if the stored URI is still valid
    let needsUpload = true;
    if (asset.uri) {
      try {
        const statuses = await checkFileStatuses([asset.uri]);
        const st = statuses[asset.uri];
        if (st && !st.deleted && st.active) {
          rawUri = asset.uri;
          rawMimeType = mimeType;
          needsUpload = false;
        }
      } catch { /* assume needs upload */ }
    }

    if (needsUpload) {
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
  let overlayUri = cachedOverlayUri;
  let overlayMimeType = cachedOverlayMimeType;

  const overlayAge = cachedOverlayUpdatedAt > 0 ? Date.now() - cachedOverlayUpdatedAt : Number.POSITIVE_INFINITY;
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
