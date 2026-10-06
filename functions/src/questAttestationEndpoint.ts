// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import express, { type ErrorRequestHandler } from 'express';
import { verifiedQuestClientIp } from './questIngress';
import { defineSecret } from 'firebase-functions/params';
import { onRequest } from 'firebase-functions/v2/https';
import { appConfig, isOriginAllowed } from './config';
import { adminAppCheck, adminDb } from './firebase';
import { getHttpErrorCode, getHttpStatus } from './http';
import { createQuestAttestationService, questUnavailable, readQuestAttestationPolicy, readQuestProxyCidrs, verifyMetaQuestToken } from './questAttestation';
import { createQuestAttestationStore } from './questAttestationStore';

const QUEST_ORIGIN = 'https://appassets.androidplatform.net';
const metaAppSecret = defineSecret('META_QUEST_APP_SECRET');

export const createQuestAttestationApp = (
  service: ReturnType<typeof createQuestAttestationService>, trustedProxyCidrs: string[] | null = null,
) => {
  const app = express();
  app.disable('x-powered-by');
  app.set('trust proxy', trustedProxyCidrs || false);
  app.use((req, res, next) => {
    res.setHeader('Cache-Control', 'no-store');
    const origin = req.headers.origin;
    if (origin && origin !== QUEST_ORIGIN && !isOriginAllowed(origin)) {
      res.status(403).json({ error: 'Origin is not allowed.' });
      return;
    }
    if (origin) { res.setHeader('Access-Control-Allow-Origin', origin); res.setHeader('Vary', 'Origin'); }
    res.setHeader('Access-Control-Allow-Methods', 'POST,OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
    if (req.method === 'OPTIONS') { res.status(204).end(); return; }
    // Firebase Functions can preparse req.body before Express. Enforce the
    // original buffer as well as the parser limit; reject encoded payloads.
    const rawBody = (req as typeof req & { rawBody?: Buffer }).rawBody;
    if ((rawBody && rawBody.length > 36 * 1024) || Number(req.headers['content-length']) > 36 * 1024) {
      res.status(413).json({ error: 'Quest verification request is too large.' }); return;
    }
    if (req.headers['content-encoding'] && req.headers['content-encoding'] !== 'identity') {
      res.status(415).json({ error: 'Encoded requests are not supported.' }); return;
    }
    if (!req.is('application/json')) { res.status(415).json({ error: 'JSON is required.' }); return; }
    next();
  });
  app.use(express.json({ limit: '36kb', inflate: false }));
  for (const operation of ['challenge', 'exchange'] as const) {
    app.post(`/${operation}`, async (req, res) => {
      try {
        const client = verifiedQuestClientIp(req);
        if (!client) throw questUnavailable();
        const result = await service[operation](client, req.body);
        res.json(result);
      } catch (error) {
        const code = getHttpErrorCode(error);
        const known = code?.startsWith('quest-attestation/');
        const safe = known ? error as Error : questUnavailable();
        res.status(known ? getHttpStatus(error) : 503).json({ error: safe.message, code: known ? code : getHttpErrorCode(safe) });
      }
    });
  }
  app.use((_req, res) => { res.status(404).json({ error: 'Not found.' }); });
  const parserError: ErrorRequestHandler = (error, _req, res, _next) => {
    res.status(error?.type === 'entity.too.large' ? 413 : 400).json({ error: 'Invalid Quest verification request.' });
  };
  app.use(parserError);
  return app;
};

const service = createQuestAttestationService({
  policy: () => readQuestAttestationPolicy(process.env),
  store: createQuestAttestationStore(adminDb),
  verify: (token, policy) => verifyMetaQuestToken(token, policy, metaAppSecret.value()),
  mint: (appId, ttlMillis) => adminAppCheck.createToken(appId, { ttlMillis }),
});

// Separate from the existing managed API: only this function binds Meta's
// server secret, and its bootstrap endpoints never grant identity or credits.
export const questAttestation = onRequest({
  region: appConfig.functionRegion, timeoutSeconds: 30, memory: '256MiB',
  maxInstances: 3, concurrency: 20, secrets: [metaAppSecret],
}, createQuestAttestationApp(service, readQuestProxyCidrs(process.env)));
