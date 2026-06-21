// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
import { describe, expect, it } from 'vitest';
import {
  getNextSuggestionPracticeStep,
  getSuggestionPracticeProgress,
} from './suggestionPractice';

describe('getSuggestionPracticeProgress', () => {
  it('returns the untyped suggestion remainder', () => {
    expect(getSuggestionPracticeProgress('Hello world', 'Hello')).toEqual({
      state: 'following',
      remainder: ' world',
    });
  });

  it('distinguishes completion from a diverged draft', () => {
    expect(getSuggestionPracticeProgress('Hello', 'Hello')).toEqual({
      state: 'complete',
      remainder: '',
    });
    expect(getSuggestionPracticeProgress('Hello', 'Help')).toEqual({
      state: 'diverged',
      remainder: '',
    });
  });

  it('matches canonically equivalent Unicode input', () => {
    expect(getSuggestionPracticeProgress('Café', 'Cafe\u0301')).toEqual({
      state: 'complete',
      remainder: '',
    });
  });
});

describe('getNextSuggestionPracticeStep', () => {
  it('advances one meaningful character and carries adjacent punctuation', () => {
    expect(getNextSuggestionPracticeStep('Hi, there!', '')).toEqual({
      appendText: 'H',
      grapheme: 'H',
    });
    expect(getNextSuggestionPracticeStep('Hi, there!', 'H')).toEqual({
      appendText: 'i, ',
      grapheme: 'i',
    });
    expect(getNextSuggestionPracticeStep('Hi, there!', 'Hi, ')).toEqual({
      appendText: 't',
      grapheme: 't',
    });
  });

  it('treats a joined emoji as one user-perceived character', () => {
    expect(getNextSuggestionPracticeStep('👩‍🎓!', '')).toEqual({
      appendText: '👩‍🎓!',
      grapheme: '👩‍🎓',
    });
  });

  it('supports punctuation-only suggestions without getting stuck', () => {
    expect(getNextSuggestionPracticeStep('?!', '')).toEqual({
      appendText: '?',
      grapheme: '?',
    });
    expect(getNextSuggestionPracticeStep('?!', '?')).toEqual({
      appendText: '!',
      grapheme: '!',
    });
  });

  it('does not advance completed or diverged input', () => {
    expect(getNextSuggestionPracticeStep('Hello', 'Hello')).toBeNull();
    expect(getNextSuggestionPracticeStep('Hello', 'Goodbye')).toBeNull();
  });
});
