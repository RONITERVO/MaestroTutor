// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

const assert = require('node:assert/strict');
const http = require('node:http');
const { api } = require('../lib/functions/src/index.js');

const listen = (server) => new Promise((resolve, reject) => {
  server.once('error', reject);
  server.listen(0, '127.0.0.1', resolve);
});

const close = (server) => new Promise((resolve, reject) => {
  server.close(error => error ? reject(error) : resolve());
});

const run = async () => {
  const server = http.createServer(api);
  try {
    await listen(server);
    const address = server.address();
    assert(address && typeof address === 'object');

    const response = await fetch(`http://127.0.0.1:${address.port}/auth/session`, {
      method: 'OPTIONS',
      headers: {
        Origin: 'http://localhost',
        'Access-Control-Request-Method': 'GET',
        'Access-Control-Request-Headers': 'x-firebase-appcheck',
      },
    });

    assert.equal(response.status, 204);
    assert.equal(response.headers.get('access-control-allow-origin'), 'http://localhost');
    assert.match(
      response.headers.get('access-control-allow-headers') || '',
      /X-Firebase-AppCheck/i,
    );
  } finally {
    await close(server);
  }
};

run()
  .then(() => console.log('managed API CORS preflight test passed'))
  .catch(error => {
    console.error(error);
    process.exitCode = 1;
  });
