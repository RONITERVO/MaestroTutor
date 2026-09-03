#!/usr/bin/env node
// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { pathToFileURL } from 'node:url';
import { createHeadlessClient } from '../src/headless/client';
import { runManagedLiveNoOutputCanary } from '../src/headless/managedLiveGatewayCanary';

const profileArgument = (): string => {
  const index = process.argv.indexOf('--profile');
  return index >= 0 && process.argv[index + 1] ? process.argv[index + 1] : 'managed-live-canary';
};

const run = async () => {
  const client = await createHeadlessClient({
    accessMode: 'managed',
    profileName: profileArgument(),
  });
  const evidence = await runManagedLiveNoOutputCanary(client);
  process.stdout.write(`${JSON.stringify(evidence, null, 2)}\n`);
  if (!evidence.passed) process.exitCode = 1;
};

if (import.meta.url === pathToFileURL(process.argv[1] || '').href) {
  run().catch((error) => {
    process.stderr.write(`${error instanceof Error ? error.stack || error.message : String(error)}\n`);
    process.exitCode = 1;
  });
}
