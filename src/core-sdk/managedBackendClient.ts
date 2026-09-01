// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {
  BackendAiContentReportRequest,
  BackendAiContentReportResponse,
  BackendClearFilesResponse,
  BackendDeleteManagedAccountResponse,
  BackendDeleteFileRequest,
  BackendDeleteFileResponse,
  BackendFileStatusesRequest,
  BackendFileStatusesResponse,
  BackendGenerateContentRequest,
  BackendGenerateContentResponse,
  BackendLiveTokenRequest,
  BackendLiveTokenResponse,
  BackendMediaUploadRequest,
  BackendMediaUploadResponse,
  BackendMusicGenerationRequest,
  BackendMusicGenerationResponse,
  BackendReleaseLiveTokenLeaseRequest,
  BackendReleaseLiveTokenLeaseResponse,
  ManagedAccountSummaryResponse,
  ManagedBillingLedgerResponse,
  ManagedBillingSummary,
  ManagedSessionResponse,
  ManagedUsageLedgerResponse,
} from '../core/contracts/backend';
import type { EntitlementRecord } from '../core/contracts/integrations';
import { ServiceHttpError, ServiceNotConfiguredError } from './errors';

const BACKEND_REQUEST_TIMEOUT_MS = 120_000;
const BACKEND_GENERATION_TIMEOUT_MS = 555_000;
const BACKEND_STREAM_CONNECT_TIMEOUT_MS = 60_000;

export interface ManagedBackendCredentialPort {
  getManagedHeaders(): Promise<Record<string, string>>;
  getOptionalHeaders(): Promise<Record<string, string>>;
}

export interface ManagedBackendSessionPort {
  update(updates: {
    billingSummary?: ManagedBillingSummary | null;
    entitlements?: EntitlementRecord[] | null;
  }): Promise<void>;
}

export interface ManagedBackendClientOptions {
  baseUrl: string;
  credentials: ManagedBackendCredentialPort;
  session: ManagedBackendSessionPort;
  fetch?: typeof globalThis.fetch;
}

type ManagedStreamEvent =
  | { type: 'chunk'; chunk: unknown }
  | { type: 'final'; result?: BackendGenerateContentResponse }
  | { type: 'error'; message?: string; status?: number; code?: string };

const safeParseJson = (text: string): { ok: true; value: unknown } | { ok: false } => {
  try {
    return { ok: true, value: JSON.parse(text) };
  } catch {
    return { ok: false };
  }
};

export const readManagedJson = async <T>(response: Response): Promise<T> => {
  const text = await response.text();
  const parsed = text.trim() ? safeParseJson(text) : { ok: false } as const;
  if (!response.ok) {
    const payload = parsed.ok ? parsed.value : null;
    const record = payload && typeof payload === 'object' ? payload as Record<string, unknown> : null;
    const message =
      (record && typeof record.error === 'string' && record.error)
      || (record && typeof record.message === 'string' && record.message)
      || `Backend request failed with status ${response.status}`;
    const code = record && typeof record.code === 'string' ? record.code : undefined;
    throw new ServiceHttpError(message, response.status, code);
  }
  if (!text.trim()) throw new Error('Backend returned an empty response for a successful request.');
  if (!parsed.ok) throw new Error('Backend returned invalid JSON for a successful request.');
  return parsed.value as T;
};

export const readManagedGenerationStream = async function* (
  response: Response,
  onFinal?: (result?: BackendGenerateContentResponse) => Promise<void> | void,
): AsyncGenerator<unknown> {
  if (!response.body) throw new Error('Managed backend returned a streaming response without a body.');

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  let sawFinal = false;
  let bodyCompleted = false;

  const parseLine = (line: string): ManagedStreamEvent => {
    try {
      return JSON.parse(line) as ManagedStreamEvent;
    } catch {
      throw new Error('Managed backend returned malformed streaming data.');
    }
  };

  const consumeEvent = async (event: ManagedStreamEvent): Promise<unknown | undefined> => {
    if (event.type === 'error') {
      throw new ServiceHttpError(
        event.message || 'Managed generation stream failed.',
        Number.isFinite(event.status) ? Number(event.status) : 500,
        event.code,
      );
    }
    if (event.type === 'final') {
      sawFinal = true;
      await onFinal?.(event.result);
      return undefined;
    }
    if (event.type === 'chunk') return event.chunk;
    throw new Error('Managed backend returned an unknown streaming event.');
  };

  try {
    while (true) {
      const { value, done } = await reader.read();
      buffer += decoder.decode(value || new Uint8Array(), { stream: !done });
      const lines = buffer.split(/\r?\n/);
      buffer = lines.pop() || '';
      for (const rawLine of lines) {
        const line = rawLine.trim();
        if (!line) continue;
        const chunk = await consumeEvent(parseLine(line));
        if (chunk !== undefined) yield chunk;
      }
      if (done) {
        bodyCompleted = true;
        break;
      }
    }

    const finalLine = buffer.trim();
    if (finalLine) {
      const chunk = await consumeEvent(parseLine(finalLine));
      if (chunk !== undefined) yield chunk;
    }
    if (!sawFinal) throw new Error('Managed generation stream ended before its final accounting event.');
  } finally {
    if (!bodyCompleted) await reader.cancel('Managed stream consumer stopped.').catch(() => undefined);
    reader.releaseLock();
  }
};

