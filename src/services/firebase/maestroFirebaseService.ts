// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { getApps, initializeApp, type FirebaseApp } from 'firebase/app';
import { initializeAppCheck, ReCaptchaV3Provider, getToken as getFirebaseAppCheckToken, type AppCheck } from 'firebase/app-check';
import { getAuth, type Auth } from 'firebase/auth';
import { MAESTRO_INTEGRATION_CONFIG, isFirebaseClientConfigured } from '../../core/config/integrations';
import { ServiceNotConfiguredError } from '../shared/serviceErrors';

let cachedFirebaseApp: FirebaseApp | null = null;
let cachedFirebaseAuth: Auth | null = null;
let cachedAppCheck: AppCheck | null | undefined;

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

const initializeOptionalAppCheck = (): AppCheck | null => {
  if (cachedAppCheck !== undefined) return cachedAppCheck;
  if (!MAESTRO_INTEGRATION_CONFIG.firebaseAppCheckSiteKey) {
    cachedAppCheck = null;
    return cachedAppCheck;
  }

  try {
    const win = typeof window !== 'undefined' ? window as Window & { FIREBASE_APPCHECK_DEBUG_TOKEN?: string } : null;
    if (win && MAESTRO_INTEGRATION_CONFIG.firebaseAppCheckDebugToken) {
      win.FIREBASE_APPCHECK_DEBUG_TOKEN = MAESTRO_INTEGRATION_CONFIG.firebaseAppCheckDebugToken;
    }
    cachedAppCheck = initializeAppCheck(getFirebaseApp(), {
      provider: new ReCaptchaV3Provider(MAESTRO_INTEGRATION_CONFIG.firebaseAppCheckSiteKey),
      isTokenAutoRefreshEnabled: true,
    });
  } catch {
    cachedAppCheck = null;
  }

  return cachedAppCheck;
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
    const appCheck = initializeOptionalAppCheck();
    if (!appCheck) return null;
    try {
      const tokenResult = await getFirebaseAppCheckToken(appCheck, forceRefresh);
      return tokenResult.token || null;
    } catch {
      return null;
    }
  },
} as const;
