// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { mkdir, mkdtemp, readFile, rename, writeFile } from 'node:fs/promises';
import { homedir, tmpdir } from 'node:os';
import { dirname, isAbsolute, join, relative, resolve } from 'node:path';
import type {
  ManagedBillingSummary,
} from '../core/contracts/backend';
import type { EntitlementRecord } from '../core/contracts/integrations';
import type { AppSettings, ChatMessage } from '../core/types';

const PROFILE_NAME_PATTERN = /^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$/;
const PROFILE_FILE = 'profile.json';

export interface HeadlessProfileState {
  version: 1;
  settings: Partial<AppSettings>;
  chats: Record<string, ChatMessage[]>;
  globalProfile: string;
  managed: {
    billingSummary: ManagedBillingSummary | null;
    entitlements: EntitlementRecord[];
  };
  byok: {
    /** Provider files created by this profile and therefore safe to delete. */
    ownedFiles: string[];
  };
  updatedAt: number;
}

export interface HeadlessProfile {
  name: string | null;
  directory: string;
  isolated: boolean;
  load(): Promise<HeadlessProfileState>;
  save(state: HeadlessProfileState): Promise<void>;
}

export const createEmptyHeadlessProfileState = (now = Date.now()): HeadlessProfileState => ({
  version: 1,
  settings: {},
  chats: {},
  globalProfile: '',
  managed: { billingSummary: null, entitlements: [] },
  byok: { ownedFiles: [] },
  updatedAt: now,
});

const resolveDataRoot = (override?: string): string => {
  if (override?.trim()) return resolve(override.trim());
  const platformRoot = process.env.LOCALAPPDATA?.trim()
    ? resolve(process.env.LOCALAPPDATA, 'MaestroTutor', 'headless')
    : resolve(homedir(), '.maestrotutor', 'headless');
  return platformRoot;
};

const assertWithin = (root: string, candidate: string) => {
  const relation = relative(root, candidate);
  if (relation.startsWith('..') || isAbsolute(relation)) {
    throw new Error('Resolved profile path escaped the Maestro headless data directory.');
  }
};

const validateState = (value: unknown): HeadlessProfileState => {
  if (!value || typeof value !== 'object') throw new Error('Headless profile is not a JSON object.');
  const record = value as Partial<HeadlessProfileState>;
  if (record.version !== 1) throw new Error('Unsupported headless profile version.');
  if (!record.settings || typeof record.settings !== 'object') throw new Error('Headless profile settings are invalid.');
  if (!record.chats || typeof record.chats !== 'object') throw new Error('Headless profile chats are invalid.');
  if (!record.managed || typeof record.managed !== 'object') throw new Error('Headless profile managed state is invalid.');
  const rawOwnedFiles = record.byok && typeof record.byok === 'object'
    ? (record.byok as { ownedFiles?: unknown }).ownedFiles
    : [];
  const ownedFiles = Array.isArray(rawOwnedFiles)
    ? [...new Set(rawOwnedFiles.filter((item): item is string => typeof item === 'string' && Boolean(item.trim())).map(item => item.trim()))]
    : [];
  return {
    ...record,
    globalProfile: typeof record.globalProfile === 'string' ? record.globalProfile : '',
    byok: { ownedFiles },
  } as HeadlessProfileState;
};

export const openHeadlessProfile = async (options?: {
  name?: string;
  dataRoot?: string;
}): Promise<HeadlessProfile> => {
  const profileName = options?.name?.trim() || null;
  let directory: string;
  let isolated: boolean;

  if (profileName) {
    if (!PROFILE_NAME_PATTERN.test(profileName) || profileName === '.' || profileName === '..') {
      throw new Error('Profile names may contain only letters, numbers, dot, underscore and dash.');
    }
    const root = resolveDataRoot(options?.dataRoot || process.env.MAESTRO_HEADLESS_HOME);
    directory = resolve(root, profileName);
    assertWithin(root, directory);
    await mkdir(directory, { recursive: true });
    isolated = false;
  } else {
    directory = await mkdtemp(join(tmpdir(), 'maestro-headless-'));
    isolated = true;
  }

  const path = join(directory, PROFILE_FILE);
  return {
    name: profileName,
    directory,
    isolated,
    async load() {
      try {
        return validateState(JSON.parse(await readFile(path, 'utf8')));
      } catch (error) {
        if ((error as NodeJS.ErrnoException).code === 'ENOENT') return createEmptyHeadlessProfileState();
        throw error;
      }
    },
    async save(state) {
      const normalized: HeadlessProfileState = { ...state, version: 1, updatedAt: Date.now() };
      const temporaryPath = join(dirname(path), `${PROFILE_FILE}.${process.pid}.tmp`);
      await writeFile(temporaryPath, `${JSON.stringify(normalized, null, 2)}\n`, { encoding: 'utf8', mode: 0o600 });
      await rename(temporaryPath, path);
    },
  };
};
