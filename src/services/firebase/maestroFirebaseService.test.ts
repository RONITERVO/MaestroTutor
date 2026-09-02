// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getToken: vi.fn(),
  initialize: vi.fn(async () => undefined),
}));

vi.mock('@capacitor/core', () => ({
  Capacitor: { isNativePlatform: () => true, getPlatform: () => 'android' },
}));

vi.mock('firebase/app', () => ({
  getApps: () => [],
  initializeApp: () => ({ name: 'test-app' }),
}));

vi.mock('@capacitor-firebase/app-check', () => ({
  FirebaseAppCheck: { initialize: mocks.initialize, getToken: mocks.getToken },
}));

vi.mock('../../core/config/integrations', () => ({
  MAESTRO_INTEGRATION_CONFIG: {
    firebaseApiKey: 'api-key',
    firebaseAuthDomain: 'example.firebaseapp.com',
    firebaseProjectId: 'example',
    firebaseAppId: 'app-id',
    firebaseAppCheckSiteKey: '',
    firebaseAppCheckDebugToken: '',
  },
  isFirebaseClientConfigured: () => true,
}));

// Attestation state is module-level, so each case needs its own instance.
const importService = async () => (
  (await import('./maestroFirebaseService')).maestroFirebaseService
);

describe('App Check token acquisition', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.resetModules();
  });

  it('recovers a transient Play Integrity miss with one forced refresh', async () => {
    mocks.getToken
      .mockRejectedValueOnce(new Error('Too many requests to Play Integrity.'))
      .mockResolvedValueOnce({ token: 'attestation-jwt', expireTimeMillis: 1 });
    const service = await importService();

    await expect(service.getAppCheckToken()).resolves.toBe('attestation-jwt');
    expect(mocks.getToken.mock.calls).toEqual([
      [{ forceRefresh: false }],
      [{ forceRefresh: true }],
    ]);
    expect(service.getAppCheckFailureReason()).toBeNull();
  });

  it('keeps the attestation error when both attempts fail', async () => {
    mocks.getToken.mockRejectedValue(new Error('App is not recognised by Play.'));
    const service = await importService();

    await expect(service.getAppCheckToken()).resolves.toBeNull();
    expect(mocks.getToken).toHaveBeenCalledTimes(2);
    expect(service.getAppCheckFailureReason()).toBe('App is not recognised by Play.');
  });

  it('does not retry a caller that already asked for a forced refresh', async () => {
    mocks.getToken.mockResolvedValue({ token: '', expireTimeMillis: 0 });
    const service = await importService();

    await expect(service.getAppCheckToken(true)).resolves.toBeNull();
    expect(mocks.getToken.mock.calls).toEqual([[{ forceRefresh: true }]]);
  });
});
