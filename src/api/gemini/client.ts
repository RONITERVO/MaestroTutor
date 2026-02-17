// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { GoogleGenAI } from '@google/genai';
import { getApiKeyOrThrow } from '../../core/security/apiKeyStorage';

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

export const getAi = async () => {
  try {
    const apiKey = await getApiKeyOrThrow();
    return new GoogleGenAI({ apiKey });
  } catch (e: any) {
    const message = e?.message || 'Missing API key';
    throw new ApiError(message, { code: 'MISSING_API_KEY' });
  }
};
