// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

// Exercise the public facade with real Firestore transactions and billing.
// Only the provider transport is replaced. Never run these writes remotely.
const assert = require('node:assert/strict');
const { test, beforeEach, after } = require('node:test');
const { randomUUID, createHash } = require('node:crypto');
const { EventEmitter } = require('node:events');
const fs = require('node:fs/promises');
assert.ok(process.env.FIRESTORE_EMULATOR_HOST, 'Firestore emulator is required');
assert.match(process.env.GCLOUD_PROJECT || '', /^demo-/, 'A demo project is required');
process.env.GEMINI_API_KEY = 'emulator-no-network';
process.env.MANAGED_MAX_ACTIVE_FILES_PER_USER = '2';
process.env.MANAGED_MAX_ACTIVE_LIVE_SOCKETS = '2';

let handlers = {};
let calls = [];
const invoke = (method, args) => {
  calls.push({ method, args });
  assert.equal(typeof handlers[method], 'function', `Unexpected provider call: ${method}`);
  return handlers[method](args);
};
const sdkPath = require.resolve('@google/genai');
require.cache[sdkPath] = { id: sdkPath, filename: sdkPath, loaded: true, exports: {
  GoogleGenAI: class {
    constructor() {
      this.models = Object.fromEntries(['countTokens', 'generateContent', 'generateContentStream']
        .map((method) => [method, (args) => invoke(method, args)]));
      this.files = Object.fromEntries(['upload', 'get', 'delete']
        .map((method) => [method, (args) => invoke(`files.${method}`, args)]));
      this.authTokens = { create: (args) => invoke('authTokens.create', args) };
      this.live = { music: { connect: (args) => invoke('music.connect', args) } };
    }
  },
} };

const api = require('../lib/functions/src/gemini.js');
const { adminDb } = require('../lib/functions/src/firebase.js');
const data = require('../lib/functions/src/managedData.js');
const billing = require('../lib/functions/src/managedBilling.js');
const created = [];
const jobRefs = [];
beforeEach(() => { calls = []; handlers = {}; });
const account = async (credits = 1000) => {
  const uid = `gemini-emulator-${randomUUID()}`;
  const user = { id: uid, email: null, displayName: null, photoUrl: null };
  const token = `purchase-${randomUUID()}`;
  created.push({ uid, token });
  if (credits) await billing.grantPurchasedCredits({
    uid, user, purchaseToken: token, productId: 'emulator', orderId: null,
    creditsGranted: credits, rawPurchase: {}, rawVerification: {},
  });
  return { uid, user };
};
const summary = async ({ uid, user }) => (await billing.getManagedAccountState(uid, user)).billingSummary;
const leases = async (uid) => (await data.managedLiveQuotaRef(uid).get()).data()?.activeManagedLiveLeases || [];
const reservations = async (uid) => (await data.managedReservationsCollection(uid).get()).docs.map((doc) => doc.data());
const quota = async (uid) => (await data.managedFileQuotaRef(uid).get()).data()?.activeManagedFileCount || 0;
const activeFile = async (uid, suffix, extra = {}) => {
  const name = `files/${suffix}`;
  const uri = `https://generativelanguage.googleapis.com/v1beta/${name}`;
  await data.managedFileRef(uid, name).set({ uid, name, uri, state: 'active', deletedAt: null, createdAt: 1, ...extra });
  return { name, uri, mimeType: 'image/png', state: 'ACTIVE' };
};
const generation = (owner, extra = {}) => ({
  ...owner, model: 'gemini-flash-latest', contents: [{ role: 'user', parts: [{ text: 'hello' }] }],
  config: { systemInstruction: 'teach me', maxOutputTokens: 64 }, ...extra,
});
const live = (owner) => ({
  ...owner, model: 'gemini-2.5-flash-native-audio-preview-12-2025',
  config: { responseModalities: ['AUDIO'], systemInstruction: 'speak briefly' },
  liveOpenReason: { trigger: 'voice.tts-click', requestId: 'emulator-request', requestedAt: '2026-09-23T12:00:00.000Z' },
});
const upload = (owner) => ({ ...owner, dataUrl: 'data:image/png;base64,aGVsbG8=', mimeType: 'image/png', displayName: 'lesson.png' });
const providerUsage = { promptTokenCount: 20, candidatesTokenCount: 10, totalTokenCount: 30 };

