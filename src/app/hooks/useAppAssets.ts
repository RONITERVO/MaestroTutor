// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * useAppAssets - Loads avatar and loading assets.
 */

import { useEffect, useCallback, useRef } from 'react';
import type { MutableRefObject } from 'react';
import { fetchDefaultAvatarBlob } from '../../shared/utils/common';
import {
  deleteLegacyLoadingGifsDB,
  getMaestroProfileImageDB,
  setMaestroProfileImageDB,
} from '../../core/db/assets';
import { ensureMaestroAvatarUris, invalidateMaestroAvatarCache } from '../../api/gemini/maestroAvatarEnsure';
import { maestroAccessService } from '../../services/access/maestroAccessService';
import { getAvatarAccessScope } from '../../api/gemini/avatarAccessScope';
import { MANAGED_ACCESS_CHANGED_EVENT } from '../../core/security/managedAccessSessionStorage';

const MAESTRO_URI_REFRESH_MS = (48 * 60 * 60 * 1000) - (5 * 60 * 1000);
const API_KEY_CHANGED_EVENT = 'maestro-api-key-changed';

const mimeFromDataUrl = (dataUrl?: string | null): string | null => {
  if (!dataUrl) return null;
  const mimeMatch = dataUrl.match(/^data:([^;,]+)[;,]/);
  return mimeMatch ? mimeMatch[1] : null;
};

interface UseAppAssetsConfig {
  setLoadingAnimations: (animations: string[]) => void;
  setMaestroAvatar: (uri: string | null, mimeType: string | null) => void;
  maestroAvatarUriRef: MutableRefObject<string | null>;
  maestroAvatarMimeTypeRef: MutableRefObject<string | null>;
}

export const useAppAssets = ({
  setLoadingAnimations,
  setMaestroAvatar,
  maestroAvatarUriRef,
  maestroAvatarMimeTypeRef,
}: UseAppAssetsConfig) => {
  const isMountedRef = useRef(true);

  useEffect(() => {
    isMountedRef.current = true;
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

    if (await maestroAccessService.resolveAccessMode() === 'none') return;
    const scope = await getAvatarAccessScope();

    try {
      const uploaded = await ensureMaestroAvatarUris();
      if (scope !== await getAvatarAccessScope()) return;
      applyAvatarState(displayUrl || asset.dataUrl, uploaded.rawMimeType || displayMime, uploaded.rawUri);
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
    hydrateMaestroAvatar({ forceUpload: true, dropUri: true });
  }, [hydrateMaestroAvatar]);

  useEffect(() => {
    if (typeof window === 'undefined') return;
    let lastScope: string | undefined;
    let checkVersion = 0;
    const handler = () => {
      const version = ++checkVersion;
      void getAvatarAccessScope().then(scope => {
        if (version !== checkVersion || scope === lastScope) return;
        lastScope = scope;
        maestroAvatarUriRef.current = null;
        maestroAvatarMimeTypeRef.current = null;
        invalidateMaestroAvatarCache();
        void hydrateMaestroAvatar({ forceUpload: true, dropUri: true });
      });
    };
    void getAvatarAccessScope().then(scope => { if (!checkVersion) lastScope = scope; });
    window.addEventListener(API_KEY_CHANGED_EVENT, handler as any);
    window.addEventListener(MANAGED_ACCESS_CHANGED_EVENT, handler);
    return () => {
      checkVersion += 1;
      window.removeEventListener(API_KEY_CHANGED_EVENT, handler as any);
      window.removeEventListener(MANAGED_ACCESS_CHANGED_EVENT, handler);
    };
  }, [hydrateMaestroAvatar, maestroAvatarUriRef, maestroAvatarMimeTypeRef]);

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
        // Clean up legacy gif entries from DB
        try { await deleteLegacyLoadingGifsDB(); } catch { /* ignore */ }

        let manifest: string[] = [];
        try {
          const resp = await fetch(import.meta.env.BASE_URL + 'loading-animations/manifest.json', { cache: 'force-cache' });
          if (resp.ok) manifest = await resp.json();
        } catch { /* ignore */ }
        setLoadingAnimations(manifest);
      } catch { /* ignore */ }
    })();
  }, [setLoadingAnimations]);
};

export default useAppAssets;
