// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
/**
 * Firebase, loaded only if managed mode is actually used.
 *
 * The SDK is a few hundred kilobytes and the overwhelming majority of sessions
 * never sign in — the app's whole premise is that you bring your own API key
 * and nothing leaves the device. Importing `firebase/*` at module scope would
 * put all of that in the main bundle for everyone, to support a path most users
 * never take, which is the same cost pdf.js and jszip were moved off for.
 *
 * So every entry point here is async and the SDK arrives behind a dynamic
 * import. `getApp`/`getAuth` are deliberately asynchronous even though the
 * underlying calls are synchronous: making them look synchronous would require
 * loading the SDK eagerly to have something to return.
 */
import { Capacitor } from '@capacitor/core';
import type { FirebaseApp } from 'firebase/app';
import type { Auth } from 'firebase/auth';
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

const getFirebaseApp = async (): Promise<FirebaseApp> => {
  if (cachedFirebaseApp) return cachedFirebaseApp;
  const { getApps, initializeApp } = await import('firebase/app');
  cachedFirebaseApp = getApps()[0] || initializeApp(buildFirebaseConfig());
  return cachedFirebaseApp;
};

const initializeOptionalAppCheck = async (): Promise<boolean> => {
  if (hasInitializedAppCheck) return true;
  if (appCheckInitializationPromise) return appCheckInitializationPromise;

  appCheckInitializationPromise = (async () => {
    try {
      await getFirebaseApp();
      const { FirebaseAppCheck } = await import('@capacitor-firebase/app-check');

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

      const { ReCaptchaEnterpriseProvider } = await import('firebase/app-check');
      await FirebaseAppCheck.initialize({
        provider: new ReCaptchaEnterpriseProvider(MAESTRO_INTEGRATION_CONFIG.firebaseAppCheckSiteKey),
        isTokenAutoRefreshEnabled: true,
        ...(MAESTRO_INTEGRATION_CONFIG.firebaseAppCheckDebugToken
          ? { debugToken: MAESTRO_INTEGRATION_CONFIG.firebaseAppCheckDebugToken }
          : {}),
      });
      hasInitializedAppCheck = true;
      return true;
    } catch (error) {
      console.error('[firebase] App Check initialization failed.', error);
      appCheckInitializationPromise = null;
      return false;
    }
  })();

  return appCheckInitializationPromise;
};

export const maestroFirebaseService = {
  isConfigured: isFirebaseClientConfigured,

  getApp: (): Promise<FirebaseApp> => getFirebaseApp(),

  getAuth: async (): Promise<Auth> => {
    if (!cachedFirebaseAuth) {
      const app = await getFirebaseApp();
      const { getAuth } = await import('firebase/auth');
      cachedFirebaseAuth = getAuth(app);
    }
    return cachedFirebaseAuth;
  },

  getAppCheckToken: async (forceRefresh = false): Promise<string | null> => {
    const isReady = await initializeOptionalAppCheck();
    if (!isReady) return null;
    try {
      const { FirebaseAppCheck } = await import('@capacitor-firebase/app-check');
      const tokenResult = await FirebaseAppCheck.getToken({ forceRefresh });
      return tokenResult.token || null;
    } catch {
      return null;
    }
  },
} as const;
