// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
const assert = require('node:assert/strict');
const test = require('node:test');
const http = require('node:http');
const express = require('express');
const { createQuestAccountLinkService, readQuestLinkPolicy, questLinkHash, linkRejected } = require('../lib/functions/src/questAccountLink.js');
const { createQuestAccountLinkApp } = require('../lib/functions/src/questAccountLinkEndpoint.js');
const { createQuestAccountLinkStore } = require('../lib/functions/src/questAccountLinkStore.js');
const NOW = 1790950000000;
const policy = { questAppId: '1:123:web:abcdef', webAppId: '1:123:web:fedcba', verificationUrl: 'https://chatwithmaestro.com/quest-link.html' };
const device = { ip: 'device', appCheck: 'quest-proof', authorization: '', origin: 'https://appassets.androidplatform.net' };
const browser = { ip: 'browser', appCheck: 'browser-proof', authorization: 'Bearer identity', origin: 'https://chatwithmaestro.com' };
const environment = () => ({ QUEST_ACCOUNT_LINK_ENABLED: 'true', QUEST_ATTESTATION_ENABLED: 'true',
  QUEST_FIREBASE_APP_ID: policy.questAppId, QUEST_WEB_FIREBASE_APP_ID: policy.webAppId,
  QUEST_ACCOUNT_LINK_VERIFY_URL: policy.verificationUrl, QUEST_ATTESTATION_TRUSTED_PROXY_CIDRS: '127.0.0.1/32' });
const rejects = code => error => error.code === `quest-link/${code}`;
function harness(overrides = {}) {
  const records = new Map(), minted = [], throttles = [], verified = [];
  let time = NOW;
  const read = (hash, secret) => {
    const record = records.get(hash);
    if (!record || !['pending', 'approved'].includes(record.state) || record.expiresAt <= time
      || (secret && secret !== record.secretHash)) throw linkRejected();
    return record;
  };
  const store = {
    async throttle(...args) { throttles.push(args); },
    async issue(hash, record) { if (records.has(hash)) return false; records.set(hash, record); return true; },
    async status(hash, secret) { const r = read(hash, secret); return { state: r.state, expiresAt: r.expiresAt }; },
    async approve(hash, uid) { const r = read(hash); if (r.state !== 'pending') throw linkRejected(); r.state = 'approved'; r.uid = uid; },
    async claim(hash, secret) { const r = read(hash, secret); if (r.state !== 'approved') throw linkRejected(); r.state = 'consumed'; return { uid: r.uid, expiresAt: r.expiresAt }; },
    async cancel(hash, secret) { read(hash, secret).state = 'cancelled'; },
  };
  const service = createQuestAccountLinkService({
    policy: () => policy, store, now: () => time,
    async verifyApp(token) { verified.push(token); return { appId: token === 'quest-proof' ? policy.questAppId : token === 'browser-proof' ? policy.webAppId : 'untrusted' }; },
    async verifyUser() { return { uid: 'existing-user', auth_time: Math.floor(NOW / 1000), firebase: { sign_in_provider: 'google.com' } }; },
    async accountAvailable() { return true; },
    async mint(uid) { minted.push(uid); return 'firebase.custom.credential'; },
    ...overrides,
  });
  return { service, store, records, minted, throttles, verified, setTime: value => { time = value; },
    start: () => service.run('create', device, {}),
    approve: link => service.run('approve', browser, { code: link.code, confirm: true }),
    payload: link => ({ code: link.code, deviceSecret: link.deviceSecret }),
  };
}

