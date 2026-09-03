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
  createLiveGatewayTicket: vi.fn(),
  acceptLiveGatewayBillingSummary: vi.fn(),
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
    createLiveGatewayTicket: mocks.createLiveGatewayTicket,
    acceptLiveGatewayBillingSummary: mocks.acceptLiveGatewayBillingSummary,
    createLiveToken: mocks.createLiveToken,
    releaseLiveTokenLease: mocks.releaseLiveTokenLease,
  },
}));

import { getAi } from './client';
import { createLiveOpenReason, LIVE_OPEN_TRIGGER } from '../../../shared/liveOpenReason';

class FakeWebSocket {
  static instances: FakeWebSocket[] = [];
  readonly url: string;
  readyState = 0;
  sent: string[] = [];
  closeCalls: Array<{ code?: number; reason?: string }> = [];
  private listeners = new Map<string, Set<(event: any) => void>>();

  constructor(url: string) {
    this.url = url;
    FakeWebSocket.instances.push(this);
  }

  send(data: string) { this.sent.push(data); }
  close(code?: number, reason?: string) {
    this.closeCalls.push({ code, reason });
    this.readyState = 3;
  }
  addEventListener(type: string, listener: (event: any) => void) {
    const listeners = this.listeners.get(type) || new Set();
    listeners.add(listener);
    this.listeners.set(type, listeners);
  }
  removeEventListener(type: string, listener: (event: any) => void) {
    this.listeners.get(type)?.delete(listener);
  }
  emit(type: string, event: any = {}) {
    if (type === 'open') this.readyState = 1;
    if (type === 'close') this.readyState = 3;
    for (const listener of this.listeners.get(type) || []) listener(event);
  }
  serverMessage(message: unknown) {
    this.emit('message', { data: JSON.stringify(message) });
  }
}

describe('Gemini provider routing', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    FakeWebSocket.instances = [];
    vi.stubGlobal('WebSocket', FakeWebSocket);
    mocks.getApiKeyOrThrow.mockResolvedValue('byok-key');
    mocks.generateContent.mockResolvedValue({ text: 'managed response' });
    mocks.generateContentStream.mockResolvedValue({
      async *[Symbol.asyncIterator]() {
        yield { text: 'managed chunk' };
      },
    });
    mocks.createLiveGatewayTicket.mockResolvedValue({
      transport: 'gateway',
      gatewayUrl: 'wss://managed-live.example.test/live',
      ticket: 'ticket-id.secret-value',
      ticketExpiresAt: '2026-09-02T10:00:45.000Z',
      sessionExpiresAt: '2026-09-02T10:02:00.000Z',
    });
    mocks.acceptLiveGatewayBillingSummary.mockResolvedValue(undefined);
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

  it('routes managed Live through a one-use server-metered gateway ticket', async () => {
    mocks.resolveAccessMode.mockResolvedValue('managed');
    const onclose = vi.fn();
    const onmessage = vi.fn();
    const ai = await getAi({ apiVersion: 'v1alpha' });

    const sessionPromise = ai.live.connect({
      model: 'gemini-3.1-flash-live-preview',
      liveOpenReason: createLiveOpenReason(LIVE_OPEN_TRIGGER.USER_CAMERA_LIVE, {
        requestId: 'live-test-request-1',
        now: new Date('2026-09-02T10:00:00.000Z'),
      }),
      config: { responseModalities: ['AUDIO'] },
      callbacks: { onclose, onmessage },
    });
    await vi.waitFor(() => expect(FakeWebSocket.instances).toHaveLength(1));
    const socket = FakeWebSocket.instances[0];
    expect(socket.url).toBe('wss://managed-live.example.test/live');
    expect(socket.url).not.toContain('ticket');
    socket.emit('open');
    expect(JSON.parse(socket.sent[0])).toEqual({
      type: 'authenticate',
      ticket: 'ticket-id.secret-value',
    });
    socket.serverMessage({ type: 'ready', sessionId: 'session-1', deadlineAt: Date.now() + 60_000 });
    const session = await sessionPromise;

    expect(mocks.createLiveGatewayTicket).toHaveBeenCalledWith({
      purpose: 'live',
      model: 'gemini-3.1-flash-live-preview',
      liveOpenReason: {
        trigger: LIVE_OPEN_TRIGGER.USER_CAMERA_LIVE,
        requestId: 'live-test-request-1',
        requestedAt: '2026-09-02T10:00:00.000Z',
      },
      config: { responseModalities: ['AUDIO'] },
    });
    expect(mocks.createLiveToken).not.toHaveBeenCalled();

    session.sendRealtimeInput({ audioStreamEnd: true });
    expect(JSON.parse(socket.sent[1])).toEqual({
      type: 'realtimeInput',
      input: { audioStreamEnd: true },
    });
    socket.serverMessage({ type: 'providerMessage', message: { setupComplete: {} } });
    await vi.waitFor(() => expect(onmessage).toHaveBeenCalledWith({ setupComplete: {} }));
    socket.serverMessage({
      type: 'billing',
      status: 'settled',
      billedCredits: 2,
      billedUsd: 0.002,
      usefulOutput: true,
      usageSource: 'provider',
      billingSummary: {
        availableCredits: 98,
        reservedCredits: 0,
        lifetimePurchasedCredits: 100,
        lifetimeSpentCredits: 2,
        lifetimeSpentUsd: 0.002,
        updatedAt: 1,
        lastPurchaseAt: 1,
        lastChargeAt: 1,
        lastProductId: 'pack',
      },
    });
    await vi.waitFor(() => expect(mocks.acceptLiveGatewayBillingSummary).toHaveBeenCalledOnce());
    expect(mocks.releaseLiveTokenLease).not.toHaveBeenCalled();
    session.close();
    expect(JSON.parse(socket.sent[socket.sent.length - 1])).toEqual({ type: 'close' });
    socket.emit('close', { reason: 'closed' });
    await vi.waitFor(() => expect(onclose).toHaveBeenCalledWith({ reason: 'closed' }));
    expect(mocks.releaseLiveTokenLease).not.toHaveBeenCalled();
  });

  it('rejects an unreasoned managed Live connection before minting a token', async () => {
    mocks.resolveAccessMode.mockResolvedValue('managed');
    const ai = await getAi();

    await expect(ai.live.connect({ model: 'gemini-live-test' } as any)).rejects.toMatchObject({
      status: 400,
      code: 'LIVE_OPEN_REASON_REQUIRED',
    });
    expect(mocks.createLiveGatewayTicket).not.toHaveBeenCalled();
  });

  it('fails clearly when neither access path is available', async () => {
    mocks.resolveAccessMode.mockResolvedValue('none');
    await expect(getAi()).rejects.toMatchObject({
      status: 401,
      code: 'MISSING_ACCESS',
    });
  });
});
