// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { MAESTRO_INTEGRATION_CONFIG, isBackendConfigured } from '../../core/config/integrations';
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
  BackendReleaseLiveTokenLeaseRequest,
  BackendReleaseLiveTokenLeaseResponse,
  BackendLiveTokenRequest,
  BackendLiveTokenResponse,
  BackendMediaUploadRequest,
  BackendMediaUploadResponse,
  ManagedAccountSummaryResponse,
  ManagedBillingLedgerResponse,
  ManagedBillingSummary,
  ManagedSessionResponse,
  ManagedUsageLedgerResponse,
} from '../../core/contracts/backend';
import type {
  EntitlementRecord,
  VerifyGooglePlayPurchaseRequest,
  VerifyGooglePlayPurchaseResult,
} from '../../core/contracts/integrations';
import {
  loadManagedAccessSession,
  saveManagedAccessSession,
} from '../../core/security/managedAccessSessionStorage';
import { firebaseAuthBridgeService } from '../auth/firebaseAuthBridgeService';
import { maestroFirebaseService } from '../firebase/maestroFirebaseService';
import { ServiceHttpError, ServiceNotConfiguredError } from '../shared/serviceErrors';

const DEFAULT_BILLING_SUMMARY: ManagedBillingSummary = {
  availableCredits: 0,
  reservedCredits: 0,
  lifetimePurchasedCredits: 0,
  lifetimeSpentCredits: 0,
  lifetimeSpentUsd: 0,
  updatedAt: null,
  lastPurchaseAt: null,
  lastChargeAt: null,
  lastProductId: null,
};

const BACKEND_REQUEST_TIMEOUT_MS = 120_000;
// Cloud Functions stops generation at 540s; leave room for its final response
// to cross the network instead of aborting the client at the same instant.
const BACKEND_GENERATION_TIMEOUT_MS = 555_000;
const BACKEND_STREAM_CONNECT_TIMEOUT_MS = 60_000;

const ensureBackendBaseUrl = (): string => {
  const baseUrl = MAESTRO_INTEGRATION_CONFIG.backendBaseUrl;
  if (!baseUrl) {
    throw new ServiceNotConfiguredError(
      'backend',
      'Managed backend is not configured. Set VITE_BACKEND_BASE_URL before enabling managed access.'
    );
  }
  return baseUrl.endsWith('/') ? baseUrl : `${baseUrl}/`;
};

const buildUrl = (path: string): string => new URL(path.replace(/^\/+/, ''), ensureBackendBaseUrl()).toString();

const safeParseJson = (text: string): { ok: true; value: unknown } | { ok: false } => {
  try {
    return { ok: true, value: JSON.parse(text) };
  } catch {
    return { ok: false };
  }
};

const readJson = async <T>(response: Response): Promise<T> => {
  const text = await response.text();
  const parsed = text.trim() ? safeParseJson(text) : { ok: false } as const;
  if (!response.ok) {
    const payload = parsed.ok ? parsed.value : null;
    const record = payload && typeof payload === 'object' ? payload as Record<string, unknown> : null;
    const message =
      (record && typeof record.error === 'string' && record.error) ||
      (record && typeof record.message === 'string' && record.message) ||
      `Backend request failed with status ${response.status}`;
    const code = record && typeof record.code === 'string' ? record.code : undefined;
    throw new ServiceHttpError(message, response.status, code);
  }
  if (!text.trim()) {
    throw new Error('Backend returned an empty response for a successful request.');
  }
  if (!parsed.ok) {
    throw new Error('Backend returned invalid JSON for a successful request.');
  }
  return parsed.value as T;
};

const withRequestTimeout = (
  signal?: AbortSignal | null,
  timeoutMs = BACKEND_REQUEST_TIMEOUT_MS,
): AbortSignal => {
  const timeoutSignal = AbortSignal.timeout(timeoutMs);
  return signal ? AbortSignal.any([signal, timeoutSignal]) : timeoutSignal;
};

const updateStoredSession = async (updates: {
  billingSummary?: ManagedBillingSummary | null;
  entitlements?: EntitlementRecord[] | null;
}) => {
  const currentSession = await loadManagedAccessSession();
  if (!currentSession) return;
  await saveManagedAccessSession({
    ...currentSession,
    billingSummary: updates.billingSummary || currentSession.billingSummary,
    entitlements: updates.entitlements || currentSession.entitlements,
    lastSyncedAt: Date.now(),
  });
};

type ManagedStreamEvent =
  | { type: 'chunk'; chunk: unknown }
  | { type: 'final'; result?: BackendGenerateContentResponse }
  | { type: 'error'; message?: string; status?: number; code?: string };

export const readManagedGenerationStream = async function* (response: Response): AsyncGenerator<unknown> {
  if (!response.body) {
    throw new Error('Managed backend returned a streaming response without a body.');
  }

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
      await updateStoredSession({ billingSummary: event.result?.billingSummary || null });
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
    if (!sawFinal) {
      throw new Error('Managed generation stream ended before its final accounting event.');
    }
  } finally {
    if (!bodyCompleted) {
      await reader.cancel('Managed stream consumer stopped.').catch(() => undefined);
    }
    reader.releaseLock();
  }
};

