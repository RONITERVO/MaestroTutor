// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import { createHash } from 'node:crypto';
import express, { type Request, type Response } from 'express';
import { defineSecret } from 'firebase-functions/params';
import { onRequest } from 'firebase-functions/v2/https';
import { onSchedule } from 'firebase-functions/v2/scheduler';
import { deleteManagedAccount, submitAiContentReport } from './account';
import { type AuthContext, applyCors, getOptionalAuthContext, requireAuthContext } from './auth';
import { appConfig, getJsonBodyLimitBytes } from './config';
import { adminDb } from './firebase';
import { generateManagedContent, streamManagedContent, generateManagedMusic, uploadManagedMedia, getManagedFileStatuses, deleteManagedFile, clearManagedFiles, createManagedLiveToken, releaseManagedLiveLease, retryManagedFileCleanupJobs } from './gemini';
import { getErrorMessage, getHttpStatus } from './http';
import { countExpiredReservations, getManagedAccountState, listManagedBillingLedger, listManagedUsageLedger, sweepExpiredReservations } from './managedBilling';
import type { ManagedRateLimitBucket } from './managedData';
import { consumeRateLimit } from './rateLimit';
import { createManagedCheckoutSession, handleStripeWebhook } from './stripeBilling';

const geminiApiKeySecret = defineSecret('GEMINI_API_KEY');
const stripeSecret = defineSecret('STRIPE_SECRET');
const stripeWebhookSecret = defineSecret('STRIPE_WEBHOOK_SECRET');

const app = express();

const FIRESTORE_HEALTH_CACHE_MS = 15_000;
let firestoreHealthCheckedAt = 0;
let firestoreHealthReady = false;
let firestoreHealthProbe: Promise<boolean> | null = null;

const probeFirestore = async (): Promise<boolean> => {
  const currentTime = Date.now();
  if (currentTime - firestoreHealthCheckedAt < FIRESTORE_HEALTH_CACHE_MS) {
    return firestoreHealthReady;
  }
  if (firestoreHealthProbe) return firestoreHealthProbe;

  firestoreHealthProbe = (async () => {
    try {
      await Promise.race([
        adminDb.collection('managedSystem').doc('health').get(),
        new Promise((_, reject) => setTimeout(
          () => reject(new Error('Firestore health probe timed out.')),
          3_000,
        )),
      ]);
      firestoreHealthReady = true;
    } catch (error) {
      firestoreHealthReady = false;
      console.error('[health] Firestore readiness probe failed.', error);
    } finally {
      firestoreHealthCheckedAt = Date.now();
      firestoreHealthProbe = null;
    }
    return firestoreHealthReady;
  })();

  return firestoreHealthProbe;
};

/*
 * Registered before the JSON parser, and only for this route.
 *
 * Stripe signs the exact bytes it sent, so verification needs the raw body.
 * Once express.json() has parsed and the handler re-serialises, the bytes
 * differ and every signature check fails — with an error that looks like a
 * misconfigured secret rather than middleware ordering. It is also
 * deliberately unauthenticated: Stripe calls it, not a signed-in browser, and
 * the signature is what establishes trust.
 */
app.post(
  '/billing/stripe/webhook',
  express.raw({ type: 'application/json' }),
  async (req: Request, res: Response) => {
    try {
      await handleStripeWebhook(req, res);
    } catch (error) {
      res.status(getHttpStatus(error)).json({ error: getErrorMessage(error) });
    }
  },
);

// Browser preflight requests do not match the GET/POST route that eventually
// calls asyncRoute, so handle them before routing. Without this middleware the
// browser receives Express's generic OPTIONS response without CORS headers and
// never sends requests carrying Authorization or X-Firebase-AppCheck.
app.use((req, res, next) => {
  if (req.method !== 'OPTIONS') {
    next();
    return;
  }
  applyCors(req, res);
});

// Derived from the advertised upload limit rather than set independently, so a
// file the API says it accepts cannot be rejected by middleware. See config.ts.
app.use(express.json({ limit: getJsonBodyLimitBytes() }));

/**
 * Throttle bucket for a route, or null to skip. Live tokens get their own,
 * much tighter, bucket: each one mints real Gemini access, and a client stuck
 * reconnecting would otherwise mint them as fast as it could loop.
 */
type RateBucket = ManagedRateLimitBucket | null;