test('competing leases respect the shared cap, expired records and repeated release', async () => {
  const owner = await account();
  await data.managedLiveQuotaRef(owner.uid).set({ activeManagedLiveLeases: [
    { leaseId: 'expired', purpose: 'live', expiresAt: Date.now() - 1 }, { leaseId: '', expiresAt: Date.now() + 60000 },
  ] });
  const results = await Promise.allSettled(['live', 'music', 'live'].map((purpose) => api.reserveManagedLiveLease({
    uid: owner.uid, purpose, durationMs: 60000,
  })));
  const accepted = results.filter((result) => result.status === 'fulfilled').map((result) => result.value);
  assert.equal(accepted.length, 2);
  assert.equal(results.find((result) => result.status === 'rejected').reason.status, 429);
  assert.equal((await leases(owner.uid)).length, 2);
  for (const lease of accepted) {
    assert.equal((await data.managedLiveLeaseRef(owner.uid, lease.leaseId).get()).data().expiresAt, lease.expiresAt);
    assert.deepEqual(await api.releaseManagedLiveLease(owner.uid, lease.leaseId), { ok: true });
    assert.deepEqual(await api.releaseManagedLiveLease(owner.uid, lease.leaseId), { ok: true });
  }
  assert.deepEqual(await leases(owner.uid), []);
  assert.deepEqual(await api.releaseManagedLiveLease(owner.uid, 'missing'), { ok: false });
  await data.accountDeletionClaimRef(owner.uid).set({ createdAt: Date.now() });
  await assert.rejects(api.reserveManagedLiveLease({ uid: owner.uid, purpose: 'live', durationMs: 1000 }), { status: 409 });
  assert.equal(calls.length, 0);
});

test('upload preserves bytes, records ownership, settles once and deletes idempotently', async () => {
  const owner = await account();
  let tempPath;
  const remote = { name: 'files/upload-success', uri: 'https://generativelanguage.googleapis.com/v1beta/files/upload-success', mimeType: 'image/png', state: 'ACTIVE' };
  handlers['files.upload'] = async ({ file, config }) => {
    tempPath = file;
    assert.equal((await fs.readFile(file)).toString(), 'hello');
    assert.deepEqual(config, { mimeType: 'image/png', displayName: 'lesson.png' });
    return remote;
  };
  const result = await api.uploadManagedMedia(upload(owner));
  assert.equal(result.uri, remote.uri);
  assert.equal(result.mimeType, 'image/png');
  assert.equal(result.billingSummary.reservedCredits, 0);
  await assert.rejects(fs.stat(tempPath), { code: 'ENOENT' });
  assert.equal(await quota(owner.uid), 1);
  const record = (await data.managedFileRef(owner.uid, remote.name).get()).data();
  assert.equal(record.uid, owner.uid);
  assert.equal(record.sizeBytes, 5);
  assert.equal(record.state, 'active');
  handlers['files.delete'] = async () => { throw Object.assign(new Error('gone'), { status: 404 }); };
  assert.deepEqual(await api.deleteManagedFile(owner.uid, remote.uri), { ok: true });
  assert.deepEqual(await api.deleteManagedFile(owner.uid, remote.uri), { ok: true });
  assert.equal(await quota(owner.uid), 0);
  assert.equal(calls.filter(({ method }) => method === 'files.delete').length, 1);
  assert.equal((await reservations(owner.uid))[0].status, 'settled');
});