export const createManagedBackendClient = (options: ManagedBackendClientOptions) => {
  const fetchImpl = options.fetch || globalThis.fetch;
  if (!fetchImpl) throw new Error('A Fetch API implementation is required.');

  const ensureBaseUrl = (): string => {
    const baseUrl = options.baseUrl.trim();
    if (!baseUrl) {
      throw new ServiceNotConfiguredError(
        'backend',
        'Managed backend is not configured. Supply its base URL before enabling managed access.',
      );
    }
    return baseUrl.endsWith('/') ? baseUrl : `${baseUrl}/`;
  };
  const buildUrl = (path: string) => new URL(path.replace(/^\/+/, ''), ensureBaseUrl()).toString();
  const withTimeout = (signal: AbortSignal | null | undefined, timeoutMs: number) => {
    const timeoutSignal = AbortSignal.timeout(timeoutMs);
    return signal ? AbortSignal.any([signal, timeoutSignal]) : timeoutSignal;
  };

  const requestJson = async <T>(path: string, init?: RequestInit, timeoutMs = BACKEND_REQUEST_TIMEOUT_MS) => {
    const headers = new Headers(init?.headers || {});
    if (!headers.has('Accept')) headers.set('Accept', 'application/json');
    const isFormData = typeof FormData !== 'undefined' && init?.body instanceof FormData;
    if (init?.body && !isFormData && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
    const response = await fetchImpl(buildUrl(path), {
      ...init,
      headers,
      signal: withTimeout(init?.signal, timeoutMs),
    });
    return readManagedJson<T>(response);
  };

  const requestManagedJson = async <T>(path: string, init?: RequestInit, timeoutMs = BACKEND_REQUEST_TIMEOUT_MS) => (
    requestJson<T>(path, {
      ...init,
      headers: { ...await options.credentials.getManagedHeaders(), ...(init?.headers || {}) },
    }, timeoutMs)
  );

  const requestOptionalAuthJson = async <T>(path: string, init?: RequestInit) => (
    requestJson<T>(path, {
      ...init,
      headers: { ...await options.credentials.getOptionalHeaders(), ...(init?.headers || {}) },
    })
  );

  const requestManagedStream = async (path: string, body: unknown, signal?: AbortSignal | null) => {
    const connectionController = new AbortController();
    const timeoutId = globalThis.setTimeout(
      () => connectionController.abort(new Error('Backend stream connection timed out.')),
      BACKEND_STREAM_CONNECT_TIMEOUT_MS,
    );
    let response: Response;
    try {
      response = await fetchImpl(buildUrl(path), {
        method: 'POST',
        headers: {
          Accept: 'application/x-ndjson, application/json',
          'Content-Type': 'application/json',
          ...await options.credentials.getManagedHeaders(),
        },
        body: JSON.stringify(body),
        signal: signal ? AbortSignal.any([signal, connectionController.signal]) : connectionController.signal,
      });
    } finally {
      globalThis.clearTimeout(timeoutId);
    }
    if (!response.ok) await readManagedJson(response);
    return response;
  };

  return {
    isConfigured: () => Boolean(options.baseUrl.trim()),
    requestManagedJson,
    requestManagedStream,
    createStripeCheckoutSession: (packId: string) => requestManagedJson<{ url: string; sessionId: string }>(
      '/billing/stripe/checkout', { method: 'POST', body: JSON.stringify({ packId }) },
    ),
    getManagedSession: async (): Promise<ManagedSessionResponse> => {
      const response = await requestManagedJson<ManagedSessionResponse>('auth/session', { method: 'GET' });
      await options.session.update({ billingSummary: response.session.billingSummary, entitlements: response.session.entitlements });
      return response;
    },
    getAccountSummary: async (): Promise<ManagedAccountSummaryResponse> => {
      const response = await requestManagedJson<ManagedAccountSummaryResponse>('account/summary', { method: 'GET' });
      await options.session.update({ billingSummary: response.account.billingSummary, entitlements: response.account.entitlements });
      return response;
    },
    listUsageLedger: (limit = 50): Promise<ManagedUsageLedgerResponse> => requestManagedJson(
      `account/usage-ledger?limit=${Math.max(1, Math.min(200, Math.floor(limit)))}`, { method: 'GET' },
    ),
    listBillingLedger: (limit = 50): Promise<ManagedBillingLedgerResponse> => requestManagedJson(
      `account/billing-ledger?limit=${Math.max(1, Math.min(200, Math.floor(limit)))}`, { method: 'GET' },
    ),
    deleteManagedAccount: (): Promise<BackendDeleteManagedAccountResponse> => requestManagedJson(
      'account/delete', { method: 'POST' },
    ),
    generateContent: async (payload: BackendGenerateContentRequest, signal?: AbortSignal | null): Promise<BackendGenerateContentResponse> => {
      const response = await requestManagedJson<BackendGenerateContentResponse>('gemini/generate-content', {
        method: 'POST', body: JSON.stringify(payload), signal: signal || undefined,
      }, BACKEND_GENERATION_TIMEOUT_MS);
      await options.session.update({ billingSummary: response.billingSummary || null });
      return response;
    },
    generateContentStream: async (payload: BackendGenerateContentRequest, signal?: AbortSignal | null): Promise<AsyncIterable<unknown>> => {
      const response = await requestManagedStream('gemini/generate-content-stream', payload, signal);
      return readManagedGenerationStream(response, result => options.session.update({ billingSummary: result?.billingSummary || null }));
    },
    uploadMedia: async (payload: BackendMediaUploadRequest): Promise<BackendMediaUploadResponse> => {
      const response = await requestManagedJson<BackendMediaUploadResponse>('gemini/upload-media', {
        method: 'POST', body: JSON.stringify(payload),
      });
      await options.session.update({ billingSummary: response.billingSummary || null });
      return response;
    },
    generateMusic: async (
      payload: BackendMusicGenerationRequest,
      signal?: AbortSignal | null,
    ): Promise<BackendMusicGenerationResponse> => {
      const response = await requestManagedJson<BackendMusicGenerationResponse>('gemini/generate-music', {
        method: 'POST', body: JSON.stringify(payload), signal: signal || undefined,
      }, BACKEND_GENERATION_TIMEOUT_MS);
      await options.session.update({ billingSummary: response.billingSummary || null });
      return response;
    },
    checkFileStatuses: (payload: BackendFileStatusesRequest): Promise<BackendFileStatusesResponse> => requestManagedJson(
      'gemini/file-statuses', { method: 'POST', body: JSON.stringify(payload) },
    ),
    deleteFile: (payload: BackendDeleteFileRequest): Promise<BackendDeleteFileResponse> => requestManagedJson(
      'gemini/delete-file', { method: 'POST', body: JSON.stringify(payload) },
    ),
    clearFiles: (): Promise<BackendClearFilesResponse> => requestManagedJson('gemini/clear-files', { method: 'POST' }),
    createLiveToken: async (payload?: BackendLiveTokenRequest): Promise<BackendLiveTokenResponse> => {
      const response = await requestManagedJson<BackendLiveTokenResponse>('gemini/live-token', {
        method: 'POST', body: JSON.stringify(payload || {}),
      });
      await options.session.update({ billingSummary: response.billingSummary || null });
      return response;
    },
    releaseLiveTokenLease: (payload: BackendReleaseLiveTokenLeaseRequest): Promise<BackendReleaseLiveTokenLeaseResponse> => requestManagedJson(
      'gemini/live-token/release', { method: 'POST', body: JSON.stringify(payload) },
    ),
    submitAiContentReport: (payload: BackendAiContentReportRequest): Promise<BackendAiContentReportResponse> => requestOptionalAuthJson(
      'reports/ai-content', { method: 'POST', body: JSON.stringify(payload) },
    ),
  } as const;
};

export type ManagedBackendClient = ReturnType<typeof createManagedBackendClient>;
