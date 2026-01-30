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

export const getAi = async () => {
  try {
    const apiKey = await getApiKeyOrThrow();
    return new GoogleGenAI({ apiKey });
  } catch (e: any) {
    const message = e?.message || 'Missing API key';
    throw new ApiError(message, { code: 'MISSING_API_KEY' });
  }
};
