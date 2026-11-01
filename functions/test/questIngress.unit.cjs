// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
const assert = require('node:assert/strict');
const test = require('node:test');
const http = require('node:http');
const { createQuestAttestationApp } = require('../lib/functions/src/questAttestationEndpoint.js');
const { createQuestAccountLinkApp } = require('../lib/functions/src/questAccountLinkEndpoint.js');

const endpoints = [
  { name: 'attestation', code: 'quest-attestation/unavailable', paths: ['challenge', 'exchange'],
    app: (calls, trusted) => createQuestAttestationApp({
      challenge: async ip => { calls.push(ip); return {}; }, exchange: async ip => { calls.push(ip); return {}; },
    }, trusted) },
  { name: 'account link', code: 'quest-link/unavailable', paths: ['create', 'status', 'approve', 'redeem', 'cancel'],
    app: (calls, trusted) => createQuestAccountLinkApp({ run: async (_operation, context) => { calls.push(context.ip); return {}; } }, trusted) },
];
async function serverTest(app, action) {
  const server = http.createServer(app);
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  try { await action(`http://127.0.0.1:${server.address().port}`); }
  finally { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
}
const post = (base, path, forwarded) => fetch(`${base}/${path}`, { method: 'POST', body: '{}', headers: {
  'Content-Type': 'application/json', ...(forwarded === undefined ? {} : { 'X-Forwarded-For': forwarded }),
} });
for (const endpoint of endpoints) {
  test(`${endpoint.name} refuses unverified routing before every operation reaches its service`, async () => {
    for (const [name, trusted, forwarded] of [
      ['unknown immediate proxy', ['192.0.2.1/32'], '203.0.113.9, 192.0.2.1'],
      ['missing policy', null, '203.0.113.9'],
      ['empty policy', [], '203.0.113.9'],
      ['missing forwarded client', ['127.0.0.1/32'], undefined],
      ['empty forwarded client', ['127.0.0.1/32'], ''],
      ['only trusted proxies', ['127.0.0.1/32', '192.0.2.1/32'], '192.0.2.1'],
      ['invalid closest client', ['127.0.0.1/32'], '203.0.113.9, attacker-controlled'],
      ['address with port', ['127.0.0.1/32'], '203.0.113.9:443'],
      ['bracketed address', ['127.0.0.1/32'], '[2001:db8::9]'],
    ]) {
      const calls = [];
      await serverTest(endpoint.app(calls, trusted), async base => {
        for (const path of endpoint.paths) {
          const response = await post(base, path, forwarded);
          assert.equal(response.status, 503, `${name}/${path}`);
          assert.equal(response.headers.get('cache-control'), 'no-store');
          const body = await response.json(); assert.equal(body.code, endpoint.code);
          assert.ok(!JSON.stringify(body).includes('203.0.113') && !JSON.stringify(body).includes('attacker-controlled'));
        }
      });
      assert.deepEqual(calls, [], name);
    }
  });
  test(`${endpoint.name} accepts verified IPv4/IPv6 clients and ignores spoofed prefixes`, async () => {
    const calls = [];
    await serverTest(endpoint.app(calls, ['127.0.0.1/32', '192.0.2.1/32']), async base => {
      for (const client of ['203.0.113.9', '2001:db8::9', '::ffff:203.0.113.9']) {
        for (const prefix of ['', 'attacker-controlled, ', '198.51.100.23, ', '127.0.0.1, ']) {
          for (const path of endpoint.paths) {
            const response = await post(base, path, prefix + client + ', 192.0.2.1');
            assert.equal(response.status, 200, `${client}/${prefix}/${path}`);
            assert.equal(calls.pop(), client);
          }
        }
      }
    });
    assert.deepEqual(calls, []);
  });
}
