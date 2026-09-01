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

  it('rejects path traversal in profile names', async () => {
    await expect(openHeadlessProfile({ name: '../outside' })).rejects.toThrow('Profile names');
  });
});
