// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
const assert = require('node:assert/strict');
const test = require('node:test');
const http = require('node:http');
const express = require('express');
const { createQuestAttestationService, validateQuestClaims, verifyMetaQuestToken,
  readQuestAttestationPolicy, readQuestProxyCidrs, QUEST_APP_CHECK_TTL_MS, QUEST_CHALLENGE_TTL_MS, questNonceHash,
} = require('../lib/functions/src/questAttestation.js');
const { createQuestAttestationApp } = require('../lib/functions/src/questAttestationEndpoint.js');
const { createQuestAttestationStore } = require('../lib/functions/src/questAttestationStore.js');

const now = 1_790_940_000_000;
const nonce = Buffer.alloc(32, 7).toString('base64url');
const challenge = { createdAt: now - 1000, expiresAt: now + 299_000 };
const policy = { metaAppId: '123456789', firebaseAppId: '1:123:android:abcdef',
  packageId: 'com.maestro.quest', certificateHashes: ['a'.repeat(64)], minimumVersion: 2 };
const claims = () => ({ request_details: { timestamp: now / 1000, exp: now / 1000 + 86400, nonce },
  app_state: { app_integrity_state: 'StoreRecognized', package_id: policy.packageId,
    package_cert_sha256_digest: [...policy.certificateHashes], version: '2' },
  device_state: { device_integrity_state: 'Advanced', unique_id: 'not-stored' } });
const envelope = value => ({ data: [{ message: 'success', claims: Buffer.from(JSON.stringify(value)).toString('base64url') }] });
const rejected = error => error.status === 401 && error.code === 'quest-attestation/rejected';
const unavailable = error => error.status === 503 && error.code === 'quest-attestation/unavailable';
const environment = () => ({ QUEST_ATTESTATION_ENABLED: 'true', QUEST_META_APP_ID: policy.metaAppId,
  QUEST_FIREBASE_APP_ID: policy.firebaseAppId, QUEST_ANDROID_PACKAGE: policy.packageId,
  QUEST_SIGNING_SHA256: policy.certificateHashes.join(','), QUEST_MINIMUM_VERSION_CODE: '2',
  QUEST_ATTESTATION_TRUSTED_PROXY_CIDRS: '127.0.0.1/32' });

function harness(overrides = {}) {
  const stored = new Map();
  const minted = [], verified = [], issued = [];
  let currentTime = now;
  const service = createQuestAttestationService({
    policy: () => policy,
    now: () => currentTime,
    store: {
      async issue(subject, hash, value) { issued.push({ subject, hash, value }); stored.set(hash, value); },
      async consume(_subject, hash) { const value = stored.get(hash); stored.delete(hash); return value || null; },
    },
    async verify(token, configuration) {
      verified.push({ token, configuration });
      const value = claims(); value.request_details.nonce = issued.at(-1).nonce;
      return envelope(value);
    },
    async mint(appId, ttlMillis) { minted.push({ appId, ttlMillis }); return { token: 'firebase-app-check', ttlMillis }; },
    ...overrides,
  });
  return { service, stored, minted, verified, issued, setTime: value => { currentTime = value; },
    async start() { const value = await service.challenge('client', {}); issued.at(-1).nonce = value.nonce; return value; } };
}

test('configuration is explicit and disabled by default; incomplete or development releases cannot mint', () => {
  assert.equal(readQuestAttestationPolicy({}), null);
  assert.equal(readQuestAttestationPolicy({ QUEST_ATTESTATION_ENABLED: 'yes' }), null);
  assert.deepEqual(readQuestAttestationPolicy(environment()), policy);
  for (const [key, value] of Object.entries(environment())) {
    if (key === 'QUEST_ATTESTATION_ENABLED') continue;
    assert.throws(() => readQuestAttestationPolicy({ ...environment(), [key]: '' }), unavailable, key);
  }
  for (const change of [{ QUEST_MINIMUM_VERSION_CODE: '-1' }, { QUEST_MINIMUM_VERSION_CODE: '2147483648' },
    { QUEST_MINIMUM_VERSION_CODE: '2.5' }, { QUEST_ANDROID_PACKAGE: 'com.maestro.quest.development' },
    { QUEST_SIGNING_SHA256: 'not-a-fingerprint' }, { QUEST_META_APP_ID: '1|secret' }]) {
    assert.throws(() => readQuestAttestationPolicy({ ...environment(), ...change }), unavailable);
  }
});

