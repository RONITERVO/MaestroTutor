// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { CoreGeminiClient } from '../../core-sdk/managedGeminiClient';
import { createCoreRuntime } from '../../core-sdk/runtime';
import { generateGeminiResponse, translateText } from './generative';
import { generateImage } from './vision';
import { runTutorTextTurn, runMaestroImageGeneration } from './journeys';

const mocks = vi.hoisted(() => ({
  getAi: vi.fn(), trackUsage: vi.fn(), complete: vi.fn(), error: vi.fn(),
}));
vi.mock('./client', () => ({ getAi: mocks.getAi }));
vi.mock('../../shared/utils/costTracker', () => ({ trackGeminiUsage: mocks.trackUsage }));
vi.mock('../../core-sdk/diagnostics', () => ({
  debugLogService: { logRequest: () => ({ complete: mocks.complete, error: mocks.error }) },
}));

function client(text = 'Hola') {
  return {
    models: {
      generateContentStream: vi.fn(async function* () { yield { text }; }),
      generateContent: vi.fn(async () => ({ text: 'Hello' })),
    },
  } as unknown as CoreGeminiClient;
}

describe('browser provider boundary characterization', () => {
  beforeEach(() => vi.resetAllMocks());

  it('keeps supplied clients ahead of browser access selection for all request kinds', async () => {
    const aiClient = client();
    await generateGeminiResponse('model', 'Hola', [], { aiClient });
    await translateText('Hola', 'Spanish', 'English', { aiClient });
    await generateImage({ prompt: 'Hola', aiClient });
    expect(mocks.getAi).not.toHaveBeenCalled();
    expect(aiClient.models.generateContentStream).toHaveBeenCalledTimes(1);
    expect(aiClient.models.generateContent).toHaveBeenCalledTimes(2);
  });

  it('starts the journey before resolving browser access, then forwards cumulative stream deltas', async () => {
    const trace: string[] = [];
    const aiClient = client();
    vi.mocked(aiClient.models.generateContentStream).mockImplementation(async function* () {
      trace.push('provider');
      yield { text: 'Hola' };
      yield { text: 'Hola mundo' };
    } as any);
    mocks.getAi.mockImplementation(async () => { trace.push('resolve'); return aiClient; });
    const journal = createCoreRuntime().events;
    const runtime = createCoreRuntime({ events: { ...journal, emit: event => { trace.push(event.phase); return journal.emit(event); } } });
    const deltas: string[][] = [];
    const result = await runTutorTextTurn({ model: 'model', prompt: 'Hola', history: [],
      nativeLanguageCode: 'en', systemInstruction: 'system' }, { runtime,
      lifecycleHooks: { onTextDelta: (delta, full) => deltas.push([delta, full]) } });
    expect(trace.slice(0, 5)).toEqual(['turn.started', 'resolve', 'model.attempt-start', 'model.attempt-processing', 'provider']);
    expect(deltas).toEqual([['Hola', 'Hola'], [' mundo', 'Hola mundo']]);
    expect(result.rawResponse).toBe('Hola mundo');
    expect(mocks.getAi).toHaveBeenCalledTimes(1);
  });

  it('resolves access again per image attempt and records usage before completion, including empty images', async () => {
    const trace: string[] = [];
    const aiClient = client();
    vi.mocked(aiClient.models.generateContent)
      .mockResolvedValueOnce({ candidates: [{ content: { parts: [{ text: 'Try again' }] } }], usageMetadata: { totalTokenCount: 3 } } as any)
      .mockResolvedValueOnce({ candidates: [{ content: { parts: [{ inlineData: { mimeType: 'image/png', data: 'aGVsbG8=' } }] } }], usageMetadata: { totalTokenCount: 5 } } as any);
    mocks.getAi.mockImplementation(async () => { trace.push('resolve'); return aiClient; });
    mocks.trackUsage.mockImplementation(() => { trace.push('usage'); });
    mocks.complete.mockImplementation(() => { trace.push('complete'); });
    mocks.error.mockImplementation(() => { trace.push('error'); });
    const runtime = createCoreRuntime({ clock: { ...createCoreRuntime().clock, now: () => 0,
      sleep: async ms => { trace.push(`sleep:${ms}`); } } });
    const result = await runMaestroImageGeneration({ contextText: 'lesson', maxAttempts: 2 }, { runtime });
    expect(trace).toEqual(['resolve', 'usage', 'complete', 'error', 'sleep:1500', 'resolve', 'usage', 'complete']);
    expect(mocks.trackUsage.mock.calls.map(([usage]) => [usage.feature, usage.generatedImages, usage.usageMetadata.totalTokenCount]))
      .toEqual([['image', 0, 3], ['image', 1, 5]]);
    expect(result).toMatchObject({ attempts: 2, base64Image: 'data:image/png;base64,aGVsbG8=', mimeType: 'image/png' });
  });

  it('preserves access errors before provider execution or image retries', async () => {
    const error = new Error('Access unavailable');
    mocks.getAi.mockRejectedValue(error);
    await expect(generateImage({ prompt: 'lesson' })).rejects.toBe(error);
    await expect(translateText('Hola', 'Spanish', 'English')).rejects.toBe(error);
    await expect(runMaestroImageGeneration({ contextText: 'lesson' })).rejects.toBe(error);
    expect(mocks.getAi).toHaveBeenCalledTimes(3);
    expect(mocks.trackUsage).not.toHaveBeenCalled();
  });
});
