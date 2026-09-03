// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

process.env.MANAGED_LIVE_GATEWAY_URL = 'https://gateway.emulator.test/live';

const assert = require('node:assert/strict');
const { randomUUID } = require('node:crypto');
const { adminDb } = require('../lib/functions/src/firebase.js');
const {
  createManagedLiveGatewayTicket,
  consumeManagedLiveGatewayTicket,
  finalizeManagedLiveGatewaySession,
  recoverManagedLiveGatewayBilling,
} = require('../lib/functions/src/managedLiveGateway.js');
const {
  getManagedAccountState,
  grantPurchasedCredits,
  listManagedBillingLedger,
  listManagedUsageLedger,
} = require('../lib/functions/src/managedBilling.js');
const {
  accountDeletionClaimRef,
  liveGatewaySessionRef,
  liveGatewaySessionsCollection,
  liveGatewayTicketRef,
  liveGatewayTicketsCollection,
  managedUserRef,
  purchaseClaimId,
  purchaseClaimsCollection,
} = require('../lib/functions/src/managedData.js');
const {
  createLiveGatewayUsageCheckpoint,
  observeLiveGatewayClientMessage,
  observeLiveGatewayProviderMessage,
} = require('../lib/shared/billing/liveGateway.js');

const uid = `live-gateway-${randomUUID()}`;
const token = `live-gateway-purchase-${randomUUID()}`;
const user = { id: uid, email: null, displayName: null, photoUrl: null };
const model = 'gemini-3.1-flash-live-preview';
const liveOpenReason = () => ({
  trigger: 'user.headless-live',
  requestId: randomUUID(),
  requestedAt: new Date().toISOString(),
});

const issueTicket = () => createManagedLiveGatewayTicket({
  uid,
  user,
  model,
  config: {
    responseModalities: ['AUDIO'],
    inputAudioTranscription: {},
    outputAudioTranscription: {},
  },
  liveOpenReason: liveOpenReason(),
});

const docsForUid = async (collection) => (
  (await collection.where('uid', '==', uid).get()).docs
);

const deleteDocs = async (docs) => {
  if (!docs.length) return;
  const batch = adminDb.batch();
  for (const doc of docs) batch.delete(doc.ref);
  await batch.commit();
};