test('pairing defaults off; enabling requires distinct same-project app registrations and strict verified URL/proxy policy', () => {
  assert.equal(readQuestLinkPolicy({}), null);
  assert.deepEqual(readQuestLinkPolicy(environment()), policy);
  for (const field of Object.keys(environment()).filter(k => k !== 'QUEST_ACCOUNT_LINK_ENABLED')) {
    assert.throws(() => readQuestLinkPolicy({ ...environment(), [field]: '' }), rejects('unavailable'), field);
  }
  for (const change of [{ QUEST_WEB_FIREBASE_APP_ID: policy.questAppId }, { QUEST_WEB_FIREBASE_APP_ID: '1:999:web:abc' },
    { QUEST_ACCOUNT_LINK_VERIFY_URL: 'http://chatwithmaestro.com/quest-link.html' },
    { QUEST_ACCOUNT_LINK_VERIFY_URL: 'https://user:pass@chatwithmaestro.com/quest-link.html' },
    { QUEST_ACCOUNT_LINK_VERIFY_URL: `${policy.verificationUrl}?secret=bad` },
    { QUEST_ACCOUNT_LINK_VERIFY_URL: `${policy.verificationUrl}#bad` },
    { QUEST_ACCOUNT_LINK_VERIFY_URL: 'https://chatwithmaestro.com/elsewhere' },
    { QUEST_ATTESTATION_TRUSTED_PROXY_CIDRS: '0.0.0.0/0' }]) {
    assert.throws(() => readQuestLinkPolicy({ ...environment(), ...change }), rejects('unavailable'));
  }
});

test('a fresh browser approval grants exactly the existing Firebase UID to its originating device', async () => {
  const h = harness(), link = await h.start(), payload = h.payload(link);
  assert.match(link.code, /^[A-HJ-NP-Z2-9]{5}-[A-HJ-NP-Z2-9]{5}$/);
  assert.match(link.deviceSecret, /^[A-Za-z0-9_-]{43}$/);
  assert.equal(link.verificationUrl, policy.verificationUrl); assert.equal(link.expiresAt, NOW + 300000);
  const record = [...h.records.values()][0];
  assert.equal(record.secretHash, questLinkHash(link.deviceSecret));
  assert.equal(JSON.stringify(record).includes(link.deviceSecret), false);
  assert.deepEqual(await h.service.run('status', device, payload), { state: 'pending', expiresAt: link.expiresAt });
  await assert.rejects(h.service.run('redeem', device, payload), rejects('expired'));
  assert.deepEqual(await h.approve(link), { approved: true });
  assert.deepEqual(await h.service.run('status', device, payload), { state: 'approved', expiresAt: link.expiresAt });
  assert.deepEqual(await h.service.run('redeem', device, payload), { customToken: 'firebase.custom.credential' });
  assert.deepEqual(h.minted, ['existing-user']);
  await assert.rejects(h.service.run('redeem', device, payload), rejects('expired'));
  assert.equal(h.minted.length, 1);
});

