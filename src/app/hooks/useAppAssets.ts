// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * useAppAssets - Loads avatar and loading assets.
 */

import { useEffect, useCallback, useRef } from 'react';
import type { MutableRefObject } from 'react';
import { uniq, fetchDefaultAvatarBlob } from '../../shared/utils/common';
import {
  getLoadingGifsDB as getAssetsLoadingGifs,
  getMaestroProfileImageDB,
  setMaestroProfileImageDB,
  setLoadingGifsDB as setAssetsLoadingGifs,
} from '../../core/db/assets';
import { uploadMediaToFiles } from '../../api/gemini/files';
import { loadApiKey } from '../../core/security/apiKeyStorage';

const MAESTRO_URI_REFRESH_MS = (48 * 60 * 60 * 1000) - (5 * 60 * 1000);
const API_KEY_CHANGED_EVENT = 'maestro-api-key-changed';

const mimeFromDataUrl = (dataUrl?: string | null): string | null => {
  if (!dataUrl) return null;
  const mimeMatch = dataUrl.match(/^data:([^;,]+)[;,]/);
  return mimeMatch ? mimeMatch[1] : null;
};

interface UseAppAssetsConfig {
  setLoadingGifs: (gifs: string[]) => void;
  setMaestroAvatar: (uri: string | null, mimeType: string | null) => void;
  maestroAvatarUriRef: MutableRefObject<string | null>;
  maestroAvatarMimeTypeRef: MutableRefObject<string | null>;
}