test('failed processing preserves its error even if remote cleanup fails, releasing credits and slot', async () => {
  const owner = await account();
  let tempPath;
  handlers['files.upload'] = async ({ file }) => { tempPath = file; return { name: 'files/failed', uri: 'https://example/files/failed', mimeType: 'image/png', state: 'PROCESSING' }; };
  handlers['files.get'] = async () => ({ state: 'FAILED' });
  handlers['files.delete'] = async () => { throw new Error('cleanup unavailable'); };
  await assert.rejects(api.uploadManagedMedia(upload(owner)), /Uploaded Gemini file failed processing/);
  await assert.rejects(fs.stat(tempPath), { code: 'ENOENT' });
  assert.equal(await quota(owner.uid), 0);
  assert.equal((await summary(owner)).availableCredits, 1000);
  assert.equal((await reservations(owner.uid))[0].status, 'released');
  assert.deepEqual(calls.map(({ method }) => method), ['files.upload', 'files.get', 'files.delete']);
});

test('insufficient credits and deletion fences release upload slots before provider access', async () => {
  const owner = await account(0);
  await assert.rejects(api.uploadManagedMedia(upload(owner)), { status: 402 });
  assert.equal(await quota(owner.uid), 0);
  await data.accountDeletionClaimRef(owner.uid).set({ createdAt: Date.now() });
  await assert.rejects(api.uploadManagedMedia(upload(owner)), { status: 409 });
  assert.equal(await quota(owner.uid), 0);
  assert.equal(calls.length, 0);
});

test('settlement failure after upload releases the recorded file and quota exactly once', async () => {
  const owner = await account();
  const remote = { name: 'files/settlement-race', uri: 'https://example/files/settlement-race', mimeType: 'image/png', state: 'ACTIVE' };
  handlers['files.upload'] = async () => {
    await data.accountDeletionClaimRef(owner.uid).set({ createdAt: Date.now() });
    return remote;
  };
  handlers['files.delete'] = async ({ name }) => assert.equal(name, remote.name);
  await assert.rejects(api.uploadManagedMedia(upload(owner)), { status: 409 });
  assert.equal(await quota(owner.uid), 0);
  assert.equal((await data.managedFileRef(owner.uid, remote.name).get()).data().state, 'deleted');
  assert.equal((await reservations(owner.uid))[0].status, 'released');
  assert.deepEqual(calls.map(({ method }) => method), ['files.upload', 'files.delete']);
});

test('full upload quota evicts the least recently checked file before reserving a new slot', async () => {
  const owner = await account();
  const older = await activeFile(owner.uid, 'older', { createdAt: 1, lastCheckedAt: 3 });
  await activeFile(owner.uid, 'newer', { createdAt: 2, lastCheckedAt: 8 });
  await data.managedFileQuotaRef(owner.uid).set({ activeManagedFileCount: 2 });
  handlers['files.delete'] = async ({ name }) => assert.equal(name, older.name);
  handlers['files.upload'] = async () => ({ name: 'files/replacement', uri: 'https://example/files/replacement', mimeType: 'image/png', state: 'ACTIVE' });
  await api.uploadManagedMedia(upload(owner));
  assert.deepEqual(calls.map(({ method }) => method), ['files.delete', 'files.upload']);
  assert.equal(await quota(owner.uid), 2);
  assert.equal((await data.managedFileRef(owner.uid, older.name).get()).data().state, 'deleted');
});

test('file status checks avoid foreign metadata and release quota once for missing provider files', async () => {
  const owner = await account();
  const remote = await activeFile(owner.uid, 'status-file');
  const foreign = await activeFile(owner.uid, 'foreign-file', { uid: 'another-user' });
  await data.managedFileQuotaRef(owner.uid).set({ activeManagedFileCount: 1 });
  handlers['files.get'] = async () => { throw Object.assign(new Error('not authorized'), { status: 403 }); };
  const expected = { statuses: { invalid: { deleted: true, active: false }, [remote.uri]: { deleted: true, active: false }, [foreign.uri]: { deleted: true, active: false } } };
  assert.deepEqual(await api.getManagedFileStatuses(owner.uid, ['invalid', remote.uri, foreign.uri]), expected);
  assert.equal(await quota(owner.uid), 0);
  assert.deepEqual(await api.getManagedFileStatuses(owner.uid, [remote.uri]), { statuses: { [remote.uri]: { deleted: true, active: false } } });
  assert.equal(calls.length, 1);
  await assert.rejects(api.getManagedFileStatuses(owner.uid, Array(101).fill(remote.uri)), { status: 400 });
});

