// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

export interface GeminiModelRegistry {
  text: {
    default: string;
    aux: string;
    translation: string;
  };
  image: {
    generation: string;
  };
  audio: {
    tts: string;
    live: string;
  };
}

export const MODEL_REGISTRY_STORAGE_KEY = 'maestro_gemini_models_v1';
export const MODEL_REGISTRY_URL_STORAGE_KEY = 'https://ronitervo.github.io/MaestroTutor/gemini-models.json';

const DEFAULT_GEMINI_MODELS: GeminiModelRegistry = {
  text: {
    default: 'gemini-3-flash-preview',
    aux: 'gemini-3-flash-preview',
    translation: 'gemini-3-flash-preview',
  },
  image: {
    generation: 'gemini-2.5-flash-image',
  },
  audio: {
    tts: 'gemini-2.5-flash-preview-tts',
    live: 'gemini-2.5-flash-native-audio-preview-12-2025',
  },
};

let currentModels: GeminiModelRegistry = { ...DEFAULT_GEMINI_MODELS };

const getEnvModelRegistryUrl = (): string | null => {
  const envUrl = (import.meta as any)?.env?.VITE_GEMINI_MODEL_REGISTRY_URL;
  return isNonEmptyString(envUrl) ? envUrl : null;
};

const isNonEmptyString = (value: unknown): value is string => typeof value === 'string' && value.trim().length > 0;

const isValidRegistry = (value: any): value is GeminiModelRegistry => {
  if (!value || typeof value !== 'object') return false;
  return (
    isNonEmptyString(value?.text?.default) &&
    isNonEmptyString(value?.text?.aux) &&
    isNonEmptyString(value?.text?.translation) &&
    isNonEmptyString(value?.image?.generation) &&
    isNonEmptyString(value?.audio?.tts) &&
    isNonEmptyString(value?.audio?.live)
  );
};

const mergeWithDefaults = (value: Partial<GeminiModelRegistry>): GeminiModelRegistry => ({
  text: {
    default: value?.text?.default || DEFAULT_GEMINI_MODELS.text.default,
    aux: value?.text?.aux || DEFAULT_GEMINI_MODELS.text.aux,
    translation: value?.text?.translation || DEFAULT_GEMINI_MODELS.text.translation,
  },
  image: {
    generation: value?.image?.generation || DEFAULT_GEMINI_MODELS.image.generation,
  },
  audio: {
    tts: value?.audio?.tts || DEFAULT_GEMINI_MODELS.audio.tts,
    live: value?.audio?.live || DEFAULT_GEMINI_MODELS.audio.live,
  },
});

export const getGeminiModels = (): GeminiModelRegistry => currentModels;

export const setGeminiModels = (value: Partial<GeminiModelRegistry>) => {
  currentModels = mergeWithDefaults(value);
};

const safeReadLocalStorage = (key: string): string | null => {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
};

const safeWriteLocalStorage = (key: string, value: string) => {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Ignore storage write errors
  }
};

export const getModelRegistryUrl = (): string | null => safeReadLocalStorage(MODEL_REGISTRY_URL_STORAGE_KEY);

export const setModelRegistryUrl = (url: string) => {
  safeWriteLocalStorage(MODEL_REGISTRY_URL_STORAGE_KEY, url);
};

export const resolveModelRegistryUrl = (): string | null => getEnvModelRegistryUrl() || getModelRegistryUrl();

export const loadCachedGeminiModels = (): boolean => {
  const raw = safeReadLocalStorage(MODEL_REGISTRY_STORAGE_KEY);
  if (!raw) return false;
  try {
    const parsed = JSON.parse(raw);
    if (isValidRegistry(parsed)) {
      setGeminiModels(parsed);
      return true;
    }
  } catch {
    // Ignore cache parsing errors
  }
  return false;
};

export const getModelRegistryDefaults = (): GeminiModelRegistry => ({ ...DEFAULT_GEMINI_MODELS });

export const refreshGeminiModelsFromRemote = async (options?: { url?: string; timeoutMs?: number }) => {
  const cached = loadCachedGeminiModels();
  const url = options?.url || resolveModelRegistryUrl();
  if (!url) {
    return { updated: false, source: cached ? 'cache' : 'default' } as const;
  }

  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), options?.timeoutMs ?? 5000);

  try {
    const response = await fetch(url, { signal: controller.signal, cache: 'no-store' });
    if (!response.ok) {
      throw new Error(`Model registry fetch failed (${response.status})`);
    }
    const data = await response.json();
    if (!isValidRegistry(data)) {
      throw new Error('Model registry is missing required fields');
    }
    setGeminiModels(data);
    safeWriteLocalStorage(MODEL_REGISTRY_STORAGE_KEY, JSON.stringify(data));
    return { updated: true, source: 'remote' } as const;
  } catch (error) {
    return { updated: false, source: cached ? 'cache' : 'default', error } as const;
  } finally {
    window.clearTimeout(timeout);
  }
};
