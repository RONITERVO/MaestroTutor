// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

import { describe, expect, it } from 'vitest';
import {
  evaluateTranscriptEvidence,
  normalizeTranscript,
  requireTranscriptEvidence,
} from './transcriptEvidence';

describe('headless transcript evidence', () => {
  it('accepts punctuation and common contraction differences', () => {
    const evidence = evaluateTranscriptEvidence(
      'Hello. How are you doing? I am doing great.',
      "Hello, how are you doing? I'm doing great!",
    );
    expect(evidence).toMatchObject({ passed: true, wordRecall: 1 });
  });

  it('rejects a response-only check that dropped the final spoken clause', () => {
    const evidence = evaluateTranscriptEvidence(
      'Hello. How are you doing? I am doing great.',
      'Hello, how are you doing?',
    );
    expect(evidence.passed).toBe(false);
    expect(evidence.wordRecall).toBeCloseTo(5 / 9);
  });

  it('normalizes Unicode speech while preserving words', () => {
    expect(normalizeTranscript('  Hyvää päivää!  ')).toBe('hyvää päivää');
  });

  it('throws a diagnostic mismatch when evidence is required', () => {
    expect(() => requireTranscriptEvidence('one two three', 'one', 0.8))
      .toThrow('Live heard only 1/3 expected words');
  });
});
