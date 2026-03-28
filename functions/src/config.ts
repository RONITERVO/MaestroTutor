// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

const parseBoolean = (value: string | undefined, fallback = false): boolean => {
  if (!value) return fallback;
  const normalized = value.trim().toLowerCase();
  if (['1', 'true', 'yes', 'on'].includes(normalized)) return true;
  if (['0', 'false', 'no', 'off'].includes(normalized)) return false;
  return fallback;
};

const parseInteger = (value: string | undefined, fallback: number): number => {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? Math.floor(parsed) : fallback;
};

const parseCsv = (value: string | undefined): string[] => (
  (value || '')
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
);

const parseManagedCreditProducts = (value: string | undefined): Record<string, number> => {
  const out: Record<string, number> = {};
  for (const item of parseCsv(value)) {
    const [productId, creditsRaw] = item.split(':').map((part) => part.trim());
    const credits = Number(creditsRaw);
    if (!productId || !Number.isFinite(credits) || credits <= 0) continue;
    out[productId] = Math.floor(credits);
  }
  return out;
};

const trustedLocalOrigins = new Set([
  'http://localhost',
  'https://localhost',
  'capacitor://localhost',
]);

export const appConfig = {
  functionRegion: process.env.FUNCTION_REGION?.trim() || 'europe-west1',
  firebaseProjectId: process.env.FIREBASE_PROJECT_ID?.trim() || '',
  geminiApiKey: process.env.GEMINI_API_KEY?.trim() || '',
  googlePlayPackageName: process.env.GOOGLE_PLAY_PACKAGE_NAME?.trim() || 'com.ronitervo.maestrotutor',
  allowedOrigins: new Set([
    ...parseCsv(process.env.ALLOWED_ORIGINS),
    ...trustedLocalOrigins,
  ]),
  managedCreditProducts: parseManagedCreditProducts(process.env.MANAGED_CREDIT_PRODUCTS),
  managedCreditsPerUsd: Math.max(1, parseInteger(process.env.MANAGED_CREDITS_PER_USD, 1000)),
  requireAppCheck: parseBoolean(process.env.REQUIRE_APPCHECK, false),
  geminiLiveTokenUses: Math.max(1, parseInteger(process.env.GEMINI_LIVE_TOKEN_USES, 1)),
  managedLiveTokenLifetimeSeconds: Math.min(180, Math.max(30, parseInteger(process.env.MANAGED_LIVE_TOKEN_LIFETIME_SECONDS, 180))),
  managedMaxActiveLiveSockets: Math.min(2, Math.max(1, parseInteger(process.env.MANAGED_MAX_ACTIVE_LIVE_SOCKETS, 2))),
  reservationTtlMinutes: Math.max(5, parseInteger(process.env.RESERVATION_TTL_MINUTES, 30)),
  managedMusicSessionCredits: Math.max(1, parseInteger(process.env.MANAGED_MUSIC_SESSION_CREDITS, 120)),
  managedMaxActiveFilesPerUser: Math.max(1, parseInteger(process.env.MANAGED_MAX_ACTIVE_FILES_PER_USER, 20)),
  managedUploadCreditsPerMb: Math.max(1, parseInteger(process.env.MANAGED_UPLOAD_CREDITS_PER_MB, 10)),
  managedMaxUploadBytes: Math.max(1, parseInteger(process.env.MANAGED_MAX_UPLOAD_BYTES, 50 * 1024 * 1024)),
} as const;

export const isOriginAllowed = (origin: string | undefined): boolean => {
  if (!origin) return true;
  return appConfig.allowedOrigins.has(origin);
};

export const getCreditsForManagedProduct = (productId: string): number => (
  appConfig.managedCreditProducts[productId] || 0
);

export const getReservationTtlMs = (): number => appConfig.reservationTtlMinutes * 60_000;
