// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { GoogleGenAI, ThinkingLevel } from '@google/genai';

const apiKey = process.env.GEMINI_API_KEY?.trim();
if (!apiKey) {
  throw new Error('GEMINI_API_KEY must be present in this process environment.');
}

const model = process.env.MANAGED_GEMINI_SMOKE_MODEL?.trim() || 'gemini-3.7-flash';
const client = new GoogleGenAI({ apiKey });
const request = {
  model,
  contents: 'Return exactly the word: ready',
  config: {
    // Gemini 3 uses output tokens for reasoning before visible text. A tiny
    // ceiling can prove billing while falsely looking like an empty response.
    thinkingConfig: { thinkingLevel: ThinkingLevel.LOW },
    maxOutputTokens: 256,
  },
};

// Gemini Developer API countTokens rejects GenerateContent config. Production
// counts prompt-affecting config separately for reservation and settles from
// provider usage; this smoke proves the content count plus paid generation.
const tokenCount = await client.models.countTokens({ model, contents: request.contents });
const response = await client.models.generateContent(request);
const text = typeof response.text === 'string' ? response.text.trim() : '';
if (!text) {
  throw new Error('Gemini smoke generation returned no text.');
}

console.log(JSON.stringify({
  ok: true,
  model,
  modelVersion: response.modelVersion || null,
  countedPromptTokens: tokenCount.totalTokens ?? tokenCount.tokenCount ?? null,
  promptTokens: response.usageMetadata?.promptTokenCount ?? null,
  outputTokens: response.usageMetadata?.candidatesTokenCount ?? null,
  thinkingTokens: response.usageMetadata?.thoughtsTokenCount ?? null,
}));
