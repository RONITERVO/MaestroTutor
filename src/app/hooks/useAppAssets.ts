// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
/**
 * useAppAssets - Loads avatar and loading assets.
 */

import { useEffect } from 'react';
import type { MutableRefObject } from 'react';
import { uniq, fetchDefaultAvatarBlob } from '../../shared/utils/common';
import {
  getLoadingGifsDB as getAssetsLoadingGifs,
  getMaestroProfileImageDB,
  setMaestroProfileImageDB,
} from '../../core/db/assets';

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
  useEffect(() => {
    (async () => {
      try {
        const a = await getMaestroProfileImageDB();
        if (a && (a.dataUrl || a.uri)) {
          // Parse MIME from mimeType field or extract from dataUrl
          let nextMime: string | null = null;
          if (a?.mimeType && typeof a.mimeType === 'string') {
            nextMime = a.mimeType;
          } else if (a?.dataUrl) {
            // Extract MIME from data URL: data:<mime>;base64,... or data:<mime>,...
            const mimeMatch = a.dataUrl.match(/^data:([^;,]+)[;,]/);
            nextMime = mimeMatch ? mimeMatch[1] : null;
          }
          maestroAvatarUriRef.current = a.uri || null;
          maestroAvatarMimeTypeRef.current = nextMime;
          setMaestroAvatar(a.dataUrl || a.uri || null, nextMime);
        } else {
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
              await setMaestroProfileImageDB({ dataUrl: defaultDataUrl, mimeType: defaultMime, uri: undefined, updatedAt: Date.now() });
              maestroAvatarUriRef.current = null;
              maestroAvatarMimeTypeRef.current = defaultMime;
              setMaestroAvatar(defaultDataUrl, defaultMime);
              try {
                window.dispatchEvent(new CustomEvent('maestro-avatar-updated', {
                  detail: { dataUrl: defaultDataUrl, mimeType: defaultMime, uri: undefined }
                }));
              } catch { /* ignore */ }
            } else {
              maestroAvatarUriRef.current = null;
              maestroAvatarMimeTypeRef.current = null;
              setMaestroAvatar(null, null);
            }
          } catch {
            maestroAvatarUriRef.current = null;
            maestroAvatarMimeTypeRef.current = null;
            setMaestroAvatar(null, null);
          }
        }
      } catch {
        maestroAvatarUriRef.current = null;
        maestroAvatarMimeTypeRef.current = null;
        setMaestroAvatar(null, null);
      }
    })();
  }, [maestroAvatarMimeTypeRef, maestroAvatarUriRef, setMaestroAvatar]);

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
          const resp = await fetch('/gifs/manifest.json', { cache: 'force-cache' });
          if (resp.ok) manifest = await resp.json();
        } catch { /* ignore */ }
        const merged = uniq([...current, ...manifest]);
        setLoadingGifs(merged);
      } catch { /* ignore */ }
    })();
  }, [setLoadingGifs]);
};

export default useAppAssets;
