// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
const assert = require('node:assert/strict');
const test = require('node:test');

const { getStripeWebhookRawBody } = require('../lib/functions/src/stripeBilling.js');

test('Stripe webhook body uses Express raw bytes when available', () => {
  const body = Buffer.from('{"source":"express"}');
  const cloudFunctionsBody = Buffer.from('{"source":"firebase"}');
  assert.equal(getStripeWebhookRawBody({ body, rawBody: cloudFunctionsBody }), body);
});

test('Stripe webhook body falls back to the Cloud Functions rawBody bytes', () => {
  const rawBody = Buffer.from('{"source":"firebase"}');
  assert.equal(getStripeWebhookRawBody({ body: { source: 'parsed' }, rawBody }), rawBody);
});

test('Stripe webhook body rejects parsed or string payloads', () => {
  assert.equal(getStripeWebhookRawBody({ body: { source: 'parsed' } }), null);
  assert.equal(getStripeWebhookRawBody({ body: '{"source":"string"}' }), null);
});
