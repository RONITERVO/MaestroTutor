// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
const db = vi.hoisted(() => ({ save: vi.fn(), iterate: vi.fn(), profile: vi.fn(), avatar: vi.fn(), tasks: vi.fn() }));
vi.mock('../../chat', () => ({ safeSaveChatHistoryDB: db.save, getAllChatMetasDB: async () => ({}),
  getChatMetaDB: vi.fn(), getChatHistoryDB: vi.fn(), iterateChatHistoriesDB: db.iterate,
  hasAnyChatHistoriesDB: async () => true, resetRoomAgentTasks: vi.fn() }));
vi.mock('..', () => ({ getGlobalProfileDB: db.profile }));
vi.mock('../../../core/db/assets', () => ({ getMaestroProfileImageDB: db.avatar }));
vi.mock('../services/backupArchive', () => ({ stageBackup: vi.fn(), commitBackupStage: vi.fn(), discardBackupStage: vi.fn(), hasStagedBackupPair: vi.fn(), writeTaskBackup: db.tasks }));
import { useDataBackup } from './useDataBackup';
import { initialSettings, useMaestroStore } from '../../../store';
import { BackupDecoder, type BackupEntry } from '../../../core-sdk/backup/archive';
afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.resetAllMocks(); });

it('exports an empty selected chat with profile and avatar as a restorable archive', async () => {
  useMaestroStore.setState({ messages: [], settings: { ...initialSettings, selectedLanguagePairId: 'empty-pair' } });
  const avatar = { dataUrl: 'data:image/png;base64,AQID', mimeType: 'image/png' };
  db.profile.mockResolvedValue({ text: 'My learning preferences' }); db.avatar.mockResolvedValue(avatar);
  db.iterate.mockImplementation(async visit => { await visit('empty-pair', []); }); db.tasks.mockResolvedValue(0);
  const lines: string[] = [], close = vi.fn(), abort = vi.fn();
  vi.stubGlobal('showSaveFilePicker', async () => ({ createWritable: async () => ({
    write: async (line: string) => { lines.push(line); }, close, abort,
  }) }));
  const alert = vi.fn(); vi.stubGlobal('alert', alert);
  const h = renderHook(() => useDataBackup({ t: key => key }));
  await act(async () => { await h.result.current.handleSaveAllChats(); });
  expect(db.save).toHaveBeenCalledWith('empty-pair', []); expect(close).toHaveBeenCalledOnce();
  expect(abort).not.toHaveBeenCalled(); expect(alert).not.toHaveBeenCalled();
  const decoder = new BackupDecoder(), restored: BackupEntry[] = [];
  for (const line of lines) restored.push(...await decoder.push(line));
  restored.push(...decoder.finish());
  expect(restored).toContainEqual({ kind: 'chat', id: 'empty-pair', value: [] });
  expect(restored).toContainEqual({ kind: 'asset', id: 'maestroProfileImage', value: avatar });
  expect(restored).toContainEqual({ kind: 'profile', id: 'singleton', value: 'My learning preferences' });
});
