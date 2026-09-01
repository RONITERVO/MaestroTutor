// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import { createCoreEventJournal } from './events';

describe('core event journal', () => {
  it('orders events and redacts credential-shaped trace data', () => {
    const journal = createCoreEventJournal({ now: () => 7 });
    journal.emit({
      operationId: 'op-1',
      journey: 'access',
      phase: 'started',
      at: 7,
      data: {
        userId: 'user-1',
        authorization: 'Bearer private',
        nested: { appCheckToken: 'private', safe: 'visible' },
      },
    });
    journal.emit({ operationId: 'op-1', journey: 'access', phase: 'done', at: 8 });

    expect(journal.snapshot()).toEqual([
      expect.objectContaining({
        version: 1,
        sequence: 1,
        data: {
          userId: 'user-1',
          authorization: '[redacted]',
          nested: { appCheckToken: '[redacted]', safe: 'visible' },
        },
      }),
      expect.objectContaining({ sequence: 2, phase: 'done' }),
    ]);
  });
});