const getOptionalHeaders = async (): Promise<Record<string, string>> => {
  const headers: Record<string, string> = {};
  const session = await loadManagedAccessSession();

  if (session) {
    const identity = await firebaseAuthBridgeService.getCurrentIdentity(false);
    const token = identity?.firebaseIdToken || session.firebaseIdToken;
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    if (identity && identity.firebaseIdToken !== session.firebaseIdToken) {
      await saveManagedAccessSession({
        ...session,
        user: identity.user,
        firebaseIdToken: identity.firebaseIdToken,
        refreshToken: identity.refreshToken,
        expiresAt: identity.expiresAt,
        lastSyncedAt: Date.now(),
      });
    }
  }

  const appCheckToken = await maestroFirebaseService.getAppCheckToken(false);
  if (appCheckToken) {
    headers['X-Firebase-AppCheck'] = appCheckToken;
  }

  return headers;
};

const getManagedHeaders = async (): Promise<Record<string, string>> => {
  const session = await loadManagedAccessSession();
  if (!session?.user?.id) {
    throw new Error('Managed access session is missing.');
  }
  const identity = await firebaseAuthBridgeService.getCurrentIdentity(false);
  const token = identity?.firebaseIdToken || session.firebaseIdToken;
  if (!token) {
    throw new Error('Managed access session is missing.');
  }

  if (identity && identity.firebaseIdToken !== session.firebaseIdToken) {
    await saveManagedAccessSession({
      ...session,
      user: identity.user,
      firebaseIdToken: identity.firebaseIdToken,
      refreshToken: identity.refreshToken,
      expiresAt: identity.expiresAt,
      lastSyncedAt: Date.now(),
    });
  }

  const headers: Record<string, string> = {
    Authorization: `Bearer ${token}`,
  };

  const appCheckToken = await maestroFirebaseService.getAppCheckToken(false);
  if (appCheckToken) {
    headers['X-Firebase-AppCheck'] = appCheckToken;
  }

  return headers;
};

const requestJson = async <T>(
  path: string,
  init?: RequestInit,
  timeoutMs = BACKEND_REQUEST_TIMEOUT_MS,
): Promise<T> => {
  const headers = new Headers(init?.headers || {});
  if (!headers.has('Accept')) headers.set('Accept', 'application/json');
  if (init?.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }
  const response = await fetch(buildUrl(path), {
    ...init,
    headers,
    signal: withRequestTimeout(init?.signal, timeoutMs),
  });
  return readJson<T>(response);
};

const requestManagedJson = async <T>(
  path: string,
  init?: RequestInit,
  timeoutMs = BACKEND_REQUEST_TIMEOUT_MS,
): Promise<T> => {
  const authHeaders = await getManagedHeaders();
  return requestJson<T>(path, {
    ...init,
    headers: {
      ...authHeaders,
      ...(init?.headers || {}),
    },
  }, timeoutMs);
};

const requestOptionalAuthJson = async <T>(path: string, init?: RequestInit): Promise<T> => {
  const headers = await getOptionalHeaders();
  return requestJson<T>(path, {
    ...init,
    headers: {
      ...headers,
      ...(init?.headers || {}),
    },
  });
};

