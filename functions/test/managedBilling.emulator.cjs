// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

const assert = require('node:assert/strict');
const { randomUUID } = require('node:crypto');
const { adminDb } = require('../lib/functions/src/firebase.js');
const {
  getManagedAccountState,
  grantPurchasedCredits,
  releaseManagedReservation,
  reserveManagedCredits,
} = require('../lib/functions/src/managedBilling.js');
const {
  accountDeletionClaimRef,
  managedAccountRef,
  managedUserRef,
  purchaseClaimId,
  purchaseClaimsCollection,
} = require('../lib/functions/src/managedData.js');

const uid = `emulator-${randomUUID()}`;
const token = `purchase-${randomUUID()}`;
const secondToken = `purchase-${randomUUID()}`;
const user = { id: uid, email: null, displayName: null, photoUrl: null };

const grant = (purchaseToken = token) => grantPurchasedCredits({
  uid,
  user,
  purchaseToken,
  productId: 'emulator-pack',
  orderId: null,
  creditsGranted: 100,
  rawPurchase: {},
  rawVerification: {},
});

const reserve = () => reserveManagedCredits({
  uid,
  user,
  operation: 'emulator-test',
  model: 'emulator-model',
  estimatedCredits: 60,
  estimatedUsd: 0.06,
});

const run = async () => {
  await grant();

  const concurrent = await Promise.allSettled([reserve(), reserve()]);
  const fulfilled = concurrent.filter((result) => result.status === 'fulfilled');
  const rejected = concurrent.filter((result) => result.status === 'rejected');
  assert.equal(fulfilled.length, 1, 'exactly one reservation should fit the balance');
  assert.equal(rejected.length, 1, 'the competing reservation should be rejected');
  assert.equal(rejected[0].reason.status, 402);

  const reservation = fulfilled[0].value;
  await releaseManagedReservation(uid, reservation.reservationId, 'emulator-test');
  const afterRelease = await getManagedAccountState(uid, user);
  assert.equal(afterRelease.billingSummary.availableCredits, 100);
  assert.equal(afterRelease.billingSummary.reservedCredits, 0);

  const duplicate = await grant();
  assert.equal(duplicate.alreadyProcessed, true);
  assert.equal(duplicate.billingSummary.availableCredits, 100);

  const secondPurchase = await grant(secondToken);
  assert.equal(secondPurchase.alreadyProcessed, false);
  assert.equal(secondPurchase.billingSummary.availableCredits, 200);

  assert.equal((await managedUserRef(uid).get()).exists, true);
  assert.equal((await managedAccountRef(uid).get()).exists, true);

  await accountDeletionClaimRef(uid).create({ createdAt: Date.now(), schemaVersion: 2 });
  await assert.rejects(reserve, (error) => error && error.status === 409);
};

run()
  .then(() => console.log('managed billing emulator test passed'))
  .finally(async () => {
    await Promise.all([
      adminDb.recursiveDelete(managedUserRef(uid)).catch(() => undefined),
      accountDeletionClaimRef(uid).delete().catch(() => undefined),
      purchaseClaimsCollection().doc(purchaseClaimId('stripe', token)).delete().catch(() => undefined),
      purchaseClaimsCollection().doc(purchaseClaimId('stripe', secondToken)).delete().catch(() => undefined),
    ]);
    await adminDb.terminate();
  })
  .catch((error) => {
    console.error(error);
    process.exitCode = 1;
  });
