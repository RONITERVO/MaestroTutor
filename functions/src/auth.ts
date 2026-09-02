// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import type { Request, Response } from 'express';
import type { DecodedIdToken } from 'firebase-admin/auth';
import { appConfig, isOriginAllowed } from './config';
import { adminAppCheck, adminAuth } from './firebase';
import { createHttpError } from './http';

export interface AppUser {
  id: string;
  email: string | null;
  displayName: string | null;
  photoUrl: string | null;
}

export interface AuthContext {
  uid: string;
  token: DecodedIdToken;
  user: AppUser;
}

export const applyCors = (req: Request, res: Response): boolean => {
  const origin = typeof req.headers.origin === 'string' ? req.headers.origin : undefined;
  if (origin && !isOriginAllowed(origin)) {
    res.status(403).json({ error: 'Origin is not allowed.' });
    return false;
  }

  if (origin) {
    res.setHeader('Access-Control-Allow-Origin', origin);
    res.setHeader('Vary', 'Origin');
  }

  res.setHeader('Access-Control-Allow-Methods', 'GET,POST,OPTIONS');
  res.setHeader(
    'Access-Control-Allow-Headers',
    'Authorization,Content-Type,X-Firebase-AppCheck'
  );

  if (req.method === 'OPTIONS') {
    res.status(204).send('');
    return false;
  }

  return true;
};

const getBearerToken = (req: Request): string | null => {
  const rawHeader = req.headers.authorization;
  if (typeof rawHeader !== 'string') return null;
  const match = rawHeader.match(/^Bearer\s+(.+)$/i);
  return match?.[1] || null;
};

/*
 * Stable identifiers for the two App Check rejections. The prose is for logs
 * and curl; clients match on these instead, so a wording change here cannot
 * silently turn a recognised, explainable failure back into a raw server
 * string in front of a user.
 */
export const APP_CHECK_MISSING_CODE = 'app-check/missing';
export const APP_CHECK_INVALID_CODE = 'app-check/invalid';

const shouldEnforceAppCheck = (): boolean => appConfig.requireAppCheck;

export const verifyAppCheckIfNeeded = async (req: Request): Promise<void> => {
  if (!shouldEnforceAppCheck()) return;
  const appCheckToken = req.headers['x-firebase-appcheck'];
  if (typeof appCheckToken !== 'string' || !appCheckToken.trim()) {
    throw createHttpError(401, 'Missing Firebase App Check token.', APP_CHECK_MISSING_CODE);
  }
  try {
    await adminAppCheck.verifyToken(appCheckToken.trim());
  } catch {
    throw createHttpError(401, 'Invalid Firebase App Check token.', APP_CHECK_INVALID_CODE);
  }
};

const buildAuthContext = (decodedToken: DecodedIdToken): AuthContext => ({
  uid: decodedToken.uid,
  token: decodedToken,
  user: {
    id: decodedToken.uid,
    email: typeof decodedToken.email === 'string' ? decodedToken.email : null,
    displayName: typeof decodedToken.name === 'string' ? decodedToken.name : null,
    photoUrl: typeof decodedToken.picture === 'string' ? decodedToken.picture : null,
  },
});

export const getOptionalAuthContext = async (req: Request): Promise<AuthContext | null> => {
  await verifyAppCheckIfNeeded(req);
  const bearerToken = getBearerToken(req);
  if (!bearerToken) {
    return null;
  }

  let decodedToken: DecodedIdToken;
  try {
    decodedToken = await adminAuth.verifyIdToken(bearerToken, true);
  } catch {
    throw createHttpError(401, 'Invalid Firebase Authentication token.', 'auth/invalid-token');
  }

  return buildAuthContext(decodedToken);
};

export const requireAuthContext = async (req: Request): Promise<AuthContext> => {
  const auth = await getOptionalAuthContext(req);
  if (!auth) {
    throw createHttpError(401, 'Missing Authorization bearer token.', 'auth/missing-token');
  }
  return auth;
};