export const maestroBackendService = {
  isConfigured: isBackendConfigured,

  /**
   * Ask the backend to open a Stripe Checkout session for a credit pack.
   *
   * Returns a URL to send the browser to. Nothing is granted here: the credits
   * arrive when Stripe calls the webhook, which is the only signal that the
   * payment actually settled. The redirect back is presentation only.
   */
  createStripeCheckoutSession: async (packId: string): Promise<{ url: string; sessionId: string }> => (
    requestManagedJson<{ url: string; sessionId: string }>('/billing/stripe/checkout', {
      method: 'POST',
      body: JSON.stringify({ packId }),
    })
  ),

  requestManagedJson,

  requestManagedStream: async (
    path: string,
    body: unknown,
    signal?: AbortSignal | null,
  ): Promise<Response> => {
    const authHeaders = await getManagedHeaders();
    const connectionController = new AbortController();
    const timeoutId = globalThis.setTimeout(
      () => connectionController.abort(new Error('Backend stream connection timed out.')),
      BACKEND_STREAM_CONNECT_TIMEOUT_MS,
    );
    let response: Response;
    try {
      response = await fetch(buildUrl(path), {
        method: 'POST',
        headers: {
          Accept: 'application/x-ndjson, application/json',
          'Content-Type': 'application/json',
          ...authHeaders,
        },
        body: JSON.stringify(body),
        signal: signal
          ? AbortSignal.any([signal, connectionController.signal])
          : connectionController.signal,
      });
    } finally {
      globalThis.clearTimeout(timeoutId);
    }

    if (!response.ok) {
      throw new Error((await readJson<{ error?: string; message?: string }>(response)).error || `Backend request failed with status ${response.status}`);
    }

    return response;
  },

  getManagedSession: async (): Promise<ManagedSessionResponse> => {
    const response = await requestManagedJson<ManagedSessionResponse>('auth/session', {
      method: 'GET',
    });
    await updateStoredSession({
      billingSummary: response.session.billingSummary || DEFAULT_BILLING_SUMMARY,
      entitlements: response.session.entitlements,
    });
    return response;
  },

  getAccountSummary: async (): Promise<ManagedAccountSummaryResponse> => {
    const response = await requestManagedJson<ManagedAccountSummaryResponse>('account/summary', {
      method: 'GET',
    });
    await updateStoredSession({
      billingSummary: response.account.billingSummary,
      entitlements: response.account.entitlements,
    });
    return response;
  },

  listUsageLedger: async (limit = 50): Promise<ManagedUsageLedgerResponse> => (
    requestManagedJson<ManagedUsageLedgerResponse>(`account/usage-ledger?limit=${Math.max(1, Math.min(200, Math.floor(limit)))}`, {
      method: 'GET',
    })
  ),

  listBillingLedger: async (limit = 50): Promise<ManagedBillingLedgerResponse> => (
    requestManagedJson<ManagedBillingLedgerResponse>(`account/billing-ledger?limit=${Math.max(1, Math.min(200, Math.floor(limit)))}`, {
      method: 'GET',
    })
  ),

  deleteManagedAccount: async (): Promise<BackendDeleteManagedAccountResponse> => (
    requestManagedJson<BackendDeleteManagedAccountResponse>('account/delete', {
      method: 'POST',
    })
  ),

  verifyGooglePlayPurchase: async (
    payload: VerifyGooglePlayPurchaseRequest
  ): Promise<VerifyGooglePlayPurchaseResult> => {
    const response = await requestManagedJson<VerifyGooglePlayPurchaseResult>('billing/google-play/verify', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    await updateStoredSession({
      billingSummary: response.billingSummary,
      entitlements: response.entitlements,
    });
    return response;
  },

  generateContent: async (
    payload: BackendGenerateContentRequest,
    signal?: AbortSignal | null,
  ): Promise<BackendGenerateContentResponse> => {
    const response = await requestManagedJson<BackendGenerateContentResponse>('gemini/generate-content', {
      method: 'POST',
      body: JSON.stringify(payload),
      signal: signal || undefined,
    }, BACKEND_GENERATION_TIMEOUT_MS);
    await updateStoredSession({ billingSummary: response.billingSummary || null });
    return response;
  },

  generateContentStream: async (
    payload: BackendGenerateContentRequest,
    signal?: AbortSignal | null,
  ): Promise<AsyncIterable<unknown>> => {
    const response = await maestroBackendService.requestManagedStream(
      'gemini/generate-content-stream',
      payload,
      signal,
    );
    return readManagedGenerationStream(response);
  },

  uploadMedia: async (
    payload: BackendMediaUploadRequest
  ): Promise<BackendMediaUploadResponse> => {
    const response = await requestManagedJson<BackendMediaUploadResponse>('gemini/upload-media', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    await updateStoredSession({ billingSummary: response.billingSummary || null });
    return response;
  },

  checkFileStatuses: async (
    payload: BackendFileStatusesRequest
  ): Promise<BackendFileStatusesResponse> => (
    requestManagedJson<BackendFileStatusesResponse>('gemini/file-statuses', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  ),

  deleteFile: async (
    payload: BackendDeleteFileRequest
  ): Promise<BackendDeleteFileResponse> => (
    requestManagedJson<BackendDeleteFileResponse>('gemini/delete-file', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  ),

  clearFiles: async (): Promise<BackendClearFilesResponse> => (
    requestManagedJson<BackendClearFilesResponse>('gemini/clear-files', {
      method: 'POST',
    })
  ),

  createLiveToken: async (
    payload?: BackendLiveTokenRequest
  ): Promise<BackendLiveTokenResponse> => {
    const response = await requestManagedJson<BackendLiveTokenResponse>('gemini/live-token', {
      method: 'POST',
      body: JSON.stringify(payload || {}),
    });
    await updateStoredSession({ billingSummary: response.billingSummary || null });
    return response;
  },

  releaseLiveTokenLease: async (
    payload: BackendReleaseLiveTokenLeaseRequest
  ): Promise<BackendReleaseLiveTokenLeaseResponse> => (
    requestManagedJson<BackendReleaseLiveTokenLeaseResponse>('gemini/live-token/release', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  ),

  submitAiContentReport: async (
    payload: BackendAiContentReportRequest
  ): Promise<BackendAiContentReportResponse> => (
    requestOptionalAuthJson<BackendAiContentReportResponse>('reports/ai-content', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  ),
} as const;
