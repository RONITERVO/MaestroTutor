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

export interface LiveConnectAccess {
  apiKey: string;
  isManaged: boolean;
  release: () => Promise<void>;
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

export const getAi = async (options?: { apiVersion?: string }) => {
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

export const resolveLiveConnectAccess = async (options?: {
  purpose?: 'live' | 'music';
  durationSeconds?: number;
}): Promise<LiveConnectAccess> => {
  try {
    return {
      apiKey: await getApiKeyOrThrow(),
      isManaged: false,
      release: async () => {},
    };
  } catch (error: any) {
    if (!maestroBackendService.isConfigured()) {
      const message = error?.message || 'Missing API key';
      throw new ApiError(message, { code: 'MISSING_API_KEY' });
    }

    const isManaged = await maestroAccessService.isUsingManagedAccess();
    if (!isManaged) {
      const message = error?.message || 'Missing API key';
      throw new ApiError(message, { code: 'MISSING_API_KEY' });
    }

    try {
      const token = await maestroBackendService.createLiveToken(options);
      let released = false;
      return {
        apiKey: token.token,
        isManaged: true,
        release: async () => {
          if (released) return;
          released = true;
          if (!token.leaseId) return;
          try {
            await maestroBackendService.releaseLiveTokenLease({ leaseId: token.leaseId });
          } catch {
            // Ignore lease release failures; the backend expiry window remains the fallback.
          }
        },
      };
    } catch (backendError: any) {
      const message = typeof backendError?.message === 'string'
        ? backendError.message
        : 'Managed live mode is temporarily unavailable.';
      throw new ApiError(
        message,
        {
          code: 'MANAGED_LIVE_TOKEN_FAILED',
        }
      );
    }
  }
};

export const resolveLiveConnectApiKey = async (options?: {
  purpose?: 'live' | 'music';
  durationSeconds?: number;
}): Promise<string> => (await resolveLiveConnectAccess(options)).apiKey;
