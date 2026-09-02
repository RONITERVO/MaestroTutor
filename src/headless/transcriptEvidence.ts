// Copyright 2025 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

export interface TranscriptEvidence {
  expected: string;
  actual: string;
  normalizedExpected: string;
  normalizedActual: string;
  matchedWordCount: number;
  expectedWordCount: number;
  wordRecall: number;
  minimumWordRecall: number;
  passed: boolean;
}

const expandCommonContractions = (value: string): string => value
  .replace(/\bi['’]m\b/gu, 'i am')
  .replace(/\b(you|we|they)['’]re\b/gu, '$1 are')
  .replace(/\b(he|she|it)['’]s\b/gu, '$1 is')
  .replace(/\b(can)['’]t\b/gu, '$1 not')
  .replace(/\b(won)['’]t\b/gu, 'will not')
  .replace(/\b([\p{L}\p{N}]+)n['’]t\b/gu, '$1 not');

export const normalizeTranscript = (value: string): string => expandCommonContractions(
  value.normalize('NFKC').toLowerCase(),
)
  .replace(/[^\p{L}\p{N}]+/gu, ' ')
  .replace(/\s+/g, ' ')
  .trim();

const countWords = (words: readonly string[]): Map<string, number> => {
  const counts = new Map<string, number>();
  for (const word of words) counts.set(word, (counts.get(word) || 0) + 1);
  return counts;
};

/**
 * Measures whether Live heard the known words, without making provider
 * punctuation, contractions or exact output phrasing into a golden string.
 */
export const evaluateTranscriptEvidence = (
  expected: string,
  actual: string,
  minimumWordRecall = 0.8,
): TranscriptEvidence => {
  if (!Number.isFinite(minimumWordRecall) || minimumWordRecall < 0 || minimumWordRecall > 1) {
    throw new Error('minTranscriptWordRecall must be between 0 and 1.');
  }
  const normalizedExpected = normalizeTranscript(expected);
  if (!normalizedExpected) throw new Error('expectedTranscript must contain at least one word.');
  const normalizedActual = normalizeTranscript(actual);
  const expectedWords = normalizedExpected.split(' ');
  const actualCounts = countWords(normalizedActual ? normalizedActual.split(' ') : []);
  let matchedWordCount = 0;
  for (const word of expectedWords) {
    const remaining = actualCounts.get(word) || 0;
    if (remaining <= 0) continue;
    matchedWordCount += 1;
    actualCounts.set(word, remaining - 1);
  }
  const wordRecall = matchedWordCount / expectedWords.length;
  return {
    expected,
    actual,
    normalizedExpected,
    normalizedActual,
    matchedWordCount,
    expectedWordCount: expectedWords.length,
    wordRecall,
    minimumWordRecall,
    passed: wordRecall >= minimumWordRecall,
  };
};

export const requireTranscriptEvidence = (
  expected: string | undefined,
  actual: string,
  minimumWordRecall?: number,
): TranscriptEvidence | null => {
  if (expected === undefined) return null;
  const evidence = evaluateTranscriptEvidence(expected, actual, minimumWordRecall);
  if (!evidence.passed) {
    throw new Error(
      `Live heard only ${evidence.matchedWordCount}/${evidence.expectedWordCount} expected words `
      + `(recall ${evidence.wordRecall.toFixed(3)}, required ${evidence.minimumWordRecall.toFixed(3)}). `
      + `Expected "${evidence.expected}"; heard "${evidence.actual}".`,
    );
  }
  return evidence;
};