const getAnonymousRateLimitId = (req: Request): string => {
  const connectionAddress = req.ip || req.socket.remoteAddress || 'unknown';
  const digest = createHash('sha256').update(connectionAddress).digest('hex');
  return `anonymous-${digest}`;
};

const asyncRoute = (
  authMode: 'none' | 'optional' | 'required',
  handler: (req: Request, res: Response, auth: AuthContext | null) => Promise<void>,
  rateBucket: RateBucket = 'default'
) => async (req: Request, res: Response) => {
  try {
    if (!applyCors(req, res)) return;
    let auth: AuthContext | null = null;
    if (authMode === 'required') {
      auth = await requireAuthContext(req);
    } else if (authMode === 'optional') {
      auth = await getOptionalAuthContext(req);
    }
    const rateLimitId = auth?.uid || (authMode === 'optional' ? getAnonymousRateLimitId(req) : null);
    if (rateLimitId && rateBucket) {
      await consumeRateLimit({
        uid: rateLimitId,
        bucket: rateBucket,
        limitPerMinute: auth
          ? (rateBucket === 'live-token'
            ? appConfig.liveTokenRateLimitPerMinute
            : appConfig.rateLimitPerMinute)
          : appConfig.anonymousReportRateLimitPerMinute,
      });
    }
    await handler(req, res, auth);
  } catch (error) {
    if (!res.headersSent) {
      res.status(getHttpStatus(error)).json({ error: getErrorMessage(error) });
      return;
    }
    if (!res.writableEnded) {
      res.write(`${JSON.stringify({
        type: 'error',
        message: getErrorMessage(error),
        status: getHttpStatus(error),
      })}\n`);
      res.end();
    }
  }
};

app.get('/health', asyncRoute('none', async (_req, res) => {
  const firestoreReady = await probeFirestore();
  const geminiConfigured = Boolean(appConfig.geminiApiKey);
  const ready = firestoreReady && geminiConfigured;
  res.status(ready ? 200 : 503).json({
    ok: ready,
    region: appConfig.functionRegion,
    firestoreReady,
    // Presence checks are intentionally named "configured", not "ready".
    // Secret Manager has no API for the provider's prepaid Gemini balance, and
    // a countTokens call can succeed while paid generation is rejected. The
    // release runbook's real generation smoke test is the readiness proof.
    geminiConfigured,
    stripeConfigured: Boolean(appConfig.stripeSecretKey && appConfig.stripeWebhookSecret),
    paidProviderSmokeRequired: true,
    appCheckRequired: appConfig.requireAppCheck,
    managedCreditPacks: appConfig.creditPacks.map((pack) => pack.id),
    managedGenerationModels: [...appConfig.managedAllowedGeminiModels],
    managedLiveModels: [...appConfig.managedAllowedLiveModels],
    managedMusicModels: [...appConfig.managedAllowedMusicModels],
  });
}));

app.get('/auth/session', asyncRoute('required', async (_req, res, auth) => {
  const account = await getManagedAccountState(auth!.uid, auth!.user);
  res.json({ session: account });
}));

app.get('/account/summary', asyncRoute('required', async (_req, res, auth) => {
  const account = await getManagedAccountState(auth!.uid, auth!.user);
  res.json({ account });
}));

app.get('/account/usage-ledger', asyncRoute('required', async (req, res, auth) => {
  const limit = Number(req.query.limit || 50);
  const entries = await listManagedUsageLedger(auth!.uid, limit);
  res.json({ entries });
}));

app.get('/account/billing-ledger', asyncRoute('required', async (req, res, auth) => {
  const limit = Number(req.query.limit || 50);
  const entries = await listManagedBillingLedger(auth!.uid, limit);
  res.json({ entries });
}));

app.post('/account/delete', asyncRoute('required', async (_req, res, auth) => {
  const result = await deleteManagedAccount({
    uid: auth!.uid,
    user: auth!.user,
  });
  res.json(result);
}));

app.post('/billing/stripe/checkout', asyncRoute('required', async (req, res, auth) => {
  const result = await createManagedCheckoutSession({
    uid: auth!.uid,
    user: auth!.user,
    packId: String(req.body?.packId || ''),
  });
  res.json(result);
}));

app.post('/reports/ai-content', asyncRoute('optional', async (req, res, auth) => {
  const result = await submitAiContentReport({
    req,
    auth,
    payload: (req.body && typeof req.body === 'object') ? req.body as Record<string, unknown> : {},
  });
  res.json(result);
}));

