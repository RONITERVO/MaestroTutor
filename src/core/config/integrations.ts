// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
const parseCsv = (value?: string): string[] => {
  if (!value) return [];
  return value
    .split(',')
    .map(item => item.trim())
    .filter(Boolean);
};

export const MAESTRO_INTEGRATION_CONFIG = {
  firebaseApiKey: import.meta.env.VITE_FIREBASE_API_KEY?.trim() || '',
  firebaseAuthDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN?.trim() || '',
  firebaseProjectId: import.meta.env.VITE_FIREBASE_PROJECT_ID?.trim() || '',
  firebaseStorageBucket: import.meta.env.VITE_FIREBASE_STORAGE_BUCKET?.trim() || '',
  firebaseMessagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID?.trim() || '',
  firebaseAppId: import.meta.env.VITE_FIREBASE_APP_ID?.trim() || '',
  firebaseMeasurementId: import.meta.env.VITE_FIREBASE_MEASUREMENT_ID?.trim() || '',
  firebaseFunctionsRegion: import.meta.env.VITE_FIREBASE_FUNCTIONS_REGION?.trim() || '',
  firebaseAppCheckSiteKey: import.meta.env.VITE_FIREBASE_APPCHECK_SITE_KEY?.trim() || '',
  firebaseAppCheckDebugToken: import.meta.env.VITE_FIREBASE_APPCHECK_DEBUG_TOKEN?.trim() || '',
  backendBaseUrl: import.meta.env.VITE_BACKEND_BASE_URL?.trim() || '',
  googleWebClientId: import.meta.env.VITE_GOOGLE_WEB_CLIENT_ID?.trim() || '',
  googleServerClientId: import.meta.env.VITE_GOOGLE_SERVER_CLIENT_ID?.trim() || '',
  googlePlayPackageName: import.meta.env.VITE_GOOGLE_PLAY_PACKAGE_NAME?.trim() || 'com.ronitervo.maestrotutor',
  managedBillingProductIds: parseCsv(import.meta.env.VITE_MANAGED_BILLING_PRODUCT_IDS),
} as const;

export const isFirebaseClientConfigured = (): boolean => (
  Boolean(MAESTRO_INTEGRATION_CONFIG.firebaseApiKey)
  && Boolean(MAESTRO_INTEGRATION_CONFIG.firebaseAuthDomain)
  && Boolean(MAESTRO_INTEGRATION_CONFIG.firebaseProjectId)
  && Boolean(MAESTRO_INTEGRATION_CONFIG.firebaseAppId)
);

export const isBackendConfigured = (): boolean => Boolean(MAESTRO_INTEGRATION_CONFIG.backendBaseUrl);

export const isGoogleAuthConfigured = (): boolean => (
  isFirebaseClientConfigured() && isBackendConfigured()
);

export const isManagedBillingProduct = (productId: string): boolean => (
  MAESTRO_INTEGRATION_CONFIG.managedBillingProductIds.includes(productId)
);
