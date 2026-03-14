// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { ChatMessage, ReplySuggestion, TtsAudioCacheEntry, TtsProvider, SttProvider } from '../../../core/types';
import { normalizeUploadedAttachmentVariants, buildUploadedAttachmentState } from './uploadedAttachmentVariants';

export const INLINE_CAP_IMAGE = 2_000_000; // ~2MB (increased from 1MB to reduce data loss from large images)
export const INLINE_CAP_VIDEO = 4_000_000; // ~4MB
export const INLINE_CAP_OTHER = 8_000_000; // ~8MB
export const INLINE_CAP_AUDIO = 10_000_000; // ~10MB for cached TTS data URLs
export const MAX_TTS_CACHE_ENTRIES_PER_PARENT = 80;

export const sanitizeForPersistence = (m: ChatMessage): ChatMessage => {
  const out: ChatMessage = { ...m };
  const normalizedUploadedAttachmentState = buildUploadedAttachmentState(
    normalizeUploadedAttachmentVariants(out.uploadedFileVariants)
  );
  out.uploadedFileVariants = normalizedUploadedAttachmentState.uploadedFileVariants;

  const inferMimeFromDataUrl = (dataUrl?: string | null): string | undefined => {
    if (!dataUrl || typeof dataUrl !== 'string') return undefined;
    const m = dataUrl.match(/^data:([^;,]+)(?:;[^,]*)?,/i);
    return m ? m[1].toLowerCase() : undefined;
  };

  const capForMime = (mime?: string | null): number => {
    const t = (mime || '').toLowerCase();
    if (t === 'image/svg+xml') return INLINE_CAP_OTHER;
    if (t.startsWith('video/')) return INLINE_CAP_VIDEO;
    if (t.startsWith('image/')) return INLINE_CAP_IMAGE;
    return INLINE_CAP_OTHER;
  };

  const getEffectiveMime = (explicitMime?: string | null, dataUrl?: string | null): string | undefined => (
    explicitMime || inferMimeFromDataUrl(dataUrl)
  );

  const clearOriginalAttachmentSource = () => {
    out.imageUrl = undefined;
    out.imageMimeType = undefined;
  };

  const clearOptimizedAttachmentSource = () => {
    delete (out as any).storageOptimizedImageUrl;
    delete (out as any).storageOptimizedImageMimeType;
  };

  const hasUploadedVariants = Array.isArray(out.uploadedFileVariants) && out.uploadedFileVariants.length > 0;

  const warnPersistenceDrop = (reason: string) => {
    console.warn(
      `[sanitizeForPersistence] ${reason}.${hasUploadedVariants ? ' uploaded attachment variants exist (remote only).' : ' WARNING: No uploaded attachment variants - media will be lost!'}`
    );
  };

  const sanitizeTtsCache = (entries?: TtsAudioCacheEntry[] | null): TtsAudioCacheEntry[] | undefined => {
    if (!Array.isArray(entries) || entries.length === 0) return undefined;
    const seen = new Set<string>();
    const sanitized: TtsAudioCacheEntry[] = [];
    entries.forEach((entry) => {
      if (!entry) return;
      const key = typeof entry.key === 'string' ? entry.key : '';
      const audio = typeof entry.audioDataUrl === 'string' ? entry.audioDataUrl : '';
      if (!key || !audio) return;
      if (audio.length > INLINE_CAP_AUDIO) return;
      if (seen.has(key)) return;
      seen.add(key);
      sanitized.push({
        key,
        langCode: entry.langCode || '',
        provider: entry.provider || 'gemini-live',
        audioDataUrl: audio,
        updatedAt: typeof entry.updatedAt === 'number' ? entry.updatedAt : Date.now(),
        voiceName: entry.voiceName,
        voiceId: entry.voiceId,
      });
    });
    if (!sanitized.length) return undefined;
    if (sanitized.length > MAX_TTS_CACHE_ENTRIES_PER_PARENT) {
      return sanitized.slice(-MAX_TTS_CACHE_ENTRIES_PER_PARENT);
    }
    return sanitized;
  };

  const sanitizeSuggestion = (suggestion: ReplySuggestion): ReplySuggestion => {
    const next: ReplySuggestion = { ...suggestion };
    if (Array.isArray(next.ttsAudioCache)) {
      const sanitizedCache = sanitizeTtsCache(next.ttsAudioCache);
      if (sanitizedCache && sanitizedCache.length) {
        next.ttsAudioCache = sanitizedCache;
      } else {
        delete next.ttsAudioCache;
      }
    }
    return next;
  };

  const originalDataUrl = typeof out.imageUrl === 'string' && out.imageUrl ? out.imageUrl : undefined;
  const originalMime = getEffectiveMime(out.imageMimeType, originalDataUrl);
  const optimizedDataUrl =
    typeof (out as any).storageOptimizedImageUrl === 'string' && (out as any).storageOptimizedImageUrl
      ? (out as any).storageOptimizedImageUrl as string
      : undefined;
  const optimizedMime = getEffectiveMime((out as any).storageOptimizedImageMimeType, optimizedDataUrl) || originalMime;
  const keepOriginalSvg =
    originalMime === 'image/svg+xml' &&
    typeof originalDataUrl === 'string' &&
    /^data:image\/svg\+xml/i.test(originalDataUrl);
  const originalFitsCap = !!(originalDataUrl && originalMime && originalDataUrl.length <= capForMime(originalMime));
  const optimizedFitsCap = !!(optimizedDataUrl && optimizedMime && optimizedDataUrl.length <= capForMime(optimizedMime));

  // Keep exactly one local attachment source for persistence so later sends can re-upload if remote URIs expire.
  // For normal media we prefer the optimized copy. For SVG we prefer the original source so the SVG text can be reconstructed.
  if (optimizedDataUrl) {
    if (keepOriginalSvg) {
      if (originalFitsCap) {
        clearOptimizedAttachmentSource();
        out.imageMimeType = originalMime;
      } else if (optimizedFitsCap) {
        warnPersistenceDrop(
          `SVG source exceeds ${Math.round(capForMime(originalMime) / 1000)}KB cap (${Math.round(originalDataUrl!.length / 1000)}KB). Keeping raster fallback only`
        );
        clearOriginalAttachmentSource();
        (out as any).storageOptimizedImageMimeType = optimizedMime;
      } else {
        warnPersistenceDrop(
          `Both SVG source (${Math.round(originalDataUrl!.length / 1000)}KB) and raster fallback (${Math.round(optimizedDataUrl.length / 1000)}KB) exceed persistence caps`
        );
        clearOriginalAttachmentSource();
        clearOptimizedAttachmentSource();
      }
    } else if (optimizedFitsCap) {
      clearOriginalAttachmentSource();
      (out as any).storageOptimizedImageMimeType = optimizedMime;
    } else if (originalFitsCap) {
      clearOptimizedAttachmentSource();
      out.imageMimeType = originalMime;
    } else {
      const preferredMime = optimizedMime || originalMime || 'attachment/*';
      warnPersistenceDrop(
        `${preferredMime} local attachment source exceeds persistence cap (${Math.round(Math.max(originalDataUrl?.length || 0, optimizedDataUrl.length) / 1000)}KB)`
      );
      clearOriginalAttachmentSource();
      clearOptimizedAttachmentSource();
    }
  } else if (originalDataUrl) {
    if (!originalFitsCap) {
      warnPersistenceDrop(
        `${originalMime || 'attachment/*'} local attachment source exceeds ${Math.round(capForMime(originalMime) / 1000)}KB cap (${Math.round(originalDataUrl.length / 1000)}KB)`
      );
      clearOriginalAttachmentSource();
    } else {
      out.imageMimeType = originalMime;
    }
  } else {
    clearOptimizedAttachmentSource();
  }

  // Keep uploadedFileVariants - they are validated on send via checkFileStatuses.
  // If a remote upload expires, resend will recover from the persisted local source kept above whenever possible.

  if (typeof out.rawAssistantResponse === 'string' && out.rawAssistantResponse.length > 200_000) {
    out.rawAssistantResponse = out.rawAssistantResponse.slice(0, 200_000);
  }

  if (typeof out.llmRawResponse === 'string' && out.llmRawResponse.length > 250_000) {
    out.llmRawResponse = out.llmRawResponse.slice(0, 250_000);
  }

  if (Array.isArray(out.ttsAudioCache)) {
    const sanitizedCache = sanitizeTtsCache(out.ttsAudioCache);
    if (sanitizedCache && sanitizedCache.length) {
      out.ttsAudioCache = sanitizedCache;
    } else {
      delete out.ttsAudioCache;
    }
  }

  if (Array.isArray(out.replySuggestions)) {
    out.replySuggestions = out.replySuggestions.map(sanitizeSuggestion);
  }

  if (out.recordedUtterance) {
    const audio = typeof out.recordedUtterance.dataUrl === 'string' ? out.recordedUtterance.dataUrl : '';
    if (!audio || audio.length > INLINE_CAP_AUDIO) {
      delete out.recordedUtterance;
    } else {
      const rawProvider = out.recordedUtterance.provider as string;
      const provider: SttProvider = (rawProvider === 'gemini') ? 'gemini' : 'browser';
      out.recordedUtterance = {
        dataUrl: audio,
        provider,
        langCode: out.recordedUtterance.langCode,
        transcript: out.recordedUtterance.transcript,
        sampleRate: out.recordedUtterance.sampleRate,
      };
    }
  }
  return out;
};

