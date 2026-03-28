// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { Capacitor } from '@capacitor/core';
import { FirebaseAuthentication } from '@capacitor-firebase/authentication';
import {
  GoogleAuthProvider,
  browserLocalPersistence,
  setPersistence,
  signInWithPopup,
  signOut as signOutWeb,
} from 'firebase/auth';
import type { AppUser } from '../../core/contracts/integrations';
import { maestroFirebaseService } from '../firebase/maestroFirebaseService';
import { ServiceNotConfiguredError } from '../shared/serviceErrors';

export interface ManagedAuthIdentity {
  firebaseIdToken: string;
  refreshToken: string | null;
  expiresAt: string | null;
  user: AppUser;
}

const provider = new GoogleAuthProvider();
provider.setCustomParameters({ prompt: 'select_account' });

const isNativeAndroid = Capacitor.isNativePlatform() && Capacitor.getPlatform() === 'android';

const mapNativeUser = (user: {
  uid: string;
  email?: string | null;
  displayName?: string | null;
  photoUrl?: string | null;
}): AppUser => ({
  id: user.uid,
  email: user.email || null,
  displayName: user.displayName || null,
  photoUrl: user.photoUrl || null,
});

const mapWebUser = (user: {
  uid: string;
  email: string | null;
  displayName: string | null;
  photoURL: string | null;
}): AppUser => ({
  id: user.uid,
  email: user.email,
  displayName: user.displayName,
  photoUrl: user.photoURL,
});

export const firebaseAuthBridgeService = {
  isNativeAndroid,

  beginGoogleSignIn: async (): Promise<ManagedAuthIdentity> => {
    if (isNativeAndroid) {
      const result = await FirebaseAuthentication.signInWithGoogle({
        useCredentialManager: true,
      });
      if (!result.user) {
        throw new Error('Firebase Google sign-in did not return a user.');
      }
      const tokenResult = await FirebaseAuthentication.getIdToken({ forceRefresh: true });
      return {
        firebaseIdToken: tokenResult.token,
        refreshToken: result.credential?.serverAuthCode || null,
        expiresAt: null,
        user: mapNativeUser(result.user),
      };
    }

    if (!maestroFirebaseService.isConfigured()) {
      throw new ServiceNotConfiguredError(
        'firebase-client',
        'Firebase client SDK is not configured. Fill the VITE_FIREBASE_* values before enabling Google sign-in.'
      );
    }

    const auth = maestroFirebaseService.getAuth();
    await setPersistence(auth, browserLocalPersistence);
    const result = await signInWithPopup(auth, provider);
    return {
      firebaseIdToken: await result.user.getIdToken(true),
      refreshToken: result.user.refreshToken,
      expiresAt: null,
      user: mapWebUser(result.user),
    };
  },

  getCurrentIdentity: async (forceRefresh = false): Promise<ManagedAuthIdentity | null> => {
    if (isNativeAndroid) {
      const result = await FirebaseAuthentication.getCurrentUser();
      if (!result.user) return null;
      const tokenResult = await FirebaseAuthentication.getIdToken({ forceRefresh });
      return {
        firebaseIdToken: tokenResult.token,
        refreshToken: null,
        expiresAt: null,
        user: mapNativeUser(result.user),
      };
    }

    if (!maestroFirebaseService.isConfigured()) return null;
    const auth = maestroFirebaseService.getAuth();
    if (!auth.currentUser) return null;
    return {
      firebaseIdToken: await auth.currentUser.getIdToken(forceRefresh),
      refreshToken: auth.currentUser.refreshToken,
      expiresAt: null,
      user: mapWebUser(auth.currentUser),
    };
  },

  signOut: async (): Promise<void> => {
    if (isNativeAndroid) {
      await FirebaseAuthentication.signOut();
      return;
    }

    if (!maestroFirebaseService.isConfigured()) return;
    await signOutWeb(maestroFirebaseService.getAuth());
  },
} as const;
