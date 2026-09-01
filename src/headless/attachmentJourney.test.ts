// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createCoreEventJournal } from '../core-sdk/events';
import { createCoreRuntime } from '../core-sdk/runtime';
import type { HeadlessClient } from './client';
import { runHeadlessChatTurn } from './chatJourney';
import { runHeadlessAttachmentTurn } from './attachmentJourney';

vi.mock('./chatJourney', () => ({
  runHeadlessChatTurn: vi.fn(async () => ({ ok: true })),
}));

const mockedChatTurn = vi.mocked(runHeadlessChatTurn);

const createClient = (remove: HeadlessClient['files']['delete']) => {
  const events = createCoreEventJournal();
  return {
    events,
    client: {
      runtime: createCoreRuntime({ events }),
      files: {
        upload: vi.fn(async input => ({ uri: 'files/fixture', mimeType: input.mimeType })),
        delete: remove,
      },
    } as unknown as HeadlessClient,
  };
};

const input = {
  text: 'Inspect this file.',
  dataUrl: 'data:text/plain;base64,SGVsbG8=',
  mimeType: 'text/plain',
  cleanup: true,
};

describe('headless attachment journey', () => {
  beforeEach(() => mockedChatTurn.mockClear());

  it('uses one operation id and reports an unconfirmed deletion as failed cleanup', async () => {
    const { client, events } = createClient(vi.fn(async () => ({ ok: false })));
    const result = await runHeadlessAttachmentTurn(client, input);

    expect(mockedChatTurn).toHaveBeenCalledWith(client, expect.objectContaining({
      operationId: result.operationId,
    }));
    expect(result.cleanedUp).toBe(false);
    expect(result.cleanupFailures).toHaveLength(1);
    expect(events.snapshot().filter(event => event.phase.startsWith('attachment.'))
      .every(event => event.operationId === result.operationId)).toBe(true);
    expect(events.snapshot().some(event => event.phase === 'attachment.cleanupFailed')).toBe(true);
  });

  it('does not let a cleanup transport error replace a successful chat result', async () => {
    const { client } = createClient(vi.fn(async () => { throw new Error('delete failed'); }));
    await expect(runHeadlessAttachmentTurn(client, input)).resolves.toMatchObject({
      cleanedUp: false,
      cleanupFailures: [{ message: 'delete failed' }],
    });
  });

  it('preserves the primary chat error when best-effort cleanup also fails', async () => {
    mockedChatTurn.mockRejectedValueOnce(new Error('chat failed'));
    const { client } = createClient(vi.fn(async () => { throw new Error('delete failed'); }));
    await expect(runHeadlessAttachmentTurn(client, input)).rejects.toThrow('chat failed');
  });
});
