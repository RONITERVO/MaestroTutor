// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { GoogleGenAI } from '@google/genai';
import type {
  BackendGenerateContentRequest,
  BackendGenerateContentResponse,
  BackendLiveTokenRequest,
  BackendLiveTokenResponse,
  BackendReleaseLiveTokenLeaseRequest,
} from '../core/contracts/backend';
import {
  requireLiveOpenReason,
  type LiveOpenReason,
} from '../../shared/liveOpenReason';

export interface CoreLiveConnectRequest extends Record<string, unknown> {
  model: string;
  liveOpenReason: LiveOpenReason;
  config?: Record<string, unknown>;
  callbacks?: Record<string, (...args: any[]) => unknown>;
}

export interface CoreGeminiClient {
  models: {
    generateContent(request: any): Promise<any>;
    generateContentStream(request: any): Promise<AsyncIterable<any>>;
  };
  live: {
    connect(request: CoreLiveConnectRequest): Promise<any>;
    music: { connect(request: any): Promise<any> };
  };
}

export interface ManagedGeminiBackendPort {
  generateContent(payload: BackendGenerateContentRequest, signal?: AbortSignal | null): Promise<BackendGenerateContentResponse>;
  generateContentStream(payload: BackendGenerateContentRequest, signal?: AbortSignal | null): Promise<AsyncIterable<unknown>>;
  createLiveToken(payload: BackendLiveTokenRequest): Promise<BackendLiveTokenResponse>;
  releaseLiveTokenLease(payload: BackendReleaseLiveTokenLeaseRequest): Promise<unknown>;
}

export interface ManagedGeminiClientOptions {
  apiVersion?: string;
  warn?: (message: string, error: unknown) => void;
  createTokenClient?: (token: string, apiVersion: string) => CoreGeminiClient;
}

const splitManagedConfig = (config: unknown): {
  config?: Record<string, unknown>;
  signal?: AbortSignal;
} => {
  if (!config || typeof config !== 'object' || Array.isArray(config)) return {};
  const { abortSignal, ...serializableConfig } = config as Record<string, unknown> & { abortSignal?: AbortSignal };
  return {
    config: Object.keys(serializableConfig).length ? serializableConfig : undefined,
    signal: abortSignal,
  };
};

const asRequest = (request: unknown): Record<string, unknown> => (
  request && typeof request === 'object' && !Array.isArray(request)
    ? request as Record<string, unknown>
    : {}
);

const createModelRequiredError = () => {
  const error = new Error('A managed Live session requires a model.') as Error & { status: number; code: string };
  error.status = 400;
  error.code = 'MODEL_REQUIRED';
  return error;
};

const createLiveOpenReasonRequiredError = () => {
  const error = new Error('A valid, auditable Gemini Live open reason is required.') as Error & { status: number; code: string };
  error.status = 400;
  error.code = 'LIVE_OPEN_REASON_REQUIRED';
  return error;
};

const splitLiveConnectRequest = (rawRequest: unknown): {
  providerRequest: Record<string, unknown>;
  liveOpenReason: LiveOpenReason;
} => {
  const request = asRequest(rawRequest);
  let liveOpenReason: LiveOpenReason;
  try {
    liveOpenReason = requireLiveOpenReason(request.liveOpenReason);
  } catch {
    throw createLiveOpenReasonRequiredError();
  }
  const { liveOpenReason: _clientOnlyReason, ...providerRequest } = request;
  return { providerRequest, liveOpenReason };
};

/** Enforce the same Live-open policy for BYOK while keeping metadata out of the Google SDK request. */
export const createLiveReasonGatedGeminiClient = (client: CoreGeminiClient): CoreGeminiClient => ({
  models: client.models,
  live: {
    connect: async rawRequest => {
      const { providerRequest } = splitLiveConnectRequest(rawRequest);
      return client.live.connect(providerRequest as CoreLiveConnectRequest);
    },
    music: client.live.music,
  },
});

export const createManagedGeminiClient = (
  backend: ManagedGeminiBackendPort,
  options: ManagedGeminiClientOptions = {},
): CoreGeminiClient => {
  const apiVersion = options.apiVersion || 'v1alpha';
  const createTokenClient = options.createTokenClient || ((token: string, version: string) => (
    new GoogleGenAI({ apiKey: token, apiVersion: version }) as unknown as CoreGeminiClient
  ));
  const warn = options.warn || ((message: string, error: unknown) => console.warn(message, error));

  const releaseLease = (leaseId: string) => {
    let released = false;
    return async () => {
      if (released) return;
      released = true;
      try {
        await backend.releaseLiveTokenLease({ leaseId });
      } catch (error) {
        warn('[managed-ai] Failed to release a live-token lease.', error);
      }
    };
  };

  const connectManagedLive = async (rawRequest: unknown) => {
    const { providerRequest: request, liveOpenReason } = splitLiveConnectRequest(rawRequest);
    const model = typeof request.model === 'string' ? request.model.trim() : '';
    if (!model) throw createModelRequiredError();
    const config = request.config && typeof request.config === 'object' && !Array.isArray(request.config)
      ? request.config as Record<string, unknown>
      : undefined;
    const tokenLease = await backend.createLiveToken({
      purpose: 'live',
      model,
      liveOpenReason,
      ...(config ? { config } : {}),
    });
    const release = releaseLease(tokenLease.leaseId);
    const callbacks = request.callbacks && typeof request.callbacks === 'object'
      ? request.callbacks as Record<string, (...args: unknown[]) => unknown>
      : {};
    const wrappedRequest = {
      ...request,
      callbacks: {
        ...callbacks,
        onclose: (event: unknown) => {
          void release();
          callbacks.onclose?.(event);
        },
        onerror: (event: unknown) => callbacks.onerror?.(event),
      },
    };
    const tokenClient = createTokenClient(tokenLease.token, apiVersion);
    try {
      const session = await tokenClient.live.connect(wrappedRequest as any);
      const closeable = session as { close?: () => unknown } | null | undefined;
      const close = typeof closeable?.close === 'function' ? closeable.close.bind(closeable) : null;
      if (closeable && close) {
        closeable.close = () => {
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
      generateContent: async rawRequest => {
        const request = asRequest(rawRequest);
        const { config, signal } = splitManagedConfig(request.config);
        return backend.generateContent({
          model: String(request.model || ''),
          contents: request.contents,
          ...(config ? { config } : {}),
        }, signal);
      },
      generateContentStream: async rawRequest => {
        const request = asRequest(rawRequest);
        const { config, signal } = splitManagedConfig(request.config);
        return backend.generateContentStream({
          model: String(request.model || ''),
          contents: request.contents,
          ...(config ? { config } : {}),
        }, signal);
      },
    },
    live: {
      connect: request => connectManagedLive(request),
      music: {
        connect: async () => {
          throw new Error('Managed music generation must use the authenticated backend music route.');
        },
      },
    },
  };
};