const run = async () => {
  await grantPurchasedCredits({
    uid,
    user,
    purchaseToken: token,
    productId: 'emulator-pack',
    orderId: null,
    creditsGranted: 1_000,
    rawPurchase: {},
    rawVerification: {},
  });

  const emptyTicket = await issueTicket();
  assert.equal(emptyTicket.gatewayUrl, 'wss://gateway.emulator.test/live');
  assert.equal('token' in emptyTicket, false, 'gateway response must not contain a provider token');
  assert.equal('leaseId' in emptyTicket, false, 'internal quota lease ids must stay server-side');
  assert.ok(emptyTicket.billingSummary.reservedCredits > 0);
  await assert.rejects(
    consumeManagedLiveGatewayTicket(emptyTicket.ticket, 'wrong-pricing-version'),
    (error) => error && error.status === 409,
  );

  const uses = await Promise.allSettled([
    consumeManagedLiveGatewayTicket(emptyTicket.ticket),
    consumeManagedLiveGatewayTicket(emptyTicket.ticket),
  ]);
  const accepted = uses.filter((result) => result.status === 'fulfilled');
  const rejected = uses.filter((result) => result.status === 'rejected');
  assert.equal(accepted.length, 1, 'one-use ticket must admit exactly one connection');
  assert.equal(rejected.length, 1, 'the competing connection must be rejected');
  assert.equal(rejected[0].reason.status, 409);
  assert.deepEqual(accepted[0].value.config?.responseModalities, ['AUDIO']);
  const consumedTicketId = emptyTicket.ticket.split('.')[0];
  const consumedTicketData = (await liveGatewayTicketRef(consumedTicketId).get()).data();
  const consumedSessionData = (await liveGatewaySessionRef(consumedTicketId).get()).data();
  assert.equal('secretHash' in consumedTicketData, false, 'consumed ticket secret hash must be scrubbed');
  assert.equal('config' in consumedTicketData, false, 'consumed ticket config must be scrubbed');
  assert.equal('config' in consumedSessionData, false, 'transient Live config must not be persisted in the session');

  let inputOnly = createLiveGatewayUsageCheckpoint();
  inputOnly = observeLiveGatewayClientMessage(inputOnly, {
    audio: { data: 'AQIDBA==', mimeType: 'audio/pcm;rate=16000' },
  });
  const released = await finalizeManagedLiveGatewaySession(
    accepted[0].value.sessionId,
    'setup-only-timeout',
    inputOnly,
  );
  assert.equal(released.status, 'released');
  assert.equal(released.billedCredits, 0);
  assert.equal(released.usefulOutput, false);
  const releasedAgain = await finalizeManagedLiveGatewaySession(
    accepted[0].value.sessionId,
    'duplicate-finalize',
    inputOnly,
  );
  assert.equal(releasedAgain.status, 'released');
  assert.equal((await listManagedUsageLedger(uid, 100)).filter((row) => row.operation === 'liveGateway').length, 0);
  const releaseRows = (await listManagedBillingLedger(uid, 100)).filter((row) => (
    row.kind === 'reservation-release' && row.metadata?.ticketId === emptyTicket.ticket.split('.')[0]
  ));
  assert.equal(releaseRows.length, 1, 'a no-output release must be independently auditable');
  assert.equal(releaseRows[0].metadata.liveOpenTrigger, 'user.headless-live');
  assert.equal(releaseRows[0].metadata.reason, 'live-gateway-setup-only-timeout');
  const afterRelease = await getManagedAccountState(uid, user);
  assert.equal(afterRelease.billingSummary.availableCredits, 1_000);
  assert.equal(afterRelease.billingSummary.reservedCredits, 0);

  const meteredTicket = await issueTicket();
  const reservedCredits = meteredTicket.billingSummary.reservedCredits;
  const meteredSession = await consumeManagedLiveGatewayTicket(meteredTicket.ticket);
  let providerUsage = createLiveGatewayUsageCheckpoint();
  providerUsage = observeLiveGatewayProviderMessage(providerUsage, {
    usageMetadata: {
      promptTokenCount: 160,
      responseTokenCount: 96,
      totalTokenCount: 256,
      promptTokensDetails: [{ modality: 'AUDIO', tokenCount: 160 }],
      responseTokensDetails: [{ modality: 'AUDIO', tokenCount: 96 }],
    },
    serverContent: {
      modelTurn: { parts: [{ text: 'A useful answer.' }] },
      turnComplete: true,
    },
  });
  const concurrentFinalizations = await Promise.all([
    finalizeManagedLiveGatewaySession(meteredSession.sessionId, 'turn-complete', providerUsage),
    finalizeManagedLiveGatewaySession(meteredSession.sessionId, 'duplicate-race', providerUsage),
  ]);
  assert.ok(concurrentFinalizations.some((result) => result.status === 'settled'));
  const settled = await finalizeManagedLiveGatewaySession(
    meteredSession.sessionId,
    'read-idempotent-result',
    providerUsage,
  );
  assert.equal(settled.status, 'settled');
  assert.ok(settled.billedCredits > 0);
  assert.ok(settled.billedCredits < reservedCredits, 'actual usage should replace the larger window reservation');
  assert.equal(settled.usageSource, 'provider');

  const usageRows = (await listManagedUsageLedger(uid, 100)).filter((row) => row.operation === 'liveGateway');
  const chargeRows = (await listManagedBillingLedger(uid, 100)).filter((row) => (
    row.kind === 'charge' && row.metadata?.operation === 'liveGateway'
  ));
  assert.equal(usageRows.length, 1, 'duplicate finalization must not duplicate usage ledger rows');
  assert.equal(chargeRows.length, 1, 'duplicate finalization must not duplicate charges');

  const expiredTicket = await issueTicket();
  const expiredTicketId = expiredTicket.ticket.split('.')[0];
  await liveGatewayTicketRef(expiredTicketId).set({ expiresAt: Date.now() - 1 }, { merge: true });
  await assert.rejects(
    consumeManagedLiveGatewayTicket(expiredTicket.ticket),
    (error) => error && error.status === 410,
  );
  const expiredTicketData = (await liveGatewayTicketRef(expiredTicketId).get()).data();
  assert.equal('secretHash' in expiredTicketData, false, 'expired ticket secret hash must be scrubbed');
  assert.equal('config' in expiredTicketData, false, 'expired ticket config must be scrubbed');

  const abandonedTicket = await issueTicket();
  const abandonedTicketId = abandonedTicket.ticket.split('.')[0];
  await liveGatewayTicketRef(abandonedTicketId).set({ expiresAt: Date.now() - 1 }, { merge: true });
  const abandonedRecovery = await recoverManagedLiveGatewayBilling(100);
  assert.ok(abandonedRecovery.expiredTickets >= 1);
  const abandonedTicketData = (await liveGatewayTicketRef(abandonedTicketId).get()).data();
  assert.equal('secretHash' in abandonedTicketData, false, 'recovered ticket secret hash must be scrubbed');
  assert.equal('config' in abandonedTicketData, false, 'recovered ticket config must be scrubbed');

  const recoveryTicket = await issueTicket();
  const recoverySession = await consumeManagedLiveGatewayTicket(recoveryTicket.ticket);
  const recoveryCheckpoint = observeLiveGatewayProviderMessage(
    createLiveGatewayUsageCheckpoint(),
    {
      usageMetadata: {
        promptTokenCount: 10,
        responseTokenCount: 4,
        totalTokenCount: 14,
        promptTokensDetails: [{ modality: 'AUDIO', tokenCount: 10 }],
        responseTokensDetails: [{ modality: 'AUDIO', tokenCount: 4 }],
      },
      serverContent: { modelTurn: { parts: [{ text: 'recover me' }] } },
    },
  );
  await liveGatewaySessionRef(recoverySession.sessionId).set({
    deadlineAt: Date.now() - 1,
    checkpoint: recoveryCheckpoint,
  }, { merge: true });
  const recovery = await recoverManagedLiveGatewayBilling(100);
  assert.ok(recovery.finalizedSessions >= 1);
  const recovered = await finalizeManagedLiveGatewaySession(
    recoverySession.sessionId,
    'read-recovered-result',
  );
  assert.equal(recovered.status, 'settled');
  assert.equal(recovered.usefulOutput, true);

  const finalAccount = await getManagedAccountState(uid, user);
  assert.equal(finalAccount.billingSummary.reservedCredits, 0);
};

run()
  .then(() => console.log('managed Live gateway emulator test passed'))
  .finally(async () => {
    await Promise.all([
      deleteDocs(await docsForUid(liveGatewayTicketsCollection())).catch(() => undefined),
      deleteDocs(await docsForUid(liveGatewaySessionsCollection())).catch(() => undefined),
      adminDb.recursiveDelete(managedUserRef(uid)).catch(() => undefined),
      accountDeletionClaimRef(uid).delete().catch(() => undefined),
      purchaseClaimsCollection().doc(purchaseClaimId('stripe', token)).delete().catch(() => undefined),
    ]);
    await adminDb.terminate();
  })
  .catch((error) => {
    console.error(error);
    process.exitCode = 1;
  });
