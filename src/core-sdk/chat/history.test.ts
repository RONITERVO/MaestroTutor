// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it, vi } from 'vitest';
import { sanitizeHistoryWithVerifiedMedia, type DerivedHistoryItem } from './history';

describe('verified media history', () => {
  it('keeps active files and strips deleted, inactive and expired avatar references', async () => {
    const history: DerivedHistoryItem[] = [{
      role: 'user',
      text: 'Remember the available media as text context.',
      fileParts: [
        { fileUri: 'files/active', mimeType: 'image/png' },
        { fileUri: 'files/deleted', mimeType: 'image/png' },
        { fileUri: 'files/processing', mimeType: 'video/mp4' },
      ],
      avatarFileUri: 'files/avatar-expired',
      avatarMimeType: 'image/png',
    }];
    const onStrip = vi.fn();

    const result = await sanitizeHistoryWithVerifiedMedia(history, async uris => {
      expect(uris).toEqual([
        'files/active',
        'files/deleted',
        'files/processing',
        'files/avatar-expired',
      ]);
      return {
        'files/active': { deleted: false, active: true },
        'files/deleted': { deleted: true, active: false },
        'files/processing': { deleted: false, active: false },
        'files/avatar-expired': { deleted: true, active: false },
      };
    }, onStrip);

    expect(result).toEqual([{
      role: 'user',
      text: 'Remember the available media as text context.',
      fileParts: [{ fileUri: 'files/active', mimeType: 'image/png' }],
    }]);
    expect(onStrip).toHaveBeenCalledTimes(3);
  });

  it('does not call the provider for text-only history', async () => {
    const resolver = vi.fn();
    const history: DerivedHistoryItem[] = [{ role: 'assistant', text: 'Text only' }];
    await expect(sanitizeHistoryWithVerifiedMedia(history, resolver)).resolves.toBe(history);
    expect(resolver).not.toHaveBeenCalled();
  });
});
