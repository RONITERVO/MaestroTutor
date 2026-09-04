// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { GoogleGenAI } from '@google/genai';
import type {
  BackendGenerateContentRequest,
  BackendGenerateContentResponse,
  BackendLiveGatewayTicketResponse,
  BackendLiveTokenRequest,
  BackendLiveTokenResponse,
  BackendReleaseLiveTokenLeaseRequest,
  ManagedBillingSummary,
} from '../core/contracts/backend';
import {
  LIVE_GATEWAY_CONNECT_TIMEOUT_MS,
  type LiveGatewayClientMessage,
  type LiveGatewayServerMessage,
} from '../../shared/liveGatewayProtocol';
import {
  requireLiveOpenReason,
  type LiveOpenReason,
} from '../../shared/liveOpenReason';

export interface CoreLiveConnectRequest extends Record<string, unknown> {
  turnTiming?: { mark(name: string): void; linkGateway(sessionId: string): void };
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
  createLiveGatewayTicket(payload: BackendLiveTokenRequest): Promise<BackendLiveGatewayTicketResponse>;
  acceptLiveGatewayBillingSummary?(billingSummary: ManagedBillingSummary): Promise<void> | void;
  /** Diagnostic-only migration escape hatch; production uses the metered gateway. */
  createLiveToken?(payload: BackendLiveTokenRequest): Promise<BackendLiveTokenResponse>;
  /** Diagnostic-only migration escape hatch; production uses the metered gateway. */
  releaseLiveTokenLease?(payload: BackendReleaseLiveTokenLeaseRequest): Promise<unknown>;
}

export interface ManagedGatewaySocket {
  readonly readyState: number;
  send(data: string): void;
  close(code?: number, reason?: string): void;
  addEventListener(type: 'open' | 'message' | 'error' | 'close', listener: (event: any) => void): void;
  removeEventListener(type: 'open' | 'message' | 'error' | 'close', listener: (event: any) => void): void;
}

export interface ManagedGeminiClientOptions {
  /** The legacy transport exists only for the provider diagnostic script. */
  transport?: 'gateway' | 'legacy-ephemeral';
  apiVersion?: string;
  warn?: (message: string, error: unknown) => void;
  createTokenClient?: (token: string, apiVersion: string) => CoreGeminiClient;
  createGatewaySocket?: (url: string) => ManagedGatewaySocket;
  gatewayConnectTimeoutMs?: number;
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

const createCodedError = (message: string, status: number, code: string) => {
  const error = new Error(message) as Error & { status: number; code: string };
  error.status = status;
  error.code = code;
  return error;
};

const createModelRequiredError = () => createCodedError(
  'A managed Live session requires a model.',
  400,
  'MODEL_REQUIRED',
);

const createLiveOpenReasonRequiredError = () => createCodedError(
  'A valid, auditable Gemini Live open reason is required.',
  400,
  'LIVE_OPEN_REASON_REQUIRED',
);

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
  const { liveOpenReason: _clientOnlyReason, turnTiming: _clientOnlyTiming, ...providerRequest } = request;
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

const requireLiveRequest = (rawRequest: unknown) => {
  const { providerRequest: request, liveOpenReason } = splitLiveConnectRequest(rawRequest);
  const model = typeof request.model === 'string' ? request.model.trim() : '';
  if (!model) throw createModelRequiredError();
  const config = request.config && typeof request.config === 'object' && !Array.isArray(request.config)
    ? request.config as Record<string, unknown>
    : undefined;
  const callbacks = request.callbacks && typeof request.callbacks === 'object'
    ? request.callbacks as Record<string, (...args: unknown[]) => unknown>
    : {};
  return { request, model, config, callbacks, liveOpenReason };
};

const parseGatewayMessage = async (data: unknown): Promise<LiveGatewayServerMessage> => {
  let text: string;
  if (typeof data === 'string') {
    text = data;
  } else if (typeof Blob !== 'undefined' && data instanceof Blob) {
    text = await data.text();
  } else if (data instanceof ArrayBuffer) {
    text = new TextDecoder().decode(data);
  } else if (ArrayBuffer.isView(data)) {
    text = new TextDecoder().decode(data);
  } else {
    throw createCodedError('Managed Live gateway returned a non-JSON message.', 502, 'LIVE_GATEWAY_PROTOCOL');
  }
  const parsed = JSON.parse(text) as unknown;
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed) || typeof (parsed as { type?: unknown }).type !== 'string') {
    throw createCodedError('Managed Live gateway returned an invalid message.', 502, 'LIVE_GATEWAY_PROTOCOL');
  }
  return parsed as LiveGatewayServerMessage;
};

