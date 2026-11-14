// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { testBackendDependencyCompatibility } from '../../scripts/backend-dependency-compatibility.mjs';

test('Live gateway Firebase dependency compatibility', async t => {
  await testBackendDependencyCompatibility(t, fileURLToPath(new URL('../', import.meta.url)));
});
