// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Browser persistence and refresh adapter. Core owns the single in-memory registry.
import { getGeminiModels, setGeminiModels, isValidRegistry } from '../../core-sdk/modelRegistry';
export { getGeminiModels, setGeminiModels, getModelRegistryDefaults } from '../../core-sdk/modelRegistry';
export type { GeminiModelRegistry, GeminiModelRegistryInput } from '../../core-sdk/modelRegistry';

export const MODEL_REGISTRY_STORAGE_KEY = 'maestro_gemini_models_v3';
export const MODEL_REGISTRY_URL_STORAGE_KEY = 'maestro_gemini_models_url';
export const DEFAULT_MODEL_REGISTRY_URL = 'https://chatwithmaestro.com/gemini-models.json';
const getEnvModelRegistryUrl = (): string | null => {
  const envUrl = (import.meta as any)?.env?.VITE_GEMINI_MODEL_REGISTRY_URL;
  return isNonEmptyString(envUrl) ? envUrl : null;
};

const isNonEmptyString = (value: unknown): value is string => typeof value === 'string' && value.trim().length > 0;

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

export const resolveModelRegistryUrl = (): string => {
  const envUrl = getEnvModelRegistryUrl();
  const envNormalized = envUrl && envUrl.includes('ronitervo.github.io') ? DEFAULT_MODEL_REGISTRY_URL : envUrl;

  const stored = getModelRegistryUrl();
  let storedNormalized: string | null = stored;
  if (stored && stored.includes('ronitervo.github.io')) {
    safeWriteLocalStorage(MODEL_REGISTRY_URL_STORAGE_KEY, DEFAULT_MODEL_REGISTRY_URL);
    storedNormalized = DEFAULT_MODEL_REGISTRY_URL;
  }

  return envNormalized || storedNormalized || DEFAULT_MODEL_REGISTRY_URL;
};

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
    safeWriteLocalStorage(MODEL_REGISTRY_STORAGE_KEY, JSON.stringify(getGeminiModels()));
    return { updated: true, source: 'remote' } as const;
  } catch (error) {
    return { updated: false, source: cached ? 'cache' : 'default', error } as const;
  } finally {
    window.clearTimeout(timeout);
  }
};
