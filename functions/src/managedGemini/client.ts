// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

/** Configured provider client for managed requests. No client credentials cross this boundary. */

import { GoogleGenAI } from '@google/genai';
import { appConfig } from '../config';
import { createHttpError } from '../http';

export const getGeminiClient = (apiVersion?: string): GoogleGenAI => {
  if (!appConfig.geminiApiKey) {
    throw createHttpError(500, 'GEMINI_API_KEY is not configured on the backend.');
  }
  return new GoogleGenAI({ apiKey: appConfig.geminiApiKey, ...(apiVersion ? { apiVersion } : {}) });
};
