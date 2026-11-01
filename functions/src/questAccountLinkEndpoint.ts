// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import express, { type ErrorRequestHandler } from 'express';
import { verifiedQuestClientIp } from './questIngress';
import { onRequest } from 'firebase-functions/v2/https';
import { appConfig, isOriginAllowed } from './config';
import { adminAppCheck, adminAuth, adminDb } from './firebase';
import { accountDeletionClaimRef, managedUserRef } from './managedData';
import { getHttpErrorCode, getHttpStatus } from './http';
import { readQuestProxyCidrs } from './questAttestation';
import { createQuestAccountLinkService, linkUnavailable, readQuestLinkPolicy } from './questAccountLink';
import { createQuestAccountLinkStore } from './questAccountLinkStore';

export const createQuestAccountLinkApp = (
  service: ReturnType<typeof createQuestAccountLinkService>, trustedProxyCidrs: string[] | null = null,
) => {
  const app = express();
  app.disable('x-powered-by');
  app.set('trust proxy', trustedProxyCidrs || false);
  app.use((req, res, next) => {
    res.setHeader('Cache-Control', 'no-store');
    res.setHeader('Referrer-Policy', 'no-referrer');
    const origin = req.headers.origin;
    if (origin && origin !== 'https://appassets.androidplatform.net' && !isOriginAllowed(origin)) {
      res.status(403).json({ error: 'Origin is not allowed.' }); return;
    }
    if (origin) { res.setHeader('Access-Control-Allow-Origin', origin); res.setHeader('Vary', 'Origin'); }
    res.setHeader('Access-Control-Allow-Methods', 'POST,OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Authorization,Content-Type,X-Firebase-AppCheck');
    if (req.method === 'OPTIONS') { res.status(204).end(); return; }
    const raw = (req as typeof req & { rawBody?: Buffer }).rawBody;
    if ((raw && raw.length > 2048) || Number(req.headers['content-length']) > 2048) {
      res.status(413).json({ error: 'Account-link request is too large.' }); return;
    }
    if (req.headers['content-encoding'] && req.headers['content-encoding'] !== 'identity') {
      res.status(415).json({ error: 'Encoded requests are not supported.' }); return;
    }
    if (!req.is('application/json')) { res.status(415).json({ error: 'JSON is required.' }); return; }
    next();
  });
  app.use(express.json({ limit: '2kb', inflate: false }));
  for (const operation of ['create', 'status', 'approve', 'redeem', 'cancel'] as const) {
    app.post(`/${operation}`, async (req, res) => {
      try {
        const client = verifiedQuestClientIp(req);
        if (!client) throw linkUnavailable();
        const result = await service.run(operation, {
          ip: client, origin: req.headers.origin,
          appCheck: req.header('X-Firebase-AppCheck') || '', authorization: req.header('Authorization') || '',
        }, req.body);
        res.json(result);
      } catch (error) {
        const known = getHttpErrorCode(error)?.startsWith('quest-link/');
        const safe = known ? error as Error : linkUnavailable();
        res.status(known ? getHttpStatus(error) : 503).json({ error: safe.message, code: getHttpErrorCode(safe) });
      }
    });
  }
  app.use((_req, res) => { res.status(404).json({ error: 'Not found.' }); });
  const parserError: ErrorRequestHandler = (error, _req, res, _next) => {
    res.status(error?.type === 'entity.too.large' ? 413 : 400).json({ error: 'Invalid account-link request.' });
  };
  app.use(parserError);
  return app;
};

const service = createQuestAccountLinkService({
  policy: () => readQuestLinkPolicy(process.env),
  store: createQuestAccountLinkStore(adminDb),
  verifyApp: token => adminAppCheck.verifyToken(token),
  verifyUser: token => adminAuth.verifyIdToken(token, true),
  accountAvailable: async uid => {
    const [user, deletion, managed] = await Promise.all([
      adminAuth.getUser(uid).catch(error => {
        if (error?.code === 'auth/user-not-found') return null;
        throw error;
      }), accountDeletionClaimRef(uid).get(), managedUserRef(uid).get(),
    ]);
    return Boolean(user && !user.disabled && !deletion.exists && managed.data()?.status !== 'deleting');
  },
  mint: uid => adminAuth.createCustomToken(uid),
});
// Always verifies both App Check provenance and browser approval. The managed
// API's optional App Check rollback switch cannot weaken this bootstrap path.
export const questAccountLink = onRequest({
  region: appConfig.functionRegion, timeoutSeconds: 30, memory: '256MiB', maxInstances: 3, concurrency: 20,
}, createQuestAccountLinkApp(service, readQuestProxyCidrs(process.env)));
