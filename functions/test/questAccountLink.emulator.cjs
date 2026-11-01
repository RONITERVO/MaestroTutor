// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
const assert = require('node:assert/strict');
const { randomUUID } = require('node:crypto');
if (!process.env.FIRESTORE_EMULATOR_HOST) throw new Error('Quest pairing transaction tests require the Firestore emulator.');
const { adminDb } = require('../lib/functions/src/firebase.js');
const { accountDeletionClaimRef } = require('../lib/functions/src/managedData.js');
const { createQuestAccountLinkStore, QUEST_LINKS_COLLECTION, QUEST_LINK_RATES_COLLECTION } = require('../lib/functions/src/questAccountLinkStore.js');
const { questLinkHash } = require('../lib/functions/src/questAccountLink.js');
async function run() {
  const store = createQuestAccountLinkStore(adminDb), now = Date.now(), secret = questLinkHash(randomUUID());
  const fresh = () => ({ createdAt: now, expiresAt: now + 300000, secretHash: secret, state: 'pending' });
  const hash = questLinkHash(randomUUID()), uid = randomUUID();
  const issue = await Promise.all(Array.from({ length: 5 }, () => store.issue(hash, fresh())));
  assert.equal(issue.filter(Boolean).length, 1, 'code collisions cannot overwrite an existing session');
  const approved = await Promise.allSettled(Array.from({ length: 5 }, (_, i) => store.approve(hash, i === 0 ? uid : randomUUID(), now + 1)));
  assert.equal(approved.filter(r => r.status === 'fulfilled').length, 1, 'one account wins competing approvals');
  const approvedUid = (await adminDb.collection(QUEST_LINKS_COLLECTION).doc(hash).get()).data().uid;
  await assert.rejects(store.claim(hash, questLinkHash('wrong-secret'), now + 2), e => e.status === 410);
  const claims = await Promise.allSettled(Array.from({ length: 8 }, () => store.claim(hash, secret, now + 2)));
  const won = claims.filter(r => r.status === 'fulfilled'); assert.equal(won.length, 1, 'one concurrent redemption');
  assert.equal(won[0].value.uid, approvedUid);
  const consumed = (await adminDb.collection(QUEST_LINKS_COLLECTION).doc(hash).get()).data();
  assert.deepEqual(Object.keys(consumed).sort(), ['createdAt', 'expiresAt', 'purgeAt', 'state']);
  assert.equal(consumed.state, 'consumed');
  const cancelled = questLinkHash(randomUUID()); await store.issue(cancelled, fresh()); await store.approve(cancelled, uid, now);
  await store.cancel(cancelled, secret, now + 1);
  await assert.rejects(store.claim(cancelled, secret, now + 2), e => e.status === 410);
  const cancelledRecord = (await adminDb.collection(QUEST_LINKS_COLLECTION).doc(cancelled).get()).data();
  assert.equal(cancelledRecord.uid, undefined); assert.equal(cancelledRecord.secretHash, undefined);
  const expired = questLinkHash(randomUUID()); await store.issue(expired, fresh());
  await assert.rejects(store.status(expired, secret, now + 300000), e => e.status === 410);
  await assert.rejects(store.approve(expired, uid, now + 300000), e => e.status === 410);
  const deleted = questLinkHash(randomUUID()); await store.issue(deleted, fresh());
  await accountDeletionClaimRef(uid).set({ createdAt: now });
  await assert.rejects(store.approve(deleted, uid, now + 1), e => e.status === 410);
  const deletingUid = randomUUID(), deleting = questLinkHash(randomUUID());
  await store.issue(deleting, fresh()); await store.approve(deleting, deletingUid, now);
  await accountDeletionClaimRef(deletingUid).set({ createdAt: now });
  await assert.rejects(store.claim(deleting, secret, now + 1), e => e.status === 410);
  const rateSubject = randomUUID();
  const rates = await Promise.allSettled(Array.from({ length: 8 }, () => store.throttle(rateSubject, 'create', 3, now)));
  assert.equal(rates.filter(r => r.status === 'fulfilled').length, 3, 'concurrent throttles enforce a hard limit');
  assert.ok(rates.filter(r => r.status === 'rejected').every(r => r.reason.status === 429));
  await store.throttle(rateSubject, 'create', 3, now + 60000);
  await store.throttle(rateSubject, 'cancel', 3, now);
  const rows = await adminDb.collection(QUEST_LINK_RATES_COLLECTION).get();
  for (const row of rows.docs) {
    assert.match(row.id, /^[a-f0-9]{64}$/);
    assert.deepEqual(Object.keys(row.data()).sort(), ['count', 'purgeAt', 'startedAt']);
  }
  console.log('Quest account-link transactions passed: competing approvals/redemptions, cancellation, expiry, deletion, hard throttles and credential minimization.');
}
run().catch(error => { console.error(error); process.exitCode = 1; });
