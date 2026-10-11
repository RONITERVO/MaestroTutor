// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
const assert = require('node:assert/strict');
const http = require('node:http');
const { test } = require('node:test');
const { api } = require('../lib/functions/src/index.js');
const { appConfig } = require('../lib/functions/src/config.js');
const { adminAppCheck, adminAuth } = require('../lib/functions/src/firebase.js');
const billing = require('../lib/functions/src/stripeBilling.js');
const accounts = require('../lib/functions/src/managedBilling.js');
const limits = require('../lib/functions/src/rateLimit.js');

const questId = '1:123:web:abcdef';
const webId = '1:123:web:fedcba';
const questOrigin = 'https://appassets.androidplatform.net';

test('actual managed HTTP routes enforce the Quest purchase boundary without a second account', async t => {
  const oldConfig = { requireAppCheck: appConfig.requireAppCheck, questFirebaseAppId: appConfig.questFirebaseAppId };
  const allowedQuestOrigin = appConfig.allowedOrigins.has(questOrigin);
  appConfig.requireAppCheck = true;
  appConfig.questFirebaseAppId = questId;
  appConfig.allowedOrigins.add(questOrigin);
  const verified = t.mock.method(adminAppCheck, 'verifyToken', async token => {
    if (token === 'quest-proof') return { appId: questId };
    if (token === 'web-proof') return { appId: webId };
    throw new Error('Invalid proof');
  });
  t.mock.method(adminAuth, 'verifyIdToken', async (token, checkRevoked) => {
    assert.equal(token, 'identity'); assert.equal(checkRevoked, true);
    return { uid: 'same-maestro-user', email: 'test@example.invalid' };
  });
  t.mock.method(limits, 'consumeRateLimit', async () => {});
  const checkout = t.mock.method(billing, 'createManagedCheckoutSession', async params => {
    assert.equal(params.uid, 'same-maestro-user');
    return { sessionId: 'offline-session', url: 'https://checkout.stripe.com/offline' };
  });
  const balance = t.mock.method(accounts, 'getManagedAccountState', async uid => ({
    user: { id: uid }, billingSummary: { availableCredits: 17 },
  }));
  const server = http.createServer(api);
  await new Promise((resolve, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', resolve); });
  const baseUrl = `http://127.0.0.1:${server.address().port}`;
  const request = async ({ proof = 'quest-proof', origin, body = {}, path = '/billing/stripe/checkout', method = 'POST' } = {}) => {
    const headers = { Authorization: 'Bearer identity', 'Content-Type': 'application/json' };
    if (proof) headers['X-Firebase-AppCheck'] = proof;
    if (origin) headers.Origin = origin;
    const response = await fetch(baseUrl + path, { method, headers,
      ...(method === 'POST' ? { body: JSON.stringify({ packId: 'pack_1000', ...body }) } : {}) });
    return { status: response.status, body: await response.json() };
  };
  const deny = async (input, status = 403, code = 'billing/checkout-unavailable') => {
    const before = checkout.mock.callCount();
    const result = await request(input);
    assert.equal(result.status, status); assert.equal(result.body.code, code);
    assert.equal(checkout.mock.callCount(), before, 'denied requests never reach Stripe or create a session');
  };
  try {
    for (const origin of [undefined, 'http://localhost', questOrigin]) {
      await t.test(`Quest proof cannot buy with origin ${origin || '(absent)'}`, () => deny({ origin }));
    }
    await t.test('a body claiming the web app cannot override verified Quest identity', () => deny({
      body: { appId: webId, appCheckAppId: webId, platform: 'web' },
    }));
    await t.test('local Quest origin denies even a web proof', () => deny({ proof: 'web-proof', origin: questOrigin }));
    await t.test('only verified proof contents identify an app', () => deny({ proof: 'made-up-web-proof' }, 401, 'app-check/invalid'));
    await t.test('missing proof fails authentication before checkout', () => deny({ proof: '' }, 401, 'app-check/missing'));
    await t.test('original web checkout still uses the shared account', async () => {
      const before = checkout.mock.callCount(), proofsBefore = verified.mock.callCount();
      const result = await request({ proof: 'web-proof', body: { appId: questId } });
      assert.equal(result.status, 200); assert.equal(result.body.sessionId, 'offline-session');
      assert.equal(checkout.mock.callCount(), before + 1);
      assert.equal(verified.mock.callCount(), proofsBefore + 1, 'proof is verified once, not once per layer');
    });
    for (const path of ['/auth/session', '/account/summary']) {
      await t.test(`Quest preserves ${path}`, async () => {
        const result = await request({ path, method: 'GET', origin: questOrigin });
        assert.equal(result.status, 200);
        const account = result.body.account || result.body.session;
        assert.equal(account.user.id, 'same-maestro-user'); assert.equal(account.billingSummary.availableCredits, 17);
        assert.ok(balance.mock.callCount() > 0);
      });
    }
    await t.test('disabling attestation issuance does not make already minted Quest proofs buyable', async () => {
      // The purchase rule depends on the registration, not the issuance feature flag.
      const oldEnabled = process.env.QUEST_ATTESTATION_ENABLED;
      process.env.QUEST_ATTESTATION_ENABLED = 'false';
      try { await deny({}); } finally {
        if (oldEnabled === undefined) delete process.env.QUEST_ATTESTATION_ENABLED;
        else process.env.QUEST_ATTESTATION_ENABLED = oldEnabled;
      }
    });
    await t.test('App Check incident rollback cannot silently enable purchases', async () => {
      appConfig.requireAppCheck = false;
      try {
        await deny({}, 503);
        await deny({ origin: questOrigin });
        const account = await request({ method: 'GET', path: '/account/summary', proof: '' });
        assert.equal(account.status, 200, 'existing service rollback behavior is unchanged');
      } finally { appConfig.requireAppCheck = true; }
    });
    await t.test('before Quest is configured, existing web checkout remains available', async () => {
      appConfig.questFirebaseAppId = '';
      try {
        assert.equal((await request({ proof: 'web-proof' })).status, 200);
        await deny({ proof: 'web-proof', origin: questOrigin });
      } finally { appConfig.questFirebaseAppId = questId; }
    });
  } finally {
    await new Promise((resolve, reject) => server.close(error => error ? reject(error) : resolve()));
    Object.assign(appConfig, oldConfig);
    if (!allowedQuestOrigin) appConfig.allowedOrigins.delete(questOrigin);
  }
});