test('partial file cleanup records failures; detached retry jobs back off and complete idempotently', async () => {
  const owner = await account();
  const good = await activeFile(owner.uid, `good-${randomUUID()}`);
  const bad = await activeFile(owner.uid, `bad-${randomUUID()}`);
  await data.managedFileQuotaRef(owner.uid).set({ activeManagedFileCount: 2 });
  handlers['files.delete'] = async ({ name }) => { if (name === bad.name) throw new Error('provider unavailable'); };
  const result = await api.clearManagedFiles(owner.uid);
  assert.equal(result.deletedCount, 1);
  assert.equal(result.failedCount, 1);
  assert.deepEqual(result.failedNames, [bad.name]);
  assert.deepEqual(result.cleanedMetadataIds, [data.managedFileRef(owner.uid, good.name).id]);
  const failed = (await data.managedFileRef(owner.uid, bad.name).get()).data();
  assert.equal(failed.cleanupPending, true);
  assert.equal(failed.cleanupAttempts, 1);
  assert.equal(await quota(owner.uid), 1);
  const job = data.cleanupJobsCollection().doc(createHash('sha256').update(bad.name).digest('hex'));
  jobRefs.push(job);
  assert.equal(await api.queueManagedFileCleanupJobs([bad.name, ` ${bad.name} `, '']), 1);
  assert.deepEqual(await api.retryManagedFileCleanupJobs(), { attempted: 1, completed: 0 });
  const pending = (await job.get()).data();
  assert.equal(pending.attempts, 1);
  assert.equal(pending.retryAt.toMillis() - pending.lastAttemptAt, 120000);
  assert.equal('uid' in pending, false);
  assert.deepEqual(await api.retryManagedFileCleanupJobs(), { attempted: 0, completed: 0 });
  await job.set({ retryAt: data.timestampFromMillis(0) }, { merge: true });
  handlers['files.delete'] = async () => { throw Object.assign(new Error('gone'), { status: 404 }); };
  assert.deepEqual(await api.retryManagedFileCleanupJobs(), { attempted: 1, completed: 1 });
  assert.deepEqual(await api.retryManagedFileCleanupJobs(), { attempted: 0, completed: 0 });
  assert.equal((await job.get()).data().status, 'completed');
});

test('generation preserves counted inputs, pinned model, provider config and exact settlement', async () => {
  const owner = await account();
  handlers.countTokens = async () => ({ totalTokens: 10 });
  handlers.generateContent = async () => ({ text: 'hello learner', candidates: [], usageMetadata: providerUsage, modelVersion: 'gemini-3.8-flash' });
  const params = generation(owner);
  const result = await api.generateManagedContent(params);
  assert.deepEqual(calls, [
    { method: 'countTokens', args: { model: 'gemini-3.8-flash', contents: params.contents } },
    { method: 'countTokens', args: { model: 'gemini-3.8-flash', contents: 'teach me' } },
    { method: 'countTokens', args: { model: 'gemini-3.8-flash', contents: 'Managed generation config:\n{"maxOutputTokens":65536}' } },
    { method: 'generateContent', args: { model: 'gemini-3.8-flash', contents: params.contents, config: { ...params.config, maxOutputTokens: 65536 } } },
  ]);
  assert.equal(result.text, 'hello learner');
  assert.deepEqual(result.usageMetadata, providerUsage);
  assert.equal(result.billingSummary.reservedCredits, 0);
  assert.equal((await reservations(owner.uid))[0].status, 'settled');
  assert.equal((await billing.listManagedUsageLedger(owner.uid, 100)).length, 1);
});

