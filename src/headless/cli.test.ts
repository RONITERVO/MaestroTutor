// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { parseArguments } from './cli';

const temporaryDirectories: string[] = [];

afterEach(async () => {
  await Promise.all(temporaryDirectories.splice(0).map(path => rm(path, { recursive: true, force: true })));
});

describe('headless CLI arguments', () => {
  it('loads large structured parameters from a file', async () => {
    const directory = await mkdtemp(join(tmpdir(), 'maestro-cli-'));
    temporaryDirectories.push(directory);
    const path = join(directory, 'request.json');
    const pcmBase64 = 'A'.repeat(64_000);
    await writeFile(path, JSON.stringify({ pcmBase64, sampleRate: 16_000 }));

    expect(parseArguments([
      'speech.synthetic.live',
      '--profile', 'ci',
      '--params-file', path,
    ])).toMatchObject({
      method: 'speech.synthetic.live',
      profileName: 'ci',
      params: { pcmBase64, sampleRate: 16_000 },
    });
  });

  it('rejects ambiguous inline and file parameter sources', () => {
    expect(() => parseArguments(['system.describe', '--params', '{}', '--params-file', 'request.json']))
      .toThrow('--params and --params-file are mutually exclusive');
  });

  it('selects BYOK without accepting an API key on the command line', () => {
    expect(parseArguments(['system.describe', '--access', 'byok'])).toMatchObject({ accessMode: 'byok' });
    expect(() => parseArguments(['system.describe', '--access', 'other'])).toThrow('--access requires managed or byok');
  });
});
