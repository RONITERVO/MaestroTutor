// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { beforeEach, describe, expect, it, vi } from 'vitest';

const nativeAuthState = vi.hoisted(() => {
  const methods = {
    signInWithGoogle: vi.fn(),
    getIdToken: vi.fn(),
    getCurrentUser: vi.fn(),
    signOut: vi.fn(),
  };
  const thenReads = { count: 0 };
  const plugin = new Proxy(methods, {
    get(target, property, receiver) {
      if (property === 'then') {
        thenReads.count += 1;
        return () => {
          throw new Error('FirebaseAuthentication.then() must not be invoked.');
        };
      }
      return Reflect.get(target, property, receiver);
    },
  });
  return { methods, plugin, thenReads };
});

vi.mock('@capacitor/core', () => ({
  Capacitor: {
    getPlatform: () => 'android',
    isNativePlatform: () => true,
  },
}));

vi.mock('@capacitor-firebase/authentication', () => ({
  FirebaseAuthentication: nativeAuthState.plugin,
}));

vi.mock('../firebase/maestroFirebaseService', () => ({
  maestroFirebaseService: {
    getAuth: vi.fn(),
    isConfigured: () => false,
  },
}));

import { firebaseAuthBridgeService } from './firebaseAuthBridgeService';

const nativeUser = {
  uid: 'native-user',
  email: 'user@example.com',
  displayName: 'Native User',
  photoUrl: 'https://example.com/avatar.png',
};

describe('firebaseAuthBridgeService native plugin loading', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    nativeAuthState.thenReads.count = 0;
    nativeAuthState.methods.signInWithGoogle.mockResolvedValue({
      user: nativeUser,
      credential: { serverAuthCode: 'server-code' },
    });
    nativeAuthState.methods.getCurrentUser.mockResolvedValue({ user: nativeUser });
    nativeAuthState.methods.getIdToken.mockResolvedValue({ token: 'firebase-token' });
    nativeAuthState.methods.signOut.mockResolvedValue(undefined);
  });

  it('unwraps the plugin after the module import before Google sign-in', async () => {
    await expect(firebaseAuthBridgeService.beginGoogleSignIn()).resolves.toMatchObject({
      firebaseIdToken: 'firebase-token',
      refreshToken: 'server-code',
      user: { id: 'native-user' },
    });

    expect(nativeAuthState.methods.signInWithGoogle).toHaveBeenCalledWith({
      useCredentialManager: true,
    });
    expect(nativeAuthState.thenReads.count).toBe(0);
  });

  it('uses the same safe loader for restoring identity and signing out', async () => {
    await expect(firebaseAuthBridgeService.getCurrentIdentity(true)).resolves.toMatchObject({
      firebaseIdToken: 'firebase-token',
      user: { id: 'native-user' },
    });
    await expect(firebaseAuthBridgeService.signOut()).resolves.toBeUndefined();

    expect(nativeAuthState.methods.getIdToken).toHaveBeenCalledWith({ forceRefresh: true });
    expect(nativeAuthState.methods.signOut).toHaveBeenCalledOnce();
    expect(nativeAuthState.thenReads.count).toBe(0);
  });
});