test('ownership and token-count failures cannot reach generation; provider failures refund the reservation', async () => {
  const owner = await account();
  const params = generation(owner);
  await assert.rejects(api.generateManagedContent({ ...params, contents: [{ parts: [{ fileData: { fileUri: 'https://example/files/foreign', mimeType: 'image/png' } }] }] }), { status: 403 });
  assert.equal(calls.length, 0);
  handlers.countTokens = async () => { throw new Error('count unavailable'); };
  await assert.rejects(api.generateManagedContent(params), { status: 502 });
  assert.equal((await reservations(owner.uid)).length, 0);
  handlers.countTokens = async () => ({ totalTokens: 10 });
  const original = new Error('provider failed');
  handlers.generateContent = async () => { throw original; };
  await assert.rejects(api.generateManagedContent(params), (error) => error === original);
  assert.equal((await summary(owner)).availableCredits, 1000);
  assert.equal((await reservations(owner.uid))[0].status, 'released');
});

class StreamResponse extends EventEmitter {
  headers = {}; chunks = []; writable = true; destroyed = false; headersSent = false; writableEnded = false;
  setHeader(name, value) { this.headers[name] = value; }
  write(value) { this.headersSent = true; this.chunks.push(JSON.parse(value)); }
  end() { this.writableEnded = true; this.emit('close'); }
}

test('client disconnect still drains provider usage and settles, without writing a final to the closed client', async () => {
  const owner = await account();
  const response = new StreamResponse();
  let drained = false;
  handlers.countTokens = async () => ({ totalTokens: 10 });
  handlers.generateContentStream = async function* () {
    yield { text: 'first', modelVersion: 'gemini-3.8-flash' };
    response.destroyed = true;
    response.emit('close');
    yield { text: 'last', usageMetadata: providerUsage };
    drained = true;
  };
  await api.streamManagedContent({ ...generation(owner), response });
  assert.equal(drained, true);
  assert.deepEqual(response.chunks, [{ type: 'chunk', chunk: { text: 'first', modelVersion: 'gemini-3.8-flash' } }]);
  assert.equal(response.listenerCount('close'), 0);
  assert.equal(response.listenerCount('error'), 0);
  assert.equal((await summary(owner)).reservedCredits, 0);
  const usage = await billing.listManagedUsageLedger(owner.uid, 100);
  assert.equal(usage.length, 1);
  assert.equal(usage[0].metadata.disconnectRecovered, true);
  assert.equal(usage[0].metadata.candidatesTokenCount, 10);
});

test('stream failure after delivery emits an error and releases held credit', async () => {
  const owner = await account();
  const response = new StreamResponse();
  handlers.countTokens = async () => ({ totalTokens: 10 });
  handlers.generateContentStream = async function* () {
    yield { text: 'partial' };
    throw Object.assign(new Error('upstream failed'), { status: 502 });
  };
  await api.streamManagedContent({ ...generation(owner), response });
  assert.deepEqual(response.chunks, [
    { type: 'chunk', chunk: { text: 'partial' } }, { type: 'error', message: 'upstream failed', status: 502 },
  ]);
  assert.equal(response.writableEnded, true);
  assert.equal(response.headers['Content-Type'], 'application/x-ndjson; charset=utf-8');
  assert.equal(response.listenerCount('close'), 0);
  assert.equal((await summary(owner)).availableCredits, 1000);
});

test('Live token validation, reservation denial and mint failure leave no active lease or held credit', async () => {
  const owner = await account();
  await assert.rejects(api.createManagedLiveToken({ ...live(owner), liveOpenReason: undefined }), { status: 400 });
  assert.deepEqual(await leases(owner.uid), []);
  const poor = await account(0);
  await assert.rejects(api.createManagedLiveToken(live(poor)), { status: 402 });
  assert.deepEqual(await leases(poor.uid), []);
  assert.equal(calls.length, 0);
  handlers['authTokens.create'] = async () => { throw new Error('mint unavailable'); };
  await assert.rejects(api.createManagedLiveToken(live(owner)), /mint unavailable/);
  assert.deepEqual(await leases(owner.uid), []);
  assert.equal((await summary(owner)).availableCredits, 1000);
});

