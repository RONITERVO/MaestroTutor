// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { createHash, randomBytes } from 'node:crypto';
import { createHttpError } from './http';
import { readQuestProxyCidrs } from './questAttestation';

export const QUEST_LINK_TTL_MS = 300_000;
export const QUEST_LINK_POLL_MS = 5_000;
const ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
export const questLinkHash = (value: string): string => createHash('sha256').update(value).digest('hex');
export const linkUnavailable = () => createHttpError(503, 'Quest account linking is unavailable. Try again later.', 'quest-link/unavailable');
export const linkRejected = () => createHttpError(410, 'This account link is no longer available. Start again in the book.', 'quest-link/expired');
const invalid = () => createHttpError(400, 'Invalid account-link request.', 'quest-link/invalid');
const unauthorized = () => createHttpError(401, 'Account-link verification failed.', 'quest-link/unauthorized');

export interface QuestLinkPolicy { questAppId: string; webAppId: string; verificationUrl: string }
export const readQuestLinkPolicy = (env: NodeJS.ProcessEnv): QuestLinkPolicy | null => {
  if (env.QUEST_ACCOUNT_LINK_ENABLED !== 'true') return null;
  const questAppId = env.QUEST_FIREBASE_APP_ID?.trim() || '';
  const webAppId = env.QUEST_WEB_FIREBASE_APP_ID?.trim() || '';
  const verificationUrl = env.QUEST_ACCOUNT_LINK_VERIFY_URL?.trim() || '';
  const appId = /^1:([0-9]+):(web|android):[a-f0-9]+$/;
  if (!appId.test(questAppId) || !/^1:[0-9]+:web:[a-f0-9]+$/.test(webAppId)
    || questAppId === webAppId || questAppId.match(appId)?.[1] !== webAppId.match(appId)?.[1]
    || env.QUEST_ATTESTATION_ENABLED !== 'true' || !readQuestProxyCidrs(env)) throw linkUnavailable();
  try {
    const url = new URL(verificationUrl);
    if (url.protocol !== 'https:' || url.username || url.password || url.search || url.hash
      || url.pathname !== '/quest-link.html') throw linkUnavailable();
  } catch { throw linkUnavailable(); }
  return { questAppId, webAppId, verificationUrl };
};

export type QuestLinkState = 'pending' | 'approved' | 'consumed' | 'cancelled';
export interface QuestLinkRecord {
  createdAt: number; expiresAt: number; secretHash?: string; state: QuestLinkState; uid?: string;
}
export interface QuestLinkResult { state: 'pending' | 'approved'; expiresAt: number }
export interface QuestLinkStore {
  throttle(subject: string, bucket: string, limit: number, now: number): Promise<void>;
  issue(codeHash: string, record: QuestLinkRecord): Promise<boolean>;
  status(codeHash: string, secretHash: string, now: number): Promise<QuestLinkResult>;
  approve(codeHash: string, uid: string, now: number): Promise<void>;
  claim(codeHash: string, secretHash: string, now: number): Promise<{ uid: string; expiresAt: number }>;
  cancel(codeHash: string, secretHash: string, now: number): Promise<void>;
}
export interface QuestLinkContext { ip: string; appCheck: string; authorization: string; origin?: string }
export type QuestLinkOperation = 'create' | 'status' | 'approve' | 'redeem' | 'cancel';
interface Dependencies {
  policy(): QuestLinkPolicy | null;
  store: QuestLinkStore;
  verifyApp(token: string): Promise<{ appId: string }>;
  verifyUser(token: string): Promise<{ uid: string; auth_time: number; firebase: { sign_in_provider: string } }>;
  accountAvailable(uid: string): Promise<boolean>;
  mint(uid: string): Promise<string>;
  now?: () => number;
}
const body = (input: unknown, keys: string[]): Record<string, unknown> => {
  if (!input || typeof input !== 'object' || Array.isArray(input)
    || Object.keys(input).length !== keys.length || !keys.every(key => Object.hasOwn(input, key))) throw invalid();
  return input as Record<string, unknown>;
};
const codeHash = (value: unknown): string => {
  // Exactly ten unambiguous characters, optionally grouped for display.
  if (typeof value !== 'string' || !/^[A-HJ-NP-Z2-9]{5}-?[A-HJ-NP-Z2-9]{5}$/.test(value)) throw invalid();
  return questLinkHash(value.replace('-', ''));
};
const secretHash = (value: unknown): string => {
  if (typeof value !== 'string' || !/^[A-Za-z0-9_-]{43}$/.test(value)) throw invalid();
  return questLinkHash(value);
};

