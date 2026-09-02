// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  resolveAccessMode: vi.fn(),
  getApiKeyOrThrow: vi.fn(),
  googleGenAi: vi.fn(),
  generateContent: vi.fn(),
  generateContentStream: vi.fn(),
  createLiveToken: vi.fn(),
  releaseLiveTokenLease: vi.fn(),
  directConnect: vi.fn(),
  directClient: {
    models: {},
    live: { connect: vi.fn(), music: { connect: vi.fn() } },
  },
  tokenConnect: vi.fn(),
  tokenMusicConnect: vi.fn(),
}));

vi.mock('@google/genai', () => ({
  GoogleGenAI: mocks.googleGenAi,
}));

vi.mock('../../core/security/apiKeyStorage', () => ({
  getApiKeyOrThrow: mocks.getApiKeyOrThrow,
}));

vi.mock('../../services/access/maestroAccessService', () => ({
  maestroAccessService: {
    resolveAccessMode: mocks.resolveAccessMode,
  },
}));

vi.mock('../../services/backend/maestroBackendService', () => ({
  maestroBackendService: {
    generateContent: mocks.generateContent,
    generateContentStream: mocks.generateContentStream,
    createLiveToken: mocks.createLiveToken,
    releaseLiveTokenLease: mocks.releaseLiveTokenLease,
  },
}));

import { getAi } from './client';
import { createLiveOpenReason, LIVE_OPEN_TRIGGER } from '../../../shared/liveOpenReason';

describe('Gemini provider routing', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getApiKeyOrThrow.mockResolvedValue('byok-key');
    mocks.generateContent.mockResolvedValue({ text: 'managed response' });
    mocks.generateContentStream.mockResolvedValue({
      async *[Symbol.asyncIterator]() {
        yield { text: 'managed chunk' };
      },
    });
    mocks.createLiveToken.mockResolvedValue({
      leaseId: 'lease-1',
      token: 'ephemeral-token',
      expiresAt: null,
      uses: 1,
    });
    mocks.releaseLiveTokenLease.mockResolvedValue({ ok: true });
    mocks.tokenConnect.mockResolvedValue({ close: vi.fn() });
    mocks.tokenMusicConnect.mockResolvedValue({ close: vi.fn() });
    mocks.directClient.live.connect = mocks.directConnect;
    mocks.directConnect.mockResolvedValue({ close: vi.fn() });
    mocks.googleGenAi.mockImplementation(function MockGoogleGenAi(options: { apiKey: string }) {
      return options.apiKey === 'byok-key'
        ? mocks.directClient
        : {
          live: {
            connect: mocks.tokenConnect,
            music: { connect: mocks.tokenMusicConnect },
          },
        };
    });
  });

  it('keeps BYOK on the direct SDK path', async () => {
    mocks.resolveAccessMode.mockResolvedValue('byok');

    const ai = await getAi({ apiVersion: 'v1alpha' });

    expect(ai).not.toBe(mocks.directClient);
    expect(ai.models).toBe(mocks.directClient.models);
    expect(mocks.googleGenAi).toHaveBeenCalledWith({
      apiKey: 'byok-key',
      apiVersion: 'v1alpha',
    });
    expect(mocks.generateContent).not.toHaveBeenCalled();

    await ai.live.connect({
      model: 'gemini-live-test',
      liveOpenReason: createLiveOpenReason(LIVE_OPEN_TRIGGER.VOICE_TTS_CLICK, {
        requestId: 'live-byok-test-1',
        now: new Date('2026-09-02T10:00:00.000Z'),
      }),
    });
    expect(mocks.directConnect).toHaveBeenCalledWith({ model: 'gemini-live-test' });
  });

  it('routes managed generation and its abort signal through the backend', async () => {
    mocks.resolveAccessMode.mockResolvedValue('managed');
    const abortSignal = new AbortController().signal;
    const ai = await getAi();

    await ai.models.generateContent({
      model: 'gemini-flash-latest',
      contents: [{ role: 'user', parts: [{ text: 'hello' }] }],
      config: { temperature: 0.5, abortSignal },
    });

    expect(mocks.generateContent).toHaveBeenCalledWith({
      model: 'gemini-flash-latest',
      contents: [{ role: 'user', parts: [{ text: 'hello' }] }],
      config: { temperature: 0.5 },
    }, abortSignal);
  });

  it('mints a model-scoped live token and releases its lease exactly once', async () => {
    mocks.resolveAccessMode.mockResolvedValue('managed');
    const onclose = vi.fn();
    const ai = await getAi({ apiVersion: 'v1alpha' });

    const session = await ai.live.connect({
      model: 'gemini-3.1-flash-live-preview',
      liveOpenReason: createLiveOpenReason(LIVE_OPEN_TRIGGER.USER_CAMERA_LIVE, {
        requestId: 'live-test-request-1',
        now: new Date('2026-09-02T10:00:00.000Z'),
      }),
      config: { responseModalities: ['AUDIO'] },
      callbacks: { onclose },
    });
    const wrappedRequest = mocks.tokenConnect.mock.calls[0][0];

    expect(mocks.createLiveToken).toHaveBeenCalledWith({
      purpose: 'live',
      model: 'gemini-3.1-flash-live-preview',
      liveOpenReason: {
        trigger: LIVE_OPEN_TRIGGER.USER_CAMERA_LIVE,
        requestId: 'live-test-request-1',
        requestedAt: '2026-09-02T10:00:00.000Z',
      },
      config: { responseModalities: ['AUDIO'] },
    });
    expect(mocks.googleGenAi).toHaveBeenLastCalledWith({
      apiKey: 'ephemeral-token',
      apiVersion: 'v1alpha',
    });

    wrappedRequest.callbacks.onerror(new Error('recoverable callback'));
    expect(mocks.releaseLiveTokenLease).not.toHaveBeenCalled();
    session.close();
    wrappedRequest.callbacks.onclose({ reason: 'closed' });
    await vi.waitFor(() => {
      expect(mocks.releaseLiveTokenLease).toHaveBeenCalledTimes(1);
    });
    expect(onclose).toHaveBeenCalledWith({ reason: 'closed' });
    expect(wrappedRequest.liveOpenReason).toBeUndefined();
  });

  it('rejects an unreasoned managed Live connection before minting a token', async () => {
    mocks.resolveAccessMode.mockResolvedValue('managed');
    const ai = await getAi();

    await expect(ai.live.connect({ model: 'gemini-live-test' } as any)).rejects.toMatchObject({
      status: 400,
      code: 'LIVE_OPEN_REASON_REQUIRED',
    });
    expect(mocks.createLiveToken).not.toHaveBeenCalled();
  });

  it('fails clearly when neither access path is available', async () => {
    mocks.resolveAccessMode.mockResolvedValue('none');
    await expect(getAi()).rejects.toMatchObject({
      status: 401,
      code: 'MISSING_ACCESS',
    });
  });
});