export const useAppAssets = ({
  setLoadingGifs,
  setMaestroAvatar,
  maestroAvatarUriRef,
  maestroAvatarMimeTypeRef,
}: UseAppAssetsConfig) => {
  const isMountedRef = useRef(true);

  useEffect(() => {
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  const applyAvatarState = useCallback((displayUrl: string | null, mimeType: string | null, uri: string | null) => {
    if (!isMountedRef.current) return;
    maestroAvatarUriRef.current = uri || null;
    maestroAvatarMimeTypeRef.current = mimeType;
    setMaestroAvatar(displayUrl, mimeType);
  }, [maestroAvatarMimeTypeRef, maestroAvatarUriRef, setMaestroAvatar]);

  const refreshMaestroUriIfNeeded = useCallback(async (
    asset: { dataUrl?: string; mimeType?: string; uri?: string; updatedAt?: number } | null,
    displayUrl: string | null,
    displayMime: string | null,
    forceUpload?: boolean
  ) => {
    if (!asset?.dataUrl) return;
    const ageMs = typeof asset.updatedAt === 'number' ? Date.now() - asset.updatedAt : Number.POSITIVE_INFINITY;
    const shouldRefresh = !!forceUpload || !asset.uri || ageMs > MAESTRO_URI_REFRESH_MS;
    if (!shouldRefresh) return;

    const apiKey = await loadApiKey();
    if (!apiKey) return;

    try {
      const mimeForUpload = asset.mimeType || displayMime || 'image/png';
      const uploaded = await uploadMediaToFiles(asset.dataUrl, mimeForUpload, 'maestro-avatar');
      const refreshed = {
        dataUrl: asset.dataUrl,
        mimeType: uploaded.mimeType || mimeForUpload,
        uri: uploaded.uri,
        updatedAt: Date.now(),
      };
      await setMaestroProfileImageDB(refreshed);
      applyAvatarState(displayUrl || refreshed.dataUrl || uploaded.uri, refreshed.mimeType || null, uploaded.uri);
      try {
        window.dispatchEvent(new CustomEvent('maestro-avatar-updated', { detail: refreshed }));
      } catch { /* ignore */ }
    } catch {
      // Ignore upload failures (missing key, offline, etc.)
    }
  }, [applyAvatarState]);

  const hydrateMaestroAvatar = useCallback(async (opts?: { forceUpload?: boolean; dropUri?: boolean }) => {
    try {
      let a = await getMaestroProfileImageDB();
      if (a && (a.dataUrl || a.uri)) {
        const nextMime = (a?.mimeType && typeof a.mimeType === 'string')
          ? a.mimeType
          : mimeFromDataUrl(a?.dataUrl);
        const displayUrl = a.dataUrl || a.uri || null;
        const shouldDropUri = !!opts?.dropUri || !!opts?.forceUpload;
        applyAvatarState(displayUrl, nextMime, shouldDropUri ? null : (a.uri || null));
        await refreshMaestroUriIfNeeded(a, displayUrl, nextMime, opts?.forceUpload);
        return;
      }

      try {
        const blob = await fetchDefaultAvatarBlob();
        if (blob) {
          const defaultMime = blob.type || 'image/png';
          const defaultDataUrl: string = await new Promise((resolve, reject) => {
            const fr = new FileReader();
            fr.onloadend = () => resolve(fr.result as string);
            fr.onerror = () => reject(fr.error || new Error('DataURL conversion failed'));
            fr.readAsDataURL(blob);
          });
          const asset = { dataUrl: defaultDataUrl, mimeType: defaultMime, uri: undefined, updatedAt: Date.now() };
          await setMaestroProfileImageDB(asset);
          applyAvatarState(defaultDataUrl, defaultMime, null);
          try {
            window.dispatchEvent(new CustomEvent('maestro-avatar-updated', {
              detail: { dataUrl: defaultDataUrl, mimeType: defaultMime, uri: undefined }
            }));
          } catch { /* ignore */ }
          await refreshMaestroUriIfNeeded(asset, defaultDataUrl, defaultMime, opts?.forceUpload);
        } else {
          applyAvatarState(null, null, null);
        }
      } catch {
        applyAvatarState(null, null, null);
      }
    } catch {
      applyAvatarState(null, null, null);
    }
  }, [applyAvatarState, refreshMaestroUriIfNeeded]);

  useEffect(() => {
    hydrateMaestroAvatar();
  }, [hydrateMaestroAvatar]);

  useEffect(() => {
    if (typeof window === 'undefined') return;
    const handler = (event: any) => {
      const hasKey = !!event?.detail?.hasKey;
      if (hasKey) {
        hydrateMaestroAvatar({ forceUpload: true });
      } else {
        hydrateMaestroAvatar({ dropUri: true });
      }
    };
    window.addEventListener(API_KEY_CHANGED_EVENT, handler as any);
    return () => window.removeEventListener(API_KEY_CHANGED_EVENT, handler as any);
  }, [hydrateMaestroAvatar]);

  useEffect(() => {
    const handler = (event: any) => {
      try {
        const uri = event?.detail?.uri as string | undefined;
        const mimeType = event?.detail?.mimeType as string | undefined;
        const dataUrl = event?.detail?.dataUrl as string | undefined;
        maestroAvatarUriRef.current = uri || null;
        // Explicitly set to the provided mimeType or null to avoid stale values
        maestroAvatarMimeTypeRef.current = (mimeType && typeof mimeType === 'string') ? mimeType : null;
        setMaestroAvatar(dataUrl || uri || null, mimeType || null);
      } catch { /* ignore */ }
    };
    window.addEventListener('maestro-avatar-updated', handler as any);
    return () => window.removeEventListener('maestro-avatar-updated', handler as any);
  }, [maestroAvatarMimeTypeRef, maestroAvatarUriRef, setMaestroAvatar]);

  useEffect(() => {
    (async () => {
      try {
        const current = (await getAssetsLoadingGifs()) || [];
        let manifest: string[] = [];
        try {
          const resp = await fetch(import.meta.env.BASE_URL + 'gifs/manifest.json', { cache: 'force-cache' });
          if (resp.ok) manifest = await resp.json();
        } catch { /* ignore */ }
        const merged = uniq([...current, ...manifest]);
        setLoadingGifs(merged);
        if (current.length === 0 && merged.length > 0) {
          try { await setAssetsLoadingGifs(merged); } catch { /* ignore */ }
        }
      } catch { /* ignore */ }
    })();
  }, [setLoadingGifs]);
};

export default useAppAssets;