test('verified claims accept the expected release and tolerate new non-security claims', () => {
  const value = claims(); value.future_claim = { example: true }; value.device_ban = { is_banned: false };
  value.app_state.package_cert_sha256_digest.push('b'.repeat(64));
  validateQuestClaims(envelope(value), policy, nonce, challenge, now);
});

for (const [name, alter] of [
  ['nonce mismatch', c => { c.request_details.nonce = 'other'; }],
  ['expired token', c => { c.request_details.exp = now / 1000; }],
  ['future token', c => { c.request_details.timestamp += 31; }],
  ['old token', c => { c.request_details.timestamp -= 301; }],
  ['predates challenge', c => { c.request_details.timestamp -= 32; }],
  ['fractional timestamp', c => { c.request_details.timestamp += 0.5; }],
  ['numeric string time', c => { c.request_details.timestamp = String(now / 1000); }],
  ['wrong package', c => { c.app_state.package_id = 'another.app'; }],
  ['wrong certificate', c => { c.app_state.package_cert_sha256_digest = ['b'.repeat(64)]; }],
  ['missing certificates', c => { c.app_state.package_cert_sha256_digest = []; }],
  ['mixed malformed certificates', c => { c.app_state.package_cert_sha256_digest.push(42); }],
  ['old version', c => { c.app_state.version = '1'; }],
  ['numeric version', c => { c.app_state.version = 2; }],
  ['noncanonical version', c => { c.app_state.version = '02'; }],
  ['store unknown', c => { c.app_state.app_integrity_state = 'NotEvaluated'; }],
  ['store unrecognized', c => { c.app_state.app_integrity_state = 'NotRecognized'; }],
  ['basic device integrity', c => { c.device_state.device_integrity_state = 'Basic'; }],
  ['untrusted device', c => { c.device_state.device_integrity_state = 'NotTrusted'; }],
  ['banned device', c => { c.device_ban = { is_banned: true }; }],
  ['malformed ban', c => { c.device_ban = { is_banned: 'false' }; }],
]) test(`rejects ${name}`, () => {
  const value = claims(); alter(value);
  assert.throws(() => validateQuestClaims(envelope(value), policy, nonce, challenge, now), rejected);
});

test('rejects provider errors, raw JWT claims, ambiguous or malformed success payloads', () => {
  for (const response of [claims(), null, {}, { data: [] }, { data: [{ message: 'invalid signature' }] },
    { data: [{ message: 'token expired' }] }, { data: envelope(claims()).data.concat(envelope(claims()).data) },
    { data: [{ message: 'success', claims: 'e30=' }] }, { data: [{ message: 'success', claims: 'e30' }] },
    { data: [{ message: 'success', claims: '_w' }] },
    { data: [{ message: 'success', claims: 'a'.repeat(33_000) }] }]) {
    assert.throws(() => validateQuestClaims(response, policy, nonce, challenge, now), rejected);
  }
  assert.throws(() => validateQuestClaims(envelope(claims()), policy, nonce, { ...challenge, expiresAt: now }, now), rejected);
});

test('one challenge mints only once and only for the server configured app and lifetime', async () => {
  const h = harness(); const c = await h.start();
  assert.match(c.nonce, /^[A-Za-z0-9_-]{43}$/);
  assert.equal(c.expiresAt, now + QUEST_CHALLENGE_TTL_MS);
  assert.equal(h.issued[0].hash, questNonceHash(c.nonce));
  const results = await Promise.allSettled(Array.from({ length: 8 }, () => h.service.exchange('client', { nonce: c.nonce, token: 'header.body.sig' })));
  assert.equal(results.filter(r => r.status === 'fulfilled').length, 1);
  assert.equal(h.verified.length, 1); assert.equal(h.minted.length, 1);
  assert.deepEqual(h.minted[0], { appId: policy.firebaseAppId, ttlMillis: 1800000 });
  assert.equal(QUEST_APP_CHECK_TTL_MS, 1800000);
});

