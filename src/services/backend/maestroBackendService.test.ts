// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  loadManagedAccessSession: vi.fn(),
  saveManagedAccessSession: vi.fn(),
}));

vi.mock('../../core/security/managedAccessSessionStorage', () => ({
  loadManagedAccessSession: mocks.loadManagedAccessSession,
  saveManagedAccessSession: mocks.saveManagedAccessSession,
}));

vi.mock('../auth/firebaseAuthBridgeService', () => ({
  firebaseAuthBridgeService: {},
}));

vi.mock('../firebase/maestroFirebaseService', () => ({
  maestroFirebaseService: {},
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