const assertGatewayUrl = (rawUrl: string): string => {
  let url: URL;
  try {
    url = new URL(rawUrl);
  } catch {
    throw createCodedError('Managed Live gateway returned an invalid WebSocket URL.', 502, 'LIVE_GATEWAY_URL');
  }
  if (url.protocol !== 'wss:' || url.username || url.password || url.search || url.hash) {
    throw createCodedError('Managed Live gateway requires a clean secure WebSocket URL.', 502, 'LIVE_GATEWAY_URL');
  }
  return url.toString();
};

const defaultGatewaySocket = (url: string): ManagedGatewaySocket => {
  if (typeof WebSocket === 'undefined') {
    throw createCodedError('This runtime does not provide WebSocket support.', 500, 'LIVE_GATEWAY_UNSUPPORTED');
  }
  return new WebSocket(url) as unknown as ManagedGatewaySocket;
};

const gatewayError = (message: string, code = 'LIVE_GATEWAY_CONNECTION') => (
  createCodedError(message, 502, code)
);

const createManagedGatewaySession = async (params: {
  backend: ManagedGeminiBackendPort;
  ticketLease: BackendLiveGatewayTicketResponse;
  callbacks: Record<string, (...args: unknown[]) => unknown>;
  createSocket: (url: string) => ManagedGatewaySocket;
  connectTimeoutMs: number;
  timing?: CoreLiveConnectRequest['turnTiming'];
  warn: (message: string, error: unknown) => void;
}): Promise<Record<string, (...args: any[]) => unknown>> => {
  const gatewayUrl = assertGatewayUrl(params.ticketLease.gatewayUrl);
  const socket = params.createSocket(gatewayUrl);
  let ready = false;
  let closed = false;
  let closeRequested = false;
  let closeFallback: ReturnType<typeof globalThis.setTimeout> | null = null;
  let inbound = Promise.resolve();

  const invoke = async (name: string, ...args: unknown[]) => {
    try {
      await params.callbacks[name]?.(...args);
    } catch (error) {
      params.warn(`[managed-ai] Live callback ${name} failed.`, error);
    }
  };

  const send = (message: LiveGatewayClientMessage) => {
    if (closed) throw gatewayError('Managed Live gateway session is closed.', 'LIVE_GATEWAY_CLOSED');
    socket.send(JSON.stringify(message));
  };

  const session = {
    sendRealtimeInput: (input: Record<string, unknown>) => send({ type: 'realtimeInput', input }),
    sendClientContent: (input: Record<string, unknown>) => send({ type: 'clientContent', input }),
    sendToolResponse: (input: Record<string, unknown>) => send({ type: 'toolResponse', input }),
    close: () => {
      if (closed || closeRequested) return;
      closeRequested = true;
      try {
        send({ type: 'close' });
        closeFallback = globalThis.setTimeout(() => socket.close(1000, 'client-close-timeout'), 2_000);
      } catch {
        socket.close(1000, 'client-close');
      }
    },
  };

  return new Promise((resolve, reject) => {
    let completed = false;
    const timeoutId = globalThis.setTimeout(() => {
      if (completed) return;
      completed = true;
      socket.close(4000, 'connect-timeout');
      reject(gatewayError('Managed Live gateway connection timed out.', 'LIVE_GATEWAY_TIMEOUT'));
    }, Math.max(1_000, params.connectTimeoutMs));

    const cleanup = () => {
      globalThis.clearTimeout(timeoutId);
      if (closeFallback) globalThis.clearTimeout(closeFallback);
      socket.removeEventListener('open', onOpen);
      socket.removeEventListener('message', onMessage);
      socket.removeEventListener('error', onError);
      socket.removeEventListener('close', onClose);
    };

    const rejectBeforeReady = (error: Error) => {
      if (completed) return;
      completed = true;
      cleanup();
      try { socket.close(4002, 'connection-failed'); } catch { /* already closed */ }
      reject(error);
    };

    const handleMessage = async (event: { data?: unknown }) => {
      const message = await parseGatewayMessage(event.data);
      if (message.type === 'ready') {
        params.timing?.linkGateway(message.sessionId);
        if (ready) throw gatewayError('Managed Live gateway sent duplicate readiness.', 'LIVE_GATEWAY_PROTOCOL');
        ready = true;
        completed = true;
        globalThis.clearTimeout(timeoutId);
        await invoke('onopen');
        resolve(session);
        return;
      }
      if (message.type === 'providerMessage') {
        if (!ready) throw gatewayError('Managed Live gateway sent provider data before readiness.', 'LIVE_GATEWAY_PROTOCOL');
        await invoke('onmessage', message.message);
        return;
      }
      if (message.type === 'billing') {
        if (message.billingSummary) {
          await params.backend.acceptLiveGatewayBillingSummary?.(message.billingSummary);
        }
        return;
      }
      if (message.type === 'error') {
        const error = gatewayError(message.message || 'Managed Live gateway failed.', message.code || 'LIVE_GATEWAY_ERROR');
        if (!ready) {
          rejectBeforeReady(error);
          return;
        }
        await invoke('onerror', error);
        socket.close(1011, 'gateway-error');
        return;
      }
      throw gatewayError('Managed Live gateway returned an unknown message.', 'LIVE_GATEWAY_PROTOCOL');
    };

    function onOpen() {
      try {
        send({ type: 'authenticate', ticket: params.ticketLease.ticket });
      } catch (error) {
        rejectBeforeReady(error instanceof Error ? error : gatewayError(String(error)));
      }
    }

    function onMessage(event: { data?: unknown }) {
      inbound = inbound.then(() => handleMessage(event)).catch(async (error: unknown) => {
        const normalized = error instanceof Error ? error : gatewayError(String(error));
        if (!ready) {
          rejectBeforeReady(normalized);
          return;
        }
        await invoke('onerror', normalized);
        socket.close(1002, 'protocol-error');
      });
    }

    function onError(event: unknown) {
      const error = gatewayError('Managed Live gateway WebSocket failed.');
      if (!ready) {
        rejectBeforeReady(error);
      } else {
        void invoke('onerror', event || error);
      }
    }

    function onClose(event: unknown) {
      if (closed) return;
      closed = true;
      const wasReady = ready;
      if (!wasReady) {
        rejectBeforeReady(gatewayError('Managed Live gateway closed before it was ready.'));
        return;
      }
      cleanup();
      void inbound.finally(() => invoke('onclose', event));
    }

    socket.addEventListener('open', onOpen);
    socket.addEventListener('message', onMessage);
    socket.addEventListener('error', onError);
    socket.addEventListener('close', onClose);
  });
};

