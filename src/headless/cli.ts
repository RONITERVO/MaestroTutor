#!/usr/bin/env node
// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { pathToFileURL } from 'node:url';
import { loadEnvFile } from 'node:process';
import { createHeadlessClient } from './client';
import { dispatchHeadlessMethod } from './dispatcher';
import { runJsonRpcServer } from './jsonRpc';

interface ParsedArguments {
  method: string;
  params: unknown;
  profileName?: string;
  backendBaseUrl?: string;
  envFile?: string;
}

const parseArguments = (argv: string[]): ParsedArguments => {
  const positional: string[] = [];
  let params: unknown = {};
  let profileName: string | undefined;
  let backendBaseUrl: string | undefined;
  let envFile: string | undefined;
  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (argument === '--params') {
      const raw = argv[++index];
      if (!raw) throw new Error('--params requires a JSON object.');
      params = JSON.parse(raw);
    } else if (argument === '--profile') {
      profileName = argv[++index];
      if (!profileName) throw new Error('--profile requires a name.');
    } else if (argument === '--backend') {
      backendBaseUrl = argv[++index];
      if (!backendBaseUrl) throw new Error('--backend requires a URL.');
    } else if (argument === '--env-file') {
      envFile = argv[++index];
      if (!envFile) throw new Error('--env-file requires a path.');
    } else {
      positional.push(argument);
    }
  }
  return { method: positional[0] || 'system.describe', params, profileName, backendBaseUrl, envFile };
};

export const runCli = async (argv = process.argv.slice(2)) => {
  const parsed = parseArguments(argv);
  if (parsed.envFile) loadEnvFile(parsed.envFile);
  if (parsed.method === 'rpc') {
    await runJsonRpcServer({ profileName: parsed.profileName, backendBaseUrl: parsed.backendBaseUrl });
    return;
  }
  const client = await createHeadlessClient({
    profileName: parsed.profileName,
    backendBaseUrl: parsed.backendBaseUrl,
    onEvent: event => process.stderr.write(`${JSON.stringify({ type: 'event', event })}\n`),
  });
  const result = await dispatchHeadlessMethod(client, parsed.method, parsed.params);
  process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
};

if (import.meta.url === pathToFileURL(process.argv[1] || '').href) {
  runCli().catch(error => {
    process.stderr.write(`${error instanceof Error ? error.stack || error.message : String(error)}\n`);
    process.exitCode = 1;
  });
}
