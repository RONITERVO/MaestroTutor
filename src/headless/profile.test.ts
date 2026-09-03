// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { mkdtemp, readFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { openHeadlessProfile } from './profile';

describe('headless profiles', () => {
  it('uses isolated profiles by default', async () => {
    const profile = await openHeadlessProfile();
    expect(profile.isolated).toBe(true);
    expect(profile.name).toBeNull();
    expect((await profile.load()).version).toBe(1);
  });

  it('persists named state beneath the configured root', async () => {
    const root = await mkdtemp(join(tmpdir(), 'maestro-profile-test-'));
    const profile = await openHeadlessProfile({ name: 'ci-profile', dataRoot: root });
    const state = await profile.load();
    state.settings.selectedLanguagePairId = 'es-ES__en-US';
    await profile.save(state);
    expect(JSON.parse(await readFile(join(profile.directory, 'profile.json'), 'utf8')).settings.selectedLanguagePairId)
      .toBe('es-ES__en-US');
  });

  it('persists BYOK file ownership and migrates older version-one profiles safely', async () => {
    const root = await mkdtemp(join(tmpdir(), 'maestro-profile-test-'));
    const profile = await openHeadlessProfile({ name: 'owned-files', dataRoot: root });
    const state = await profile.load();
    state.byok.ownedFiles.push('files/headless-one');
    await profile.save(state);
    expect((await profile.load()).byok.ownedFiles).toEqual(['files/headless-one']);

    const legacy = JSON.parse(await readFile(join(profile.directory, 'profile.json'), 'utf8'));
    delete legacy.byok;
    await import('node:fs/promises').then(({ writeFile }) => (
      writeFile(join(profile.directory, 'profile.json'), JSON.stringify(legacy), 'utf8')
    ));
    expect((await profile.load()).byok.ownedFiles).toEqual([]);
  });

  it('rejects path traversal in profile names', async () => {
    await expect(openHeadlessProfile({ name: '../outside' })).rejects.toThrow('Profile names');
  });
});
