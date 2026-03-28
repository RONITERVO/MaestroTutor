// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { Capacitor } from '@capacitor/core';
import { FirebaseAppCheck } from '@capacitor-firebase/app-check';
import { getApps, initializeApp, type FirebaseApp } from 'firebase/app';
import { ReCaptchaV3Provider } from 'firebase/app-check';
import { getAuth, type Auth } from 'firebase/auth';
import { MAESTRO_INTEGRATION_CONFIG, isFirebaseClientConfigured } from '../../core/config/integrations';
import { ServiceNotConfiguredError } from '../shared/serviceErrors';

let cachedFirebaseApp: FirebaseApp | null = null;
let cachedFirebaseAuth: Auth | null = null;
let appCheckInitializationPromise: Promise<boolean> | null = null;
let hasInitializedAppCheck = false;

const isNativeAppCheckPlatform = Capacitor.isNativePlatform() && Capacitor.getPlatform() !== 'web';

const buildFirebaseConfig = () => {
  if (!isFirebaseClientConfigured()) {
    throw new ServiceNotConfiguredError(
      'firebase-client',
      'Firebase client SDK is not configured. Fill the VITE_FIREBASE_* values before enabling managed access.'
    );
  }

  return {
    apiKey: MAESTRO_INTEGRATION_CONFIG.firebaseApiKey,
    authDomain: MAESTRO_INTEGRATION_CONFIG.firebaseAuthDomain,
    projectId: MAESTRO_INTEGRATION_CONFIG.firebaseProjectId,
    storageBucket: MAESTRO_INTEGRATION_CONFIG.firebaseStorageBucket || undefined,
    messagingSenderId: MAESTRO_INTEGRATION_CONFIG.firebaseMessagingSenderId || undefined,
    appId: MAESTRO_INTEGRATION_CONFIG.firebaseAppId,
    measurementId: MAESTRO_INTEGRATION_CONFIG.firebaseMeasurementId || undefined,
  };
};

const getFirebaseApp = (): FirebaseApp => {
  if (cachedFirebaseApp) return cachedFirebaseApp;
  cachedFirebaseApp = getApps()[0] || initializeApp(buildFirebaseConfig());
  return cachedFirebaseApp;
};

const initializeOptionalAppCheck = async (): Promise<boolean> => {
  if (hasInitializedAppCheck) return true;
  if (appCheckInitializationPromise) return appCheckInitializationPromise;

  appCheckInitializationPromise = (async () => {
    try {
      getFirebaseApp();

      if (isNativeAppCheckPlatform) {
        await FirebaseAppCheck.initialize({
          isTokenAutoRefreshEnabled: true,
          ...(MAESTRO_INTEGRATION_CONFIG.firebaseAppCheckDebugToken
            ? { debugToken: true }
            : {}),
        });
        hasInitializedAppCheck = true;
        return true;
      }

      if (!MAESTRO_INTEGRATION_CONFIG.firebaseAppCheckSiteKey) {
        return false;
      }

      await FirebaseAppCheck.initialize({
        provider: new ReCaptchaV3Provider(MAESTRO_INTEGRATION_CONFIG.firebaseAppCheckSiteKey),
        isTokenAutoRefreshEnabled: true,
        ...(MAESTRO_INTEGRATION_CONFIG.firebaseAppCheckDebugToken
          ? { debugToken: MAESTRO_INTEGRATION_CONFIG.firebaseAppCheckDebugToken }
          : {}),
      });
      hasInitializedAppCheck = true;
      return true;
    } catch {
      appCheckInitializationPromise = null;
      return false;
    }
  })();

  return appCheckInitializationPromise;
};

export const maestroFirebaseService = {
  isConfigured: isFirebaseClientConfigured,

  getApp: (): FirebaseApp => getFirebaseApp(),

  getAuth: (): Auth => {
    if (!cachedFirebaseAuth) {
      cachedFirebaseAuth = getAuth(getFirebaseApp());
    }
    return cachedFirebaseAuth;
  },

  getAppCheckToken: async (forceRefresh = false): Promise<string | null> => {
    const isReady = await initializeOptionalAppCheck();
    if (!isReady) return null;
    try {
      const tokenResult = await FirebaseAppCheck.getToken({ forceRefresh });
      return tokenResult.token || null;
    } catch {
      return null;
    }
  },
} as const;
