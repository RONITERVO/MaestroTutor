// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { MAESTRO_INTEGRATION_CONFIG, isBackendConfigured } from '../../core/config/integrations';
import type {
  BackendClearFilesResponse,
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
  getManagedAccessSessionOrThrow,
  loadManagedAccessSession,
  saveManagedAccessSession,
} from '../../core/security/managedAccessSessionStorage';
import { firebaseAuthBridgeService } from '../auth/firebaseAuthBridgeService';
import { maestroFirebaseService } from '../firebase/maestroFirebaseService';
import { ServiceNotConfiguredError } from '../shared/serviceErrors';

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

const safeParseJson = (text: string): unknown => {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
};

const readJson = async <T>(response: Response): Promise<T> => {
  const text = await response.text();
  const payload = text ? safeParseJson(text) : null;
  if (!response.ok) {
    const record = payload && typeof payload === 'object' ? payload as Record<string, unknown> : null;
    const message =
      (record && typeof record.error === 'string' && record.error) ||
      (record && typeof record.message === 'string' && record.message) ||
      `Backend request failed with status ${response.status}`;
    throw new Error(message);
  }
  return payload as T;
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

const getManagedHeaders = async (): Promise<Record<string, string>> => {
  const session = await getManagedAccessSessionOrThrow();
  const identity = await firebaseAuthBridgeService.getCurrentIdentity(false);
  const token = identity?.firebaseIdToken || session.firebaseIdToken;

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

const requestJson = async <T>(path: string, init?: RequestInit): Promise<T> => {
  const headers = new Headers(init?.headers || {});
  if (!headers.has('Accept')) headers.set('Accept', 'application/json');
  if (init?.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }
  const response = await fetch(buildUrl(path), {
    ...init,
    headers,
  });
  return readJson<T>(response);
};

const requestManagedJson = async <T>(path: string, init?: RequestInit): Promise<T> => {
  const authHeaders = await getManagedHeaders();
  return requestJson<T>(path, {
    ...init,
    headers: {
      ...authHeaders,
      ...(init?.headers || {}),
    },
  });
};

export const maestroBackendService = {
  isConfigured: isBackendConfigured,

  requestManagedJson,

  requestManagedStream: async (path: string, body: unknown): Promise<Response> => {
    const authHeaders = await getManagedHeaders();
    const response = await fetch(buildUrl(path), {
      method: 'POST',
      headers: {
        Accept: 'application/x-ndjson, application/json',
        'Content-Type': 'application/json',
        ...authHeaders,
      },
      body: JSON.stringify(body),
    });

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
    payload: BackendGenerateContentRequest
  ): Promise<BackendGenerateContentResponse> => {
    const response = await requestManagedJson<BackendGenerateContentResponse>('gemini/generate-content', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    await updateStoredSession({ billingSummary: response.billingSummary || null });
    return response;
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
} as const;
