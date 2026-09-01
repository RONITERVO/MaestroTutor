// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ThinkingLevel } from '@google/genai';

const mocks = vi.hoisted(() => ({
  getAi: vi.fn(),
  generateContentStream: vi.fn(),
  logRequest: vi.fn(() => ({
    complete: vi.fn(),
    error: vi.fn(),
  })),
}));

vi.mock('./client', async () => {
  const actual = await vi.importActual<typeof import('./client')>('./client');
  return {
    ...actual,
    getAi: mocks.getAi,
  };
});

vi.mock('../../features/diagnostics', () => ({
  debugLogService: {
    logRequest: mocks.logRequest,
  },
}));

import { generateGeminiResponse } from './generative';

const streamWithText = (text: string, modelVersion: string) => ({
  async *[Symbol.asyncIterator]() {
    yield { text, modelVersion, candidates: [], usageMetadata: { totalTokenCount: 1 } };
  },
});

describe('generateGeminiResponse capability fallbacks', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getAi.mockResolvedValue({
      models: {
        generateContentStream: mocks.generateContentStream,
      },
    });
  });

  it('retries without Google Search and reports the capability only after success', async () => {
    const searchQuotaError = Object.assign(new Error(JSON.stringify({
      error: {
        code: 429,
        status: 'RESOURCE_EXHAUSTED',
        message: 'Search is unavailable for this project.',
      },
    })), { status: 429, code: '429' });
    mocks.generateContentStream
      .mockRejectedValueOnce(searchQuotaError)
      .mockResolvedValueOnce(streamWithText('Hola', 'gemini-3.6-flash'));
    const onGoogleSearchUnavailable = vi.fn();

    const response = await generateGeminiResponse(
      'gemini-flash-latest',
      'Hola',
      [],
      { useGoogleSearch: true, onGoogleSearchUnavailable }
    );

    expect(response.text).toBe('Hola');
    expect(mocks.generateContentStream).toHaveBeenCalledTimes(2);
    expect(mocks.generateContentStream.mock.calls[0][0].config.tools).toEqual([{ googleSearch: {} }]);
    expect(mocks.generateContentStream.mock.calls[1][0].config.tools).toBeUndefined();
    expect(onGoogleSearchUnavailable).toHaveBeenCalledTimes(1);
  });

  it('uses the pinned Flash-Lite fallback with maximum thinking after high demand', async () => {
    const highDemandError = Object.assign(new Error('Model is in high demand.'), {
      status: 503,
      code: 'UNAVAILABLE',
    });
    mocks.generateContentStream
      .mockRejectedValueOnce(highDemandError)
      .mockResolvedValueOnce(streamWithText('Listo', 'gemini-3.5-flash-lite'));

    const response = await generateGeminiResponse('gemini-flash-latest', 'Hola', [], {
      configOverrides: {
        thinkingConfig: {
          includeThoughts: true,
          thinkingBudget: 512,
        },
      },
    });

    expect(response.modelUsed).toBe('gemini-3.5-flash-lite');
    expect(mocks.generateContentStream).toHaveBeenCalledTimes(2);
    expect(mocks.generateContentStream.mock.calls[1][0]).toMatchObject({
      model: 'gemini-3.5-flash-lite',
      config: {
        thinkingConfig: {
          includeThoughts: true,
          thinkingLevel: ThinkingLevel.HIGH,
        },
      },
    });
    expect(mocks.generateContentStream.mock.calls[1][0].config.thinkingConfig.thinkingBudget).toBeUndefined();
  });
});
