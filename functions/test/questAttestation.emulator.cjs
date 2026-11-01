// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
const assert = require('node:assert/strict');
const { randomUUID } = require('node:crypto');
if (!process.env.FIRESTORE_EMULATOR_HOST) throw new Error('Quest transaction tests require the Firestore emulator.');
const { adminDb } = require('../lib/functions/src/firebase.js');
const { createQuestAttestationStore, QUEST_CHALLENGES_COLLECTION, QUEST_RATE_COLLECTION } = require('../lib/functions/src/questAttestationStore.js');
const { questNonceHash } = require('../lib/functions/src/questAttestation.js');

async function run() {
  const store = createQuestAttestationStore(adminDb), subject = randomUUID(), now = Date.now();
  const c = { createdAt: now, expiresAt: now + 300000 };
  const hash = questNonceHash(randomUUID());
  await store.issue(subject, hash, c);
  const persisted = (await adminDb.collection(QUEST_CHALLENGES_COLLECTION).doc(hash).get()).data();
  assert.deepEqual(Object.keys(persisted).sort(), ['createdAt', 'expiresAt', 'purgeAt']);
  const results = await Promise.all(Array.from({ length: 8 }, () => store.consume(subject, hash, now + 1)));
  assert.equal(results.filter(Boolean).length, 1, 'exactly one concurrent caller claims the proof');
  assert.deepEqual(results.find(Boolean), c);
  assert.equal((await adminDb.collection(QUEST_CHALLENGES_COLLECTION).doc(hash).get()).exists, false);
  for (let i = 0; i < 9; i++) await store.issue(subject, questNonceHash(randomUUID()), c);
  await assert.rejects(store.issue(subject, questNonceHash(randomUUID()), c), e => e.status === 429);
  // Rate allowance renews by timestamp, independently of asynchronous TTL deletion.
  await store.issue(subject, questNonceHash(randomUUID()), { createdAt: now + 60000, expiresAt: now + 360000 });
  const expired = questNonceHash(randomUUID());
  await store.issue(randomUUID(), expired, c);
  assert.equal(await store.consume(randomUUID(), expired, c.expiresAt), null);
  // Missing nonces must still commit their throttle increment (not roll it back).
  const missingSubject = randomUUID();
  for (let i = 0; i < 30; i++) assert.equal(await store.consume(missingSubject, '0'.repeat(64), now), null);
  await assert.rejects(store.consume(missingSubject, '0'.repeat(64), now), e => e.status === 429);
  const rows = await adminDb.collection(QUEST_RATE_COLLECTION).get();
  assert.ok(rows.size > 0);
  for (const row of rows.docs) {
    assert.deepEqual(Object.keys(row.data()).sort(), ['count', 'purgeAt', 'startedAt']);
    assert.match(row.id, /^(challenge|exchange)-[a-f0-9]{64}$/);
  }
  console.log('Quest attestation transactions passed: concurrency, expiry, replay, throttles and data minimization.');
}
run().catch(error => { console.error(error); process.exitCode = 1; });
