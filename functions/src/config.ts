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

/**
 * A buyable bundle of credits.
 *
 * One catalogue serves both storefronts. Google Play and Stripe have to agree
 * on what a pack contains, and two separate lists would let them drift the same
 * way the two pricing tables did — with the difference that here the drift is
 * the user paying one price and receiving another store's quantity.
 */
export interface CreditPack {
  /** Internal id. What Stripe checkout is asked for. */
  id: string;
  credits: number;
  /** Stripe price, in the smallest currency unit. */
  priceCents: number;
  /** The matching Google Play product, when the pack is sold there too. */
  playProductId?: string;
}

/** `id:credits:cents[:playProductId]`, comma separated. */
const parseCreditPacks = (value: string | undefined): CreditPack[] => {
  const packs: CreditPack[] = [];
  for (const item of parseCsv(value)) {
    const [id, creditsRaw, centsRaw, playProductId] = item.split(':').map((part) => part.trim());
    const credits = Number(creditsRaw);
    const priceCents = Number(centsRaw);
    if (!id || !Number.isFinite(credits) || credits <= 0) continue;
    if (!Number.isFinite(priceCents) || priceCents <= 0) continue;
    packs.push({
      id,
      credits: Math.floor(credits),
      priceCents: Math.floor(priceCents),
      ...(playProductId ? { playProductId } : {}),
    });
  }
  return packs;
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
  creditPacks: parseCreditPacks(process.env.MANAGED_CREDIT_PACKS),
  billingCurrency: (process.env.BILLING_CURRENCY?.trim() || 'eur').toLowerCase(),
  /** Where Stripe sends the buyer back to. Required for checkout. */
  appUrl: process.env.APP_URL?.trim() || '',
  stripeSecretKey: process.env.STRIPE_SECRET_KEY?.trim() || '',
  stripeWebhookSecret: process.env.STRIPE_WEBHOOK_SECRET?.trim() || '',
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
  /** Requests per minute per user, per class of operation. See rateLimit.ts. */
  rateLimitPerMinute: Math.max(1, parseInteger(process.env.MANAGED_RATE_LIMIT_PER_MINUTE, 60)),
  liveTokenRateLimitPerMinute: Math.max(1, parseInteger(process.env.MANAGED_LIVE_TOKEN_RATE_LIMIT_PER_MINUTE, 6)),
} as const;

/**
 * The JSON body limit Express should enforce.
 *
 * Derived rather than configured separately. Uploads arrive base64-encoded
 * inside JSON, which inflates them by about a third, plus room for the
 * surrounding envelope. Setting this equal to the upload limit — as it was —
 * meant any upload above roughly three quarters of the advertised maximum was
 * rejected by middleware with a bare 413 before the application ever saw it,
 * so the limit the user was told about was not the limit they got.
 */
export const getJsonBodyLimitBytes = (): number => (
  Math.ceil(appConfig.managedMaxUploadBytes * 4 / 3) + 1024 * 1024
);

export const isOriginAllowed = (origin: string | undefined): boolean => {
  if (!origin) return true;
  return appConfig.allowedOrigins.has(origin);
};

/** Credits for a Google Play product id, or 0 if it is not a known pack. */
export const getCreditsForManagedProduct = (productId: string): number => (
  appConfig.creditPacks.find((pack) => pack.playProductId === productId)?.credits || 0
);

export const getCreditPackById = (packId: string): CreditPack | undefined => (
  appConfig.creditPacks.find((pack) => pack.id === packId)
);

export const isStripeConfigured = (): boolean => (
  Boolean(appConfig.stripeSecretKey) && Boolean(appConfig.stripeWebhookSecret)
);

export const getReservationTtlMs = (): number => appConfig.reservationTtlMinutes * 60_000;