const connectLegacyManagedLive = async (params: {
  backend: ManagedGeminiBackendPort;
  rawRequest: unknown;
  apiVersion: string;
  createTokenClient: (token: string, apiVersion: string) => CoreGeminiClient;
  warn: (message: string, error: unknown) => void;
}) => {
  if (!params.backend.createLiveToken || !params.backend.releaseLiveTokenLease) {
    throw createCodedError('Legacy managed Live transport is unavailable.', 500, 'LEGACY_LIVE_UNAVAILABLE');
  }
  const { request, model, config, callbacks, liveOpenReason } = requireLiveRequest(params.rawRequest);
  const tokenLease = await params.backend.createLiveToken({
    purpose: 'live',
    model,
    liveOpenReason,
    ...(config ? { config } : {}),
  });
  let released = false;
  const release = async () => {
    if (released) return;
    released = true;
    try {
      await params.backend.releaseLiveTokenLease!({ leaseId: tokenLease.leaseId });
    } catch (error) {
      params.warn('[managed-ai] Failed to release a live-token lease.', error);
    }
  };
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
  const tokenClient = params.createTokenClient(tokenLease.token, params.apiVersion);
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

export const createManagedGeminiClient = (
  backend: ManagedGeminiBackendPort,
  options: ManagedGeminiClientOptions = {},
): CoreGeminiClient => {
  const apiVersion = options.apiVersion || 'v1alpha';
  const createTokenClient = options.createTokenClient || ((token: string, version: string) => (
    new GoogleGenAI({ apiKey: token, apiVersion: version }) as unknown as CoreGeminiClient
  ));
  const createSocket = options.createGatewaySocket || defaultGatewaySocket;
  const warn = options.warn || ((message: string, error: unknown) => console.warn(message, error));

  const connectManagedLive = async (rawRequest: unknown) => {
    if (options.transport === 'legacy-ephemeral') {
      return connectLegacyManagedLive({ backend, rawRequest, apiVersion, createTokenClient, warn });
    }
    // Reject before reserving credits when the runtime cannot possibly open
    // the managed transport. Injected sockets cover deterministic tests and
    // non-browser hosts; modern supported Node and browsers expose WebSocket.
    if (!options.createGatewaySocket && typeof WebSocket === 'undefined') {
      throw createCodedError('This runtime does not provide WebSocket support.', 500, 'LIVE_GATEWAY_UNSUPPORTED');
    }
    const { model, config, callbacks, liveOpenReason } = requireLiveRequest(rawRequest);
    const timing = asRequest(rawRequest).turnTiming as CoreLiveConnectRequest['turnTiming'];
    timing?.mark('gateway.ticket-start');
    const ticketLease = await backend.createLiveGatewayTicket({
      purpose: 'live',
      model,
      liveOpenReason,
      ...(config ? { config } : {}),
    });
    timing?.mark('gateway.ticket-ready');
    return createManagedGatewaySession({
      timing,
      backend,
      ticketLease,
      callbacks,
      createSocket,
      connectTimeoutMs: options.gatewayConnectTimeoutMs || LIVE_GATEWAY_CONNECT_TIMEOUT_MS,
      warn,
    });
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
