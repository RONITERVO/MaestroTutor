// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getGeminiModels as getCoreModels } from '../../core-sdk/modelRegistry';
import { getGeminiModels, getModelRegistryDefaults, loadCachedGeminiModels, MODEL_REGISTRY_STORAGE_KEY,
  MODEL_REGISTRY_URL_STORAGE_KEY, DEFAULT_MODEL_REGISTRY_URL, refreshGeminiModelsFromRemote,
  resolveModelRegistryUrl, setGeminiModels } from './models';

const values = new Map<string, string>();
beforeEach(() => {
  values.clear();
  vi.stubGlobal('localStorage', { getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => values.set(key, value) });
  vi.stubGlobal('window', { setTimeout, clearTimeout });
});
afterEach(() => { setGeminiModels(getModelRegistryDefaults()); vi.unstubAllGlobals(); });

describe('browser model registry persistence contract', () => {
  it('restores cached models and preserves the cache if the remote is unavailable', async () => {
    const saved = { ...getModelRegistryDefaults(), text: { ...getGeminiModels().text, aux: 'cached-aux' } };
    values.set(MODEL_REGISTRY_STORAGE_KEY, JSON.stringify(saved));
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('offline')));
    expect(await refreshGeminiModelsFromRemote()).toMatchObject({ updated: false, source: 'cache' });
    expect(getGeminiModels()).toEqual(saved);
    expect(JSON.parse(values.get(MODEL_REGISTRY_STORAGE_KEY)!)).toEqual(saved);
  });
  it('updates the same model state and cache after validated remote configuration', async () => {
    const remote = { ...getModelRegistryDefaults(), image: { generation: 'remote-image' } };
    const fetch = vi.fn().mockResolvedValue({ ok: true, json: async () => remote });
    vi.stubGlobal('fetch', fetch);
    expect(await refreshGeminiModelsFromRemote({ url: 'https://example.test/models.json' })).toEqual({ updated: true, source: 'remote' });
    expect(fetch).toHaveBeenCalledWith('https://example.test/models.json', { signal: expect.any(AbortSignal), cache: 'no-store' });
    expect(getGeminiModels()).toEqual(remote);
    expect(getCoreModels()).toBe(getGeminiModels());
    expect(JSON.parse(values.get(MODEL_REGISTRY_STORAGE_KEY)!)).toEqual(remote);
  });
  it('ignores malformed cache and keeps defaults after invalid remote configuration', async () => {
    values.set(MODEL_REGISTRY_STORAGE_KEY, '{');
    expect(loadCachedGeminiModels()).toBe(false);
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({ text: {} }) }));
    expect(await refreshGeminiModelsFromRemote()).toMatchObject({ updated: false, source: 'default' });
    expect(getGeminiModels()).toEqual(getModelRegistryDefaults());
  });
  it('retains the legacy hosted URL migration and tolerates storage denial', () => {
    values.set(MODEL_REGISTRY_URL_STORAGE_KEY, 'https://ronitervo.github.io/MaestroTutor/gemini-models.json');
    expect(resolveModelRegistryUrl()).toBe(DEFAULT_MODEL_REGISTRY_URL);
    expect(values.get(MODEL_REGISTRY_URL_STORAGE_KEY)).toBe(DEFAULT_MODEL_REGISTRY_URL);
    vi.stubGlobal('localStorage', { getItem: () => { throw new Error('denied'); } });
    expect(loadCachedGeminiModels()).toBe(false);
  });
});
