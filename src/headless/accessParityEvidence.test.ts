// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import { compareHeadlessFirstLessonParity } from './accessParityEvidence';

const proof = (accessMode: 'managed' | 'byok') => ({
  accessMode,
  passed: true,
  uploadGeneratedMedia: true,
  userTurnCount: 14,
  coverage: { text: true, attachments: true, toolUploads: true },
  turns: [{ kind: 'text' }, { kind: 'attachment-image' }, { kind: 'live-audio' }],
  aftersteps: [
    { responseSource: 'chat', tool: 'image', uploaded: true },
    { responseSource: 'chat', tool: 'audio-note', uploaded: true },
    { responseSource: 'live', tool: 'music', uploaded: true },
  ],
});

describe('headless managed/BYOK parity evidence', () => {
  it('accepts matching complete semantic proof from both access modes', () => {
    expect(compareHeadlessFirstLessonParity(proof('managed'), proof('byok'))).toMatchObject({
      passed: true,
      mismatches: [],
    });
  });

  it('rejects skipped coverage, transport drift and untested upload cost paths', () => {
    const byok = proof('byok');
    byok.passed = false;
    byok.coverage.attachments = false;
    byok.uploadGeneratedMedia = false;
    byok.turns.reverse();
    byok.aftersteps[1].uploaded = false;
    const comparison = compareHeadlessFirstLessonParity(proof('managed'), byok);
    expect(comparison.passed).toBe(false);
    expect(comparison.mismatches).toEqual(expect.arrayContaining([
      'BYOK first-lesson proof is incomplete.',
      'Generated-media upload was not exercised in both access modes.',
      'Managed and BYOK journey turn order differs.',
      'The audio-note upload path was not proved in both access modes.',
    ]));
  });
});
