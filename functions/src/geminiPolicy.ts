// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { createHttpError } from './http';
import { DEFAULT_GEMINI_PRICING, resolvePricingRule } from '../../shared/pricing/registry';

export type ManagedContentOperation = 'generateContent' | 'streamContent' | 'generateImage';

const LEGACY_MANAGED_GENERATION_MODEL_PINS = new Map<string, string>([
  ['gemini-flash-latest', 'gemini-3.7-flash'],
  ['gemini-flash-lite-latest', 'gemini-3.5-flash-lite'],
]);

/**
 * Keep installed clients working without letting a provider-owned `latest`
 * alias choose a different model or price for prepaid traffic.
 */
export const resolvePinnedManagedGenerationModel = (model: string): string => {
  const normalized = (model || '').trim();
  const unqualified = normalized.startsWith('models/')
    ? normalized.slice('models/'.length)
    : normalized;
  return LEGACY_MANAGED_GENERATION_MODEL_PINS.get(unqualified.toLowerCase()) || normalized;
};

const normalizedResponseModalities = (config: Record<string, unknown> | undefined): string[] => {
  const modalities = config?.responseModalities;
  if (!Array.isArray(modalities)) return [];
  return modalities
    .filter((value): value is string => typeof value === 'string')
    .map((value) => value.trim().toUpperCase());
};

/** Billing operations are derived server-side; the caller never chooses one. */
export const resolveManagedContentOperation = (
  config: Record<string, unknown> | undefined,
  streaming: boolean,
  model = '',
): ManagedContentOperation => (
  normalizedResponseModalities(config).includes('IMAGE')
    || Boolean(resolvePricingRule(model, DEFAULT_GEMINI_PRICING)?.generatedImageUsdFallback)
    ? 'generateImage'
    : streaming
      ? 'streamContent'
      : 'generateContent'
);

export const requireAllowedManagedModel = (
  model: string,
  allowedModels: ReadonlySet<string>,
  surface: string,
): string => {
  const normalized = model.trim();
  if (!normalized) {
    throw createHttpError(400, `A Gemini model is required for ${surface}.`);
  }
  const unqualified = normalized.startsWith('models/')
    ? normalized.slice('models/'.length)
    : normalized;
  if (!allowedModels.has(normalized) && !allowedModels.has(unqualified)) {
    throw createHttpError(400, `Gemini model "${normalized}" is not enabled for managed ${surface}.`);
  }
  return normalized;
};

/** A model cannot be enabled for prepaid generation until it has a rate. */
export const requirePricedManagedGenerationModel = (model: string): string => {
  const rule = resolvePricingRule(model, DEFAULT_GEMINI_PRICING);
  if (!rule || rule.unpricedReason) {
    throw createHttpError(
      500,
      `Managed model "${model}" has no billable pricing rule.`,
    );
  }
  return model;
};

const FORBIDDEN_MANAGED_CONFIG_KEYS = new Set([
  'abortSignal',
  'apiKey',
  'baseUrl',
  'candidateCount',
  'headers',
  'httpOptions',
  'temperature',
  'timeout',
  'topK',
  'topP',
]);

/** Prevent callers from altering backend transport or enabling unpriced tools. */
export const requireSafeManagedGenerationConfig = (
  config: Record<string, unknown> | undefined,
): Record<string, unknown> | undefined => {
  if (!config) return undefined;
  if (typeof config !== 'object' || Array.isArray(config)) {
    throw createHttpError(400, 'Managed generation config must be an object.');
  }
  if (
    config.maxOutputTokens !== undefined
    && (
      !Number.isSafeInteger(config.maxOutputTokens)
      || Number(config.maxOutputTokens) <= 0
    )
  ) {
    throw createHttpError(400, 'Managed maxOutputTokens must be a positive integer.');
  }
  for (const key of Object.keys(config)) {
    if (FORBIDDEN_MANAGED_CONFIG_KEYS.has(key)) {
      throw createHttpError(400, `Managed generation config does not allow "${key}".`);
    }
  }

  if (config.tools !== undefined) {
    if (!Array.isArray(config.tools) || config.tools.length !== 1) {
      throw createHttpError(400, 'Managed generation supports only the Google Search tool.');
    }
    const tool = config.tools[0];
    if (
      !tool
      || typeof tool !== 'object'
      || Array.isArray(tool)
      || Object.keys(tool).length !== 1
      || !Object.prototype.hasOwnProperty.call(tool, 'googleSearch')
      || typeof (tool as { googleSearch?: unknown }).googleSearch !== 'object'
      || (tool as { googleSearch?: unknown }).googleSearch === null
      || Array.isArray((tool as { googleSearch?: unknown }).googleSearch)
    ) {
      throw createHttpError(400, 'Managed generation supports only the Google Search tool.');
    }
  }
  return config;
};

