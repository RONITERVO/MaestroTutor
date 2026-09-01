// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { GoogleGenAI } from '@google/genai';
import { getApiKeyOrThrow } from '../../core/security/apiKeyStorage';
import { maestroAccessService } from '../../services/access/maestroAccessService';
import { maestroBackendService } from '../../services/backend/maestroBackendService';
import {
  createManagedGeminiClient,
  type CoreGeminiClient,
} from '../../core-sdk/managedGeminiClient';
import { ApiError } from '../../core-sdk/errors';

export { ApiError } from '../../core-sdk/errors';

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

export type MaestroGeminiClient = CoreGeminiClient;

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
    return createManagedGeminiClient(maestroBackendService, options);
  }
  throw new ApiError('Sign in for managed access or add a Gemini API key.', {
    status: 401,
    code: 'MISSING_ACCESS',
  });
};
