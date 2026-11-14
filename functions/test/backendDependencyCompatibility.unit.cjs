// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
const { test } = require('node:test');
const { resolve } = require('node:path');

test('Functions Firebase dependency compatibility', async t => {
  const { testBackendDependencyCompatibility } = await import('../../scripts/backend-dependency-compatibility.mjs');
  await testBackendDependencyCompatibility(t, resolve(__dirname, '..'));
});
