const assert = require('node:assert/strict');
const test = require('node:test');
const { admitManagedSpend } = require('../lib/functions/src/spendAdmission.js');

test('daily admission uses conservative integer accounting and rejects any excess', () => {
  assert.equal(admitManagedSpend(undefined, 0.0000001, 100), 1);
  assert.equal(admitManagedSpend(99_999_999, 0.000001, 100), 100_000_000);
  assert.throws(() => admitManagedSpend(100_000_000, 0.000001, 100), error => error.status === 503);
  for (const bad of [-1, NaN, Infinity, 'corrupt']) {
    assert.throws(() => admitManagedSpend(bad, 1, 100), error => error.status === 503);
  }
  assert.throws(() => admitManagedSpend(0, 1, NaN), error => error.status === 503);
});
