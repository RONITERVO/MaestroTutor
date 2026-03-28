// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

import express, { type Request, type Response } from 'express';
import { onRequest } from 'firebase-functions/v2/https';
import { onSchedule } from 'firebase-functions/v2/scheduler';
import { deleteManagedAccount, submitAiContentReport } from './account';
import { type AuthContext, applyCors, getOptionalAuthContext, requireAuthContext } from './auth';
import { appConfig } from './config';
import { adminDb } from './firebase';
import { generateManagedContent, streamManagedContent, uploadManagedMedia, getManagedFileStatuses, deleteManagedFile, clearManagedFiles, createManagedLiveToken, releaseManagedLiveLease } from './gemini';
import { getErrorMessage, getHttpStatus } from './http';
import { getManagedAccountState, listManagedBillingLedger, listManagedUsageLedger, sweepExpiredReservations } from './managedBilling';
import { verifyManagedGooglePlayPurchase } from './playBilling';

const app = express();
app.use(express.json({ limit: '50mb' }));

const asyncRoute = (
  authMode: 'none' | 'optional' | 'required',
  handler: (req: Request, res: Response, auth: AuthContext | null) => Promise<void>
) => async (req: Request, res: Response) => {
  try {
    if (!applyCors(req, res)) return;
    let auth: AuthContext | null = null;
    if (authMode === 'required') {
      auth = await requireAuthContext(req);
    } else if (authMode === 'optional') {
      auth = await getOptionalAuthContext(req);
    }
    await handler(req, res, auth);
  } catch (error) {
    if (!res.headersSent) {
      res.status(getHttpStatus(error)).json({ error: getErrorMessage(error) });
      return;
    }
    if (!res.writableEnded) {
      res.write(`${JSON.stringify({ type: 'error', message: getErrorMessage(error) })}\n`);
      res.end();
    }
  }
};

app.get('/health', asyncRoute('none', async (_req, res) => {
  res.json({
    ok: true,
    region: appConfig.functionRegion,
    firestoreReady: Boolean(adminDb),
    managedBillingProducts: Object.keys(appConfig.managedCreditProducts),
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

app.post('/billing/google-play/verify', asyncRoute('required', async (req, res, auth) => {
  const purchase = req.body?.purchase;
  const result = await verifyManagedGooglePlayPurchase({
    uid: auth!.uid,
    user: auth!.user,
    purchase,
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
    operation: String(req.body?.operation || 'generateContent'),
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
    operation: String(req.body?.operation || 'generateContent'),
    response: res,
  });
}));

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
  const result = await createManagedLiveToken({
    uid: auth!.uid,
    user: auth!.user,
    purpose: req.body?.purpose === 'music' ? 'music' : 'live',
    durationSeconds: Number(req.body?.durationSeconds || 0) || undefined,
  });
  res.json(result);
}));

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
    await sweepExpiredReservations(50);
  }
);
