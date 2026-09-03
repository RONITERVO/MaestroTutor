// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it, vi } from 'vitest';
import {
  createDirectHeadlessAi,
  createDirectHeadlessFilePort,
  resolveHeadlessAccessMode,
} from './access';

describe('headless access adapters', () => {
  it('requires an explicit supported access mode', () => {
    expect(resolveHeadlessAccessMode('managed')).toBe('managed');
    expect(resolveHeadlessAccessMode('byok')).toBe('byok');
    expect(() => resolveHeadlessAccessMode('other')).toThrow('managed');
  });

  it('uses the direct Files API in BYOK mode', async () => {
    const upload = vi.fn(async () => ({
      uri: 'https://generativelanguage.googleapis.com/v1beta/files/test',
      name: 'files/test',
      mimeType: 'text/plain',
      state: 'ACTIVE',
    }));
    const remove = vi.fn(async () => undefined);
    const ai = {
      files: {
        upload,
        get: vi.fn(async () => ({ state: 'ACTIVE' })),
        delete: remove,
        list: vi.fn(async () => ({ async *[Symbol.asyncIterator]() {} })),
      },
    } as any;
    const files = createDirectHeadlessFilePort(ai);
    await expect(files.upload({
      dataUrl: 'data:text/plain;base64,aGVsbG8=',
      mimeType: 'text/plain;charset=utf-8',
      displayName: 'hello.txt',
    })).resolves.toMatchObject({ mimeType: 'text/plain' });
    expect(upload).toHaveBeenCalledOnce();
    await expect(files.delete('https://generativelanguage.googleapis.com/v1beta/files/test'))
      .resolves.toEqual({ ok: true });
    expect(remove).toHaveBeenCalledWith({ name: 'files/test' });
  });

  it('never deletes provider files that the active BYOK profile does not own', async () => {
    const remove = vi.fn(async (_request: { name: string }) => undefined);
    const list = vi.fn(async () => ({
      async *[Symbol.asyncIterator]() {
        yield { name: 'files/unrelated' };
      },
    }));
    const ai = {
      files: {
        upload: vi.fn()
          .mockResolvedValueOnce({ uri: 'https://example.test/v1beta/files/one', name: 'files/one', mimeType: 'text/plain', state: 'ACTIVE' })
          .mockResolvedValueOnce({ uri: 'https://example.test/v1beta/files/two', name: 'files/two', mimeType: 'text/plain', state: 'ACTIVE' }),
        get: vi.fn(async () => ({ state: 'ACTIVE' })),
        delete: remove,
        list,
      },
    } as any;
    const ownedFiles: string[] = [];
    const files = createDirectHeadlessFilePort(ai, {
      list: () => ownedFiles,
      add: async name => { ownedFiles.push(name); },
      remove: async name => { ownedFiles.splice(ownedFiles.indexOf(name), 1); },
    });

    await expect(files.delete('files/unrelated')).resolves.toEqual({ ok: false });
    expect(remove).not.toHaveBeenCalled();
    await files.upload({ dataUrl: 'data:text/plain;base64,b25l', mimeType: 'text/plain' });
    await files.upload({ dataUrl: 'data:text/plain;base64,dHdv', mimeType: 'text/plain' });
    await expect(files.clear()).resolves.toEqual({ deletedCount: 2, failedCount: 0, failedNames: [] });
    expect(remove.mock.calls.map(([request]) => request.name)).toEqual(['files/one', 'files/two']);
    expect(list).not.toHaveBeenCalled();
    expect(ownedFiles).toEqual([]);
  });

  it('rolls back a direct upload when durable BYOK ownership cannot be recorded', async () => {
    const remove = vi.fn(async (_request: { name: string }) => undefined);
    const files = createDirectHeadlessFilePort({
      files: {
        upload: vi.fn(async () => ({
          uri: 'https://example.test/v1beta/files/orphan',
          name: 'files/orphan',
          mimeType: 'text/plain',
          state: 'ACTIVE',
        })),
        delete: remove,
      },
    } as any, {
      list: () => [],
      add: async () => { throw new Error('profile write failed'); },
      remove: async () => undefined,
    });
    await expect(files.upload({
      dataUrl: 'data:text/plain;base64,b3JwaGFu',
      mimeType: 'text/plain',
    })).rejects.toThrow('profile write failed');
    expect(remove).toHaveBeenCalledWith({ name: 'files/orphan' });
  });

  it('keeps direct headless Live behind the shared reason gate', async () => {
    const direct = createDirectHeadlessAi('test-api-key');
    await expect((direct.ai.live.connect as any)({ model: 'test-live-model' }))
      .rejects.toMatchObject({ status: 400, code: 'LIVE_OPEN_REASON_REQUIRED' });
  });
});