test('Live token scopes provider config, settles and retains the lease until explicit release', async () => {
  const owner = await account();
  handlers['authTokens.create'] = async () => ({ name: 'tokens/fixture' });
  const result = await api.createManagedLiveToken(live(owner));
  assert.equal(result.token, 'tokens/fixture');
  assert.equal(result.uses, 1);
  assert.deepEqual(calls[0].args, { config: {
    uses: 1, expireTime: result.expiresAt, httpOptions: { apiVersion: 'v1alpha' },
    liveConnectConstraints: { model: live(owner).model, config: {
      responseModalities: ['AUDIO'], systemInstruction: 'speak briefly',
      contextWindowCompression: { triggerTokens: '25000', slidingWindow: { targetTokens: '8000' } },
      mediaResolution: 'MEDIA_RESOLUTION_LOW',
    } },
  } });
  assert.equal((await leases(owner.uid))[0].leaseId, result.leaseId);
  assert.equal((await reservations(owner.uid))[0].status, 'settled');
  await api.releaseManagedLiveLease(owner.uid, result.leaseId);
});

test('music connection failure closes the lease and releases its reservation', async () => {
  const owner = await account();
  handlers['music.connect'] = async () => { throw new Error('music unavailable'); };
  await assert.rejects(api.generateManagedMusic({ ...owner, model: 'lyria-realtime-exp', prompt: 'gentle' }), { status: 502 });
  assert.deepEqual(await leases(owner.uid), []);
  assert.equal((await summary(owner)).availableCredits, 1000);
  assert.equal((await reservations(owner.uid))[0].status, 'released');
});

test('music preserves prompt/config, trims excess PCM, settles once and releases the lease', async () => {
  const owner = await account();
  const events = [];
  handlers['music.connect'] = async ({ model, callbacks }) => {
    assert.equal(model, 'lyria-realtime-exp');
    queueMicrotask(() => callbacks.onmessage({ setupComplete: {} }));
    return {
      setWeightedPrompts: async (params) => events.push(['prompts', params]),
      setMusicGenerationConfig: async (params) => events.push(['config', params]),
      play: () => {
        events.push(['play']);
        queueMicrotask(() => callbacks.onmessage({ serverContent: { audioChunks: [{
          data: Buffer.alloc(8 * 48000 * 2 * 2 + 4).toString('base64'), mimeType: 'audio/pcm;rate=48000;channels=2',
        }] } }));
      },
      pause: () => events.push(['pause']),
      close: () => events.push(['close']),
    };
  };
  const result = await api.generateManagedMusic({ ...owner, model: 'lyria-realtime-exp', prompt: ' gentle ', durationSeconds: 3 });
  assert.deepEqual(events, [
    ['prompts', { weightedPrompts: [{ text: 'gentle. Instrumental only. No vocals, no lyrics, no copyrighted melodies. Original educational backing track.', weight: 1 }] }],
    ['config', { musicGenerationConfig: { musicGenerationMode: 'QUALITY', temperature: 1.1, guidance: 4 } }],
    ['play'], ['pause'], ['close'],
  ]);
  assert.equal(result.durationSeconds, 8);
  assert.equal(result.sampleCount, 8 * 48000 * 2);
  assert.equal(Buffer.from(result.pcmBase64, 'base64').byteLength, 8 * 48000 * 2 * 2);
  assert.equal(result.billingSummary.availableCredits, 999);
  assert.equal(result.billingSummary.reservedCredits, 0);
  assert.deepEqual(await leases(owner.uid), []);
  assert.equal((await reservations(owner.uid))[0].status, 'settled');
});

after(async () => {
  await Promise.all(created.flatMap(({ uid, token }) => [
    adminDb.recursiveDelete(data.managedUserRef(uid)),
    data.accountDeletionClaimRef(uid).delete(),
    data.purchaseClaimsCollection().doc(data.purchaseClaimId('stripe', token)).delete(),
  ]).concat(jobRefs.map((ref) => ref.delete())));
  await adminDb.terminate();
});