test('web proofs cannot bootstrap a Quest and Quest proofs cannot approve accounts', async () => {
  const h = harness(), link = await h.start();
  for (const operation of ['create', 'status', 'redeem', 'cancel']) {
    await assert.rejects(h.service.run(operation, browser, operation === 'create' ? {} : h.payload(link)), rejects('unauthorized'));
  }
  await assert.rejects(h.service.run('approve', { ...browser, appCheck: device.appCheck }, { code: link.code, confirm: true }), rejects('unauthorized'));
  assert.equal(h.records.values().next().value.state, 'pending');
});
for (const appCheck of ['', 'wrong-proof', 'x'.repeat(16385)]) test(`invalid App Check is always rejected (${appCheck.length})`, async () => {
  const h = harness();
  await assert.rejects(h.service.run('create', { ...device, appCheck }, {}), rejects('unauthorized'));
  assert.equal(h.throttles.length, 1); assert.equal(h.records.size, 0);
});
test('disabled service performs no persistence or credential verification', async () => {
  const h = harness({ policy: () => null });
  await assert.rejects(h.start(), rejects('unavailable')); assert.equal(h.throttles.length, 0); assert.equal(h.verified.length, 0);
});
test('all request bodies are exact: client UID, injected fields and partial bodies are rejected', async () => {
  const h = harness(), link = await h.start();
  for (const input of [null, [], { uid: 'attacker' }, { confirm: true }]) await assert.rejects(h.service.run('create', device, input), rejects('invalid'));
  for (const input of [{ code: link.code, confirm: false }, { code: link.code, confirm: true, uid: 'attacker' }, { code: link.code }]) {
    await assert.rejects(h.service.run('approve', browser, input), rejects('invalid'));
  }
  for (const input of [{ ...h.payload(link), uid: 'attacker' }, { code: link.code }, { code: '../path', deviceSecret: link.deviceSecret }, { ...h.payload(link), deviceSecret: 'guess' }]) {
    await assert.rejects(h.service.run('redeem', device, input), rejects('invalid'));
  }
  assert.equal(h.minted.length, 0);
});
test('knowing the displayed code does not grant status, cancellation or redemption', async () => {
  const h = harness(), link = await h.start(); await h.approve(link);
  for (const operation of ['status', 'cancel', 'redeem']) {
    await assert.rejects(h.service.run(operation, device, { code: link.code, deviceSecret: 'x'.repeat(43) }), rejects('expired'));
  }
  assert.equal(h.minted.length, 0);
});
for (const change of [{ auth_time: NOW / 1000 - 300 }, { auth_time: NOW / 1000 + 31 }, { auth_time: '1790950000' },
  { firebase: { sign_in_provider: 'custom' } }, { firebase: { sign_in_provider: 'anonymous' } }]) {
  test(`approval requires recent Google authentication: ${JSON.stringify(change)}`, async () => {
    const h = harness({ verifyUser: async () => ({ uid: 'existing-user', auth_time: NOW / 1000, firebase: { sign_in_provider: 'google.com' }, ...change }) });
    await assert.rejects(h.approve(await h.start()), rejects('recent-login-required')); assert.equal(h.minted.length, 0);
  });
}
test('approval requires configured browser origin and a verified bearer identity', async () => {
  const h = harness(), link = await h.start();
  for (const change of [{ origin: device.origin }, { origin: undefined }, { authorization: '' }]) {
    await assert.rejects(h.service.run('approve', { ...browser, ...change }, { code: link.code, confirm: true }), rejects('unauthorized'));
  }
  const invalidUser = harness({ verifyUser: async () => { throw new Error('revoked token'); } });
  await assert.rejects(invalidUser.approve(await invalidUser.start()), rejects('unauthorized'));
});
test('expiry is enforced at the boundary without relying on TTL deletion', async () => {
  const h = harness(), link = await h.start(); await h.approve(link); h.setTime(link.expiresAt);
  for (const operation of ['status', 'redeem', 'cancel']) await assert.rejects(h.service.run(operation, device, h.payload(link)), rejects('expired'));
  assert.equal(h.minted.length, 0);
});
test('cancellation makes approval and redemption impossible', async () => {
  const h = harness(), link = await h.start(); await h.service.run('cancel', device, h.payload(link));
  await assert.rejects(h.approve(link), rejects('expired'));
  await assert.rejects(h.service.run('redeem', device, h.payload(link)), rejects('expired'));
});
test('a second browser cannot replace an approved UID', async () => {
  const h = harness(), link = await h.start(); await h.approve(link);
  await assert.rejects(h.approve(link), rejects('expired')); assert.equal(h.records.values().next().value.uid, 'existing-user');
});
test('a signing failure burns the approval instead of enabling replay', async () => {
  let calls = 0;
  const h = harness({ mint: async () => { calls++; throw new Error('private signing detail'); } }), link = await h.start(); await h.approve(link);
  await assert.rejects(h.service.run('redeem', device, h.payload(link)));
  await assert.rejects(h.service.run('redeem', device, h.payload(link)), rejects('expired')); assert.equal(calls, 1);
});
test('account deletion or disabling prevents both approval and post-signing delivery', async () => {
  const blocked = harness({ accountAvailable: async () => false });
  await assert.rejects(blocked.approve(await blocked.start()), rejects('expired'));
  let checks = 0;
  const h = harness({ accountAvailable: async () => ++checks < 3 }), link = await h.start(); await h.approve(link);
  await assert.rejects(h.service.run('redeem', device, h.payload(link)), rejects('expired'));
  assert.equal(h.minted.length, 1);
});
test('a signing call that outlives the pairing deadline never returns a token', async () => {
  let h;
  h = harness({ mint: async () => { h.setTime(NOW + 300000); return 'private.token'; } });
  const link = await h.start(); await h.approve(link);
  await assert.rejects(h.service.run('redeem', device, h.payload(link)), rejects('expired'));
});
test('persistence failure is fail-closed for rate limits and issuance', async () => {
  const store = createQuestAccountLinkStore({ runTransaction: async () => { throw new Error('database credentials'); } });
  await assert.rejects(store.throttle('x', 'create', 12, NOW), rejects('unavailable'));
  await assert.rejects(store.issue('hash', {}), rejects('unavailable'));
});