export const applyManagedGenerationLimits = (
  config: Record<string, unknown> | undefined,
  maxOutputTokens: number,
): Record<string, unknown> => {
  const safeConfig = requireSafeManagedGenerationConfig(config) || {};
  const requestedOutputTokens = safeConfig.maxOutputTokens === undefined
    ? maxOutputTokens
    : Number(safeConfig.maxOutputTokens);
  return {
    ...safeConfig,
    maxOutputTokens: Math.min(maxOutputTokens, requestedOutputTokens),
  };
};

export const usesManagedGoogleSearch = (
  config: Record<string, unknown> | undefined,
): boolean => Array.isArray(config?.tools) && config.tools.length === 1;

/**
 * The Gemini Developer API countTokens endpoint rejects generation config,
 * including systemInstruction and tools. Count the real contents, the system
 * instruction, and a deterministic serialization of the remaining prompt-
 * affecting config as separate inputs. The small serialization overhead is a
 * deliberate reservation-side overestimate; settlement still uses provider
 * usage metadata.
 */
export const buildManagedPromptTokenCountInputs = (
  contents: unknown,
  config: Record<string, unknown> | undefined,
): unknown[] => {
  const inputs: unknown[] = [contents];
  if (!config) return inputs;
  const { systemInstruction, ...remainingConfig } = config;
  if (systemInstruction !== undefined && systemInstruction !== null) {
    inputs.push(systemInstruction);
  }
  if (Object.keys(remainingConfig).length > 0) {
    inputs.push(`Managed generation config:\n${JSON.stringify(remainingConfig)}`);
  }
  return inputs;
};

/** Live tokens are scoped to this validated config as well as their model. */
export const requireSafeManagedLiveConfig = (
  config: Record<string, unknown> | undefined,
): Record<string, unknown> | undefined => {
  if (!config) return undefined;
  if (typeof config !== 'object' || Array.isArray(config)) {
    throw createHttpError(400, 'Managed Live config must be an object.');
  }
  for (const key of Object.keys(config)) {
    if (FORBIDDEN_MANAGED_CONFIG_KEYS.has(key) || key === 'tools' || key === 'toolConfig') {
      throw createHttpError(400, `Managed Live config does not allow "${key}".`);
    }
  }
  if (collectGeminiFileUris(config).length > 0) {
    throw createHttpError(400, 'Managed Live config cannot reference Gemini Files API URIs.');
  }
  return config;
};

/** Collect every Files API URI referenced anywhere in a request payload. */
export const collectGeminiFileUris = (value: unknown): string[] => {
  const uris = new Set<string>();
  const visited = new Set<object>();
  const stack: unknown[] = [value];

  while (stack.length > 0) {
    const current = stack.pop();
    if (!current || typeof current !== 'object') continue;
    if (visited.has(current)) continue;
    visited.add(current);

    if (Array.isArray(current)) {
      current.forEach((item) => stack.push(item));
      continue;
    }

    for (const [key, child] of Object.entries(current as Record<string, unknown>)) {
      if (key === 'fileUri' && typeof child === 'string' && child.trim()) {
        uris.add(child.trim());
      } else if (child && typeof child === 'object') {
        stack.push(child);
      }
    }
  }

  return [...uris];
};
