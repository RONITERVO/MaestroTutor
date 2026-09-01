// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it, vi } from 'vitest';
import {
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
});
