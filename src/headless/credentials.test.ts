// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it, vi } from 'vitest';
import { createHeadlessCredentialProvider } from './credentials';

const response = (body: unknown) => new Response(JSON.stringify(body), {
  status: 200,
  headers: { 'Content-Type': 'application/json' },
});

describe('headless credential provider', () => {
  it('renews Firebase and App Check headers without exposing credential inputs', async () => {
    const fetchImpl = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(response({
        idToken: 'header.payload.signature', refreshToken: 'refresh-secret', expiresIn: '3600', localId: 'ci-user',
      }))
      .mockResolvedValueOnce(response({ token: 'app-check-jwt', ttl: '1800s' }));
    const provider = createHeadlessCredentialProvider({
      firebaseApiKey: 'public-api-key',
      firebaseAppId: '1:123:web:abc',
      firebaseEmail: 'ci@example.test',
      firebasePassword: 'password-secret',
      appCheckDebugToken: '00000000-0000-4000-8000-000000000000',
      fetchImpl,
      now: () => 1_000,
    });

    await expect(provider.getManagedHeaders(true)).resolves.toEqual({
      Authorization: 'Bearer header.payload.signature',
      'X-Firebase-AppCheck': 'app-check-jwt',
    });
    await expect(provider.getUserId()).resolves.toBe('ci-user');
    expect(fetchImpl).toHaveBeenCalledTimes(2);
    expect(provider.describe()).toEqual({
      firebase: 'password', appCheck: 'debug-token', signedOut: false, userId: 'ci-user',
    });
  });

  it('refuses required calls after sign-out', async () => {
    const provider = createHeadlessCredentialProvider({ firebaseIdToken: 'header.payload.signature' });
    provider.signOut();
    await expect(provider.getManagedHeaders(true)).rejects.toThrow('signed out');
  });
});
