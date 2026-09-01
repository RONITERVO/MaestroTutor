// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { GoogleGenAI } from '@google/genai';
import { getApiKeyOrThrow } from '../../core/security/apiKeyStorage';
import { maestroAccessService } from '../../services/access/maestroAccessService';
import { maestroBackendService } from '../../services/backend/maestroBackendService';

export class ApiError extends Error {
  status?: number;
  code?: string;
  cooldownSuggestSeconds?: number;
  constructor(message: string, opts?: { status?: number; code?: string; cooldownSuggestSeconds?: number }) {
    super(message);
    this.status = opts?.status;
    this.code = opts?.code;
    this.cooldownSuggestSeconds = opts?.cooldownSuggestSeconds;
  }
}

/**
 * Validates an API key by making a lightweight models list call.
 * Returns `{ valid: true }` for valid keys or non-key-related errors (network, quota).
 * Returns `{ valid: false }` only for definitively invalid keys.
 */
export const validateApiKey = async (apiKey: string): Promise<{ valid: boolean }> => {
  try {
    const resp = await fetch(
      `https://generativelanguage.googleapis.com/v1beta/models?pageSize=1`,
      {
        method: 'GET',
        headers: { 'x-goog-api-key': apiKey } // Safer than query param
      }
    );

    if (resp.ok) return { valid: true };

    const errorBody = await resp.json();
    const status = resp.status;
    const errorMessage = errorBody.error?.message || '';
    const errorStatus = errorBody.error?.status || '';

    // Google returns 400 (INVALID_ARGUMENT) for bad keys.
    // We strictly check for Key validity issues to avoid false positives on other errors.
    if (status === 400 && (errorStatus === 'INVALID_ARGUMENT' || errorMessage.includes('API key'))) {
      return { valid: false };
    }

    // Treat other errors (403 Project Not Enabled, 429 Quota, 500 Server) as "valid key, temporary issue"
    return { valid: true };

  } catch {
    return { valid: true };
  }
};

export interface MaestroGeminiClient {
  models: {
    generateContent: (request: any) => Promise<any>;
    generateContentStream: (request: any) => Promise<AsyncIterable<any>>;
  };
  live: {
    connect: (request: any) => Promise<any>;
    music: {
      connect: (request: any) => Promise<any>;
    };
  };
}

export const getDirectAi = async (options?: { apiVersion?: string }): Promise<GoogleGenAI> => {
  try {
    const apiKey = await getApiKeyOrThrow();
    return new GoogleGenAI({
      apiKey,
      ...(options?.apiVersion ? { apiVersion: options.apiVersion } : {}),
    });
  } catch (e: any) {
    const message = e?.message || 'Missing API key';
    throw new ApiError(message, { code: 'MISSING_API_KEY' });
  }
};

const splitManagedConfig = (config: unknown): {
  config?: Record<string, unknown>;
  signal?: AbortSignal;
} => {
  if (!config || typeof config !== 'object' || Array.isArray(config)) return {};
  const { abortSignal, ...serializableConfig } = config as Record<string, unknown> & {
    abortSignal?: AbortSignal;
  };
  return {
    config: Object.keys(serializableConfig).length ? serializableConfig : undefined,
    signal: abortSignal,
  };
};

const createManagedAi = (options?: { apiVersion?: string }): MaestroGeminiClient => {
  const releaseLease = (leaseId: string) => {
    let released = false;
    return async () => {
      if (released) return;
      released = true;
      try {
        await maestroBackendService.releaseLiveTokenLease({ leaseId });
      } catch (error) {
        console.warn('[managed-ai] Failed to release a live-token lease.', error);
      }
    };
  };

  const connectManagedLive = async (purpose: 'live' | 'music', request: any) => {
    const model = typeof request?.model === 'string' ? request.model.trim() : '';
    if (!model) {
      throw new ApiError('A managed Live session requires a model.', { status: 400, code: 'MODEL_REQUIRED' });
    }

    const tokenLease = await maestroBackendService.createLiveToken({
      purpose,
      model,
      ...(request?.config && typeof request.config === 'object'
        ? { config: request.config as Record<string, unknown> }
        : {}),
    });
    const release = releaseLease(tokenLease.leaseId);
    const callbacks = request?.callbacks || {};
    const wrappedRequest = {
      ...request,
      callbacks: {
        ...callbacks,
        onclose: (event: unknown) => {
          void release();
          callbacks.onclose?.(event);
        },
        onerror: (event: unknown) => {
          callbacks.onerror?.(event);
        },
      },
    };

    const tokenClient = new GoogleGenAI({
      apiKey: tokenLease.token,
      apiVersion: options?.apiVersion || 'v1alpha',
    });

    try {
      const session = purpose === 'music'
        ? await tokenClient.live.music.connect(wrappedRequest)
        : await tokenClient.live.connect(wrappedRequest);
      const close = typeof session?.close === 'function' ? session.close.bind(session) : null;
      if (close) {
        session.close = () => {
          try {
            return close();
          } finally {
            void release();
          }
        };
      }
      return session;
    } catch (error) {
      await release();
      throw error;
    }
  };

  return {
    models: {
      generateContent: async (request: any) => {
        const { config, signal } = splitManagedConfig(request?.config);
        return maestroBackendService.generateContent({
          model: String(request?.model || ''),
          contents: request?.contents,
          ...(config ? { config } : {}),
        }, signal);
      },
      generateContentStream: async (request: any) => {
        const { config, signal } = splitManagedConfig(request?.config);
        return maestroBackendService.generateContentStream({
          model: String(request?.model || ''),
          contents: request?.contents,
          ...(config ? { config } : {}),
        }, signal);
      },
    },
    live: {
      connect: (request: any) => connectManagedLive('live', request),
      music: {
        connect: (request: any) => connectManagedLive('music', request),
      },
    },
  };
};

/**
 * Return the Gemini transport for the active access mode.
 *
 * BYOK deliberately keeps precedence when both credentials exist, preserving
 * the local-first contract and preventing a stored API key from unexpectedly
 * spending managed credits. Without a BYOK key, a managed Firebase session is
 * routed through the authenticated backend for every supported operation.
 */
export const getAi = async (options?: { apiVersion?: string }): Promise<MaestroGeminiClient> => {
  const accessMode = await maestroAccessService.resolveAccessMode();
  if (accessMode === 'byok') {
    return getDirectAi(options);
  }
  if (accessMode === 'managed') {
    return createManagedAi(options);
  }
  throw new ApiError('Sign in for managed access or add a Gemini API key.', {
    status: 401,
    code: 'MISSING_ACCESS',
  });
};
