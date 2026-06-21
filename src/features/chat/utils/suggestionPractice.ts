// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0

export type SuggestionPracticeState = 'following' | 'complete' | 'diverged';

export interface SuggestionPracticeProgress {
  state: SuggestionPracticeState;
  remainder: string;
}

export interface SuggestionPracticeStep {
  /** Text appended when this step is marked complete, including adjacent punctuation/spacing. */
  appendText: string;
  /** The single user-perceived character shown as the drawing guide. */
  grapheme: string;
}

type SegmenterConstructor = new (
  locale?: string,
  options?: { granularity: 'grapheme' }
) => { segment: (input: string) => Iterable<{ segment: string }> };

const getGraphemes = (value: string): string[] => {
  const Segmenter = (Intl as typeof Intl & { Segmenter?: SegmenterConstructor }).Segmenter;
  if (Segmenter) {
    return Array.from(
      new Segmenter(undefined, { granularity: 'grapheme' }).segment(value),
      part => part.segment
    );
  }

  // Array.from keeps surrogate pairs intact. Combining marks are folded into the
  // previous code point so the fallback never advances through half an accent.
  return Array.from(value).reduce<string[]>((graphemes, codePoint) => {
    const previous = graphemes[graphemes.length - 1];
    const joinsPrevious = /^[\p{M}\uFE0E\uFE0F\u{1F3FB}-\u{1F3FF}]$/u.test(codePoint)
      || codePoint === '\u200d'
      || previous?.endsWith('\u200d');
    const joinsRegionalIndicator = /^\p{Regional_Indicator}$/u.test(codePoint)
      && /^\p{Regional_Indicator}$/u.test(previous ?? '');
    if ((joinsPrevious || joinsRegionalIndicator) && graphemes.length > 0) {
      graphemes[graphemes.length - 1] += codePoint;
    } else {
      graphemes.push(codePoint);
    }
    return graphemes;
  }, []);
};

const canonicallyEqual = (left: string, right: string): boolean =>
  left.normalize('NFC') === right.normalize('NFC');

const getMatchingPrefixLength = (suggestion: string, input: string): number | null => {
  const suggestionGraphemes = getGraphemes(suggestion);
  const inputGraphemes = getGraphemes(input);
  if (inputGraphemes.length > suggestionGraphemes.length) return null;

  for (let index = 0; index < inputGraphemes.length; index += 1) {
    if (!canonicallyEqual(inputGraphemes[index], suggestionGraphemes[index])) return null;
  }
  return inputGraphemes.length;
};

export const getSuggestionPracticeProgress = (
  suggestion: string,
  input: string
): SuggestionPracticeProgress => {
  const suggestionGraphemes = getGraphemes(suggestion);
  const matchingPrefixLength = getMatchingPrefixLength(suggestion, input);

  if (matchingPrefixLength === null) {
    return { state: 'diverged', remainder: '' };
  }
  if (matchingPrefixLength === suggestionGraphemes.length) {
    return { state: 'complete', remainder: '' };
  }
  return {
    state: 'following',
    remainder: suggestionGraphemes.slice(matchingPrefixLength).join(''),
  };
};

const isPracticeCharacter = (grapheme: string): boolean =>
  /[\p{L}\p{N}\p{M}\p{S}]/u.test(grapheme);

export const getNextSuggestionPracticeStep = (
  suggestion: string,
  input: string
): SuggestionPracticeStep | null => {
  const suggestionGraphemes = getGraphemes(suggestion);
  const matchingPrefixLength = getMatchingPrefixLength(suggestion, input);
  if (matchingPrefixLength === null || matchingPrefixLength >= suggestionGraphemes.length) return null;

  const remaining = suggestionGraphemes.slice(matchingPrefixLength);
  const practiceCharacterIndex = remaining.findIndex(isPracticeCharacter);
  if (practiceCharacterIndex === -1) {
    return { appendText: remaining[0], grapheme: remaining[0] };
  }

  let endIndex = practiceCharacterIndex + 1;
  while (endIndex < remaining.length && !isPracticeCharacter(remaining[endIndex])) {
    endIndex += 1;
  }

  return {
    appendText: remaining.slice(0, endIndex).join(''),
    grapheme: remaining[practiceCharacterIndex],
  };
};
