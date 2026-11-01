// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
const assert = require('node:assert/strict');
const http = require('node:http');
const { once } = require('node:events');
const { before, after, test } = require('node:test');
const { gzipSync, deflateSync } = require('node:zlib');
const { api } = require('../lib/functions/src/index.js');

let server, port;
before(async () => {
  server = http.createServer(api);
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  port = server.address().port;
});
after(async () => {
  server.closeAllConnections();
  await new Promise((resolve, reject) => server.close(error => error ? reject(error) : resolve()));
});
const request = (encoding, body, method = 'POST') => new Promise((resolve, reject) => {
  // An unknown route exercises the real parser without credentials, database
  // access, billing or a provider call. OPTIONS exercises the real CORS handler.
  const req = http.request({hostname:'127.0.0.1',port,
    path:method === 'OPTIONS' ? '/auth/session' : '/__request_body_regression__',method,
    headers:method === 'OPTIONS' ? {Origin:'http://localhost','Access-Control-Request-Method':'GET'} : {
      'Content-Type':'application/json','Content-Encoding':encoding,'Content-Length':body.length,
    }},res => {
      res.resume();
      res.once('end', () => resolve(res.statusCode));
      res.once('error', reject);
    });
  req.once('error', reject);
  req.setTimeout(5000, () => req.destroy(new Error('Local API request timed out')));
  req.end(body);
});
for (const [encoding, compress] of [['gzip',gzipSync],['deflate',deflateSync]]) {
  test(`API remains available after an invalid ${encoding} request`, async () => {
    const valid = compress(Buffer.from('{"message":"hola"}'));
    assert.equal(await request(encoding, valid), 404);
    // zlib legitimately ignores a deflate trailer, so truncate that stream.
    // gzip uses the trailing-byte case from the body-parser advisory.
    const invalid = encoding === 'gzip'
      ? Buffer.concat([valid, Buffer.from('invalid compressed trailer')])
      : valid.subarray(0, valid.length - 4);
    assert.equal(await request(encoding, invalid), 400);
    assert.equal(await request(null, undefined, 'OPTIONS'), 204);
    assert.equal(await request(encoding, valid), 404);
  });
}