async function serverTest(app, action) {
  const server = http.createServer(app); await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  try { await action(`http://127.0.0.1:${server.address().port}`); }
  finally { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
}
test('HTTP routes pass exact headers/body and verified proxy client IP without credentials in replies', async () => {
  const calls = [];
  const app = createQuestAccountLinkApp({ run: async (...args) => { calls.push(args); return { state: 'pending' }; } }, ['127.0.0.1/32']);
  await serverTest(app, async base => {
    const response = await fetch(`${base}/status`, { method: 'POST', headers: { 'Content-Type': 'application/json',
      Origin: device.origin, 'X-Firebase-AppCheck': 'proof', Authorization: 'Bearer auth', 'X-Forwarded-For': 'spoof, 203.0.113.9' }, body: '{}' });
    assert.equal(response.status, 200); assert.equal(response.headers.get('cache-control'), 'no-store');
    assert.equal(response.headers.get('referrer-policy'), 'no-referrer');
    assert.equal(calls[0][0], 'status'); assert.equal(calls[0][1].ip, '203.0.113.9');
    assert.equal(calls[0][1].appCheck, 'proof'); assert.equal(calls[0][1].authorization, 'Bearer auth');
    assert.deepEqual(await response.json(), { state: 'pending' });
  });
});
test('HTTP rejects unknown origins, oversized/preparsed bodies, encoded payloads and hides internal errors', async () => {
  let calls = 0;
  const linkApp = createQuestAccountLinkApp({ run: async () => { calls++; throw new Error('secret auth token database details'); } });
  const app = express();
  app.use((req, _res, next) => { if (req.headers['x-test-preparsed']) { req.body = {}; req.rawBody = Buffer.alloc(2049); } next(); });
  app.use(linkApp);
  await serverTest(app, async base => {
    const post = headers => fetch(`${base}/create`, { method: 'POST', headers: { 'Content-Type': 'application/json', ...headers }, body: '{}' });
    assert.equal((await post({ Origin: 'https://attacker.example' })).status, 403);
    assert.equal((await post({ 'x-test-preparsed': 'yes' })).status, 413);
    assert.equal((await post({ 'Content-Encoding': 'gzip' })).status, 415);
    assert.equal(calls, 0);
    const response = await post({}); assert.equal(response.status, 503);
    assert.deepEqual(await response.json(), { error: 'Quest account linking is unavailable. Try again later.', code: 'quest-link/unavailable' });
    const preflight = await fetch(`${base}/create`, { method: 'OPTIONS', headers: { Origin: device.origin } });
    assert.equal(preflight.status, 204); assert.match(preflight.headers.get('access-control-allow-headers'), /X-Firebase-AppCheck/);
  });
});
