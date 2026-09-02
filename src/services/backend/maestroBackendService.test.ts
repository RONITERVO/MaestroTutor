// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  loadManagedAccessSession: vi.fn(),
  saveManagedAccessSession: vi.fn(),
  getCurrentIdentity: vi.fn(),
  getAppCheckToken: vi.fn(),
  getAppCheckFailureReason: vi.fn(),
}));

vi.mock('../../core/security/managedAccessSessionStorage', () => ({
  loadManagedAccessSession: mocks.loadManagedAccessSession,
  saveManagedAccessSession: mocks.saveManagedAccessSession,
}));

vi.mock('../auth/firebaseAuthBridgeService', () => ({
  firebaseAuthBridgeService: { getCurrentIdentity: mocks.getCurrentIdentity },
}));

vi.mock('../firebase/maestroFirebaseService', () => ({
  maestroFirebaseService: {
    getAppCheckToken: mocks.getAppCheckToken,
    getAppCheckFailureReason: mocks.getAppCheckFailureReason,
  },
}));

vi.mock('../../core/config/integrations', () => ({
  MAESTRO_INTEGRATION_CONFIG: {
    backendBaseUrl: 'https://backend.example',
  },
}));

import { readManagedGenerationStream } from './maestroBackendService';

const managedSession = {
  provider: 'firebase' as const,
  user: { id: 'user-1', email: null, displayName: null, photoUrl: null },
  firebaseIdToken: 'token',
  refreshToken: null,
  expiresAt: null,
  entitlements: [],
  billingSummary: {
    availableCredits: 20,
    reservedCredits: 0,
    lifetimePurchasedCredits: 20,
    lifetimeSpentCredits: 0,
    lifetimeSpentUsd: 0,
    updatedAt: null,
    lastPurchaseAt: null,
    lastChargeAt: null,
    lastProductId: null,
  },
  lastSyncedAt: 1,
};

const responseFor = (lines: unknown[]): Response => new Response(
  `${lines.map(line => JSON.stringify(line)).join('\n')}\n`,
  { status: 200, headers: { 'Content-Type': 'application/x-ndjson' } },
);

describe('managed generation stream protocol', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.loadManagedAccessSession.mockResolvedValue(managedSession);
    mocks.saveManagedAccessSession.mockResolvedValue(undefined);
  });

  it('yields SDK-shaped chunks and persists final billing', async () => {
    const billingSummary = { ...managedSession.billingSummary, availableCredits: 17 };
    const chunks: unknown[] = [];
    for await (const chunk of readManagedGenerationStream(responseFor([
      { type: 'chunk', chunk: { text: 'Hello', candidates: [] } },
      { type: 'final', result: { billingSummary } },
    ]))) {
      chunks.push(chunk);
    }

    expect(chunks).toEqual([{ text: 'Hello', candidates: [] }]);
    expect(mocks.saveManagedAccessSession).toHaveBeenCalledWith(expect.objectContaining({
      billingSummary,
      lastSyncedAt: expect.any(Number),
    }));
  });

  it('rejects a partial stream after preserving its final billing event', async () => {
    const billingSummary = { ...managedSession.billingSummary, availableCredits: 18 };
    const consume = async () => {
      const chunks: unknown[] = [];
      for await (const chunk of readManagedGenerationStream(responseFor([
        { type: 'chunk', chunk: { text: 'Partial' } },
        { type: 'final', result: { billingSummary } },
        { type: 'error', message: 'Provider disconnected', status: 502, code: 'UPSTREAM' },
      ]))) {
        chunks.push(chunk);
      }
      return chunks;
    };

    await expect(consume()).rejects.toMatchObject({
      status: 502,
      code: 'UPSTREAM',
    });
    expect(mocks.saveManagedAccessSession).toHaveBeenCalledWith(expect.objectContaining({ billingSummary }));
  });

  it('rejects a cleanly closed stream without final accounting', async () => {
    const consume = async () => {
      for await (const _chunk of readManagedGenerationStream(responseFor([
        { type: 'chunk', chunk: { text: 'Unaccounted' } },
      ]))) {
        // Consume the stream.
      }
    };

    await expect(consume()).rejects.toThrow('before its final accounting event');
  });

  it('cancels the network body when a consumer stops early', async () => {
    const cancel = vi.fn();
    const encoder = new TextEncoder();
    const response = new Response(new ReadableStream({
      start(controller) {
        controller.enqueue(encoder.encode(`${JSON.stringify({
          type: 'chunk',
          chunk: { text: 'First' },
        })}\n`));
      },
      cancel,
    }));
    const iterator = readManagedGenerationStream(response);

    await expect(iterator.next()).resolves.toEqual({
      done: false,
      value: { text: 'First' },
    });
    await iterator.return(undefined);

    expect(cancel).toHaveBeenCalledTimes(1);
  });
});

describe('App Check preflight', () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mocks.loadManagedAccessSession.mockResolvedValue(managedSession);
    mocks.saveManagedAccessSession.mockResolvedValue(undefined);
    mocks.getCurrentIdentity.mockResolvedValue(null);
    // The client captures a fetch implementation when it is constructed, so the
    // stub has to be in place before the module is imported below. It also
    // keeps this suite off the network entirely: a real .env points the backend
    // base URL at the deployed API.
    vi.stubGlobal('fetch', fetchMock);
    vi.resetModules();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  const importBackendService = async () => (
    (await import('./maestroBackendService')).maestroBackendService
  );

  it('refuses a managed request the device cannot attest, keeping the real reason', async () => {
    mocks.getAppCheckToken.mockResolvedValue(null);
    mocks.getAppCheckFailureReason.mockReturnValue('Play Integrity is unavailable on this device.');
    const backend = await importBackendService();

    await expect(backend.getAccountSummary()).rejects.toMatchObject({
      status: 401,
      code: 'app-check/unavailable',
      message: 'Play Integrity is unavailable on this device.',
    });
    // A request without the header can only come back 401, so it never leaves.
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('sends the attestation header once the device can produce one', async () => {
    mocks.getAppCheckToken.mockResolvedValue('attestation-jwt');
    fetchMock.mockResolvedValue(new Response(
      JSON.stringify({ account: { billingSummary: null, entitlements: [] } }),
      { status: 200 },
    ));
    const backend = await importBackendService();

    await backend.getAccountSummary();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const headers = new Headers(fetchMock.mock.calls[0][1].headers);
    expect(headers.get('X-Firebase-AppCheck')).toBe('attestation-jwt');
    expect(headers.get('Authorization')).toBe('Bearer token');
  });
});
