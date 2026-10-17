// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { createHash, randomBytes } from 'node:crypto';
import { isIP } from 'node:net';
import { createHttpError } from './http';

export const QUEST_CHALLENGE_TTL_MS = 5 * 60_000;
export const QUEST_APP_CHECK_TTL_MS = 30 * 60_000;
const CLOCK_SKEW_MS = 30_000;
const MAX_META_RESPONSE_BYTES = 64 * 1024;
const NONCE = /^[A-Za-z0-9_-]{43}$/;
const HASH = /^[a-f0-9]{64}$/;
const VERSION = /^[1-9][0-9]{0,9}$/;

export interface QuestAttestationPolicy {
  metaAppId: string;
  firebaseAppId: string;
  packageId: string;
  certificateHashes: readonly string[];
  minimumVersion: number;
}

export interface QuestChallenge {
  createdAt: number;
  expiresAt: number;
}

export interface QuestAttestationStore {
  // Each operation atomically consumes its own rate allowance. Storage failure
  // denies admission; the billing throttle's fail-open behavior is unsuitable.
  issue(subject: string, nonceHash: string, challenge: QuestChallenge): Promise<void>;
  consume(subject: string, nonceHash: string, now: number): Promise<QuestChallenge | null>;
}

export const questNonceHash = (nonce: string): string => createHash('sha256').update(nonce).digest('hex');
export const questUnavailable = () => createHttpError(503, 'Quest verification is unavailable.', 'quest-attestation/unavailable');
const invalid = () => createHttpError(401, 'Quest verification failed. Request a new challenge.', 'quest-attestation/rejected');
const malformed = () => createHttpError(400, 'Invalid Quest verification request.', 'quest-attestation/invalid-request');
const record = (value: unknown): value is Record<string, unknown> => (
  typeof value === 'object' && value !== null && !Array.isArray(value)
);

/** Trust only operator-verified ingress ranges, never arbitrary Forwarded data. */
export const readQuestProxyCidrs = (env: NodeJS.ProcessEnv): string[] | null => {
  const values = (env.QUEST_ATTESTATION_TRUSTED_PROXY_CIDRS || '').split(',').map(s => s.trim());
  if (values.length > 32 || values.some(value => {
    const parts = value.split('/'), family = isIP(parts[0]);
    if (!family || parts.length > 2) return true;
    if (parts.length === 1) return false;
    return !/^[1-9][0-9]*$/.test(parts[1]) || Number(parts[1]) > (family === 4 ? 32 : 128);
  })) return null;
  return values;
};

/** Disabled/misconfigured releases never fall back to reCAPTCHA or debug tokens. */
export const readQuestAttestationPolicy = (env: NodeJS.ProcessEnv): QuestAttestationPolicy | null => {
  if (env.QUEST_ATTESTATION_ENABLED !== 'true') return null;
  const metaAppId = env.QUEST_META_APP_ID?.trim() || '';
  const firebaseAppId = env.QUEST_FIREBASE_APP_ID?.trim() || '';
  const packageId = env.QUEST_ANDROID_PACKAGE?.trim() || '';
  const certificateHashes = (env.QUEST_SIGNING_SHA256 || '').split(',').map(s => s.trim().toLowerCase());
  const version = env.QUEST_MINIMUM_VERSION_CODE?.trim() || '';
  if (!readQuestProxyCidrs(env) || !/^[1-9][0-9]{0,29}$/.test(metaAppId)
    || !/^1:[0-9]+:(android|web):[a-f0-9]+$/.test(firebaseAppId)
    || !/^[a-zA-Z][\w]*(\.[a-zA-Z][\w]*)+$/.test(packageId)
    || packageId === 'com.maestro.quest.development'
    || certificateHashes.length > 8 || !certificateHashes.every(s => HASH.test(s))
    || !VERSION.test(version) || Number(version) > 2147483647) throw questUnavailable();
  return { metaAppId, firebaseAppId, packageId, certificateHashes, minimumVersion: Number(version) };
};