test('bad proof, provider outage and mint failure burn the challenge without retry minting', async () => {
  for (const overrides of [
    { verify: async () => ({ data: [{ message: 'invalid signature' }] }) },
    { verify: async () => { throw new Error('provider down'); } },
    { mint: async () => { throw new Error('mint down'); } },
  ]) {
    const h = harness(overrides); const c = await h.start();
    await assert.rejects(h.service.exchange('client', { nonce: c.nonce, token: 'header.body.sig' }));
    await assert.rejects(h.service.exchange('client', { nonce: c.nonce, token: 'header.body.sig' }), rejected);
    assert.equal(h.minted.length, 0);
  }
});

test('proof that finishes after challenge expiry never mints', async () => {
  const h = harness(); const c = await h.start(); h.setTime(c.expiresAt);
  await assert.rejects(h.service.exchange('client', { nonce: c.nonce, token: 'header.body.sig' }), rejected);
  assert.equal(h.minted.length, 0);
});

test('disabled endpoint, arbitrary parameters and malformed payloads never reach storage/provider', async () => {
  const off = harness({ policy: () => null });
  await assert.rejects(off.service.challenge('client', {}), unavailable);
  assert.equal(off.issued.length, 0);
  const h = harness();
  for (const body of [null, [], { appId: 'caller-app' }]) await assert.rejects(h.service.challenge('client', body), e => e.status === 400);
  for (const body of [null, [], {}, { nonce, token: 'broken' }, { nonce, token: 'a.b.c', appId: 'caller-app' },
    { nonce, token: 'a'.repeat(33_000) + '.b.c' }, { nonce: '../escape', token: 'a.b.c' }]) {
    await assert.rejects(h.service.exchange('client', body), e => e.status === 400);
  }
  assert.equal(h.issued.length, 0); assert.equal(h.verified.length, 0); assert.equal(h.minted.length, 0);
});

test('storage outage fails closed, never reaching Meta or minting a token', async () => {
  const store = createQuestAttestationStore({ collection: () => ({ doc: () => ({}) }),
    runTransaction: async () => { throw new Error('database secret diagnostics'); } });
  const h = harness({ store });
  await assert.rejects(h.service.challenge('client', {}), unavailable);
  await assert.rejects(h.service.exchange('client', { nonce, token: 'a.b.c' }), unavailable);
  assert.equal(h.verified.length, 0); assert.equal(h.minted.length, 0);
});

test('Meta transport is fixed, rejects redirects/errors/oversize, and sanitizes secret-bearing failures', async () => {
  let request;
  const proof = envelope(claims());
  assert.deepEqual(await verifyMetaQuestToken('header.body.sig', policy, 'private-secret', async (url, init) => {
    request = { url, init }; return new Response(JSON.stringify(proof));
  }), proof);
  assert.equal(request.url.origin, 'https://graph.oculus.com');
  assert.equal(request.url.pathname, '/platform_integrity/verify');
  assert.equal(request.url.searchParams.get('access_token'), 'OC|123456789|private-secret');
  assert.equal(request.init.redirect, 'error'); assert.ok(request.init.signal instanceof AbortSignal);
  for (const provider of [async () => new Response('redirect', { status: 302 }),
    async () => new Response('fail', { status: 500 }), async () => new Response('x'.repeat(65537)),
    async () => new Response('not JSON'), async () => { throw new Error(request.url.href); }]) {
    await assert.rejects(verifyMetaQuestToken('header.body.sig', policy, 'private-secret', provider), error => {
      assert.ok(!error.message.includes('private-secret')); assert.ok(!error.message.includes('graph.oculus')); return unavailable(error);
    });
  }
});

async function withServer(app, run) {
  const server = http.createServer(app);
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  try { await run(`http://127.0.0.1:${server.address().port}`); }
  finally { await new Promise(resolve => server.close(resolve)); }
}