export const createQuestAccountLinkService = (dependencies: Dependencies) => {
  const now = dependencies.now || Date.now;
  return {
    async run(operation: QuestLinkOperation, context: QuestLinkContext, input: unknown): Promise<unknown> {
      const policy = dependencies.policy();
      if (!policy) throw linkUnavailable();
      // A separate committed transaction counts even invalid credentials/body.
      // Polling has its own budget so it cannot starve cancel or redemption.
      await dependencies.store.throttle(`ip:${context.ip}`, operation, operation === 'status' ? 120 : 12, now());
      if (!context.appCheck || context.appCheck.length > 16_384) throw unauthorized();
      let application: { appId: string };
      try { application = await dependencies.verifyApp(context.appCheck); } catch { throw unauthorized(); }
      if (application.appId !== (operation === 'approve' ? policy.webAppId : policy.questAppId)) throw unauthorized();
      if (operation === 'create') {
        body(input, []);
        for (let attempt = 0; attempt < 3; attempt++) {
          // A 32-character alphabet gives exactly 50 uniformly random bits.
          const code = Array.from(randomBytes(10), byte => ALPHABET[byte & 31]).join('');
          const secret = randomBytes(32).toString('base64url');
          const createdAt = now(), expiresAt = createdAt + QUEST_LINK_TTL_MS;
          if (!await dependencies.store.issue(questLinkHash(code), { createdAt, expiresAt, state: 'pending', secretHash: questLinkHash(secret) })) continue;
          return { code: `${code.slice(0, 5)}-${code.slice(5)}`, deviceSecret: secret, expiresAt,
            verificationUrl: policy.verificationUrl, pollIntervalMs: QUEST_LINK_POLL_MS };
        }
        throw linkUnavailable();
      }
      if (operation === 'approve') {
        if (context.origin !== new URL(policy.verificationUrl).origin) throw unauthorized();
        const request = body(input, ['code', 'confirm']);
        if (request.confirm !== true) throw invalid();
        const hash = codeHash(request.code);
        const token = context.authorization.match(/^Bearer ([^\s]+)$/)?.[1];
        if (!token || token.length > 16_384) throw unauthorized();
        let user: Awaited<ReturnType<Dependencies['verifyUser']>>;
        try { user = await dependencies.verifyUser(token); } catch { throw unauthorized(); }
        const age = now() - user.auth_time * 1000;
        if (!user.uid || user.uid.length > 128 || !Number.isSafeInteger(user.auth_time)
          || age < -30_000 || age >= QUEST_LINK_TTL_MS || user.firebase?.sign_in_provider !== 'google.com') {
          throw createHttpError(401, 'Sign in with Google again before linking the headset.', 'quest-link/recent-login-required');
        }
        await dependencies.store.throttle(`uid:${user.uid}`, 'approve', 6, now());
        if (!await dependencies.accountAvailable(user.uid)) throw linkRejected();
        await dependencies.store.approve(hash, user.uid, now());
        return { approved: true };
      }
      const request = body(input, ['code', 'deviceSecret']);
      const hash = codeHash(request.code), secret = secretHash(request.deviceSecret);
      if (operation === 'status') return dependencies.store.status(hash, secret, now());
      if (operation === 'cancel') {
        await dependencies.store.cancel(hash, secret, now());
        return { cancelled: true };
      }
      // Burn the approval atomically BEFORE minting. A lost response or mint
      // failure requires a new link; never cache/reissue a Firebase credential.
      const claimed = await dependencies.store.claim(hash, secret, now());
      if (!await dependencies.accountAvailable(claimed.uid) || now() >= claimed.expiresAt) throw linkRejected();
      const customToken = await dependencies.mint(claimed.uid);
      if (!customToken || customToken.length > 16_384 || now() >= claimed.expiresAt
        || !await dependencies.accountAvailable(claimed.uid)) throw linkRejected();
      return { customToken };
    },
  };
};