/** Parse only the server-to-server success envelope, never the client's JWT. */
export const validateQuestClaims = (
  response: unknown, policy: QuestAttestationPolicy, nonce: string, challenge: QuestChallenge, now: number,
): void => {
  if (!record(response) || !Array.isArray(response.data) || response.data.length !== 1) throw invalid();
  const entry: unknown = response.data[0];
  if (!record(entry) || entry.message !== 'success' || typeof entry.claims !== 'string'
    || entry.claims.length > 32_768 || !/^[A-Za-z0-9_-]+$/.test(entry.claims)) throw invalid();
  let claims: unknown;
  try {
    const bytes = Buffer.from(entry.claims, 'base64url');
    if (bytes.toString('base64url') !== entry.claims) throw invalid();
    claims = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(bytes));
  } catch { throw invalid(); }
  if (!record(claims)) throw invalid();
  const request = claims.request_details, app = claims.app_state, device = claims.device_state;
  if (!record(request) || !record(app) || !record(device)) throw invalid();
  const timestamp = request.timestamp, exp = request.exp;
  if (typeof timestamp !== 'number' || !Number.isSafeInteger(timestamp)
    || typeof exp !== 'number' || !Number.isSafeInteger(exp)
    || timestamp <= 0 || exp <= timestamp || exp * 1000 <= now
    || timestamp * 1000 < challenge.createdAt - CLOCK_SKEW_MS
    || timestamp * 1000 > now + CLOCK_SKEW_MS || now - timestamp * 1000 > QUEST_CHALLENGE_TTL_MS
    || challenge.createdAt > now || now >= challenge.expiresAt || request.nonce !== nonce) throw invalid();
  const certs = app.package_cert_sha256_digest;
  if (app.app_integrity_state !== 'StoreRecognized' || app.package_id !== policy.packageId
    || typeof app.version !== 'string' || !VERSION.test(app.version)
    || Number(app.version) < policy.minimumVersion || Number(app.version) > 2147483647
    || !Array.isArray(certs) || certs.length === 0 || certs.length > 8
    || !certs.every(c => typeof c === 'string' && HASH.test(c))
    || !certs.some(c => policy.certificateHashes.includes(c))
    || device.device_integrity_state !== 'Advanced') throw invalid();
  // Unknown extra claims are tolerated. An explicitly present ban must be well
  // formed and false; omission means Meta did not report a ban.
  if (claims.device_ban !== undefined
    && (!record(claims.device_ban) || claims.device_ban.is_banned !== false)) throw invalid();
};

/** Fixed provider endpoint, bounded body/deadline, no redirects or token logging. */
export const verifyMetaQuestToken = async (
  token: string, policy: QuestAttestationPolicy, appSecret: string, fetcher: typeof fetch = fetch,
): Promise<unknown> => {
  if (!appSecret || /[\s|]/.test(appSecret)) throw questUnavailable();
  const url = new URL('https://graph.oculus.com/platform_integrity/verify');
  url.searchParams.set('token', token);
  url.searchParams.set('access_token', `OC|${policy.metaAppId}|${appSecret}`);
  try {
    const response = await fetcher(url, { method: 'GET', redirect: 'error', signal: AbortSignal.timeout(10_000) });
    if (!response.ok || !response.body) throw questUnavailable();
    const reader = response.body.getReader();
    const chunks: Uint8Array[] = [];
    let length = 0;
    try {
      while (true) {
        const next = await reader.read();
        if (next.done) break;
        length += next.value.byteLength;
        if (length > MAX_META_RESPONSE_BYTES) throw questUnavailable();
        chunks.push(next.value);
      }
      return JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(Buffer.concat(chunks)));
    } finally { await reader.cancel().catch(() => {}); }
  } catch {
    // Fetch errors may embed the URL, including the Meta secret. Never propagate
    // or log provider errors; the client only gets a stable retryable error.
    throw questUnavailable();
  }
};

export const createQuestAttestationService = (dependencies: {
  policy: () => QuestAttestationPolicy | null;
  store: QuestAttestationStore;
  verify: (token: string, policy: QuestAttestationPolicy) => Promise<unknown>;
  mint: (appId: string, ttlMillis: number) => Promise<{ token: string; ttlMillis: number }>;
  now?: () => number;
}) => {
  const now = dependencies.now || Date.now;
  const policy = () => {
    const value = dependencies.policy();
    if (!value) throw questUnavailable();
    return value;
  };
  return {
    async challenge(subject: string, body: unknown) {
      policy();
      if (!record(body) || Object.keys(body).length !== 0) throw malformed();
      const nonce = randomBytes(32).toString('base64url');
      const createdAt = now(), expiresAt = createdAt + QUEST_CHALLENGE_TTL_MS;
      await dependencies.store.issue(subject, questNonceHash(nonce), { createdAt, expiresAt });
      return { nonce, expiresAt };
    },
    async exchange(subject: string, body: unknown) {
      const configuration = policy();
      if (!record(body) || Object.keys(body).length !== 2
        || typeof body.nonce !== 'string' || !NONCE.test(body.nonce)
        || typeof body.token !== 'string' || body.token.length > 32_768
        || !/^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$/.test(body.token)) throw malformed();
      const { nonce, token } = body;
      // Burn before the provider call. Concurrent requests, failures and lost
      // responses cannot mint again. A failed attempt needs a fresh challenge.
      const challenge = await dependencies.store.consume(subject, questNonceHash(nonce), now());
      if (!challenge) throw invalid();
      const response = await dependencies.verify(token, configuration);
      validateQuestClaims(response, configuration, nonce, challenge, now());
      const minted = await dependencies.mint(configuration.firebaseAppId, QUEST_APP_CHECK_TTL_MS);
      return { token: minted.token, ttlMillis: minted.ttlMillis };
    },
  };
};