export const hashForTts = (value: string): string => {
  let acc = 0;
  for (let i = 0; i < value.length; i++) {
    acc = ((acc << 5) - acc) + value.charCodeAt(i);
    acc |= 0;
  }
  return Math.abs(acc).toString(36);
};

export const computeTtsCacheKey = (text: string, langCode: string, provider: TtsProvider, voiceName?: string): string => {
  const normalized = `${provider}::${voiceName || ''}::${langCode || ''}::${text}`;
  return `${hashForTts(normalized)}-${normalized.length.toString(36)}`;
};

export const getCachedAudioForKey = (entries: TtsAudioCacheEntry[] | undefined, key: string): string | undefined => {
  if (!Array.isArray(entries) || !key) return undefined;
  const match = entries.find(entry => entry && entry.key === key);
  return match ? match.audioDataUrl : undefined;
};

export const upsertTtsCacheEntries = (entries: TtsAudioCacheEntry[] | undefined, entry: TtsAudioCacheEntry): TtsAudioCacheEntry[] => {
  const base = Array.isArray(entries) ? entries.filter(e => e && e.key !== entry.key) : [];
  const normalized: TtsAudioCacheEntry = {
    ...entry,
    langCode: entry.langCode,
    provider: entry.provider,
    updatedAt: entry.updatedAt || Date.now(),
  };
  const combined = [...base, normalized];
  if (combined.length > MAX_TTS_CACHE_ENTRIES_PER_PARENT) {
    return combined.slice(-MAX_TTS_CACHE_ENTRIES_PER_PARENT);
  }
  return combined;
};