app.post('/gemini/generate-content', asyncRoute('required', async (req, res, auth) => {
  const result = await generateManagedContent({
    uid: auth!.uid,
    user: auth!.user,
    model: String(req.body?.model || ''),
    contents: req.body?.contents,
    config: req.body?.config,
  });
  res.json(result);
}));

app.post('/gemini/generate-content-stream', asyncRoute('required', async (req, res, auth) => {
  await streamManagedContent({
    uid: auth!.uid,
    user: auth!.user,
    model: String(req.body?.model || ''),
    contents: req.body?.contents,
    config: req.body?.config,
    response: res,
  });
}));

app.post('/gemini/generate-music', asyncRoute('required', async (req, res, auth) => {
  const result = await generateManagedMusic({
    uid: auth!.uid,
    user: auth!.user,
    model: String(req.body?.model || ''),
    prompt: String(req.body?.prompt || ''),
    durationSeconds: Number(req.body?.durationSeconds || 0) || undefined,
  });
  res.json(result);
}, 'live-token'));

app.post('/gemini/upload-media', asyncRoute('required', async (req, res, auth) => {
  const result = await uploadManagedMedia({
    uid: auth!.uid,
    user: auth!.user,
    dataUrl: String(req.body?.dataUrl || ''),
    mimeType: String(req.body?.mimeType || ''),
    displayName: typeof req.body?.displayName === 'string' ? req.body.displayName : undefined,
  });
  res.json(result);
}));

app.post('/gemini/file-statuses', asyncRoute('required', async (req, res, auth) => {
  const uris = Array.isArray(req.body?.uris) ? req.body.uris.map((uri: unknown) => String(uri)) : [];
  const result = await getManagedFileStatuses(auth!.uid, uris);
  res.json(result);
}));

app.post('/gemini/delete-file', asyncRoute('required', async (req, res, auth) => {
  const result = await deleteManagedFile(auth!.uid, String(req.body?.nameOrUri || ''));
  res.json(result);
}));

app.post('/gemini/clear-files', asyncRoute('required', async (_req, res, auth) => {
  const result = await clearManagedFiles(auth!.uid);
  res.json(result);
}));

app.post('/gemini/live-token', asyncRoute('required', async (req, res, auth) => {
  if (req.body?.purpose === 'music') {
    res.status(400).json({ error: 'Managed music uses the authenticated music generation route.' });
    return;
  }
  const result = await createManagedLiveToken({
    uid: auth!.uid,
    user: auth!.user,
    model: String(req.body?.model || ''),
    config: req.body?.config,
    purpose: 'live',
    durationSeconds: Number(req.body?.durationSeconds || 0) || undefined,
  });
  res.json(result);
}, 'live-token'));

app.post('/gemini/live-token/release', asyncRoute('required', async (req, res, auth) => {
  const leaseId = typeof req.body?.leaseId === 'string' ? req.body.leaseId : '';
  const result = await releaseManagedLiveLease(auth!.uid, leaseId);
  res.json(result);
}));

export const api = onRequest(
  {
    region: appConfig.functionRegion,
    timeoutSeconds: 540,
    memory: '1GiB',
    maxInstances: appConfig.functionMaxInstances,
    concurrency: appConfig.functionConcurrency,
    secrets: [geminiApiKeySecret, stripeSecret, stripeWebhookSecret],
  },
  app
);

export const releaseExpiredReservations = onSchedule(
  {
    region: appConfig.functionRegion,
    schedule: 'every 10 minutes',
    timeZone: 'UTC',
    timeoutSeconds: 540,
  },
  async () => {
    const batchLimit = 200;
    let releasedCount = 0;
    let batchCount = 0;
    do {
      batchCount = await sweepExpiredReservations(batchLimit);
      releasedCount += batchCount;
    } while (batchCount === batchLimit);

    const remainingCount = await countExpiredReservations();
    console.info('[billing] Expired reservation sweep completed.', {
      expiredReservationsReleased: releasedCount,
      expiredReservationsRemaining: remainingCount,
    });
  }
);

export const retryManagedFileCleanup = onSchedule(
  {
    region: appConfig.functionRegion,
    schedule: 'every 60 minutes',
    timeZone: 'UTC',
    timeoutSeconds: 540,
    secrets: [geminiApiKeySecret],
  },
  async () => {
    const result = await retryManagedFileCleanupJobs(100);
    console.info('[files] Managed cleanup retry completed.', result);
  },
);
