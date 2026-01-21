import { ChatMessage, ReplySuggestion, TtsAudioCacheEntry, TtsProvider, SttProvider } from '../../../core/types';

export const INLINE_CAP_IMAGE = 1_000_000; // ~1MB
export const INLINE_CAP_VIDEO = 4_000_000; // ~4MB
export const INLINE_CAP_OTHER = 8_000_000; // ~8MB
export const INLINE_CAP_AUDIO = 10_000_000; // ~10MB for cached TTS data URLs
export const MAX_TTS_CACHE_ENTRIES_PER_PARENT = 80;

export const sanitizeForPersistence = (m: ChatMessage): ChatMessage => {
  const out: ChatMessage = { ...m };

  const inferMimeFromDataUrl = (dataUrl?: string | null): string | undefined => {
    if (!dataUrl || typeof dataUrl !== 'string') return undefined;
    const m = dataUrl.match(/^data:([^;]+);base64,/i);
    return m ? m[1] : undefined;
  };

  const capForMime = (mime?: string | null): number => {
    const t = (mime || '').toLowerCase();
    if (t.startsWith('video/')) return INLINE_CAP_VIDEO;
    if (t.startsWith('image/')) return INLINE_CAP_IMAGE;
    return INLINE_CAP_OTHER;
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
        provider: entry.provider || 'gemini',
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

  // Optimization: If an optimized LLM image/media exists, promote it to the main display slot for storage.
  // This discards the full-resolution 'imageUrl' to save significant space in IndexedDB,
  // while ensuring the message history remains visually complete (albeit at lower quality) upon reload.
  if (typeof (out as any).storageOptimizedImageUrl === 'string' && (out as any).storageOptimizedImageUrl) {
    out.imageUrl = (out as any).storageOptimizedImageUrl;
    out.imageMimeType = (out as any).storageOptimizedImageMimeType || out.imageMimeType;
    // Remove the specific LLM fields since we've promoted them to the main fields for persistence
    delete (out as any).storageOptimizedImageUrl;
    delete (out as any).storageOptimizedImageMimeType;
  }

  // Cap the size of the image (whether original or promoted optimized version)
  if (typeof out.imageUrl === 'string') {
    const effMime = out.imageMimeType || inferMimeFromDataUrl(out.imageUrl) || 'image/*';
    const cap = capForMime(effMime);
    if (out.imageUrl.length > cap) {
      out.imageUrl = undefined;
      out.imageMimeType = undefined;
    }
  }

  // Ensure any lingering LLM-specific media fields are removed from the persisted object
  if ('storageOptimizedImageUrl' in out) delete (out as any).storageOptimizedImageUrl;
  if ('storageOptimizedImageMimeType' in out) delete (out as any).storageOptimizedImageMimeType;
  // Remove uploaded file URIs - they expire after 48 hours from Google's Files API
  // so we can't persist them. On reload, we'll re-upload if needed.
  if ('uploadedFileUri' in out) delete (out as any).uploadedFileUri;
  if ('uploadedFileMimeType' in out) delete (out as any).uploadedFileMimeType;

  if (typeof out.rawAssistantResponse === 'string' && out.rawAssistantResponse.length > 200_000) {
    out.rawAssistantResponse = out.rawAssistantResponse.slice(0, 200_000);
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