test('HTTP bootstrap enforces origin, JSON, body limits and no-store without requiring an existing App Check token', async () => {
  const h = harness(); const app = createQuestAttestationApp(h.service, ['127.0.0.1/32']);
  await withServer(app, async url => {
    const post = (path, body, headers = {}) => fetch(url + path, { method: 'POST', body,
      headers: { 'Content-Type': 'application/json', Origin: 'https://appassets.androidplatform.net', 'X-Forwarded-For': '203.0.113.9', ...headers } });
    const preflight = await fetch(url + '/challenge', { method: 'OPTIONS', headers: { Origin: 'https://appassets.androidplatform.net' } });
    assert.equal(preflight.status, 204);
    const result = await post('/challenge', '{}'); assert.equal(result.status, 200);
    assert.equal(result.headers.get('cache-control'), 'no-store');
    assert.equal(result.headers.get('access-control-allow-origin'), 'https://appassets.androidplatform.net');
    assert.equal((await post('/challenge', '{}', { Origin: 'https://evil.invalid' })).status, 403);
    assert.equal((await post('/challenge', '{')).status, 400);
    assert.equal((await post('/challenge', '{}', { 'Content-Type': 'text/plain' })).status, 415);
    assert.equal((await post('/challenge', '"' + 'x'.repeat(40000) + '"')).status, 413);
    assert.equal((await post('/not-found', '{}')).status, 404);
    assert.equal(h.issued.length, 1);
  });
});

test('HTTP rechecks Firebase-preparsed raw bodies and hides unexpected errors', async () => {
  const h = harness();
  const outer = express();
  outer.use(express.json({ limit: '1mb', verify: (req, _res, buffer) => { req.rawBody = buffer; } }));
  outer.use(createQuestAttestationApp(h.service));
  await withServer(outer, async url => {
    const result = await fetch(url + '/challenge', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: ' '.repeat(40000) + '{}' });
    assert.equal(result.status, 413); assert.equal(h.issued.length, 0);
  });
  let calls = 0;
  await withServer(createQuestAttestationApp({
    challenge: async () => { calls++; throw new Error('sensitive upstream URL'); }, exchange: async () => {},
  }, ['127.0.0.1/32']), async url => {
    const result = await fetch(url + '/challenge', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Forwarded-For': '203.0.113.9' }, body: '{}' });
    assert.equal(calls, 1); assert.equal(result.status, 503); assert.equal((await result.json()).code, 'quest-attestation/unavailable');
  });
});


test('proxy trust requires explicit IP ranges and rejects wildcard or permissive settings', () => {
  for (const value of ['', '*', 'true', '2', '0.0.0.0/0', '::/0', '192.0.2.1/33', '::1/129', '192.0.2.1/-1']) {
    assert.equal(readQuestProxyCidrs({ QUEST_ATTESTATION_TRUSTED_PROXY_CIDRS: value }), null);
  }
  assert.deepEqual(readQuestProxyCidrs({ QUEST_ATTESTATION_TRUSTED_PROXY_CIDRS: '192.0.2.1/32,2001:db8::/32' }), ['192.0.2.1/32', '2001:db8::/32']);
});

test('trusted ingress resolves the nearest untrusted IP; caller-prepended values cannot evade throttling', async () => {
  const subjects = [];
  const service = { async challenge(subject) { subjects.push(subject); return {}; }, async exchange() {} };
  const post = (url, forwarded) => fetch(url + '/challenge', { method: 'POST', body: '{}', headers: {
    'Content-Type': 'application/json', 'X-Forwarded-For': forwarded,
  } });
  await withServer(createQuestAttestationApp(service, ['loopback']), async url => {
    await post(url, '198.51.100.2, 192.0.2.1');
    await post(url, '203.0.113.2, 192.0.2.1');
    assert.deepEqual(subjects.splice(0), ['192.0.2.1', '192.0.2.1']);
  });
  await withServer(createQuestAttestationApp(service), async url => {
    assert.equal((await post(url, '198.51.100.2')).status, 503);
    assert.equal((await post(url, '203.0.113.2')).status, 503);
    assert.deepEqual(subjects, []);
  });
});
