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

const shouldEnforceAppCheck = (): boolean => appConfig.requireAppCheck;

const verifyAppCheckIfNeeded = async (req: Request): Promise<void> => {
  if (!shouldEnforceAppCheck()) return;
  const appCheckToken = req.headers['x-firebase-appcheck'];
  if (typeof appCheckToken !== 'string' || !appCheckToken.trim()) {
    throw createHttpError(401, 'Missing Firebase App Check token.');
  }
  try {
    await adminAppCheck.verifyToken(appCheckToken.trim());
  } catch {
    throw createHttpError(401, 'Invalid Firebase App Check token.');
  }
};

export const requireAuthContext = async (req: Request): Promise<AuthContext> => {
  const bearerToken = getBearerToken(req);
  if (!bearerToken) {
    throw createHttpError(401, 'Missing Authorization bearer token.');
  }

  await verifyAppCheckIfNeeded(req);
  let decodedToken: DecodedIdToken;
  try {
    decodedToken = await adminAuth.verifyIdToken(bearerToken);
  } catch {
    throw createHttpError(401, 'Invalid Firebase Authentication token.');
  }

  return {
    uid: decodedToken.uid,
    token: decodedToken,
    user: {
      id: decodedToken.uid,
      email: typeof decodedToken.email === 'string' ? decodedToken.email : null,
      displayName: typeof decodedToken.name === 'string' ? decodedToken.name : null,
      photoUrl: typeof decodedToken.picture === 'string' ? decodedToken.picture : null,
    },
  };
};
